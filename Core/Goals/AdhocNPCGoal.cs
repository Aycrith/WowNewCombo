using Core.Database;
using Core.GOAP;

using Game;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Data;
using SharedLib.Extensions;
using SharedLib.NpcFinder;

using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

#pragma warning disable 162

namespace Core.Goals;

public sealed partial class AdhocNPCGoal : GoapGoal, IGoapEventListener, IRouteProvider, IDisposable
{
    private enum PathState
    {
        ApproachPathStart,
        FollowPath,
        Finished,
    }

    private enum MerchantResult
    {
        Success,
        Failed,
        TryNextNPC,
    }

    private const bool debug = false;

    private const int MAX_TIME_TO_REACH_MELEE = 10000;
    private const int TIMEOUT = 5000;
    private const float NPC_DESTINATION_PROXIMITY = 12f;
    private const int MAX_FAR_DESTINATION_RETRIES = 3;

    private readonly FrozenDictionary<NpcFlags, SearchValues<string>> npcSearchPatterns;

    public override float Cost => key.Cost;

    private readonly ILogger<AdhocNPCGoal> logger;
    private readonly ConfigurableInput input;
    private readonly KeyAction key;
    private readonly Wait wait;
    private readonly Navigation navigation;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly StopMoving stopMoving;
    private readonly ClassConfiguration classConfig;
    private readonly NpcNameTargeting npcNameTargeting;
    private readonly IMountHandler mountHandler;
    private readonly CancellationToken token;
    private readonly ExecGameCommand execGameCommand;
    private readonly GossipReader gossipReader;
    private readonly AreaDB areaDB;
    private readonly BagReader bagReader;
    private readonly SessionStat sessionStat;

    private PathState pathState = PathState.Finished;

    private readonly bool tryFindClosestNPC;
    private Creature npc;
    private NpcSearchResult[] searchResult = [];
    private int searchCount;
    private int searchIndex;

    private static readonly TimeSpan NoPathRetryDelay = TimeSpan.FromSeconds(30);
    private DateTime noPathBackoffUntilUtc;
    private int farDestinationRetryCount;

    #region IRouteProvider

    public Vector3[] MapRoute()
    {
        return Array.Empty<Vector3>();
    }

    public Vector3[] PathingRoute()
    {
        return navigation.TotalRoute;
    }

    public bool HasNext()
    {
        return navigation.HasNext();
    }

    public Vector3 NextMapPoint()
    {
        return navigation.NextMapPoint();
    }

    public DateTime LastActive => navigation.LastActive;

    #endregion

    public AdhocNPCGoal(KeyAction key, ILogger<AdhocNPCGoal> logger, ConfigurableInput input,
        Wait wait, PlayerReader playerReader, GossipReader gossipReader, AddonBits bits,
        Navigation navigation, StopMoving stopMoving, AreaDB areaDB,
        NpcNameTargeting npcNameTargeting, ClassConfiguration classConfig,
        BagReader bagReader, SessionStat sessionStat,
        IMountHandler mountHandler, ExecGameCommand exec, CancellationTokenSource cts)
        : base(nameof(AdhocNPCGoal))
    {
        this.logger = logger;
        this.input = input;
        this.key = key;
        this.wait = wait;
        this.playerReader = playerReader;
        this.bits = bits;
        this.stopMoving = stopMoving;
        this.areaDB = areaDB;
        this.npcNameTargeting = npcNameTargeting;
        this.classConfig = classConfig;
        this.bagReader = bagReader;
        this.sessionStat = sessionStat;
        this.mountHandler = mountHandler;
        token = cts.Token;
        this.execGameCommand = exec;
        this.gossipReader = gossipReader;

        this.navigation = navigation;
        navigation.OnDestinationReached += Navigation_OnDestinationReached;
        navigation.OnWayPointReached += Navigation_OnWayPointReached;
        navigation.OnNoPathFound += Navigation_OnNoPathFound;

        if (bool.TryParse(key.InCombat, out bool result))
        {
            if (!result)
                AddPrecondition(GoapKey.dangercombat, result);
            else
                AddPrecondition(GoapKey.incombat, result);
        }

        Keys = [key];

        tryFindClosestNPC = key.Path.Length == 0;

        npcSearchPatterns = Enum.GetValues<NpcFlags>().Select(static flag =>
        {
            string[] strings = flag switch
            {
                NpcFlags.Vendor => [flag.ToStringF(), "Sell"],
                _ => [flag.ToStringF()]
            };

            return new KeyValuePair<NpcFlags, SearchValues<string>>(flag, SearchValues.Create(strings, StringComparison.OrdinalIgnoreCase));
        })
        .ToFrozenDictionary(pair => pair.Key, pair => pair.Value);
    }

    public void Dispose()
    {
        navigation.Dispose();
    }

    public override bool CanRun()
    {
        if (noPathBackoffUntilUtc != default && DateTime.UtcNow < noPathBackoffUntilUtc)
        {
            return false;
        }

        return key.CanRun();
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e.GetType() == typeof(ResumeEvent))
        {
            Resume();

        }
        else if (e.GetType() == typeof(AbortEvent))
        {
            Abort();
        }
    }

    private void Resume()
    {
        if (noPathBackoffUntilUtc != default && DateTime.UtcNow < noPathBackoffUntilUtc)
        {
            pathState = PathState.Finished;
            LogWarn($"Backoff after pathing failures until {noPathBackoffUntilUtc:O}");
            return;
        }

        if (tryFindClosestNPC && !TryAutoSelectNPCAndSetPath())
        {
            pathState = PathState.Finished;
            LogWarn("No NPC with the criteria!");
            return;
        }

        input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
        stopMoving.Stop();

        SetClosestWaypoint();

        navigation.Resume();

        pathState = PathState.ApproachPathStart;
        farDestinationRetryCount = 0;

        MountIfPossible();
    }

    private void Abort()
    {
        navigation.StopMovement();
        navigation.Stop();
        npcNameTargeting.ChangeNpcType(NpcNames.None);

        if (tryFindClosestNPC)
        {
            key.Path = [];
            npc = default;
        }
    }


    public override void OnEnter() => Resume();

    public override void OnExit() => Abort();

    public override void Update()
    {
        if (bits.Drowning())
            input.PressJump();

        if (pathState != PathState.Finished)
            navigation.Update();

        wait.Update();
    }


    private void SetClosestWaypoint()
    {
        Vector3 playerMap = playerReader.MapPos;

        Span<Vector3> pathMap = stackalloc Vector3[key.Path.Length];
        key.Path.CopyTo(pathMap);

        float mapDistanceToFirst = playerMap.MapDistanceXYTo(pathMap[0]);
        float mapDistanceToLast = playerMap.MapDistanceXYTo(pathMap[^1]);

        int closestIndex = 0;
        Vector3 mapClosestPoint = Vector3.Zero;
        float distance = float.MaxValue;

        for (int i = 0; i < pathMap.Length; i++)
        {
            Vector3 p = pathMap[i];
            float d = playerMap.MapDistanceXYTo(p);
            if (d < distance)
            {
                distance = d;
                closestIndex = i;
                mapClosestPoint = p;
            }
        }

        if (mapClosestPoint == pathMap[0] || mapClosestPoint == pathMap[^1])
        {
            navigation.SetWayPoints(pathMap);
        }
        else
        {
            Span<Vector3> points = pathMap[closestIndex..];
            navigation.SetWayPoints(points);
        }
    }

    private void UpdateClosestNPC(NpcFlags npcFlag)
    {
        if (searchResult.Length == 0 || searchCount == 0)
            return;

        npc = searchResult[searchIndex].Creature;
        Vector3 worldPos = searchResult[searchIndex].WorldPosition;
        key.Path = [worldPos];

        LogFoundCloesestNPCByType(logger, npc.Name, npcFlag.ToStringF(), worldPos);
    }

    private void Navigation_OnNoPathFound()
    {
        if (pathState != PathState.ApproachPathStart || token.IsCancellationRequested)
            return;

        logger.LogError("No path found!");

        Resume();
    }

    private void Navigation_OnWayPointReached()
    {
        if (pathState is PathState.ApproachPathStart)
        {
            LogDebug("1 Reached the start point of the path.");
            navigation.SimplifyRouteToWaypoint = false;
        }
    }

    private void Navigation_OnDestinationReached()
    {
        if (pathState != PathState.ApproachPathStart || token.IsCancellationRequested)
            return;

        if (key.Path.Length > 0)
        {
            Vector3 destination = key.Path[^1];
            float destinationDistance = playerReader.WorldPos.WorldDistanceXYTo(destination);
            if (destinationDistance > NPC_DESTINATION_PROXIMITY)
            {
                farDestinationRetryCount++;
                LogWarn($"Reached path end but still {destinationDistance:F1} away from NPC destination; re-pathing.");

                if (tryFindClosestNPC && farDestinationRetryCount >= MAX_FAR_DESTINATION_RETRIES)
                {
                    LogWarn("NPC destination retries exhausted; trying next candidate NPC.");
                    Resume();
                    return;
                }

                navigation.SetWayPoints([destination]);
                navigation.Resume();
                return;
            }
        }

        farDestinationRetryCount = 0;
        LogDebug("Reached defined path end");
        navigation.StopMovement();
        stopMoving.Stop();
        wait.Update();

        input.ForceAggressiveClearTarget(wait, bits, execGameCommand);

        if (tryFindClosestNPC && npc != default)
        {
            execGameCommand.Run($"/target {npc.Name}");
            wait.Update();
        }

        bool hasTarget = bits.Target();

        if (bits.SoftInteract() &&
            !bits.SoftInteract_Hostile())
        {
            input.PressInteract();
            wait.Update();

            LogWarn($"Soft Interact found NPC with id {playerReader.SoftInteract_Id}");

            hasTarget = MoveToTargetAndReached();
        }

        if (!hasTarget && !input.KeyboardOnly)
        {
            npcNameTargeting.ChangeNpcType(NpcNames.Friendly | NpcNames.Neutral);
            npcNameTargeting.WaitForUpdate();

            ReadOnlySpan<CursorType> types = [
                CursorType.Loot,
                CursorType.Vendor,
                CursorType.Repair,
                CursorType.Innkeeper,
                CursorType.Speak
            ];

            hasTarget = npcNameTargeting.FindBy(types, token);
            wait.Update();

            if (!hasTarget)
            {
                LogWarn($"No target found by cursor({CursorType.Vendor.ToStringF()}, {CursorType.Repair.ToStringF()}, {CursorType.Innkeeper.ToStringF()})!");
            }
        }

        if (!hasTarget)
        {
            Log($"Use KeyAction.Key macro to acquire target");
            input.PressRandom(key);
            wait.Update();
        }

        wait.Until(400, bits.Target);
        if (!bits.Target())
        {
            LogWarn("No target found! Turn left to find NPC");
            input.PressFixed(input.TurnLeftKey, 250, token);
            return;
        }

        Log($"Found Target!");
        input.PressInteract();
        wait.Update();

        MerchantResult merchantResult = OpenMerchantWindow();
        if (merchantResult == MerchantResult.TryNextNPC && tryFindClosestNPC)
        {
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
            Resume();
            return;
        }

        if (merchantResult != MerchantResult.Success)
            return;

        noPathBackoffUntilUtc = default;

        // Signal that vendor/repair completed successfully
        // MailGoal uses this to know it can run
        sessionStat.VendoredOrRepairedRecently = true;

        input.PressRandom(ConsoleKey.Escape, InputDuration.DefaultPress);
        input.ForceAggressiveClearTarget(wait, bits, execGameCommand);

        return;
        // The following code no longer needed as we know for a fact we are close to an NPC spawnpoint
        // thus we know the world coordinate and Z/height component
        // then the pathfinder can reliable locate the player exact location

        Span<Vector3> reversePath = stackalloc Vector3[key.Path.Length];
        key.Path.CopyTo(reversePath);
        reversePath.Reverse();
        navigation.SetWayPoints(reversePath);

        pathState++;

        LogDebug("Go back reverse to the start point of the path.");
        navigation.ResetStuckParameters();

        // At this point the BagsFull is false
        // which mean it it would exit the Goal
        // instead keep it trapped to follow the route back
        while (navigation.HasWaypoint() &&
            !token.IsCancellationRequested &&
            pathState == PathState.FollowPath)
        {
            navigation.Update();
            wait.Update();
        }

        pathState = PathState.Finished;

        LogDebug("2 Reached the start point of the path.");
        stopMoving.Stop();

        navigation.SimplifyRouteToWaypoint = true;
        MountIfPossible();
    }

    private bool MoveToTargetAndReached()
    {
        wait.While(input.Approach.OnCooldown);

        float elapsedMs = wait.Until(MAX_TIME_TO_REACH_MELEE,
            bits.NotMoving, input.PressApproachOnCooldown);

        //LogReachedCorpse(logger, bits.Target(), elapsedMs);

        return bits.Target() && playerReader.MinRangeZero();
    }

    private void MountIfPossible()
    {
        float totalDistance = VectorExt.TotalDistance<Vector3>(navigation.TotalRoute, VectorExt.WorldDistanceXY);

        // Check if key override allows mounting
        if (key.UseMount)
        {
            if (mountHandler.CanMount() && MountHandler.ShouldMount(totalDistance))
            {
                Log("Mount up");
                mountHandler.MountUp();
                navigation.ResetStuckParameters();
                return;
            }
        }
        else
        {
            // Standard travel optimization: mount if possible, otherwise unstealth for speed
            mountHandler.OptimizeTravelSpeed(totalDistance);

            if (mountHandler.IsMounted())
            {
                navigation.ResetStuckParameters();
            }
        }
    }

    private MerchantResult OpenMerchantWindow()
    {
        float e = wait.Until(TIMEOUT, gossipReader.GossipStartOrMerchantWindowOpened);
        if (gossipReader.MerchantWindowOpened())
        {
            LogWarn($"Gossip no options! {e}ms");
        }
        else
        {
            e = wait.Until(TIMEOUT, gossipReader.GossipEnd);
            if (e < 0)
            {
                LogWarn($"Gossip - {nameof(gossipReader.GossipEnd)} not fired after {e}ms");
                return MerchantResult.Failed;
            }
            else
            {
                if (gossipReader.Gossips.TryGetValue(Gossip.Vendor, out int orderNum))
                {
                    Log($"Picked {orderNum}th for {Gossip.Vendor.ToStringF()}");
                    execGameCommand.Run($"/run SelectGossipOption({orderNum})--");
                }
                else
                {
                    LogWarn($"Target({playerReader.TargetId}) has no {Gossip.Vendor.ToStringF()} option!");
                    return MerchantResult.TryNextNPC;
                }
            }
        }

        Log($"Merchant window opened after {e}ms");

        if (key.ConsoleKey != default)
            input.PressRandom(key);

        if (bagReader.AnyGreyItem())
        {
            e = wait.Until(TIMEOUT, gossipReader.MerchantWindowSelling);
            if (e < 0)
            {
                Log($"Merchant sell nothing! {e}ms");
                goto exit;
            }

            Log($"Merchant sell grey items started after {e}ms");

            e = wait.Until(TIMEOUT, gossipReader.MerchantWindowSellingFinished);
            if (e >= 0)
            {
                Log($"Merchant sell grey items finished, took {e}ms");
            }
            else
            {
                Log($"Merchant sell grey items timeout! Too many items to sell?! Increase {nameof(TIMEOUT)} - {e}ms");
            }
        }

    exit:
        if (!string.IsNullOrEmpty(key.MacroText))
        {
            string text = key.Macro();
            execGameCommand.Run(text);
            wait.Update();
        }

        return MerchantResult.Success;
    }

    private bool TryAutoSelectNPCAndSetPath()
    {
        if (areaDB.CurrentArea == null)
        {
            return false;
        }

        ReadOnlySpan<char> name = key.Name;

        NpcFlags npcFlag = NpcFlags.None;
        foreach ((NpcFlags type, SearchValues<string> pattern) in npcSearchPatterns)
        {
            if (name.ContainsAny(pattern))
            {
                npcFlag = type;
                break;
            }
        }

        string[] allowedNames = [];

        // Parse NPC name pattern: [TYPE][ ][npc1 | npc2 | npc3]
        // Supports multiple NPC names separated by pipe character
        // Note: Faction-specific filtering could be added here in the future
        int separator = name.IndexOf(' ');
        if (separator != -1)
        {
            allowedNames = name[(separator + 1)..]
                .ToString()
                .Split('|', options: StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (allowedNames.Length > 0)
                logger.LogInformation($"Search for {npcFlag} like {string.Join(',', allowedNames)}");
        }

        NpcFlags searchFlags = npcFlag;
        if (npcFlag == NpcFlags.Repair)
        {
            // Some valid repair NPCs are only tagged as Vendor in the DB.
            searchFlags = NpcFlags.Repair | NpcFlags.Vendor;
        }

        if (searchResult.Length == 0)
        {
            searchResult = new NpcSearchResult[8];

            int found = areaDB.GetNearestNpcs(playerReader.Faction, searchFlags, playerReader.WorldPos, allowedNames, searchResult.AsSpan(), out searchCount);
            if (found == 0 || searchCount == 0)
            {
                return false;
            }

            LogFoundPotentialNPCByType(logger, searchCount, npcFlag.ToStringF());
            searchIndex = 0;
        }
        else
        {
            searchIndex++;
        }

        if (searchIndex >= searchCount)
        {
            noPathBackoffUntilUtc = DateTime.UtcNow.Add(NoPathRetryDelay);
            pathState = PathState.Finished;
            LogWarn("No more NPC to try!");

            searchIndex = 0;
            searchResult = [];
            searchCount = 0;

            return false;
        }

        LogWarn($"Try next closest NPC -- {searchIndex}");

        UpdateClosestNPC(npcFlag);

        return true;
    }


    private void Log(string text)
    {
        logger.LogInformation(text);
    }

    private void LogDebug(string text)
    {
        if (debug)
            logger.LogDebug(text);
    }

    private void LogWarn(string text)
    {
        logger.LogWarning(text);
    }


    #region Logging

    [LoggerMessage(
        EventId = 0300,
        Level = LogLevel.Information,
        Message = "Closest NPC found {type} {name} at {pos}")]
    static partial void LogFoundCloesestNPCByType(ILogger logger, string name, string type, Vector3 pos);

    [LoggerMessage(
        EventId = 0301,
        Level = LogLevel.Information,
        Message = "Found {count} potential {type} NPC.")]
    static partial void LogFoundPotentialNPCByType(ILogger logger, int count, string type);


    #endregion
}
