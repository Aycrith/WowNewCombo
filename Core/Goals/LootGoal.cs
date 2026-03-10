using Core.Database;
using Core.GOAP;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Extensions;
using SharedLib.NpcFinder;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

using WowheadDB;

namespace Core.Goals;

public sealed partial class LootGoal : GoapGoal, IGoapEventListener
{
    internal enum KeyboardLootTargetIssue
    {
        None,
        PetTarget,
        AliveTarget
    }

    internal enum PetRefusedLootOutcome
    {
        None,
        Looted,
        CorpseNotFound,
        InteractFailed
    }

    public override float Cost => 4.6f;

    private const int MAX_TIME_TO_REACH_MELEE = GoalTimeouts.MaxTimeToReachMeleeMs;
    private const int MAX_TIME_TO_WAIT_NPC_NAME = 750;
    private const int PET_BLOCKED_CORPSE_WAIT_MS = 1200;
    private const int PET_BLOCKED_CORPSE_POLL_MS = 100;
    private const int DIRECT_CORPSE_APPROACH_TIMEOUT_MS = 1200;
    private const int LOOT_INTERACTION_CONFIRM_TIMEOUT_MS = 400;

    private readonly ILogger<LootGoal> logger;
    private readonly ConfigurableInput input;

    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly BuffStatus<IPlayer> buffs;
    private readonly Wait wait;
    private readonly AreaDB areaDb;
    private readonly StopMoving stopMoving;
    private readonly BagReader bagReader;
    private readonly ClassConfiguration classConfig;
    private readonly NpcNameTargeting npcNameTargeting;
    private readonly CombatLog combatLog;
    private readonly PlayerDirection playerDirection;
    private readonly GoapAgentState state;
    private readonly ExecGameCommand execGameCommand;

    private readonly CancellationToken token;

    private readonly List<CorpseEvent> corpseLocations = [];
    private readonly HashSet<CorpseEvent> skippedCorpseCandidatesThisWindow = [];

    private CorpseEvent? activeCorpseCandidate;
    private CorpseEvent? primaryCorpseCandidate;
    private bool canGather;
    private int targetId;
    private bool petTargetRefusedThisWindow;
    private int refusedLootTargetGuid;
    private bool corpseInteractionObservedThisWindow;
    private bool directCorpseCandidateProbeAttemptedThisWindow;

    public LootGoal(ILogger<LootGoal> logger,
        ConfigurableInput input, Wait wait,
        PlayerReader playerReader, AreaDB areaDb, BagReader bagReader,
        StopMoving stopMoving, AddonBits bits, BuffStatus<IPlayer> buffs,
        ClassConfiguration classConfig, NpcNameTargeting npcNameTargeting,
        PlayerDirection playerDirection,
        GoapAgentState state, CombatLog combatLog,
        ExecGameCommand execGameCommand,
        CancellationTokenSource cts)
        : base(nameof(LootGoal))
    {
        this.logger = logger;
        this.input = input;
        this.wait = wait;
        this.playerReader = playerReader;
        this.bits = bits;
        this.buffs = buffs;
        this.areaDb = areaDb;
        this.stopMoving = stopMoving;
        this.bagReader = bagReader;
        this.combatLog = combatLog;
        this.classConfig = classConfig;
        this.npcNameTargeting = npcNameTargeting;
        this.playerDirection = playerDirection;
        this.state = state;
        this.execGameCommand = execGameCommand;

        this.token = cts.Token;
        AddPrecondition(GoapKey.pulled, false);
        AddPrecondition(GoapKey.dangercombat, false);
        AddPrecondition(GoapKey.shouldloot, true);
        AddEffect(GoapKey.shouldloot, false);
    }

    public override bool CanRun() => !RecoveryState.IsRecoveryActive(
        buffs.Food(),
        buffs.Drink(),
        playerReader.HealthPercent(),
        playerReader.ManaPercent());

    public override void OnEnter()
    {
        stopMoving.StopForward();

        float e = wait.UntilCount(Loot.RESET_UPDATE_COUNT, LootReset);
        if (e < 0)
        {
            LogWarnWindowStillOpen(logger, playerReader.LootWindowCount.Value, e);
            wait.Fixed(Loot.LOOT_PER_ITEM_TIME_MS);
        }

        if (combatLog.DamageTakenCount() == 0)
        {
            WaitForLosingTarget();
        }

        CheckInventoryFull();
        petTargetRefusedThisWindow = false;
        refusedLootTargetGuid = 0;
        corpseInteractionObservedThisWindow = false;
        directCorpseCandidateProbeAttemptedThisWindow = false;
        skippedCorpseCandidatesThisWindow.Clear();
        activeCorpseCandidate = GetClosestCorpse();
        primaryCorpseCandidate = activeCorpseCandidate;

        if (TryLoot())
        {
            HandleSuccessfulLoot();
        }
        else
        {
            HandleFailedLoot();
        }

        CleanUpAfterLooting();

        ClearTargetIfNeeded();
    }

    private void WaitForLosingTarget()
    {
        float elapsedMs = wait.Until(playerReader.DoubleNetworkLatency, bits.NoTarget);

        LogLostTarget(logger, elapsedMs);
    }

    private void CheckInventoryFull()
    {
        if (!bagReader.BagsFull())
            return;

        logger.LogWarning("Inventory is full");
    }

    private bool TryLoot()
    {
        bool keyboardSuccessful = LootKeyboard();
        if (!keyboardSuccessful)
        {
            LogKeyboardLootFailed(logger, bits.Target());
        }
        else
        {
            return true;
        }

        if (TryWaitForPetToClearCorpse())
        {
            return true;
        }

        if (TryLootByDirectTrackedCorpseRecovery())
        {
            return true;
        }

        if (TryLootByCursorFallback())
        {
            return true;
        }

        return !input.KeyboardOnly && LootMouse();
    }

    private void HandleSuccessfulLoot()
    {
        if (bits.Target() && playerReader.IsInMeleeRange() &&
            (!bits.SoftInteract() || EligibleCorpseSoftTargetExists()))
        {
            input.PressInteract();
            wait.Update();
        }

        if (petTargetRefusedThisWindow)
        {
            logger.LogInformation("Loot pet refusal outcome: {Outcome}", ClassifyPetRefusedLootOutcome(
                looted: true,
                corpseInteractionObserved: corpseInteractionObservedThisWindow,
                directCorpseCandidateProbeAttempted: directCorpseCandidateProbeAttemptedThisWindow));
        }

        int maxTimeLootWindowOpenMs =
            Math.Max(playerReader.DoubleNetworkLatency, Loot.LOOTFRAME_OPEN_TIME_MS);

        float windowOpenElapsedMs = wait.Until(maxTimeLootWindowOpenMs,
            LootWindowOpen,
            TryPressSafeApproachOnCooldown);

        int availableItems = playerReader.LootWindowCount.Value;
        state.RecentlyLooted.Add(playerReader.TargetGuid);

        int maxTimeLootWindowClosedMs =
            Math.Max(playerReader.LootWindowCount.Value, 1) *
            (playerReader.DoubleNetworkLatency + Loot.LOOT_PER_ITEM_TIME_MS);

        float windowClosedElapsedMs = wait.Until(maxTimeLootWindowClosedMs, LootWindowClosed);

        bool success = windowOpenElapsedMs >= 0 && windowClosedElapsedMs >= 0;
        if (success)
        {
            LogLootSuccess(logger, availableItems, windowOpenElapsedMs, windowClosedElapsedMs);
        }
        else
        {
            SendGoapEvent(ScreenCaptureEvent.Default);
            LogLootFailed(logger, windowOpenElapsedMs, windowClosedElapsedMs);
        }

        if (success)
        {
            GatherCorpseIfNeeded();
        }

        if (bits.LootFrameShown())
        {
            input.PressESC();
            wait.Update();
        }
    }

    private void GatherCorpseIfNeeded()
    {
        if (!canGather)
            return;

        state.GatherableCorpseCount++;

        CorpseEvent? ce = GetOrSelectActiveCorpseCandidate();
        if (ce == null)
            return;

        SendGoapEvent(new SkinCorpseEvent(ce.MapLoc, ce.Radius, targetId));
    }

    private void HandleFailedLoot()
    {
        if (primaryCorpseCandidate != null)
        {
            activeCorpseCandidate = primaryCorpseCandidate;
        }

        if (petTargetRefusedThisWindow)
        {
            logger.LogWarning("Loot pet refusal outcome: {Outcome}", ClassifyPetRefusedLootOutcome(
                looted: false,
                corpseInteractionObserved: corpseInteractionObservedThisWindow,
                directCorpseCandidateProbeAttempted: directCorpseCandidateProbeAttemptedThisWindow));
        }

        SendGoapEvent(ScreenCaptureEvent.Default);
        Log("Loot Failed, target not found!");
    }

    private void CleanUpAfterLooting()
    {
        SendGoapEvent(new RemoveClosestPoi(CorpseEvent.NAME));
        state.LootableCorpseCount = Math.Max(0, state.LootableCorpseCount - 1);

        if (activeCorpseCandidate != null && corpseLocations.Remove(activeCorpseCandidate))
        {
            activeCorpseCandidate = null;
            primaryCorpseCandidate = null;
            skippedCorpseCandidatesThisWindow.Clear();
            return;
        }

        activeCorpseCandidate = null;
        primaryCorpseCandidate = null;
        skippedCorpseCandidatesThisWindow.Clear();
        if (corpseLocations.Count > 0)
        {
            corpseLocations.Remove(GetClosestCorpse()!);
        }
    }

    private void ClearTargetIfNeeded()
    {
        if (canGather || !bits.Target() || !bits.Target_Dead())
        {
            return;
        }

        bool cleared = input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
        if (!cleared && bits.Target())
        {
            SendGoapEvent(ScreenCaptureEvent.Default);
            LogWarning("Unable to clear target! Check Bindpad settings!");
        }
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e is CorpseEvent corpseEvent)
        {
            corpseLocations.Add(corpseEvent);
        }
    }

    private bool FoundByCursor()
    {
        npcNameTargeting.ChangeNpcType(NpcNames.Corpse);

        try
        {
            wait.Fixed(playerReader.NetworkLatency);
            npcNameTargeting.WaitForUpdate();

            float elapsedMs = wait.Until(MAX_TIME_TO_WAIT_NPC_NAME, npcNameTargeting.FoundAny);
            LogFoundNpcNameCount(logger, npcNameTargeting.NpcCount, elapsedMs);
            if (elapsedMs < 0)
            {
                return false;
            }

            ReadOnlySpan<CursorType> types = [CursorType.Loot, CursorType.Vendor];
            if (!npcNameTargeting.FindBy(types, token))
            {
                return false;
            }

            corpseInteractionObservedThisWindow = true;
            Log("Nearest Corpse mouseover interaction sent...");
            float targetElapsedMs = WaitForLootInteraction();
            if (targetElapsedMs < 0 && !bits.Target() && !LootWindowOpen())
            {
                Log("Loot not opened after mouseover interaction; trying right-click fallback...");
                input.RightClickCurrentMouse();
                wait.Update();
                targetElapsedMs = WaitForLootInteraction();
            }

            if (targetElapsedMs < 0 && !bits.Target() && !LootWindowOpen())
            {
                Log("Loot still not opened after right-click; trying left-click fallback...");
                input.LeftClickCurrentMouse();
                wait.Update();
                targetElapsedMs = WaitForLootInteraction();
            }

            if (targetElapsedMs < 0 && !bits.Target() && !LootWindowOpen())
            {
                Log("Loot still not opened after mouse click fallbacks; trying direct interact...");
                input.PressInteract();
                wait.Update();
                targetElapsedMs = WaitForLootInteraction();
            }

            LogFoundNpcNameCount(logger, npcNameTargeting.NpcCount, targetElapsedMs);

            CheckForCanGather();
            if (TryOpenLootOnCurrentCorpseTarget())
            {
                return true;
            }

            if (ShouldAttemptLootOpenAfterCorpseAcquire(
                hasTarget: bits.Target(),
                targetDead: bits.Target_Dead(),
                lootWindowOpen: LootWindowOpen(),
                inLootRange: playerReader.IsInMeleeRange() || playerReader.MinRangeZero()))
            {
                return false;
            }

            if (!MoveToTargetAndReached())
            {
                return false;
            }

            return TryOpenLootOnCurrentCorpseTarget();
        }
        finally
        {
            npcNameTargeting.ChangeNpcType(NpcNames.None);
        }
    }

    private CorpseEvent? GetClosestCorpse()
    {
        CorpseEvent? closest = null;

        float minDistance = float.MaxValue;
        Vector3 playerWorldLoc = playerReader.WorldPos;

        foreach (CorpseEvent corpse in corpseLocations)
        {
            if (skippedCorpseCandidatesThisWindow.Contains(corpse))
            {
                continue;
            }

            Vector3 worldPos = WorldMapAreaDB.ToWorld_FlipXY(corpse.MapLoc, playerReader.WorldMapArea);

            float distance = playerWorldLoc.WorldDistanceXYTo(worldPos);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = corpse;
            }
        }

        return closest;
    }

    private CorpseEvent? GetOrSelectActiveCorpseCandidate()
    {
        if (activeCorpseCandidate != null &&
            !skippedCorpseCandidatesThisWindow.Contains(activeCorpseCandidate) &&
            corpseLocations.Contains(activeCorpseCandidate))
        {
            return activeCorpseCandidate;
        }

        activeCorpseCandidate = GetClosestCorpse();
        return activeCorpseCandidate;
    }

    private float WaitForLootInteraction()
        => wait.Until(GetLootInteractionTimeoutMs(playerReader.DoubleNetworkLatency), () => bits.Target() || LootWindowOpen());

    private void CheckForCanGather()
    {
        if (!classConfig.GatherCorpse ||
            areaDb.CurrentArea == null)
            return;

        targetId = playerReader.TargetId;
        Area area = areaDb.CurrentArea;

        canGather = GatherAvailable(classConfig, areaDb, area, targetId);

        LogShouldGather(logger, targetId, canGather);
    }

    private static bool GatherAvailable(ClassConfiguration config, AreaDB areaDB, Area area, int npcId) =>
        (config.Skin && areaDB.TryGetCreature(npcId, out Creature c) && c.SkinLoot != 0) ||
        (config.Herb && area.gatherable.AsSpan().BinarySearch(npcId) >= 0) ||
        (config.Mine && area.minable.AsSpan().BinarySearch(npcId) >= 0) ||
        (config.Salvage && area.salvegable.AsSpan().BinarySearch(npcId) >= 0);

    private bool LootWindowOpen()
    {
        return playerReader.LootWindowCount.Value > 0 ||
            (LootStatus)playerReader.LootEvent.Value is LootStatus.READY;
    }

    private bool LootWindowClosed() => !bits.LootFrameShown();

    private bool LootMouse()
    {
        stopMoving.Stop();
        wait.Update();

        if (FoundByCursor())
        {
            return true;
        }
        else if (corpseLocations.Count > 0)
        {
            Vector3 playerMap = playerReader.MapPos;
            CorpseEvent e = GetOrSelectActiveCorpseCandidate()!;
            float heading = DirectionCalculator.CalculateMapHeading(playerMap, e.MapLoc);
            playerDirection.SetDirection(heading);
            wait.Fixed(playerReader.DoubleNetworkLatency);
            wait.Update();

            logger.LogInformation("Look at possible closest corpse and try once again...");

            if (FoundByCursor())
            {
                return true;
            }
        }

        if (ShouldSkipKeyboardRetryAfterPetRefusal(petTargetRefusedThisWindow))
        {
            Log($"Skipping keyboard retry after pet target refusal for guid {refusedLootTargetGuid}; preferring corpse completion only for this loot window.");
            return false;
        }

        return LootKeyboard();
    }

    private bool LootKeyboard()
    {
        CorpseEvent? e = GetOrSelectActiveCorpseCandidate();
        if (e != null)
        {
            float targetDirection = DirectionCalculator.CalculateMapHeading(playerReader.MapPos, e.MapLoc);
            playerDirection.SetDirection(targetDirection);

            wait.Fixed(playerReader.DoubleNetworkLatency);
            wait.Update();
        }

        bool targetClearedThisWindow = false;

        if (TrySoftInteractCorpse())
        {
            if (state.RecentlyLooted.Contains(playerReader.TargetGuid))
            {
                logger.LogError("Keyboard target already looted 1");
                input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
                targetClearedThisWindow = true;
            }
            else
            {
                return true;
            }
        }
        else if (bits.Target() && state.RecentlyLooted.Contains(playerReader.TargetGuid))
        {
            logger.LogError("Keyboard target already looted 1");
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
            targetClearedThisWindow = true;
        }

        if (!bits.Target() && ShouldRetryLastTargetAfterTargetClear(targetClearedThisWindow))
        {
            input.PressLastTargetAndWait(wait, bits.Target);

            if (state.RecentlyLooted.Contains(playerReader.TargetGuid))
            {
                logger.LogError("Keyboard target already looted 2");
                input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
                targetClearedThisWindow = true;
            }
        }

        if (bits.Target())
        {
            int targetGuid = playerReader.TargetGuid;
            Log($"Keyboard last target {targetGuid}!");
            if (state.RecentlyLooted.Contains(targetGuid))
            {
                input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
                targetClearedThisWindow = true;

                LogWarning($"Keyboard target already looted! {targetGuid}");
            }
            else
            {
                Log($"Keyboard last target found!");
            }
        }

        if (!bits.Target())
        {
            if (ShouldAttemptTrackedCorpseCandidateAfterTargetClear(
                targetClearedThisWindow,
                GetOrSelectActiveCorpseCandidate() != null) &&
                TryInteractWithActiveCorpseCandidateInRange())
            {
                return true;
            }

            LogWarning($"Keyboard No target found!");
            return false;
        }

        KeyboardLootTargetIssue targetIssue = ClassifyKeyboardLootTarget(
            hasTarget: bits.Target(),
            targetDead: bits.Target_Dead(),
            targetGuid: playerReader.TargetGuid,
            petGuid: playerReader.PetGuid);

        if (targetIssue == KeyboardLootTargetIssue.PetTarget)
        {
            petTargetRefusedThisWindow = true;
            refusedLootTargetGuid = playerReader.TargetGuid;
            LogWarning($"Keyboard refusing pet target during loot! {playerReader.TargetGuid}");
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
            targetClearedThisWindow = true;

            if (ShouldAttemptTrackedCorpseCandidateAfterTargetClear(
                targetClearedThisWindow,
                GetOrSelectActiveCorpseCandidate() != null) &&
                TryInteractWithActiveCorpseCandidateInRange())
            {
                return true;
            }

            return false;
        }

        if (targetIssue == KeyboardLootTargetIssue.AliveTarget)
        {
            LogWarning("Keyboard Don't attack alive target!");

            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);

            return false;
        }

        CheckForCanGather();

        return (bits.Target() && playerReader.MinRangeZero()) || MoveToTargetAndReached();
    }

    internal static KeyboardLootTargetIssue ClassifyKeyboardLootTarget(
        bool hasTarget,
        bool targetDead,
        int targetGuid,
        int petGuid)
    {
        if (!hasTarget)
        {
            return KeyboardLootTargetIssue.None;
        }

        if (petGuid != 0 && targetGuid == petGuid)
        {
            return KeyboardLootTargetIssue.PetTarget;
        }

        return targetDead
            ? KeyboardLootTargetIssue.None
            : KeyboardLootTargetIssue.AliveTarget;
    }

    internal static bool ShouldRetryLastTargetAfterTargetClear(bool targetClearedThisWindow)
        => !targetClearedThisWindow;

    internal static bool ShouldAttemptTrackedCorpseCandidateAfterTargetClear(
        bool targetClearedThisWindow,
        bool hasTrackedCorpseCandidate)
        => targetClearedThisWindow && hasTrackedCorpseCandidate;

    internal static bool ShouldTryDirectTrackedCorpseRecovery(
        bool petTargetRefusedThisWindow,
        bool hasTrackedCorpseCandidate)
        => petTargetRefusedThisWindow && hasTrackedCorpseCandidate;

    internal static bool ShouldTrySecondaryTrackedCorpseCandidate(
        bool primaryCandidateFailed,
        bool hasSecondaryCandidate,
        int unresolvedTrackedCorpseCount)
        => primaryCandidateFailed &&
        hasSecondaryCandidate &&
        unresolvedTrackedCorpseCount > 1;

    internal static int GetLootInteractionTimeoutMs(int doubleNetworkLatencyMs)
        => Math.Max(doubleNetworkLatencyMs, LOOT_INTERACTION_CONFIRM_TIMEOUT_MS);

    internal static bool ShouldContinuePassivePetClearWait(
        bool lootWindowOpen,
        bool hasEligibleCorpseTarget,
        bool corpseNameVisible,
        bool petStillBlocking)
        => !lootWindowOpen &&
        !hasEligibleCorpseTarget &&
        !corpseNameVisible &&
        petStillBlocking;

    internal static bool ShouldRetryDirectCorpseProbe(
        int attemptsUsed,
        int maxAttempts,
        bool corpseCandidateStillInRange,
        bool lootWindowOpen)
        => !lootWindowOpen &&
        corpseCandidateStillInRange &&
        attemptsUsed < maxAttempts;

    internal static bool ShouldProbeTrackedCorpseBeforeCursorFallback(bool petTargetRefusedThisWindow)
        => !petTargetRefusedThisWindow;

    internal static bool ShouldSkipKeyboardRetryAfterPetRefusal(bool petTargetRefusedThisWindow) => petTargetRefusedThisWindow;

    internal static bool ShouldWaitForPetToClearCorpse(
        bool petTargetRefusedThisWindow,
        int refusedLootTargetGuid,
        int petGuid,
        int corpseLocationCount)
        => petTargetRefusedThisWindow &&
        petGuid != 0 &&
        refusedLootTargetGuid == petGuid &&
        corpseLocationCount > 0;

    internal static bool ShouldFaceClosestCorpseBeforeCursorFallback(bool petTargetRefusedThisWindow, int corpseLocationCount)
        => petTargetRefusedThisWindow && corpseLocationCount > 0;

    internal static bool ShouldAttemptLootOpenAfterCorpseAcquire(
        bool hasTarget,
        bool targetDead,
        bool lootWindowOpen,
        bool inLootRange)
        => hasTarget &&
        targetDead &&
        !lootWindowOpen &&
        inLootRange;

    internal static PetRefusedLootOutcome ClassifyPetRefusedLootOutcome(
        bool looted,
        bool corpseInteractionObserved,
        bool directCorpseCandidateProbeAttempted)
    {
        if (!looted && !corpseInteractionObserved && !directCorpseCandidateProbeAttempted)
        {
            return PetRefusedLootOutcome.CorpseNotFound;
        }

        if (!looted)
        {
            return PetRefusedLootOutcome.InteractFailed;
        }

        return PetRefusedLootOutcome.Looted;
    }

    private bool TryLootByCursorFallback()
    {
        if (corpseLocations.Count == 0)
        {
            return false;
        }

        stopMoving.Stop();
        wait.Update();

        if (ShouldFaceClosestCorpseBeforeCursorFallback(petTargetRefusedThisWindow, corpseLocations.Count))
        {
            FaceClosestTrackedCorpse();
        }

        if (ShouldProbeTrackedCorpseBeforeCursorFallback(petTargetRefusedThisWindow) &&
            TryInteractWithActiveCorpseCandidateInRange())
        {
            return true;
        }

        FaceClosestTrackedCorpse();
        Log("Look at active corpse candidate and do bounded cursor fallback...");
        return FoundByCursor();
    }

    private bool TryLootByDirectTrackedCorpseRecovery()
    {
        if (!ShouldTryDirectTrackedCorpseRecovery(
            petTargetRefusedThisWindow,
            GetOrSelectActiveCorpseCandidate() != null))
        {
            return false;
        }

        if (TryResolveActiveCorpseCandidate(allowApproach: true, directInteractAttempts: 2))
        {
            return true;
        }

        bool advanced = TryAdvanceToSecondaryCorpseCandidate();
        if (!ShouldTrySecondaryTrackedCorpseCandidate(
            primaryCandidateFailed: true,
            hasSecondaryCandidate: advanced,
            unresolvedTrackedCorpseCount: Math.Max(state.LootableCorpseCount, corpseLocations.Count)))
        {
            return false;
        }

        Log("Primary corpse candidate failed after pet block; trying one secondary tracked corpse candidate.");
        if (TryResolveActiveCorpseCandidate(allowApproach: true, directInteractAttempts: 2))
        {
            return true;
        }

        return false;
    }

    private bool TryWaitForPetToClearCorpse()
    {
        if (!ShouldWaitForPetToClearCorpse(
            petTargetRefusedThisWindow,
            refusedLootTargetGuid,
            playerReader.PetGuid,
            corpseLocations.Count))
        {
            return false;
        }

        logger.LogInformation(
            "Pet blocked corpse for loot; waiting up to {waitMs}ms before bounded corpse fallback.",
            PET_BLOCKED_CORPSE_WAIT_MS);
        stopMoving.Stop();
        wait.Update();
        FaceClosestTrackedCorpse();

        long deadlineTick = Environment.TickCount64 + PET_BLOCKED_CORPSE_WAIT_MS;
        while (!token.IsCancellationRequested && Environment.TickCount64 < deadlineTick)
        {
            bool lootWindowOpen = LootWindowOpen();
            if (lootWindowOpen)
            {
                return true;
            }

            bool hasEligibleCorpseTarget = HasEligibleCurrentCorpseTargetForLoot();
            bool corpseNameVisible = CorpseNameVisible();
            bool petStillBlocking = !bits.Target() || playerReader.TargetGuid == refusedLootTargetGuid;
            if (!ShouldContinuePassivePetClearWait(
                lootWindowOpen,
                hasEligibleCorpseTarget,
                corpseNameVisible,
                petStillBlocking))
            {
                return false;
            }

            wait.Update(PET_BLOCKED_CORPSE_POLL_MS);
        }

        logger.LogInformation(
            "Pet blocked corpse wait expired after {waitMs}ms for pet guid {petGuid}.",
            PET_BLOCKED_CORPSE_WAIT_MS,
            refusedLootTargetGuid);
        return false;
    }

    private bool TryAdvanceToSecondaryCorpseCandidate()
    {
        if (activeCorpseCandidate == null)
        {
            return false;
        }

        skippedCorpseCandidatesThisWindow.Add(activeCorpseCandidate);
        activeCorpseCandidate = GetClosestCorpse();
        return activeCorpseCandidate != null;
    }

    private void FaceClosestTrackedCorpse()
    {
        CorpseEvent? corpse = GetOrSelectActiveCorpseCandidate();
        if (corpse == null)
        {
            return;
        }

        Vector3 playerMap = playerReader.MapPos;
        float heading = DirectionCalculator.CalculateMapHeading(playerMap, corpse.MapLoc);
        playerDirection.SetDirection(heading);
        wait.Fixed(playerReader.DoubleNetworkLatency);
        wait.Update();
    }

    private bool TrySoftInteractCorpse()
    {
        if (!bits.SoftInteract_Enabled() ||
            (bits.SoftInteract() && !EligibleCorpseSoftTargetExists()))
        {
            return false;
        }

        input.PressInteract();
        wait.Update();

        if (state.RecentlyLooted.Contains(playerReader.TargetGuid))
        {
            return true;
        }

        corpseInteractionObservedThisWindow = bits.Target() || LootWindowOpen();
        CheckForCanGather();

        return LootWindowOpen() || (bits.Target() && playerReader.MinRangeZero()) || MoveToTargetAndReached();
    }

    private bool HasEligibleCurrentCorpseTargetForLoot()
    {
        return bits.Target() &&
            bits.Target_Dead() &&
            playerReader.TargetGuid != 0 &&
            playerReader.TargetGuid != refusedLootTargetGuid &&
            !state.RecentlyLooted.Contains(playerReader.TargetGuid);
    }

    private bool CorpseNameVisible()
    {
        npcNameTargeting.ChangeNpcType(NpcNames.Corpse);

        try
        {
            npcNameTargeting.WaitForUpdate(token);
            return npcNameTargeting.FoundAny();
        }
        finally
        {
            npcNameTargeting.ChangeNpcType(NpcNames.None);
        }
    }

    private bool TryInteractWithActiveCorpseCandidateInRange()
        => TryInteractWithActiveCorpseCandidateInRange(maxAttempts: 1);

    private bool TryInteractWithActiveCorpseCandidateInRange(int maxAttempts)
    {
        CorpseEvent? corpse = GetOrSelectActiveCorpseCandidate();
        if (corpse == null || maxAttempts <= 0)
        {
            return false;
        }

        if (!IsTrackedCorpseCandidateInInteractRange(GetTrackedCorpseCandidateDistance(corpse)))
        {
            return false;
        }

        int attemptsUsed = 0;
        while (attemptsUsed < maxAttempts)
        {
            attemptsUsed++;

            FaceClosestTrackedCorpse();
            Log("Tracked corpse candidate in interact range; pressing interact before cursor fallback.");
            directCorpseCandidateProbeAttemptedThisWindow = true;
            input.PressInteract();
            wait.Update();

            corpseInteractionObservedThisWindow = bits.Target() || LootWindowOpen();
            if (TryOpenLootOnCurrentCorpseTarget())
            {
                return true;
            }

            bool corpseCandidateStillInRange = corpseLocations.Contains(corpse) &&
                IsTrackedCorpseCandidateInInteractRange(GetTrackedCorpseCandidateDistance(corpse));

            if (!ShouldRetryDirectCorpseProbe(
                attemptsUsed,
                maxAttempts,
                corpseCandidateStillInRange,
                LootWindowOpen()))
            {
                break;
            }

            Log("Tracked corpse candidate direct probe did not open loot; refacing and retrying once.");
            wait.Fixed(Math.Max(playerReader.NetworkLatency, 50));
        }

        return false;
    }

    private bool TryResolveActiveCorpseCandidate(bool allowApproach, int directInteractAttempts)
    {
        FaceClosestTrackedCorpse();
        if (TryInteractWithActiveCorpseCandidateInRange(directInteractAttempts))
        {
            return true;
        }

        if (!allowApproach || !TryApproachActiveCorpseCandidate())
        {
            return false;
        }

        return TryInteractWithActiveCorpseCandidateInRange(directInteractAttempts);
    }

    private float GetTrackedCorpseCandidateDistance(CorpseEvent corpse)
    {
        Vector3 worldPos = WorldMapAreaDB.ToWorld_FlipXY(corpse.MapLoc, playerReader.WorldMapArea);
        return playerReader.WorldPos.WorldDistanceXYTo(worldPos);
    }

    internal static bool IsTrackedCorpseCandidateInInteractRange(float corpseDistanceYards)
        => corpseDistanceYards <= 5f;

    private bool TryApproachActiveCorpseCandidate()
    {
        CorpseEvent? corpse = GetOrSelectActiveCorpseCandidate();
        if (corpse == null)
        {
            return false;
        }

        float initialDistance = GetTrackedCorpseCandidateDistance(corpse);
        if (IsTrackedCorpseCandidateInInteractRange(initialDistance))
        {
            return true;
        }

        FaceClosestTrackedCorpse();
        Log("Active corpse candidate out of interact range; doing one bounded forward probe.");
        input.StartForward(true);

        try
        {
            long deadlineTick = Environment.TickCount64 + DIRECT_CORPSE_APPROACH_TIMEOUT_MS;
            while (!token.IsCancellationRequested && Environment.TickCount64 < deadlineTick)
            {
                if (LootWindowOpen())
                {
                    return true;
                }

                if (bits.Target() && bits.Target_Dead() && playerReader.MinRangeZero())
                {
                    return true;
                }

                if (IsTrackedCorpseCandidateInInteractRange(GetTrackedCorpseCandidateDistance(corpse)))
                {
                    return true;
                }

                wait.Update(PET_BLOCKED_CORPSE_POLL_MS);
            }

            return false;
        }
        finally
        {
            stopMoving.Stop();
            wait.Update();
        }
    }

    private bool TryOpenLootOnCurrentCorpseTarget()
    {
        if (!ShouldAttemptLootOpenAfterCorpseAcquire(
            hasTarget: bits.Target(),
            targetDead: bits.Target_Dead(),
            lootWindowOpen: LootWindowOpen(),
            inLootRange: playerReader.IsInMeleeRange() || playerReader.MinRangeZero()))
        {
            return LootWindowOpen();
        }

        Log("Corpse targeted in loot range without loot window; pressing interact to complete loot.");
        input.PressInteract();
        wait.Update();

        return WaitForLootInteraction() >= 0 && LootWindowOpen();
    }

    private bool EligibleCorpseSoftTargetExists() =>
        bits.SoftInteract() &&
        bits.SoftInteract_Hostile() &&
        bits.SoftInteract_Dead() &&
        !bits.SoftInteract_Tagged() &&
        playerReader.SoftInteract_Type == GuidType.Creature;

    private bool MoveToTargetAndReached()
    {
        if (!bits.Moving())
        {
            logger.LogInformation("Moving to corpse...");
            wait.While(input.Approach.OnCooldown);

            TryPressSafeApproachOnCooldownIfNeeded();
            float movementStartedMs = wait.Until(input.Approach.PressDuration, bits.Moving);
            logger.LogWarning("Movement Detected ? {elapsedMs}ms", movementStartedMs);
        }

        float elapsedMs = wait.Until(MAX_TIME_TO_REACH_MELEE,
            NotMovingOrLootAvailable, TryPressSafeApproachOnCooldownIfNeeded);

        LogReachedCorpse(logger, bits.Target(), bits.Moving(), playerReader.MinRangeZero(), elapsedMs);

        return LootWindowOpen() || (bits.Target() && playerReader.MinRangeZero());
    }

    private bool NotMovingOrLootAvailable() => !bits.Target() || bits.NotMoving() || playerReader.LootWindowCount.Value > 0;

    private void TryPressSafeApproachOnCooldownIfNeeded()
    {
        if (bits.Target() && (!bits.SoftInteract() || EligibleCorpseSoftTargetExists()))
        {
            if (!bits.Moving() && !playerReader.IsInMeleeRange())
            {
                if (input.PressedApproachOnCooldown())
                {
                    wait.Update();
                }
            }
        }
    }

    private void TryPressSafeApproachOnCooldown()
    {
        if (bits.Target() && input.PressedApproachOnCooldown())
        {
            wait.Update();
        }
    }


    private bool LootReset()
    {
        return (LootStatus)playerReader.LootEvent.Value == LootStatus.CORPSE;
    }

    #region Logging

    private void Log(string text)
    {
        logger.LogInformation(text);
    }

    private void LogWarning(string text)
    {
        logger.LogWarning(text);
    }

    [LoggerMessage(
        EventId = 0130,
        Level = LogLevel.Information,
        Message = "Loot Successful items: {count} - open: {openElapsedMs}ms - close: {closedElapsedMs}ms")]
    static partial void LogLootSuccess(ILogger logger, int count, float openElapsedMs, float closedElapsedMs);

    [LoggerMessage(
        EventId = 0131,
        Level = LogLevel.Information,
        Message = "Loot Failed open: {openElapsedMs}ms - close: {closedElapsedMs}ms")]
    static partial void LogLootFailed(ILogger logger, float openElapsedMs, float closedElapsedMs);

    [LoggerMessage(
        EventId = 0132,
        Level = LogLevel.Information,
        Message = "Found NpcName Count: {npcCount} {elapsedMs}ms")]
    static partial void LogFoundNpcNameCount(ILogger logger, int npcCount, float elapsedMs);

    [LoggerMessage(
        EventId = 0133,
        Level = LogLevel.Information,
        Message = "Has target ? {hasTarget} | moving ? {moving} | meleeRange ? {meleeRange} | Reached corpse ? {elapsedMs}ms")]
    static partial void LogReachedCorpse(ILogger logger, bool hasTarget, bool moving, bool meleeRange, float elapsedMs);

    [LoggerMessage(
        EventId = 0134,
        Level = LogLevel.Information,
        Message = "Should gather {targetId} ? {shouldGather}")]
    static partial void LogShouldGather(ILogger logger, int targetId, bool shouldGather);

    [LoggerMessage(
        EventId = 0135,
        Level = LogLevel.Information,
        Message = "Lost target {elapsedMs}ms")]
    static partial void LogLostTarget(ILogger logger, float elapsedMs);

    [LoggerMessage(
        EventId = 0136,
        Level = LogLevel.Error,
        Message = "Keyboard loot failed! Has target ? {hasTarget}")]
    static partial void LogKeyboardLootFailed(ILogger logger, bool hasTarget);

    [LoggerMessage(
        EventId = 0147,
        Level = LogLevel.Warning,
        Message = "OnEnter window still open! Available Loot: {count} {elapsedMs}ms")]
    static partial void LogWarnWindowStillOpen(ILogger logger, int count, float elapsedMs);

    #endregion
}
