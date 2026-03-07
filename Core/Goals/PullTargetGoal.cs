using Core.GOAP;

using Microsoft.Extensions.Logging;

using SharedLib.NpcFinder;

using System;
using System.Collections.Generic;
using System.Threading;

using static System.Diagnostics.Stopwatch;

namespace Core.Goals;

internal enum PullFailureAction
{
    WaitForCastState,
    WaitForTargetState,
    SoftRetryApproach,
    HardClearTarget
}

public sealed class PullTargetGoal : GoapGoal, IGoapEventListener
{
    public override float Cost => 7f;

    private const int AcquireTargetTimeMs = 5000;
    private const int MAX_PULL_DURATION = 15_000;
    private const int RangedPullFailureAbortCount = 4;
    private const int RangedPullFailureSoftAbortWindowMs = 6000;
    private const int PullRetryApproachDelayMs = 120;
    private const int FaceTargetAssistCooldownMs = 350;
    private const float FaceAssistDirectionDeltaRadians = MathF.PI / 36f; // ~5 deg
    private const int RuntimeMetricsWindowMs = 10 * 60 * 1000;

    private readonly ILogger<PullTargetGoal> logger;
    private readonly ConfigurableInput input;
    private readonly ClassConfiguration classConfig;
    private readonly Wait wait;
    private readonly CombatLog combatLog;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly StopMoving stopMoving;
    private readonly StuckDetector stuckDetector;
    private readonly NpcNameTargeting npcNameTargeting;
    private readonly CastingHandler castingHandler;
    private readonly IMountHandler mountHandler;
    private readonly CombatTracker combatTracker;
    private readonly IBlacklist targetBlacklist;
    private readonly ExecGameCommand execGameCommand;
    private DateTime lastBrokenGearWarning = DateTime.MinValue;

    private readonly KeyAction? approachKey;
    private readonly Action approachAction;

    private readonly bool requiresNpcNameFinder;

    private long pullStart;
    private long lastFaceTargetAssistTick;
    private int consecutiveRangedPullFailures;
    private int lastFailureTargetGuid;
    private readonly object runtimeMetricsLock = new();
    private readonly Queue<long> softRetryTicks = [];

    private double PullDurationMs => GetElapsedTime(pullStart).TotalMilliseconds;

    public PullTargetGoal(ILogger<PullTargetGoal> logger, ConfigurableInput input,
        Wait wait, CombatLog combatlog, PlayerReader playerReader,
        AddonBits bits,
        IBlacklist targetBlacklist,
        StopMoving stopMoving, CastingHandler castingHandler,
        IMountHandler mountHandler, NpcNameTargeting npcNameTargeting,
        StuckDetector stuckDetector, CombatTracker combatTracker,
        ClassConfiguration classConfig,
        ExecGameCommand execGameCommand)
        : base(nameof(PullTargetGoal))
    {
        this.logger = logger;
        this.input = input;
        this.wait = wait;
        this.combatLog = combatlog;
        this.playerReader = playerReader;
        this.bits = bits;
        this.stopMoving = stopMoving;
        this.castingHandler = castingHandler;
        this.mountHandler = mountHandler;
        this.npcNameTargeting = npcNameTargeting;
        this.stuckDetector = stuckDetector;
        this.combatTracker = combatTracker;
        this.targetBlacklist = targetBlacklist;
        this.classConfig = classConfig;
        this.execGameCommand = execGameCommand;

        Keys = classConfig.Pull.Sequence;

        approachAction = DefaultApproach;

        for (int i = 0; i < Keys.Length; i++)
        {
            KeyAction keyAction = Keys[i];

            if (keyAction.Name.Equals(input.Approach.Name, StringComparison.OrdinalIgnoreCase))
            {
                approachAction = ConditionalApproach;
                approachKey = keyAction;
            }

            if (keyAction.Requirements.Contains(RequirementFactory.AddVisible))
            {
                requiresNpcNameFinder = true;
            }
        }

        AddPrecondition(GoapKey.hastarget, true);
        AddPrecondition(GoapKey.targetisalive, true);
        if (classConfig.Mode != Mode.AssistFocus)
        {
            AddPrecondition(GoapKey.targettargetsus, false);
        }
        AddPrecondition(GoapKey.targethostile, true);
        AddPrecondition(GoapKey.withinpullrange, true);
        AddPrecondition(GoapKey.itemsbroken, false);

        AddEffect(GoapKey.pulled, true);
    }

    public override void OnEnter()
    {
        wait.Update();
        stuckDetector.Reset();
        lastFaceTargetAssistTick = 0;

        int currentTargetGuid = bits.Target() ? playerReader.TargetGuid : 0;
        if (currentTargetGuid != lastFailureTargetGuid)
        {
            consecutiveRangedPullFailures = 0;
            lastFailureTargetGuid = currentTargetGuid;
        }

        if (mountHandler.IsMounted())
        {
            mountHandler.Dismount();
        }

        if (Keys.Length != 0 && !input.StopAttack.OnCooldown())
        {
            Log("Stop auto interact!");
            input.PressStopAttack();
            wait.Update();
            stopMoving.Stop();
            wait.Update(playerReader.DoubleNetworkLatency);
            wait.Update();

            // Verify the character actually stopped — click-to-move
            // momentum can persist after a single StopForward() call.
            if (bits.Moving())
            {
                stopMoving.StopForward();
                wait.Until(500, () => !bits.Moving());
            }
        }

        if (requiresNpcNameFinder)
        {
            npcNameTargeting.ChangeNpcType(NpcNames.Enemy);
        }

        // Wait for GCD from any pre-pull ability (e.g., Adhoc spells like Life Tap) to
        // clear before attempting pull spells. Without this, a GCD-active state exhausts
        // the 4-attempt abort budget before the GCD expires, causing the bot to refuse
        // body-pull on a healthy target. Uses CancellationToken.None so it always waits
        // the full remaining GCD rather than being cut short by the interrupt watchdog.
        if (Keys.Length > 0 && playerReader.GCD.Value > 0)
        {
            castingHandler.WaitForGCD(Keys[0], false, false, CancellationToken.None);
        }

        pullStart = GetTimestamp();
    }

    public override void OnExit()
    {
        if (requiresNpcNameFinder)
        {
            npcNameTargeting.ChangeNpcType(NpcNames.None);
        }
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e.GetType() == typeof(ResumeEvent))
        {
            pullStart = GetTimestamp();
        }
    }

    public override void Update()
    {
        wait.Update();

        int currentTargetGuid = bits.Target() ? playerReader.TargetGuid : 0;
        if (currentTargetGuid != lastFailureTargetGuid)
        {
            consecutiveRangedPullFailures = 0;
            lastFailureTargetGuid = currentTargetGuid;
        }

        if (!bits.Target() || bits.Combat())
        {
            consecutiveRangedPullFailures = 0;
            if (!bits.Target())
            {
                lastFailureTargetGuid = 0;
            }
        }

        if (IsGearTooBrokenToFight())
        {
            if ((DateTime.UtcNow - lastBrokenGearWarning).TotalSeconds >= 3)
            {
                Log("Gear durability critically low/broken; blocking combat pull and clearing target.");
                lastBrokenGearWarning = DateTime.UtcNow;
            }

            input.PressStopAttack();
            if (bits.Target())
            {
                input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
            }
            return;
        }

        if (bits.Target() && targetBlacklist.Is())
        {
            Log("Blacklisted target detected during pull, clearing target.");
            input.PressStopAttack();
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
            return;
        }

        if (PullDurationMs > MAX_PULL_DURATION)
        {
            input.PressStopAttack();
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
            Log("Pull taking too long. Clear target and face away!");
            input.TurnRandomDir(1000);
            return;
        }

        if (classConfig.AutoPetAttack &&
            bits.Pet() &&
            (!playerReader.PetTarget() ||
            playerReader.TargetGuid != playerReader.PetTargetGuid) &&
            !input.PetAttack.OnCooldown())
        {
            input.PressStopAttack();
            input.PressPetAttack();
        }

        TryFaceTargetForRangedPull();

        bool castAny = false;
        bool spellInQueue = false;

        ReadOnlySpan<KeyAction> keys = Keys;
        for (int i = 0; i < keys.Length; i++)
        {
            KeyAction keyAction = keys[i];

            if (keyAction.Name.Equals(input.Approach.Name,
                StringComparison.OrdinalIgnoreCase))
                continue;

            if (!keyAction.CanRun())
                continue;

            spellInQueue = castingHandler.SpellInQueue();
            if (spellInQueue)
            {
                break;
            }

            bool isRangedPullAction = IsRangedPullAction(keyAction);
            bool interrupt() => keyAction.CanBeInterrupted() || PullPrevention();
            bool beforeCombat = bits.Combat();
            int beforeTargetHealth = playerReader.TargetHealth();
            int beforeCastCount = playerReader.CastCount;
            int beforeDamageDoneCount = combatLog.DamageDoneCount();
            int beforeDamageTakenCount = combatLog.DamageTakenCount();

            bool castResult = castingHandler.Cast(keyAction, interrupt);
            if (castResult)
            {
                // Accumulate: a later cast failure must not erase an earlier success
                if (!keyAction.BaseAction)
                    castAny = true;
                if (isRangedPullAction)
                {
                    consecutiveRangedPullFailures = 0;
                }
            }
            else if (isRangedPullAction &&
                bits.Target() &&
                !bits.Combat() &&
                !playerReader.IsInMeleeRange())
            {
                bool likelyPullSuccess = TryConfirmLikelyPullSuccess(
                    beforeCombat,
                    beforeTargetHealth,
                    beforeCastCount,
                    beforeDamageDoneCount,
                    beforeDamageTakenCount);
                if (likelyPullSuccess)
                {
                    logger.LogDebug(
                        "Ranged pull '{PullAbility}' unconfirmed by cast signal, but success evidence detected; suppressing retry churn",
                        keyAction.Name);
                    castAny = true;
                    consecutiveRangedPullFailures = 0;
                    return;
                }

                consecutiveRangedPullFailures++;

                if (consecutiveRangedPullFailures >= RangedPullFailureAbortCount)
                {
                    PullFailureAction failureAction = DecideRangedFailureAction(
                        isCasting: playerReader.IsCasting(),
                        spellInQueue: castingHandler.SpellInQueue(),
                        hasTarget: bits.Target(),
                        pullDurationMs: PullDurationMs,
                        softAbortWindowMs: RangedPullFailureSoftAbortWindowMs);

                    if (failureAction == PullFailureAction.WaitForCastState)
                    {
                        wait.Fixed(Math.Max(playerReader.NetworkLatency, 50));
                        return;
                    }

                    if (failureAction == PullFailureAction.WaitForTargetState)
                    {
                        wait.Fixed(Math.Max(playerReader.NetworkLatency, 50));
                        return;
                    }

                    if (failureAction == PullFailureAction.SoftRetryApproach)
                    {
                        RecordSoftRetry();
                        logger.LogWarning(
                            "Ranged pull '{PullAbility}' failed {FailureCount}x; forcing short approach retry before clear",
                            keyAction.Name,
                            consecutiveRangedPullFailures);

                        input.PressApproachOnCooldown();
                        wait.Fixed(PullRetryApproachDelayMs);
                        consecutiveRangedPullFailures = RangedPullFailureAbortCount - 1;
                        return;
                    }

                    Log($"Ranged pull '{keyAction.Name}' failed {consecutiveRangedPullFailures}x; refusing body-pull and clearing target.");
                    input.PressStopAttack();
                    input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
                    consecutiveRangedPullFailures = 0;
                    lastFailureTargetGuid = 0;
                    return;
                }

                logger.LogDebug(
                    "Ranged pull '{PullAbility}' failed to confirm ({FailureCount}/{MaxFailures}); retrying without approach",
                    keyAction.Name,
                    consecutiveRangedPullFailures,
                    RangedPullFailureAbortCount);
                wait.Fixed(Math.Max(playerReader.NetworkLatency, 50));
                return;
            }
            else if (PullPrevention() &&
                !bits.Combat() &&
                (playerReader.IsCasting() || bits.Any_AutoAttack()))
            {
                Log("Preventing pulling possible tagged target!");
                input.PressStopAttack();
                input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
                return;
            }
        }

        if (bits.Target() && combatLog.EvadeMobs.Contains(playerReader.TargetGuid))
        {
            Log("Evading mob");

            input.PressStopAttack();
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
            return;
        }
        else if (bits.Target())
        {
            combatLog.ToPull.Add(playerReader.TargetGuid);
        }

        // Also skip approach if we entered combat (pull spell aggroed the mob)
        if (castAny || spellInQueue || playerReader.IsCasting() || bits.Combat() || (bits.AutoShot() && !playerReader.IsInMeleeRange()))
            return;

        approachAction();
    }

    private void DefaultApproach()
    {
        if (input.Approach.OnCooldown())
            return;

        if (!bits.SoftInteract() || EligibleEnemySoftTargetExists())
        {
            input.PressApproach();
            wait.Update();
        }

        if (!stuckDetector.IsMoving())
            stuckDetector.Update();
    }

    private void ConditionalApproach()
    {
        if (approachKey == null ||
            (!approachKey.CanRun() && !approachKey.OnCooldown()))
        {
            stopMoving.Stop();
            return;
        }

        DefaultApproach();
    }

    private bool PullPrevention()
    {
        return targetBlacklist.Is() ||
            playerReader.TargetTarget is not
            (UnitsTarget.None or
            UnitsTarget.Me or
            UnitsTarget.Pet or
            UnitsTarget.PartyOrPet);
    }

    private bool EligibleEnemySoftTargetExists() =>
        bits.SoftInteract() &&
        bits.SoftInteract_Hostile() &&
        !bits.SoftInteract_Dead() &&
        !bits.SoftInteract_Tagged() &&
        playerReader.SoftInteract_Type == GuidType.Creature;

    private void TryFaceTargetForRangedPull()
    {
        if (!bits.Target() ||
            !bits.Target_Alive() ||
            !bits.Target_Hostile() ||
            playerReader.IsCasting() ||
            castingHandler.SpellInQueue())
        {
            return;
        }

        long nowTick = GetTimestamp();
        if (lastFaceTargetAssistTick != 0 &&
            GetElapsedTime(lastFaceTargetAssistTick).TotalMilliseconds < FaceTargetAssistCooldownMs)
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

        // Retry fast interact once before any movement adjustment.
        wait.Fixed(Math.Max(playerReader.HalfNetworkLatency, 25));
        input.PressFastInteract();
        stopMoving.StopForward();
        wait.Update();

        if (DidDirectionChangeEnough(directionBeforeAssist, playerReader.Direction))
        {
            return;
        }

        // Only tap approach when truly outside pull range.
        if (!playerReader.WithInPullRange())
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

    internal static PullFailureAction DecideRangedFailureAction(
        bool isCasting,
        bool spellInQueue,
        bool hasTarget,
        double pullDurationMs,
        int softAbortWindowMs)
    {
        if (isCasting || spellInQueue)
        {
            return PullFailureAction.WaitForCastState;
        }

        if (!hasTarget)
        {
            return PullFailureAction.WaitForTargetState;
        }

        if (pullDurationMs < softAbortWindowMs)
        {
            return PullFailureAction.SoftRetryApproach;
        }

        return PullFailureAction.HardClearTarget;
    }

    private bool TryConfirmLikelyPullSuccess(
        bool beforeCombat,
        int beforeTargetHealth,
        int beforeCastCount,
        int beforeDamageDoneCount,
        int beforeDamageTakenCount)
    {
        bool likelySuccess = IsLikelyPullSuccess(
            enteredCombat: !beforeCombat && bits.Combat(),
            targetHealthDropped: DidTargetHealthDrop(beforeTargetHealth, playerReader.TargetHealth()),
            castStateProgressed: playerReader.CastCount > beforeCastCount,
            combatLogProgressed: DidCombatLogProgress(beforeDamageDoneCount, beforeDamageTakenCount, combatLog.DamageDoneCount(), combatLog.DamageTakenCount()));
        if (likelySuccess)
        {
            return true;
        }

        wait.Fixed(Math.Max(playerReader.HalfNetworkLatency, 50));
        return IsLikelyPullSuccess(
            enteredCombat: !beforeCombat && bits.Combat(),
            targetHealthDropped: DidTargetHealthDrop(beforeTargetHealth, playerReader.TargetHealth()),
            castStateProgressed: playerReader.CastCount > beforeCastCount,
            combatLogProgressed: DidCombatLogProgress(beforeDamageDoneCount, beforeDamageTakenCount, combatLog.DamageDoneCount(), combatLog.DamageTakenCount()));
    }

    internal static bool IsLikelyPullSuccess(
        bool enteredCombat,
        bool targetHealthDropped,
        bool castStateProgressed,
        bool combatLogProgressed)
    {
        return enteredCombat ||
            targetHealthDropped ||
            castStateProgressed ||
            combatLogProgressed;
    }

    internal static bool DidTargetHealthDrop(int beforeTargetHealth, int afterTargetHealth)
    {
        return beforeTargetHealth > 0 &&
            afterTargetHealth > 0 &&
            afterTargetHealth < beforeTargetHealth;
    }

    internal static bool DidCombatLogProgress(
        int beforeDamageDoneCount,
        int beforeDamageTakenCount,
        int afterDamageDoneCount,
        int afterDamageTakenCount)
    {
        return afterDamageDoneCount > beforeDamageDoneCount ||
            afterDamageTakenCount > beforeDamageTakenCount;
    }

    private void RecordSoftRetry()
    {
        lock (runtimeMetricsLock)
        {
            long nowTick = Environment.TickCount64;
            softRetryTicks.Enqueue(nowTick);
            PruneRuntimeSamples(softRetryTicks, nowTick, RuntimeMetricsWindowMs);
        }
    }

    private static void PruneRuntimeSamples(Queue<long> queue, long nowTick, int windowMs)
    {
        while (queue.Count > 0 && (nowTick - queue.Peek()) > windowMs)
        {
            queue.Dequeue();
        }
    }

    public PullRuntimeSnapshot GetRuntimeSnapshot()
    {
        lock (runtimeMetricsLock)
        {
            long nowTick = Environment.TickCount64;
            PruneRuntimeSamples(softRetryTicks, nowTick, RuntimeMetricsWindowMs);

            return new PullRuntimeSnapshot(
                PullFailureSoftRetryCountWindow: softRetryTicks.Count,
                WindowSeconds: RuntimeMetricsWindowMs / 1000);
        }
    }

    private static bool IsRangedPullAction(KeyAction keyAction)
    {
        // Treat every explicit cast action (not just Shoot/Throw) as ranged so
        // the consecutive-failure abort logic works for all caster pull sequences.
        // The caller guards with !playerReader.IsInMeleeRange(), keeping this safe
        // for melee classes whose pull spells only fail when target is truly out of range.
        return !keyAction.BaseAction;
    }

    private void Log(string text)
    {
        logger.LogInformation(text);
    }

    private bool IsGearTooBrokenToFight()
    {
        return bits.Items_Broken() || playerReader.AvgEquipDurability() <= 5;
    }
}
