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

    private const int TIMEOUT = 5000;
    private const float NPC_DESTINATION_PROXIMITY = 12f;
    private const int MAX_FAR_DESTINATION_RETRIES = 3;
    private const int KEYBOARD_ONLY_VENDOR_ACQUIRE_MAX_ATTEMPTS = 4;
    private const int KEYBOARD_ONLY_VENDOR_TURN_MS = 180;
    private const int KEYBOARD_ONLY_VENDOR_TARGET_WAIT_MS = 220;
    // Keep broad enough to reach nearby town service NPCs from common grind loops,
    // but still reject obviously remote candidates before pathing.
    private const float MAX_AUTO_NPC_TRAVEL_DISTANCE = 750f;
    private const float MAX_AUTO_NPC_VERTICAL_DELTA = 60f;

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

    private bool tryFindClosestNPC => key.Path.Length == 0;
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

        if (!key.CanRun())
        {
            return false;
        }

        // Defensive gate: sell actions that are configured to require bag-full should not
        // run while there are still general-purpose bag slots available, even if a profile
        // requirement evaluates incorrectly due transient bag meta state.
        if (RequiresBagFullSellGate() && bagReader.TotalFreeGeneralSlotCount() > 0)
        {
            return false;
        }

        return true;
    }

    private bool RequiresBagFullSellGate()
    {
        if (!key.Name.Contains("Sell", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(key.Requirement) &&
            key.Requirement.Contains("BagFull", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (string requirement in key.Requirements)
        {
            if (requirement.Contains("BagFull", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    private bool IsReasonableAutoNpcCandidate(in NpcSearchResult candidate, out string reason)
    {
        // Ghostlands service data currently includes "Samir" at a location that is not safely reachable
        // on the live client route being tested (bot runs into terrain/zonewall near the target point).
        // Skip this candidate during auto NPC selection to prevent suicidal repair/vendor runs.
        if (playerReader.UIMapId.Value == 1942 &&
            candidate.Creature.Name.Equals("Samir", StringComparison.OrdinalIgnoreCase))
        {
            reason = "known bad Ghostlands service target";
            return false;
        }

        float xyDistance = playerReader.WorldPos.WorldDistanceXYTo(candidate.WorldPosition);
        float playerZ = playerReader.WorldPos.Z;
        bool hasReliablePlayerZ = MathF.Abs(playerZ) > 1f;
        float zDelta = hasReliablePlayerZ
            ? MathF.Abs(candidate.WorldPosition.Z - playerZ)
            : 0f;

        if (xyDistance > MAX_AUTO_NPC_TRAVEL_DISTANCE)
        {
            reason = $"distance {xyDistance:F1} > {MAX_AUTO_NPC_TRAVEL_DISTANCE:F0}";
            return false;
        }

        // Allow significant Z changes only when the NPC is still geographically close.
        if (hasReliablePlayerZ &&
            zDelta > MAX_AUTO_NPC_VERTICAL_DELTA &&
            xyDistance > 80f)
        {
            reason = $"z delta {zDelta:F1} > {MAX_AUTO_NPC_VERTICAL_DELTA:F0} (xy={xyDistance:F1})";
            return false;
        }

        reason = string.Empty;
        return true;
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

        if (ShouldSkipVendorApproachForSoftInteract(bits.SoftInteract(), bits.SoftInteract_Hostile()))
        {
            input.PressInteract();
            wait.Update();

            LogWarn($"Soft Interact found NPC with id {playerReader.SoftInteract_Id}");
            // When soft-interact already points to a friendly NPC, avoid approach loops.
            // At this point we're close enough to attempt direct gossip/vendor interaction.
            hasTarget = bits.Target() || bits.SoftInteract();
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
            if (input.KeyboardOnly)
            {
                bool keyboardOnlyPathUsed = true;
                bool acquiredByKeyboardOnly = TryAcquireVendorTargetKeyboardOnly(
                    out int attemptCount,
                    out int turnAdjustCount,
                    out string failureReason);

                hasTarget = acquiredByKeyboardOnly;

                if (!hasTarget)
                {
                    string candidateName = npc == default ? string.Empty : npc.Name;
                    LogWarn($"Vendor acquisition failed. Candidate='{candidateName}', KeyboardOnlyPathUsed={keyboardOnlyPathUsed}, AttemptCount={attemptCount}, TurnAdjustCount={turnAdjustCount}, FailureReason={failureReason}");
                }
            }
            else
            {
                Log($"Use KeyAction.Key macro to acquire target");
                input.PressRandom(key);
                wait.Update();
            }
        }

        wait.Until(400, () => bits.Target() || bits.SoftInteract());
        if (!bits.Target() && !bits.SoftInteract())
        {
            LogWarn("No target found! Vendor acquisition exhausted.");

            if (tryFindClosestNPC)
            {
                input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
                Resume();
                return;
            }

            return;
        }

        Log($"Found Target!");
        input.PressInteract();
        wait.Update();

        MerchantResult merchantResult = OpenMerchantWindow(out bool performedVendorWork);
        if (merchantResult == MerchantResult.TryNextNPC && tryFindClosestNPC)
        {
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
            Resume();
            return;
        }

        if (merchantResult != MerchantResult.Success)
            return;

        noPathBackoffUntilUtc = default;

        // Signal that vendor/repair completed successfully.
        // MailGoal uses this to know it can run, but only after actual vendor work.
        if (performedVendorWork)
        {
            sessionStat.VendoredOrRepairedRecently = true;
        }

        input.PressRandom(ConsoleKey.Escape, InputDuration.DefaultPress);
        input.ForceAggressiveClearTarget(wait, bits, execGameCommand);

        // Clear navigation state so next goal (FollowRoute) doesn't see stale vendor path
        navigation.Stop();
        pathState = PathState.Finished;

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

    internal static bool ShouldSkipVendorApproachForSoftInteract(bool hasSoftInteract, bool softInteractHostile)
        => hasSoftInteract && !softInteractHostile;

    internal static bool ShouldApplyVendorAcquireTurnAdjust(int attemptIndex)
        => attemptIndex < KEYBOARD_ONLY_VENDOR_ACQUIRE_MAX_ATTEMPTS - 1;

    internal static ConsoleKey GetVendorAcquireTurnKey(int turnAdjustCount, ConsoleKey turnLeftKey, ConsoleKey turnRightKey)
        => turnAdjustCount % 2 == 0 ? turnLeftKey : turnRightKey;

    private bool TryAcquireVendorTargetKeyboardOnly(out int attemptCount, out int turnAdjustCount, out string failureReason)
    {
        attemptCount = 0;
        turnAdjustCount = 0;
        failureReason = "target_not_acquired_keyboard_only";

        for (int attemptIndex = 0; attemptIndex < KEYBOARD_ONLY_VENDOR_ACQUIRE_MAX_ATTEMPTS; attemptIndex++)
        {
            attemptCount = attemptIndex + 1;

            if (bits.Target() || ShouldSkipVendorApproachForSoftInteract(bits.SoftInteract(), bits.SoftInteract_Hostile()))
            {
                return true;
            }

            stopMoving.Stop();

            Log($"KeyboardOnly vendor acquire attempt {attemptCount}/{KEYBOARD_ONLY_VENDOR_ACQUIRE_MAX_ATTEMPTS}");
            input.PressRandom(key);
            wait.Update();
            input.PressInteract();
            wait.Update();

            float targetWait = wait.Until(KEYBOARD_ONLY_VENDOR_TARGET_WAIT_MS, () => bits.Target() || bits.SoftInteract());
            if (targetWait >= 0)
            {
                return true;
            }

            if (ShouldApplyVendorAcquireTurnAdjust(attemptIndex))
            {
                ConsoleKey turnKey = GetVendorAcquireTurnKey(turnAdjustCount, input.TurnLeftKey, input.TurnRightKey);
                turnAdjustCount++;
                input.PressFixed(turnKey, KEYBOARD_ONLY_VENDOR_TURN_MS, token);
                wait.Update();
            }
        }

        return bits.Target() || bits.SoftInteract();
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

    private MerchantResult OpenMerchantWindow(out bool performedVendorWork)
    {
        performedVendorWork = false;
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

        bool hadRepairNeed = bits.Items_Broken() || playerReader.AvgEquipDurability() < 100;
        bool hadGreyToSell = bagReader.AnyGreyItem();

        if (key.ConsoleKey != default)
            input.PressRandom(key);

        if (hadGreyToSell)
        {
            Log($"Merchant sell nothing! {e}ms");
            goto exit;
        }

        Log($"Merchant sell items started after {e}ms");

        e = wait.Until(TIMEOUT, gossipReader.MerchantWindowSellingFinished);
        if (e >= 0)
        {
            Log($"Merchant sell items finished, took {e}ms");
        }
        else
        {
            Log($"Merchant sell items timeout! Too many items to sell?! Increase {nameof(TIMEOUT)} - {e}ms");
        }

    exit:
        if (!string.IsNullOrEmpty(key.MacroText))
        {
            string text = key.Macro();
            execGameCommand.Run(text);
            wait.Update();
        }

        performedVendorWork = hadRepairNeed || hadGreyToSell;
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

        while (searchIndex < searchCount)
        {
            ref readonly NpcSearchResult candidate = ref searchResult[searchIndex];
            if (IsReasonableAutoNpcCandidate(candidate, out string reason))
            {
                break;
            }

            LogWarn($"Skipping NPC candidate '{candidate.Creature.Name}' ({candidate.WorldPosition}) - {reason}");
            searchIndex++;
        }

        if (searchIndex >= searchCount)
        {
            // Fallback: Try hard-coded vendor locations when auto-search exhausted
            if (TryUseHardCodedVendor(npcFlag))
            {
                LogWarn("Auto-search exhausted - using hard-coded vendor fallback");
                return true;
            }

            noPathBackoffUntilUtc = DateTime.UtcNow.Add(NoPathRetryDelay);
            pathState = PathState.Finished;
            LogWarn("No more reasonable NPC candidates to try!");

            searchIndex = 0;
            searchResult = [];
            searchCount = 0;

            return false;
        }

        LogWarn($"Try next closest NPC -- {searchIndex}");

        UpdateClosestNPC(npcFlag);

        return true;
    }

    /// <summary>
    /// Fallback method: Try to use hard-coded vendor locations when auto-search fails
    /// </summary>
    private bool TryUseHardCodedVendor(NpcFlags npcFlag)
    {
        // Only use fallback for vendors
        if (npcFlag != NpcFlags.Vendor)
            return false;

        if (areaDB.CurrentWorldMapArea == null)
            return false;

        string zoneName = areaDB.CurrentWorldMapArea.Value.AreaName;
        
        if (!VendorLocations.TryGetVendorsForZone(zoneName, out var vendors) || vendors == null || vendors.Count == 0)
        {
            LogWarn($"No hard-coded vendors available for zone: {zoneName}");
            return false;
        }

        var closestVendor = VendorLocations.FindClosestVendor(vendors, playerReader.WorldPos);
        if (closestVendor == null)
            return false;

        // Create a temporary creature for the vendor
        npc = new Creature
        {
            Name = closestVendor.Name,
            Entry = 0, // Unknown entry
            Faction = 0, // Assume friendly
            NpcFlag = NpcFlags.Vendor
        };

        // Set the vendor position as the path
        key.Path = [closestVendor.WorldPosition];

        logger.LogInformation($"Using hard-coded vendor: {closestVendor.Name} at {closestVendor.WorldPosition} (Priority {closestVendor.Priority})");
        if (!string.IsNullOrEmpty(closestVendor.Notes))
        {
            LogDebug($"Vendor notes: {closestVendor.Notes}");
        }

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
