using Core.Database;
using Core.GOAP;

using Game;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Data;
using SharedLib.Extensions;
using SharedLib.NpcFinder;

using System;
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

    private enum ServiceInteractionResult
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
    private const int MAX_TIME_TO_REACH_SERVICE_TARGET = 2000;
    // Keep broad enough to reach nearby town service NPCs from common grind loops,
    // but still reject obviously remote candidates before pathing.
    private const float MAX_AUTO_NPC_TRAVEL_DISTANCE = 750f;
    private const float MAX_AUTO_NPC_VERTICAL_DELTA = 60f;

    public override float Cost => key.Cost;

    private readonly ILogger<AdhocNPCGoal> logger;
    private readonly ConfigurableInput input;
    private readonly KeyAction key;
    private readonly Wait wait;
    private readonly Navigation navigation;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly StopMoving stopMoving;
    private readonly PlayerDirection playerDirection;
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
    private readonly NpcServiceKind requestedServiceKind;
    private NpcServiceCandidate currentCandidate;
    private readonly HashSet<string> rejectedServiceCandidates = new(StringComparer.OrdinalIgnoreCase);
    private NpcServiceCandidate[] searchResult = [];
    private int searchCount;
    private int searchIndex;
    private bool fallbackSearchLoaded;

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
        Navigation navigation, StopMoving stopMoving, PlayerDirection playerDirection, AreaDB areaDB,
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
        this.playerDirection = playerDirection;
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
        requestedServiceKind = GetRequestedServiceKind(key.Name);
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
            currentCandidate = default;
            searchResult = [];
            searchCount = 0;
            searchIndex = 0;
            fallbackSearchLoaded = false;
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

    internal static NpcServiceKind GetRequestedServiceKind(string actionName)
    {
        if (actionName.Contains(nameof(NpcServiceKind.Repair), StringComparison.OrdinalIgnoreCase))
        {
            return NpcServiceKind.Repair;
        }

        if (actionName.Contains(nameof(NpcServiceKind.Innkeeper), StringComparison.OrdinalIgnoreCase))
        {
            return NpcServiceKind.Innkeeper;
        }

        if (actionName.Contains(nameof(NpcServiceKind.Trainer), StringComparison.OrdinalIgnoreCase))
        {
            return NpcServiceKind.Trainer;
        }

        if (actionName.Contains(nameof(NpcServiceKind.FlightMaster), StringComparison.OrdinalIgnoreCase) ||
            actionName.Contains("Flightmaster", StringComparison.OrdinalIgnoreCase) ||
            actionName.Contains(nameof(Gossip.Taxi), StringComparison.OrdinalIgnoreCase))
        {
            return NpcServiceKind.FlightMaster;
        }

        if (actionName.Contains(nameof(NpcServiceKind.Vendor), StringComparison.OrdinalIgnoreCase) ||
            actionName.Contains("Sell", StringComparison.OrdinalIgnoreCase))
        {
            return NpcServiceKind.Vendor;
        }

        return NpcServiceKind.None;
    }

    internal static NpcFlags GetFallbackSearchFlags(NpcServiceKind serviceKind)
    {
        return serviceKind switch
        {
            NpcServiceKind.Vendor => NpcFlags.Vendor,
            NpcServiceKind.Repair => NpcFlags.Repair | NpcFlags.Vendor,
            NpcServiceKind.Innkeeper => NpcFlags.Innkeeper,
            NpcServiceKind.Trainer => NpcFlags.Trainer | NpcFlags.ClassTrainer | NpcFlags.ProfessionTrainer,
            NpcServiceKind.FlightMaster => NpcFlags.FlightMaster,
            _ => NpcFlags.None
        };
    }

    internal static string BuildRejectedServiceCandidateKey(NpcServiceKind serviceKind, int entry, string candidateName)
        => $"{(int)serviceKind}:{entry}:{candidateName}";

    internal static bool ShouldAttemptSingleOptionVendorFallback(NpcServiceKind serviceKind, int gossipCount, bool hasVendorOption)
        => (serviceKind == NpcServiceKind.Vendor || serviceKind == NpcServiceKind.Repair) &&
           gossipCount == 1 &&
           !hasVendorOption;

    internal static bool HasExpectedServiceGossip(
        NpcServiceKind serviceKind,
        IReadOnlyDictionary<Gossip, int> gossips,
        bool merchantWindowOpened)
    {
        return serviceKind switch
        {
            NpcServiceKind.Vendor => merchantWindowOpened || gossips.ContainsKey(Gossip.Vendor),
            NpcServiceKind.Repair => merchantWindowOpened || gossips.ContainsKey(Gossip.Vendor),
            NpcServiceKind.Trainer => gossips.ContainsKey(Gossip.Trainer),
            NpcServiceKind.FlightMaster => gossips.ContainsKey(Gossip.Taxi),
            NpcServiceKind.Innkeeper => merchantWindowOpened ||
                                        gossips.ContainsKey(Gossip.Binder) ||
                                        gossips.ContainsKey(Gossip.Vendor) ||
                                        gossips.Count > 0,
            _ => false
        };
    }

    private void UpdateClosestCandidate()
    {
        if (searchResult.Length == 0 || searchCount == 0)
        {
            return;
        }

        currentCandidate = searchResult[searchIndex];
        key.Path = [currentCandidate.WorldPosition];

        LogFoundCloesestNPCByType(logger, currentCandidate.Name, requestedServiceKind.ToStringF(), currentCandidate.WorldPosition);
        logger.LogInformation(
            "Service candidate selected Service={Service} Source={Source} Entry={Entry} Name={Name} World={World} Map={Map} Flags={Flags}",
            requestedServiceKind.ToStringF(),
            currentCandidate.Source.ToStringF(),
            currentCandidate.Entry,
            currentCandidate.Name,
            currentCandidate.WorldPosition,
            currentCandidate.MapPosition,
            currentCandidate.Flags);
    }

    private bool IsReasonableAutoNpcCandidate(in NpcServiceCandidate candidate, out string reason)
    {
        if (rejectedServiceCandidates.Contains(candidate.IdentityKey))
        {
            reason = "previously rejected after service validation";
            return false;
        }

        // Ghostlands service data currently includes "Samir" at a location that is not safely reachable
        // on the live client route being tested (bot runs into terrain/zonewall near the target point).
        // Skip this candidate during auto NPC selection to prevent suicidal repair/vendor runs.
        if (playerReader.UIMapId.Value == 1942 &&
            candidate.Name.Equals("Samir", StringComparison.OrdinalIgnoreCase))
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

        if (tryFindClosestNPC && currentCandidate != default)
        {
            execGameCommand.Run($"/target {currentCandidate.Name}");
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
                    string candidateName = currentCandidate.Name ?? string.Empty;
                    LogWarn($"Service acquisition failed. Service={requestedServiceKind.ToStringF()}, Candidate='{candidateName}', Source={currentCandidate.Source.ToStringF()}, KeyboardOnlyPathUsed={keyboardOnlyPathUsed}, AttemptCount={attemptCount}, TurnAdjustCount={turnAdjustCount}, FailureReason={failureReason}");
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
            LogWarn($"No target found! Service acquisition exhausted for {requestedServiceKind.ToStringF()}.");

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

        ServiceInteractionResult interactionResult = CompleteServiceInteraction(out bool performedVendorWork);
        if (interactionResult == ServiceInteractionResult.TryNextNPC && tryFindClosestNPC)
        {
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
            Resume();
            return;
        }

        if (interactionResult != ServiceInteractionResult.Success)
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

    internal static bool ShouldUseKeyboardOnlyVendorFacing(bool keyboardOnly, bool hasNpcCandidate)
        => keyboardOnly && hasNpcCandidate;

    internal static bool ShouldUseVendorNameTargetCommand(bool keyboardOnly, string candidateName)
        => keyboardOnly && !string.IsNullOrWhiteSpace(candidateName);

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
            FaceNpcCandidateForKeyboardOnlyAcquire();

            if (TryTargetVendorByNameCommand())
            {
                return true;
            }

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

    private void FaceNpcCandidateForKeyboardOnlyAcquire()
    {
        if (!ShouldUseKeyboardOnlyVendorFacing(input.KeyboardOnly, currentCandidate != default))
        {
            return;
        }

        if (key.Path.Length == 0)
        {
            return;
        }

        Vector3 candidateWorldPosition = key.Path[^1];
        Vector3 playerMap = playerReader.MapPos;
        Vector3 npcMap = WorldMapAreaDB.ToMap_FlipXY(candidateWorldPosition, playerReader.WorldMapArea);
        float heading = DirectionCalculator.CalculateMapHeading(playerMap, npcMap);
        if (float.IsNaN(heading) || float.IsInfinity(heading))
        {
            return;
        }

        // Ignore-distance 0 ensures we still nudge facing when already next to the vendor.
        playerDirection.SetDirection(heading, candidateWorldPosition, 0f, token);
    }

    private bool TryTargetVendorByNameCommand()
    {
        string candidateName = currentCandidate.Name;
        if (!ShouldUseVendorNameTargetCommand(input.KeyboardOnly, candidateName))
        {
            return false;
        }

        string escapedName = candidateName.Replace("\"", string.Empty, StringComparison.Ordinal);
        execGameCommand.Run($"/targetexact {escapedName}");
        wait.Update();
        wait.Until(180, () => bits.Target() || bits.SoftInteract());
        if (bits.Target() || bits.SoftInteract())
        {
            return true;
        }

        execGameCommand.Run("/targetfriend");
        wait.Update();
        wait.Until(120, () => bits.Target() || bits.SoftInteract());
        return bits.Target() || bits.SoftInteract();
    }

    private ServiceInteractionResult CompleteServiceInteraction(out bool performedServiceWork)
    {
        performedServiceWork = false;

        ServiceInteractionResult interactionResult = OpenExpectedServiceWindow(out performedServiceWork, out string failureReason);
        if (interactionResult == ServiceInteractionResult.Success)
        {
            return interactionResult;
        }

        bool approachVerified = PerformServiceApproachVerification();
        logger.LogInformation(
            "Service interaction verification Service={Service} CandidateSource={Source} Entry={Entry} Name={Name} ApproachVerified={ApproachVerified} TargetId={TargetId} TargetName={TargetName} FailureReason={FailureReason}",
            requestedServiceKind.ToStringF(),
            currentCandidate.Source.ToStringF(),
            currentCandidate.Entry,
            currentCandidate.Name,
            approachVerified,
            playerReader.TargetId,
            ResolveTargetName(playerReader.TargetId),
            failureReason);

        if (!approachVerified)
        {
            RejectCurrentServiceCandidate("approach_verification_failed");
            return tryFindClosestNPC
                ? ServiceInteractionResult.TryNextNPC
                : ServiceInteractionResult.Failed;
        }

        input.PressInteract();
        wait.Update();

        ServiceInteractionResult retryResult = OpenExpectedServiceWindow(out performedServiceWork, out string retryFailureReason);
        if (retryResult == ServiceInteractionResult.Success)
        {
            return retryResult;
        }

        RejectCurrentServiceCandidate(retryFailureReason);
        return tryFindClosestNPC
            ? ServiceInteractionResult.TryNextNPC
            : ServiceInteractionResult.Failed;
    }

    private bool PerformServiceApproachVerification()
    {
        if (!bits.Target())
        {
            return false;
        }

        stopMoving.Stop();

        if (!playerReader.MinRangeZero())
        {
            bool reached = MoveToTargetAndReached();
            wait.Update();
            return reached;
        }

        FaceNpcCandidateForKeyboardOnlyAcquire();
        wait.Update();
        return true;
    }

    private bool MoveToTargetAndReached()
    {
        if (!bits.Moving())
        {
            wait.While(input.Approach.OnCooldown);
            TryPressServiceApproachOnCooldownIfNeeded();
            wait.Until(input.Approach.PressDuration, bits.Moving);
        }

        wait.Until(
            MAX_TIME_TO_REACH_SERVICE_TARGET,
            StopMovingOrReachedServiceTarget,
            TryPressServiceApproachOnCooldownIfNeeded);

        return bits.Target() && playerReader.MinRangeZero();
    }

    private bool StopMovingOrReachedServiceTarget() => !bits.Target() || bits.NotMoving() || playerReader.MinRangeZero();

    private void TryPressServiceApproachOnCooldownIfNeeded()
    {
        if (bits.Target() && !bits.Moving() && !playerReader.IsInMeleeRange())
        {
            if (input.PressedApproachOnCooldown())
            {
                wait.Update();
            }
        }
    }

    private string ResolveTargetName(int targetId)
    {
        if (targetId == 0)
        {
            return string.Empty;
        }

        if (currentCandidate.Entry != 0 && currentCandidate.Entry == targetId)
        {
            return currentCandidate.Name;
        }

        return areaDB.TryGetCreature(targetId, out Creature creature)
            ? creature.Name
            : string.Empty;
    }

    private string DescribeGossipOptions()
    {
        if (gossipReader.Gossips.Count == 0)
        {
            return "none";
        }

        return string.Join(
            ", ",
            gossipReader.Gossips
                .OrderBy(static option => option.Value)
                .Select(static option => $"{option.Key.ToStringF()}:{option.Value}"));
    }

    private void RejectCurrentServiceCandidate(string reason)
    {
        if (currentCandidate == default)
        {
            return;
        }

        string rejectedKey = BuildRejectedServiceCandidateKey(requestedServiceKind, currentCandidate.Entry, currentCandidate.Name);
        rejectedServiceCandidates.Add(rejectedKey);

        logger.LogWarning(
            "Rejecting service candidate Service={Service} CandidateSource={Source} Entry={Entry} Name={Name} Reason={Reason}",
            requestedServiceKind.ToStringF(),
            currentCandidate.Source.ToStringF(),
            currentCandidate.Entry,
            currentCandidate.Name,
            reason);
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

    private ServiceInteractionResult OpenExpectedServiceWindow(out bool performedServiceWork, out string failureReason)
    {
        return requestedServiceKind switch
        {
            NpcServiceKind.Vendor => OpenMerchantWindow(out performedServiceWork, out failureReason),
            NpcServiceKind.Repair => OpenMerchantWindow(out performedServiceWork, out failureReason),
            NpcServiceKind.Trainer => OpenServiceGossip(Gossip.Trainer, out performedServiceWork, out failureReason),
            NpcServiceKind.FlightMaster => OpenServiceGossip(Gossip.Taxi, out performedServiceWork, out failureReason),
            NpcServiceKind.Innkeeper => OpenInnkeeperService(out performedServiceWork, out failureReason),
            _ => OpenServiceGossip(Gossip.Gossip, out performedServiceWork, out failureReason)
        };
    }

    private ServiceInteractionResult OpenInnkeeperService(out bool performedServiceWork, out string failureReason)
    {
        ServiceInteractionResult result = OpenServiceGossip(Gossip.Binder, out performedServiceWork, out failureReason);
        if (result == ServiceInteractionResult.Success)
        {
            return result;
        }

        if (gossipReader.MerchantWindowOpened())
        {
            performedServiceWork = false;
            failureReason = string.Empty;
            return ServiceInteractionResult.Success;
        }

        if (gossipReader.Gossips.Count > 0)
        {
            performedServiceWork = false;
            failureReason = string.Empty;
            return ServiceInteractionResult.Success;
        }

        return result;
    }

    private ServiceInteractionResult OpenServiceGossip(
        Gossip expectedGossip,
        out bool performedServiceWork,
        out string failureReason)
    {
        performedServiceWork = false;
        float e = wait.Until(TIMEOUT, gossipReader.GossipStartOrMerchantWindowOpened);
        if (HasExpectedServiceGossip(requestedServiceKind, gossipReader.Gossips, gossipReader.MerchantWindowOpened()))
        {
            failureReason = string.Empty;
            return ServiceInteractionResult.Success;
        }

        e = wait.Until(TIMEOUT, gossipReader.GossipEnd);
        if (e < 0)
        {
            failureReason = "gossip_end_timeout";
            LogWarn($"Gossip - {nameof(gossipReader.GossipEnd)} not fired after {e}ms");
            return ServiceInteractionResult.Failed;
        }

        if (HasExpectedServiceGossip(requestedServiceKind, gossipReader.Gossips, gossipReader.MerchantWindowOpened()))
        {
            failureReason = string.Empty;
            return ServiceInteractionResult.Success;
        }

        failureReason = $"missing_{expectedGossip.ToStringF().ToLowerInvariant()}_gossip";
        logger.LogWarning(
            "Service validation failed Service={Service} CandidateSource={Source} Entry={Entry} Name={Name} TargetId={TargetId} TargetName={TargetName} ExpectedGossip={ExpectedGossip} MerchantOpened={MerchantOpened} GossipCount={GossipCount} GossipOptions={GossipOptions}",
            requestedServiceKind.ToStringF(),
            currentCandidate.Source.ToStringF(),
            currentCandidate.Entry,
            currentCandidate.Name,
            playerReader.TargetId,
            ResolveTargetName(playerReader.TargetId),
            expectedGossip.ToStringF(),
            gossipReader.MerchantWindowOpened(),
            gossipReader.Gossips.Count,
            DescribeGossipOptions());
        return ServiceInteractionResult.Failed;
    }

    private ServiceInteractionResult OpenMerchantWindow(out bool performedVendorWork, out string failureReason)
    {
        performedVendorWork = false;
        failureReason = string.Empty;

        float e = wait.Until(TIMEOUT, gossipReader.GossipStartOrMerchantWindowOpened);
        if (!gossipReader.MerchantWindowOpened())
        {
            e = wait.Until(TIMEOUT, gossipReader.GossipEnd);
            if (e < 0)
            {
                failureReason = "gossip_end_timeout";
                LogWarn($"Gossip - {nameof(gossipReader.GossipEnd)} not fired after {e}ms");
                return ServiceInteractionResult.Failed;
            }

            if (gossipReader.Gossips.TryGetValue(Gossip.Vendor, out int orderNum))
            {
                Log($"Picked {orderNum}th for {Gossip.Vendor.ToStringF()}");
                execGameCommand.Run($"/run SelectGossipOption({orderNum})--");
                wait.Update();
                wait.Until(TIMEOUT, gossipReader.MerchantWindowOpened);
            }
            else if (ShouldAttemptSingleOptionVendorFallback(requestedServiceKind, gossipReader.Gossips.Count, gossipReader.Gossips.ContainsKey(Gossip.Vendor)))
            {
                int fallbackOrder = gossipReader.Gossips.Values.First();
                logger.LogWarning(
                    "Vendor gossip fallback Service={Service} CandidateSource={Source} Entry={Entry} Name={Name} TargetId={TargetId} TargetName={TargetName} FallbackOrder={FallbackOrder} GossipOptions={GossipOptions}",
                    requestedServiceKind.ToStringF(),
                    currentCandidate.Source.ToStringF(),
                    currentCandidate.Entry,
                    currentCandidate.Name,
                    playerReader.TargetId,
                    ResolveTargetName(playerReader.TargetId),
                    fallbackOrder,
                    DescribeGossipOptions());
                execGameCommand.Run($"/run SelectGossipOption({fallbackOrder})--");
                wait.Update();
                wait.Until(TIMEOUT, gossipReader.MerchantWindowOpened);
            }
        }

        if (!gossipReader.MerchantWindowOpened())
        {
            failureReason = "merchant_window_not_open";
            logger.LogWarning(
                "Merchant validation failed Service={Service} CandidateSource={Source} Entry={Entry} Name={Name} TargetId={TargetId} TargetName={TargetName} MerchantOpened={MerchantOpened} GossipCount={GossipCount} GossipOptions={GossipOptions}",
                requestedServiceKind.ToStringF(),
                currentCandidate.Source.ToStringF(),
                currentCandidate.Entry,
                currentCandidate.Name,
                playerReader.TargetId,
                ResolveTargetName(playerReader.TargetId),
                gossipReader.MerchantWindowOpened(),
                gossipReader.Gossips.Count,
                DescribeGossipOptions());
            return ServiceInteractionResult.Failed;
        }

        Log($"Merchant window opened after {e}ms");

        bool hadRepairNeed = bits.Items_Broken() || playerReader.AvgEquipDurability() < 100;
        bool hadGreyToSell = bagReader.AnyGreyItem();

        if (key.ConsoleKey != default)
            input.PressRandom(key);

        if (!hadGreyToSell)
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
        return ServiceInteractionResult.Success;
    }

    private bool TryAutoSelectNPCAndSetPath()
    {
        if (areaDB.CurrentArea == null || requestedServiceKind == NpcServiceKind.None)
        {
            return false;
        }

        string[] allowedNames = GetAllowedServiceNames(key.Name);
        if (searchResult.Length == 0)
        {
            if (!TryLoadAreaCuratedCandidates(allowedNames) &&
                !TryLoadFallbackCandidates(allowedNames))
            {
                return false;
            }
        }
        else
        {
            searchIndex++;
        }

        while (true)
        {
            while (searchIndex < searchCount)
            {
                ref readonly NpcServiceCandidate candidate = ref searchResult[searchIndex];
                if (IsReasonableAutoNpcCandidate(candidate, out string reason))
                {
                    LogWarn($"Try next closest NPC -- {searchIndex}");
                    UpdateClosestCandidate();
                    return true;
                }

                LogWarn($"Skipping NPC candidate '{candidate.Name}' ({candidate.WorldPosition}) - {reason}");
                searchIndex++;
            }

            if (!fallbackSearchLoaded && TryLoadFallbackCandidates(allowedNames))
            {
                continue;
            }

            if (TryUseHardCodedVendor(requestedServiceKind))
            {
                LogWarn("Auto-search exhausted - using hard-coded service fallback");
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
    }

    private bool TryLoadAreaCuratedCandidates(string[] allowedNames)
    {
        searchResult = new NpcServiceCandidate[8];
        int found = areaDB.GetNearestAreaServiceCandidates(
            playerReader.Faction,
            requestedServiceKind,
            playerReader.WorldPos,
            allowedNames,
            searchResult.AsSpan(),
            out searchCount);
        if (found == 0 || searchCount == 0)
        {
            searchResult = [];
            searchCount = 0;
            searchIndex = 0;
            return false;
        }

        fallbackSearchLoaded = false;
        searchIndex = 0;
        logger.LogInformation(
            "Found {Count} potential {Service} NPC from {Source}.",
            searchCount,
            requestedServiceKind.ToStringF(),
            NpcServiceCandidateSource.AreaCurated.ToStringF());
        return true;
    }

    private bool TryLoadFallbackCandidates(string[] allowedNames)
    {
        NpcFlags searchFlags = GetFallbackSearchFlags(requestedServiceKind);
        if (searchFlags == NpcFlags.None)
        {
            return false;
        }

        NpcSearchResult[] fallbackResults = new NpcSearchResult[8];
        int found = areaDB.GetNearestNpcs(
            playerReader.Faction,
            searchFlags,
            playerReader.WorldPos,
            allowedNames,
            fallbackResults.AsSpan(),
            out int written);
        if (found == 0 || written == 0)
        {
            return false;
        }

        searchResult = new NpcServiceCandidate[written];
        for (int i = 0; i < written; i++)
        {
            NpcSearchResult fallbackResult = fallbackResults[i];
            searchResult[i] = new NpcServiceCandidate(
                requestedServiceKind,
                NpcServiceCandidateSource.MapWideSearch,
                fallbackResult.Creature.Entry,
                fallbackResult.Creature.Name,
                fallbackResult.WorldPosition,
                WorldMapAreaDB.ToMap_FlipXY(fallbackResult.WorldPosition, playerReader.WorldMapArea),
                fallbackResult.Creature.NpcFlag,
                fallbackResult.Creature.SubName ?? string.Empty);
        }

        searchCount = written;
        searchIndex = 0;
        fallbackSearchLoaded = true;
        logger.LogInformation(
            "Found {Count} potential {Service} NPC from {Source}.",
            searchCount,
            requestedServiceKind.ToStringF(),
            NpcServiceCandidateSource.MapWideSearch.ToStringF());
        return true;
    }

    /// <summary>
    /// Fallback method: Try to use hard-coded vendor locations when auto-search fails
    /// </summary>
    private bool TryUseHardCodedVendor(NpcServiceKind serviceKind)
    {
        if (serviceKind != NpcServiceKind.Vendor && serviceKind != NpcServiceKind.Repair)
            return false;

        if (areaDB.CurrentWorldMapArea == null)
            return false;

        string zoneName = areaDB.CurrentWorldMapArea.Value.AreaName;
        
        if (!VendorLocations.TryGetVendorsForZone(zoneName, out var vendors) || vendors == null || vendors.Count == 0)
        {
            LogWarn($"No hard-coded vendors available for zone: {zoneName}");
            return false;
        }

        Predicate<VendorLocations.VendorInfo> predicate = serviceKind == NpcServiceKind.Repair
            ? static vendor => vendor.CanRepair
            : static vendor => vendor.CanSell;
        var closestVendor = VendorLocations.FindClosestVendor(vendors, playerReader.WorldPos, predicate);
        if (closestVendor == null)
            return false;

        Vector3 mapPosition = WorldMapAreaDB.ToMap_FlipXY(closestVendor.WorldPosition, playerReader.WorldMapArea);
        currentCandidate = new NpcServiceCandidate(
            serviceKind,
            NpcServiceCandidateSource.HardCodedFallback,
            0,
            closestVendor.Name,
            closestVendor.WorldPosition,
            mapPosition,
            serviceKind == NpcServiceKind.Repair ? NpcFlags.Repair | NpcFlags.Vendor : NpcFlags.Vendor,
            closestVendor.Notes ?? string.Empty);
        key.Path = [closestVendor.WorldPosition];

        logger.LogInformation($"Using hard-coded vendor: {closestVendor.Name} at {closestVendor.WorldPosition} (Priority {closestVendor.Priority})");
        if (!string.IsNullOrEmpty(closestVendor.Notes))
        {
            LogDebug($"Vendor notes: {closestVendor.Notes}");
        }

        return true;
    }

    private static string[] GetAllowedServiceNames(string keyName)
    {
        int separator = keyName.IndexOf(' ');
        if (separator == -1)
        {
            return [];
        }

        return keyName[(separator + 1)..]
            .Split('|', options: StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
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
