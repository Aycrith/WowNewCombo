using Core.BehaviorTree;
using Core.CombatRotation;
using Core.FeatureFlags;
using Core.GOAP;

using Game;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Numerics;

namespace Core.Goals;

public sealed class CombatGoal : GoapGoal, IGoapEventListener, IDisposable
{
    public override float Cost => 4f;
    private const int FaceTargetAssistCooldownMs = 1500;
    private const float FaceAssistDirectionDeltaRadians = MathF.PI / 36f; // ~5 deg
    private const int LostTargetRecoveryCooldownMs = 900;
    private const int LostTargetBurstRecoveryCooldownMs = 1600;
    private const int LostTargetReacquireTimeoutMs = 300;
    private const int LostTargetBurstThreshold = 3;
    private const int LostTargetBurstWindowMs = 10_000;
    private const int LostTargetBurstCooldownMs = 10_000;
    private const int LostTargetBurstLogCooldownMs = 3_000;
    private const int TargetlessCombatGraceHoldMs = 250;
    private const int RecentCombatProgressWindowMs = 2_500;
    private const int DeadTargetClearRecentWindowMs = 1_500;
    private const int RuntimeMetricsWindowMs = 10 * 60 * 1000;

    private readonly ILogger<CombatGoal> logger;
    private readonly ConfigurableInput input;
    private readonly ClassConfiguration classConfig;
    private readonly Wait wait;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly StopMoving stopMoving;
    private readonly CastingHandler castingHandler;
    private readonly IMountHandler mountHandler;
    private readonly CombatLog combatLog;
    private readonly GoapAgentState state;
    private readonly IRotationOptimizer rotationOptimizer;
    private readonly BehaviorTreeCombatEngine? behaviorTreeEngine;
    private readonly FeatureFlagsOptions featureFlags;
    private readonly bool prefersRangedCombat;
    private readonly bool holdRangedStandoff;

    private float lastDirection;
    private float lastMinDistance;
    private float lastMaxDistance;
    private long lastFaceTargetAssistTick;
    private long lastLostTargetHandlingTick;
    private long lastLostTargetBurstTick;
    private long lastLostTargetLogTick;
    private long lastKillCreditTick;
    private long lastDeadTargetClearTick;
    private long lastKillToLootHandoffTick;
    private readonly object runtimeMetricsLock = new();
    private readonly Queue<long> lostTargetTicks = [];
    private readonly Queue<long> lostTargetBurstTicks = [];
    private readonly Queue<long> lostTargetReacquireAttemptTicks = [];
    private readonly Queue<long> lostTargetReacquireSuccessTicks = [];
    private readonly Queue<long> targetlessCombatGraceUsedTicks = [];
    private readonly Queue<long> reacquireFallbackAttemptTicks = [];
    private readonly Queue<long> reacquireFallbackSuccessTicks = [];
    private BehaviorContext? behaviorContext;
    private LostTargetRecoveryStep lostTargetRecoveryStep;
    private bool killToLootHandoffActive;
    private bool disposed;

    private enum LostTargetRecoveryStep
    {
        None,
        LastTargetAttempted,
        NearestTargetFallbackAttempted,
        PetFallbackAttempted
    }

    /// <summary>
    /// Number of consecutive GOAP ticks on which the target-loss condition fired.
    /// A grace period of 2 ticks is required before acting, which absorbs single-frame
    /// addon latency gaps that temporarily report bits.Target() == false mid-combat.
    /// </summary>
    private int targetLostConsecutiveTicks;

    public CombatGoal(ILogger<CombatGoal> logger, ConfigurableInput input,
        Wait wait, PlayerReader playerReader, StopMoving stopMoving, AddonBits bits,
        ClassConfiguration classConfig,
        CastingHandler castingHandler, CombatLog combatLog,
        GoapAgentState state,
        IMountHandler mountHandler,
        IRotationOptimizer rotationOptimizer,
        IOptions<FeatureFlagsOptions> featureFlagsOptions,
        IBehaviorTreeCombatEngineFactory? behaviorTreeFactory = null)
        : base(nameof(CombatGoal))
    {
        this.logger = logger;
        this.input = input;

        this.wait = wait;
        this.playerReader = playerReader;
        this.bits = bits;
        this.combatLog = combatLog;
        this.state = state;

        this.stopMoving = stopMoving;
        this.castingHandler = castingHandler;
        this.mountHandler = mountHandler;
        this.classConfig = classConfig;
        this.rotationOptimizer = rotationOptimizer;
        this.featureFlags = featureFlagsOptions.Value;
        this.prefersRangedCombat = HasRangedCombatPreference(classConfig);
        this.holdRangedStandoff = ApproachTargetGoal.ShouldHoldPullStandoff(playerReader.Class);
        this.combatLog.KillCredit += HandleKillCredit;

        // Initialize behavior tree if enabled and factory provided
        if (behaviorTreeFactory != null && this.featureFlags.BehaviorTreeCombat?.Enabled == true)
        {
            this.behaviorTreeEngine = behaviorTreeFactory.CreateEngine();
            this.behaviorTreeEngine.SetBehaviorTree(this.behaviorTreeEngine.BuildCombatTree(classConfig));
            this.logger.LogInformation("[CombatGoal] Behavior tree combat system enabled");
        }

        AddPrecondition(GoapKey.incombat, true);
        // CombatGoal is the combat-state handler, not only the "already have a valid target in
        // range" executor. During loot/kill transitions WoW can remain in-combat while the target
        // is dead/cleared, and CombatGoal.Update() contains the threat reacquire / wait-to-leave-
        // combat logic for that exact case.
        //AddPrecondition(GoapKey.targettargetsus, true);
        // Do not require in-combat range at planner time. Multi-target transitions can leave
        // us briefly out of range while still in combat, and gating CombatGoal here creates a
        // planner gap (CombatGoal requires in-range, ApproachTargetGoal requires not in combat).

        AddEffect(GoapKey.producedcorpse, true);
        AddEffect(GoapKey.targetisalive, false);
        AddEffect(GoapKey.hastarget, false);

        Keys = classConfig.Combat.Sequence;
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e is GoapStateEvent s && s.Key == GoapKey.producedcorpse)
        {
            // have to check range
            // ex. target died far away have to consider the range and approximate
            float distance = (lastMaxDistance + lastMinDistance) / 2f;
            SendGoapEvent(new CorpseEvent(GetCorpseLocation(distance), distance, playerReader.Direction, playerReader.MapPos));
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        combatLog.KillCredit -= HandleKillCredit;
        disposed = true;
    }

    private void ResetCooldowns()
    {
        ReadOnlySpan<KeyAction> span = Keys;
        for (int i = 0; i < span.Length; i++)
        {
            KeyAction keyAction = span[i];
            if (keyAction.ResetOnNewTarget)
            {
                keyAction.ResetCooldown();
                keyAction.ResetCharges();
            }
        }
    }

    public override void OnEnter()
    {
        if (mountHandler.IsMounted())
        {
            mountHandler.Dismount();
        }

        lastDirection = playerReader.Direction;
        lastFaceTargetAssistTick = 0;
        lastLostTargetHandlingTick = 0;
        lastLostTargetBurstTick = 0;
        lastLostTargetLogTick = 0;
        lastDeadTargetClearTick = 0;
        lastKillToLootHandoffTick = 0;
        killToLootHandoffActive = false;
        ResetTargetLossHandlingState();
    }

    public override void OnExit()
    {
        if (combatLog.DamageTakenCount() > 0 && !bits.Target())
        {
            stopMoving.Stop();
        }

        ResetTargetLossHandlingState();
    }

    public override void Update()
    {
        wait.Update();

        // Behavior Tree Combat System: if enabled, use behavior tree for decision making
        if (featureFlags.BehaviorTreeCombat?.Enabled == true && behaviorTreeEngine != null)
        {
            UpdateBehaviorTree();
            return;
        }

        if (MathF.Abs(lastDirection - playerReader.Direction) > MathF.PI / 2)
        {
            logger.LogInformation("Turning too fast!");
            stopMoving.Stop();
        }

        lastDirection = playerReader.Direction;
        lastMinDistance = playerReader.MinRange();
        lastMaxDistance = playerReader.MaxRange();

        if (bits.Drowning())
        {
            input.PressJump();
            return;
        }

        if (classConfig.AutoPetAttack &&
            bits.Pet() &&
            (!playerReader.PetTarget() || playerReader.PetTargetGuid != playerReader.TargetGuid) &&
            !input.PetAttack.OnCooldown())
        {
            input.PressPetAttack();
        }

        if (ShouldCancelMeleeAutoAttackForRangedCombat(
            prefersRangedCombat,
            bits.Auto_Attack(),
            bits.Shoot(),
            playerReader.IsInMeleeRange(),
            playerReader.WithInCombatRange()))
        {
            input.PressStopAttack();
            wait.Update();
        }

        // For melee-centric combat profiles, close distance aggressively when a hostile target
        // exists but melee range has not been reached yet. This prevents repeated out-of-range
        // cast failures that look like indecisive spinning/standing in place.
        if (ShouldApproachCurrentTarget())
        {
            input.PressApproachOnCooldown();
        }

        TryFaceTargetForRangedCombat();

        ReadOnlySpan<KeyAction> span = Keys;

        // Combat Rotation Optimizer: if enabled, score and sort abilities
        if (rotationOptimizer.IsEnabled && span.Length > 0 && span.Length <= 32)
        {
            GameStateSnapshot state = new(
                HealthPercent: playerReader.HealthPercent(),
                ResourcePercent: playerReader.PTPercentage(),
                ResourceCurrent: playerReader.PTCurrent(),
                ResourceMax: playerReader.PTMax(),
                ComboPoints: playerReader.ComboPoints(),
                TargetHealthPercent: playerReader.TargetHealthPercent(),
                GcdRemainingMs: playerReader.GCD.Value,
                NetworkLatencyMs: playerReader.NetworkLatency,
                SpellQueueMs: playerReader.SpellQueueTimeMs,
                MainHandSwingElapsedMs: playerReader.MainHandSwing.ElapsedMs(),
                MainHandSpeedMs: playerReader.MainHandSpeedMs(),
                MobCount: GetMobCount(),
                InCombat: bits.Combat(),
                TargetAlive: bits.Target_Alive(),
                IsTargetCasting: playerReader.IsTargetCasting(),
                TickTimestamp: Environment.TickCount64);

            Span<int> sortedIndices = stackalloc int[span.Length];
            Span<float> sortedScores = stackalloc float[span.Length];
            int count = rotationOptimizer.Optimize(span, in state, sortedIndices, sortedScores);

            for (int i = 0; bits.Target_Alive() && i < count; i++)
            {
                KeyAction keyAction = span[sortedIndices[i]];
                float score = sortedScores[i];

                if (castingHandler.SpellInQueue() && !keyAction.BaseAction)
                    continue;

                bool interrupt() => bits.Target_Alive() && keyAction.CanBeInterrupted();

                if (castingHandler.CastIfReady(keyAction, interrupt))
                {
                    rotationOptimizer.RecordCastResult(keyAction, score, true);
                    break;
                }
                else
                {
                    rotationOptimizer.RecordCastResult(keyAction, score, false);
                }
            }
        }
        else
        {
            // Original static priority path (zero overhead when optimizer disabled)
            for (int i = 0; bits.Target_Alive() && i < span.Length; i++)
            {
                KeyAction keyAction = span[i];

                if (castingHandler.SpellInQueue() && !keyAction.BaseAction)
                {
                    continue;
                }

                bool interrupt() => bits.Target_Alive() && keyAction.CanBeInterrupted();

                if (castingHandler.CastIfReady(keyAction, interrupt))
                {
                    break;
                }
            }
        }

        if (bits.SoftInteract_Enabled())
        {
            DealWithSoftInteract();
        }

        bool hasTarget = bits.Target();
        bool targetDead = hasTarget && bits.Target_Dead();
        bool targetMissing = !hasTarget;

        if (!bits.Combat())
        {
            ResetTargetLossHandlingState();
            return;
        }

        // Debounce target-loss: require 2 consecutive ticks to absorb single-frame
        // addon latency gaps that transiently report no target during active combat.
        if (targetMissing || targetDead)
        {
            targetLostConsecutiveTicks++;
            if (targetLostConsecutiveTicks < 2)
                return;
        }
        else
        {
            ResetTargetLossHandlingState();
        }

        if (targetDead)
        {
            HandleDeadTargetLoss(Environment.TickCount64);
            return;
        }

        if (targetMissing)
        {
            long nowTick = Environment.TickCount64;
            if (ShouldSuppressTargetLossForKillToLootHandoff(nowTick))
            {
                ActivateKillToLootHandoff(nowTick);
                RecordTargetlessCombatGraceUsed(nowTick);
                wait.Fixed(Math.Max(TargetlessCombatGraceHoldMs, playerReader.NetworkLatency));
                return;
            }

            bool burstActive = RecordLostTargetAndCheckBurst(nowTick);
            int recoveryCooldownMs = burstActive
                ? LostTargetBurstRecoveryCooldownMs
                : LostTargetRecoveryCooldownMs;

            if (lastLostTargetHandlingTick != 0 &&
                (nowTick - lastLostTargetHandlingTick) < recoveryCooldownMs)
            {
                return;
            }

            lastLostTargetHandlingTick = nowTick;
            if (!burstActive || (nowTick - lastLostTargetLogTick) >= LostTargetBurstLogCooldownMs)
            {
                logger.LogInformation("Lost target!");
                lastLostTargetLogTick = nowTick;
            }

            if (bits.Target() && bits.Target_Dead())
            {
                logger.LogInformation("Clear current dead target!");
                input.ForceAggressiveClearTarget(wait, bits);
            }

            if (TryRecoverTargetAfterLoss(nowTick, burstActive))
            {
                return;
            }

            if (HasRecentCombatProgress(nowTick))
            {
                RecordTargetlessCombatGraceUsed(nowTick);
                wait.Fixed(Math.Max(TargetlessCombatGraceHoldMs, playerReader.NetworkLatency));
                return;
            }

            input.ForceAggressiveClearTarget(wait, bits);
            ResetTargetLossHandlingState();
        }
    }

    private void HandleDeadTargetLoss(long nowTick)
    {
        lastDeadTargetClearTick = nowTick;
        logger.LogInformation("Clear current dead target!");
        input.ForceAggressiveClearTarget(wait, bits);

        if (ShouldAttemptPetTargetRecoveryAfterDeadTarget(bits.Pet(), playerReader.PetTarget(), bits.PetTarget_Alive()) &&
            TryAdoptPetTargetAsCombatThreat())
        {
            logger.LogInformation("Recovered combat target via pet handoff after dead target clear.");
            ResetTargetLossHandlingState();
            return;
        }

        if (HasRecentCombatProgress(nowTick))
        {
            ActivateKillToLootHandoff(nowTick);
            RecordTargetlessCombatGraceUsed(nowTick);
            wait.Fixed(Math.Max(TargetlessCombatGraceHoldMs, playerReader.NetworkLatency));
        }

        targetLostConsecutiveTicks = 0;
        lastLostTargetHandlingTick = 0;
        ResetLostTargetRecoveryState();
    }

    private bool HasValidCombatTarget()
    {
        return bits.Target() &&
            bits.Target_Alive() &&
            bits.Target_Hostile();
    }

    private bool TryRecoverTargetAfterLoss(long nowTick, bool burstActive)
    {
        if (lostTargetRecoveryStep == LostTargetRecoveryStep.None &&
            ShouldAttemptPetTargetRecoveryWhileTargetMissing(
                bits.Pet(),
                playerReader.PetTarget(),
                bits.PetTarget_Alive(),
                HasRecentCombatProgress(nowTick)))
        {
            RecordLostTargetReacquireAttempt(nowTick);
            RecordReacquireFallbackAttempt(nowTick);
            if (TryAdoptPetTargetAsCombatThreat())
            {
                RecordLostTargetReacquireSuccess(nowTick);
                RecordReacquireFallbackSuccess(nowTick);
                logger.LogInformation("Recovered combat target via immediate pet handoff.");
                ResetTargetLossHandlingState();
                return true;
            }
        }

        if (lostTargetRecoveryStep == LostTargetRecoveryStep.None)
        {
            RecordLostTargetReacquireAttempt(nowTick);
            bool reacquired = input.PressFastLastTargetAndWait(
                wait,
                HasValidCombatTarget,
                timeoutMs: LostTargetReacquireTimeoutMs);
            if (reacquired)
            {
                RecordLostTargetReacquireSuccess(nowTick);
                logger.LogInformation("Reacquired target with LastTarget fallback.");
                ResetTargetLossHandlingState();
                return true;
            }

            lostTargetRecoveryStep = LostTargetRecoveryStep.LastTargetAttempted;
        }

        if (burstActive)
        {
            wait.Fixed(Math.Max(playerReader.NetworkLatency, 50));
            return true;
        }

        if (lostTargetRecoveryStep == LostTargetRecoveryStep.LastTargetAttempted)
        {
            RecordReacquireFallbackAttempt(nowTick);
            bool nearestTargetFound = input.PressNearestTargetAndWait(
                wait,
                bits.Target,
                timeoutMs: LostTargetReacquireTimeoutMs);

            if (nearestTargetFound && TryAdoptCurrentTargetAsCombatThreat())
            {
                RecordLostTargetReacquireSuccess(nowTick);
                RecordReacquireFallbackSuccess(nowTick);
                logger.LogWarning("Recovered combat target via nearest-target fallback.");
                ResetTargetLossHandlingState();
                return true;
            }

            lostTargetRecoveryStep = LostTargetRecoveryStep.NearestTargetFallbackAttempted;
        }

        if (lostTargetRecoveryStep == LostTargetRecoveryStep.NearestTargetFallbackAttempted)
        {
            RecordReacquireFallbackAttempt(nowTick);
            if (TryAdoptPetTargetAsCombatThreat())
            {
                RecordLostTargetReacquireSuccess(nowTick);
                RecordReacquireFallbackSuccess(nowTick);
                logger.LogWarning("Recovered combat target via pet fallback.");
                ResetTargetLossHandlingState();
                return true;
            }

            lostTargetRecoveryStep = LostTargetRecoveryStep.PetFallbackAttempted;
        }

        return false;
    }

    private bool TryAdoptCurrentTargetAsCombatThreat()
    {
        if (!HasValidCombatTarget())
        {
            return false;
        }

        if (bits.Target_Combat() && bits.TargetTarget_PlayerOrPet())
        {
            ResetCooldowns();
            wait.Update();
            return true;
        }

        return false;
    }

    private bool TryAdoptPetTargetAsCombatThreat()
    {
        if (!bits.Pet() || !playerReader.PetTarget() || !bits.PetTarget_Alive())
        {
            return false;
        }

        stopMoving.Stop();
        input.PressTargetPet();
        input.PressTargetOfTarget();
        wait.Update();
        return TryAdoptCurrentTargetAsCombatThreat();
    }

    private bool HasRecentCombatProgress(long nowTick)
    {
        return HasRecentCombatProgressSignal(
            damageDoneElapsedMs: combatLog.DamageDoneGuid.ElapsedMs(),
            damageTakenElapsedMs: combatLog.DamageTakenGuid.ElapsedMs(),
            nowTick: nowTick,
            lastKillCreditTick: lastKillCreditTick,
            progressWindowMs: RecentCombatProgressWindowMs);
    }

    private bool ShouldSuppressTargetLossForKillToLootHandoff(long nowTick)
    {
        if (!bits.Combat())
        {
            ResetKillToLootHandoffState();
            return false;
        }

        bool hasRecentKillCredit = HasRecentKillCreditSignal(nowTick, lastKillCreditTick, RecentCombatProgressWindowMs);
        bool hasRecentCombatProgress = HasRecentCombatProgress(nowTick);
        bool deadTargetJustCleared = HasRecentDeadTargetClearSignal(nowTick, lastDeadTargetClearTick, DeadTargetClearRecentWindowMs);
        bool recentKillToLootHandoff = killToLootHandoffActive && HasRecentTimestampSignal(nowTick, lastKillToLootHandoffTick, RecentCombatProgressWindowMs);
        bool hasPendingCorpseOrLootState = HasPendingCorpseOrLootStateSignal(
            state.ShouldConsumeCorpse,
            state.LootableCorpseCount,
            state.ConsumableCorpseCount,
            state.LastCombatKillCount);
        bool hasImmediateAlternateThreat = HasImmediateAlternateThreatSignal(
            hasLivePetTarget: ShouldAttemptPetTargetRecoveryAfterDeadTarget(bits.Pet(), playerReader.PetTarget(), bits.PetTarget_Alive()),
            damageTakenCount: combatLog.DamageTakenCount(),
            toPullCount: combatLog.ToPullCount());

        bool suppress = ShouldTreatTargetLossAsKillToLootHandoff(
            hasRecentKillCredit,
            hasRecentCombatProgress || recentKillToLootHandoff,
            deadTargetJustCleared,
            hasPendingCorpseOrLootState,
            HasValidCombatTarget(),
            hasImmediateAlternateThreat);

        if (!suppress &&
            killToLootHandoffActive &&
            (!hasPendingCorpseOrLootState || (!hasRecentKillCredit && !hasRecentCombatProgress && !deadTargetJustCleared)))
        {
            ResetKillToLootHandoffState();
        }

        return suppress;
    }

    internal static bool HasRecentCombatProgressSignal(
        int damageDoneElapsedMs,
        int damageTakenElapsedMs,
        long nowTick,
        long lastKillCreditTick,
        int progressWindowMs)
    {
        bool damageDoneRecent = damageDoneElapsedMs >= 0 && damageDoneElapsedMs <= progressWindowMs;
        bool damageTakenRecent = damageTakenElapsedMs >= 0 && damageTakenElapsedMs <= progressWindowMs;
        bool killCreditRecent = lastKillCreditTick != 0 && (nowTick - lastKillCreditTick) <= progressWindowMs;
        return damageDoneRecent || damageTakenRecent || killCreditRecent;
    }

    internal static bool HasRecentKillCreditSignal(long nowTick, long lastKillCreditTick, int windowMs)
    {
        return lastKillCreditTick != 0 && (nowTick - lastKillCreditTick) <= windowMs;
    }

    internal static bool HasRecentDeadTargetClearSignal(long nowTick, long lastDeadTargetClearTick, int windowMs)
    {
        return lastDeadTargetClearTick != 0 && (nowTick - lastDeadTargetClearTick) <= windowMs;
    }

    internal static bool HasRecentTimestampSignal(long nowTick, long lastTick, int windowMs)
    {
        return lastTick != 0 && (nowTick - lastTick) <= windowMs;
    }

    internal static bool HasImmediateAlternateThreatSignal(
        bool hasLivePetTarget,
        int damageTakenCount,
        int toPullCount)
    {
        return hasLivePetTarget || damageTakenCount > 0 || toPullCount > 0;
    }

    internal static bool ShouldTreatTargetLossAsKillToLootHandoff(
        bool hasRecentKillCredit,
        bool hasRecentCombatProgress,
        bool deadTargetJustCleared,
        bool hasPendingCorpseOrLootState,
        bool hasValidCombatTarget,
        bool hasImmediateAlternateThreat)
    {
        if (hasValidCombatTarget || hasImmediateAlternateThreat || !hasPendingCorpseOrLootState)
        {
            return false;
        }

        return hasRecentKillCredit || hasRecentCombatProgress || deadTargetJustCleared;
    }

    internal static bool HasPendingCorpseOrLootStateSignal(
        bool shouldConsumeCorpse,
        int lootableCorpseCount,
        int consumableCorpseCount,
        int lastCombatKillCount)
    {
        return shouldConsumeCorpse ||
            lootableCorpseCount > 0 ||
            consumableCorpseCount > 0 ||
            lastCombatKillCount > 0;
    }

    private void ResetLostTargetRecoveryState()
    {
        lostTargetRecoveryStep = LostTargetRecoveryStep.None;
    }

    private void ResetKillToLootHandoffState()
    {
        killToLootHandoffActive = false;
        lastKillToLootHandoffTick = 0;
        lastDeadTargetClearTick = 0;
    }

    private void ResetTargetLossHandlingState()
    {
        targetLostConsecutiveTicks = 0;
        lastLostTargetHandlingTick = 0;
        ResetLostTargetRecoveryState();
        ResetKillToLootHandoffState();
    }

    private void HandleKillCredit()
    {
        lastKillCreditTick = Environment.TickCount64;
    }

    private void ActivateKillToLootHandoff(long nowTick)
    {
        killToLootHandoffActive = true;
        lastKillToLootHandoffTick = nowTick;
    }

    private bool RecordLostTargetAndCheckBurst(long nowTick)
    {
        lock (runtimeMetricsLock)
        {
            lostTargetTicks.Enqueue(nowTick);
            PruneRuntimeSamples(lostTargetTicks, nowTick, RuntimeMetricsWindowMs);

            int lossesWithinBurstWindow = CountSamplesWithinWindow(lostTargetTicks, nowTick, LostTargetBurstWindowMs);
            if (!ShouldRegisterLostTargetBurst(
                nowTick,
                lastLostTargetBurstTick,
                lossesWithinBurstWindow,
                LostTargetBurstThreshold,
                LostTargetBurstCooldownMs))
            {
                return false;
            }

            lastLostTargetBurstTick = nowTick;
            lostTargetBurstTicks.Enqueue(nowTick);
            PruneRuntimeSamples(lostTargetBurstTicks, nowTick, RuntimeMetricsWindowMs);
            return true;
        }
    }

    internal static bool ShouldRegisterLostTargetBurst(
        long nowTick,
        long lastBurstTick,
        int lossesWithinBurstWindow,
        int burstThreshold,
        int burstCooldownMs)
    {
        return lossesWithinBurstWindow >= burstThreshold &&
            (lastBurstTick == 0 || (nowTick - lastBurstTick) >= burstCooldownMs);
    }

    internal static bool ShouldCancelMeleeAutoAttackForRangedCombat(
        bool prefersRangedCombat,
        bool meleeAutoAttacking,
        bool shooting,
        bool inMeleeRange,
        bool withinCombatRange)
    {
        return prefersRangedCombat &&
            meleeAutoAttacking &&
            !shooting &&
            !inMeleeRange &&
            withinCombatRange;
    }

    internal static bool ShouldAttemptPetTargetRecoveryAfterDeadTarget(
        bool hasPet,
        bool petHasTarget,
        bool petTargetAlive)
    {
        return hasPet && petHasTarget && petTargetAlive;
    }

    internal static bool ShouldAttemptPetTargetRecoveryWhileTargetMissing(
        bool hasPet,
        bool petHasTarget,
        bool petTargetAlive,
        bool hasRecentCombatProgress)
    {
        return hasRecentCombatProgress &&
            ShouldAttemptPetTargetRecoveryAfterDeadTarget(hasPet, petHasTarget, petTargetAlive);
    }

    internal static bool HasRangedCombatPreference(ClassConfiguration classConfiguration)
    {
        ReadOnlySpan<KeyAction> span = classConfiguration.Combat.Sequence;
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i].Name.Equals("Shoot", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void RecordLostTargetReacquireAttempt(long nowTick)
    {
        lock (runtimeMetricsLock)
        {
            lostTargetReacquireAttemptTicks.Enqueue(nowTick);
            PruneRuntimeSamples(lostTargetReacquireAttemptTicks, nowTick, RuntimeMetricsWindowMs);
        }
    }

    private void RecordLostTargetReacquireSuccess(long nowTick)
    {
        lock (runtimeMetricsLock)
        {
            lostTargetReacquireSuccessTicks.Enqueue(nowTick);
            PruneRuntimeSamples(lostTargetReacquireSuccessTicks, nowTick, RuntimeMetricsWindowMs);
        }
    }

    private void RecordTargetlessCombatGraceUsed(long nowTick)
    {
        lock (runtimeMetricsLock)
        {
            targetlessCombatGraceUsedTicks.Enqueue(nowTick);
            PruneRuntimeSamples(targetlessCombatGraceUsedTicks, nowTick, RuntimeMetricsWindowMs);
        }
    }

    private void RecordReacquireFallbackAttempt(long nowTick)
    {
        lock (runtimeMetricsLock)
        {
            reacquireFallbackAttemptTicks.Enqueue(nowTick);
            PruneRuntimeSamples(reacquireFallbackAttemptTicks, nowTick, RuntimeMetricsWindowMs);
        }
    }

    private void RecordReacquireFallbackSuccess(long nowTick)
    {
        lock (runtimeMetricsLock)
        {
            reacquireFallbackSuccessTicks.Enqueue(nowTick);
            PruneRuntimeSamples(reacquireFallbackSuccessTicks, nowTick, RuntimeMetricsWindowMs);
        }
    }

    private static void PruneRuntimeSamples(Queue<long> queue, long nowTick, int windowMs)
    {
        while (queue.Count > 0 && (nowTick - queue.Peek()) > windowMs)
        {
            queue.Dequeue();
        }
    }

    private static int CountSamplesWithinWindow(Queue<long> queue, long nowTick, int windowMs)
    {
        int count = 0;
        foreach (long tick in queue)
        {
            if ((nowTick - tick) <= windowMs)
            {
                count++;
            }
        }

        return count;
    }

    public CombatRuntimeSnapshot GetRuntimeSnapshot()
    {
        lock (runtimeMetricsLock)
        {
            long nowTick = Environment.TickCount64;
            PruneRuntimeSamples(lostTargetTicks, nowTick, RuntimeMetricsWindowMs);
            PruneRuntimeSamples(lostTargetBurstTicks, nowTick, RuntimeMetricsWindowMs);
            PruneRuntimeSamples(lostTargetReacquireAttemptTicks, nowTick, RuntimeMetricsWindowMs);
            PruneRuntimeSamples(lostTargetReacquireSuccessTicks, nowTick, RuntimeMetricsWindowMs);
            PruneRuntimeSamples(targetlessCombatGraceUsedTicks, nowTick, RuntimeMetricsWindowMs);
            PruneRuntimeSamples(reacquireFallbackAttemptTicks, nowTick, RuntimeMetricsWindowMs);
            PruneRuntimeSamples(reacquireFallbackSuccessTicks, nowTick, RuntimeMetricsWindowMs);

            return new CombatRuntimeSnapshot(
                LostTargetCountWindow: lostTargetTicks.Count,
                LostTargetBurstCountWindow: lostTargetBurstTicks.Count,
                LostTargetReacquireAttemptCountWindow: lostTargetReacquireAttemptTicks.Count,
                LostTargetReacquireSuccessCountWindow: lostTargetReacquireSuccessTicks.Count,
                TargetlessCombatGraceUsedCountWindow: targetlessCombatGraceUsedTicks.Count,
                ReacquireFallbackAttemptCountWindow: reacquireFallbackAttemptTicks.Count,
                ReacquireFallbackSuccessCountWindow: reacquireFallbackSuccessTicks.Count,
                WindowSeconds: RuntimeMetricsWindowMs / 1000);
        }
    }

    public CastingRuntimeSnapshot GetCastingRuntimeSnapshot()
    {
        return castingHandler.GetRuntimeSnapshot();
    }

    /// <summary>
    /// Gets the current number of mobs in combat with the player.
    /// Uses CombatLog.DamageTaken as a proxy for engaged enemies.
    /// </summary>
    private int GetMobCount()
    {
        // Count unique mobs that have damaged us (best available proxy)
        int mobCount = combatLog.DamageTakenCount();

        // If we have a target but no damage taken yet, count it
        if (mobCount == 0 && bits.Target_Alive())
        {
            mobCount = 1;
        }

        return mobCount;
    }

    private bool ShouldApproachCurrentTarget()
    {
        return ShouldApproachCurrentTarget(
            bits.Target(),
            bits.Target_Alive(),
            bits.Target_Hostile(),
            prefersRangedCombat,
            holdRangedStandoff,
            playerReader.WithInCombatRange(),
            playerReader.WithInPullRange(),
            playerReader.IsCasting(),
            castingHandler.SpellInQueue());
    }

    internal static bool ShouldApproachCurrentTarget(
        bool hasTarget,
        bool targetAlive,
        bool targetHostile,
        bool prefersRangedCombat,
        bool holdRangedStandoff,
        bool withinCombatRange,
        bool withinPullRange,
        bool isCasting,
        bool spellInQueue)
    {
        if (!hasTarget || !targetAlive || !targetHostile || isCasting || spellInQueue)
        {
            return false;
        }

        if (prefersRangedCombat && holdRangedStandoff && withinPullRange)
        {
            return false;
        }

        return !withinCombatRange;
    }

    private void TryFaceTargetForRangedCombat()
    {
        if (!bits.Target() ||
            !bits.Target_Alive() ||
            !bits.Target_Hostile() ||
            playerReader.IsCasting() ||
            castingHandler.SpellInQueue())
        {
            return;
        }

        long nowTick = Environment.TickCount64;
        if (lastFaceTargetAssistTick != 0 &&
            (nowTick - lastFaceTargetAssistTick) < FaceTargetAssistCooldownMs)
        {
            return;
        }

        lastFaceTargetAssistTick = nowTick;

        float directionBeforeAssist = playerReader.Direction;
        input.PressFastInteract();
        stopMoving.StopForward();
        wait.Update();

        if (DidDirectionChangeEnough(directionBeforeAssist, playerReader.Direction))
        {
            return;
        }

        // Retry once before moving to avoid unnecessary approach taps near obstacles.
        wait.Fixed(Math.Max(playerReader.HalfNetworkLatency, 25));
        input.PressFastInteract();
        stopMoving.StopForward();
        wait.Update();

        if (DidDirectionChangeEnough(directionBeforeAssist, playerReader.Direction))
        {
            return;
        }

        // Only use approach assist when truly out of combat range.
        if (!playerReader.WithInCombatRange())
        {
            input.PressApproachOnCooldown();
            wait.Update();
            stopMoving.StopForward();
        }
    }

    private static bool DidDirectionChangeEnough(float before, float after)
    {
        float diff = MathF.Abs(after - before);
        if (diff > MathF.PI)
        {
            diff = (MathF.PI * 2f) - diff;
        }

        return diff >= FaceAssistDirectionDeltaRadians;
    }

    private void FindPossibleThreats()
    {
        if (bits.Pet_Defensive())
        {
            float elapsedPetFoundTarget = wait.Until(CastingHandler.GCD,
                () => playerReader.PetTarget() && bits.PetTarget_Alive());

            if (elapsedPetFoundTarget < 0)
            {
                logger.LogWarning("Pet not found target!");
                input.ForceAggressiveClearTarget(wait, bits);
                return;
            }

            ResetCooldowns();

            input.PressTargetPet();
            input.PressTargetOfTarget();
            wait.Update();

            logger.LogWarning($"Found new target by pet. {elapsedPetFoundTarget}ms");

            return;
        }

        logger.LogInformation("Checking target in front...");
        input.PressNearestTarget();
        wait.Update();

        if (bits.Target() && !bits.Target_Dead() && bits.Target_Hostile())
        {
            if (bits.Target_Combat() && bits.TargetTarget_PlayerOrPet())
            {
                ResetCooldowns();

                logger.LogWarning("Found new target!");
                wait.Update();
                return;
            }

            logger.LogWarning("Dont pull non-hostile target!");
            input.ForceAggressiveClearTarget(wait, bits);
        }

        logger.LogWarning($"Waiting for target to exists or lose combat. Possible threats {combatLog.DamageTakenCount()}!");
        wait.Till(CastingHandler.GCD * 2,
            () => bits.Target_Alive() || !bits.Combat());
    }

    private Vector3 GetCorpseLocation(float distance)
    {
        return PointEstimator.GetMapPos(playerReader.WorldMapArea, playerReader.WorldPos, playerReader.Direction, distance);
    }

    private void DealWithSoftInteract()
    {
        if (!playerReader.IsInMeleeRange() ||
            playerReader.IsCasting() ||
            !InvalidSoftInteractExists() ||
            playerReader.TargetGuid == playerReader.SoftInteract_Guid)
        {
            return;
        }

        // Feature disabled: Soft-interact targeting logic was intentionally removed
        // pending redesign. Reserved for future soft-target implementation.
    }

    private bool InvalidSoftInteractExists()
    {
        return
        bits.SoftInteract() &&
        (
            playerReader.SoftInteract_Type != GuidType.Creature ||
            bits.SoftInteract_Dead() ||
            bits.SoftInteract_Tagged()
        );
    }

    /// <summary>
    /// Updates using behavior tree combat system.
    /// </summary>
    private void UpdateBehaviorTree()
    {
        // Initialize behavior context if needed
        if (behaviorContext == null)
        {
            behaviorContext = new BehaviorContext
            {
                Player = playerReader,
                Casting = castingHandler,
                StopMoving = stopMoving,
                Input = input,
                Logger = logger,
                CombatSequence = classConfig.Combat.Sequence ?? Array.Empty<KeyAction>(),
                ElapsedMs = 0
            };
        }

        // Execute behavior tree tick
        NodeStatus status = behaviorTreeEngine!.Tick(behaviorContext);

        if (status == NodeStatus.Failure)
        {
            logger.LogWarning("[CombatGoal] Behavior tree returned Failure, falling back to GOAP");
            // Could trigger fallback to GOAP here
        }
    }
}
