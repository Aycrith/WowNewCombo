using Core.GOAP;

using Game;

using Microsoft.Extensions.Logging;

using System;

using static System.Diagnostics.Stopwatch;

#pragma warning disable 162

namespace Core.Goals;

public sealed partial class ApproachTargetGoal : GoapGoal, IGoapEventListener
{
    private const bool debug = true;
    private const double STUCK_INTERVAL_MS = 400; // cant be lower than Approach.Cooldown
    private const double MAX_APPROACH_DURATION_MS = 15_000; // max time to chase to pull
    private const double MIN_TIME_TILL_IDLE = 2000;
    private const int MIN_CLOSER_TARGET_RANGE_IMPROVEMENT = 8;
    private const int ClearTargetRangeRegressionThreshold = 4;
    private const int ConsecutiveNoMovementRetriesBeforeClear = 3;

    public override float Cost => 8f;

    private readonly ILogger<ApproachTargetGoal> logger;
    private readonly ConfigurableInput input;
    private readonly Wait wait;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly StopMoving stopMoving;
    private readonly CombatTracker combatTracker;
    private readonly IMountHandler mountHandler;
    private readonly IBlacklist targetBlacklist;
    private readonly CombatLog combatLog;
    private readonly bool holdPullStandoff;
    private DateTime lastBrokenGearWarning = DateTime.MinValue;

    private long approachStart;

    private double nextStuckCheckTime;

    private int initialTargetGuid;
    private float initialMinRange;
    private int consecutiveNoMovementChecks;

    // Throttle Tab (nearest target) presses to avoid rapid target churn.
    private double _lastTabAttemptMs;

    private double ApproachDurationMs => GetElapsedTime(approachStart).TotalMilliseconds;

    public ApproachTargetGoal(ILogger<ApproachTargetGoal> logger,
        ConfigurableInput input, Wait wait,
        PlayerReader playerReader, AddonBits addonBits,
        StopMoving stopMoving, CombatTracker combatTracker,
        IBlacklist blacklist,
        IMountHandler mountHandler,
        CombatLog combatLog)
        : base(nameof(ApproachTargetGoal))
    {
        this.logger = logger;
        this.input = input;

        this.wait = wait;
        this.playerReader = playerReader;
        this.bits = addonBits;

        this.stopMoving = stopMoving;
        this.combatTracker = combatTracker;
        this.mountHandler = mountHandler;
        this.targetBlacklist = blacklist;
        this.combatLog = combatLog;
        this.holdPullStandoff = ShouldHoldPullStandoff(playerReader.Class);

        AddPrecondition(GoapKey.hastarget, true);
        AddPrecondition(GoapKey.targetisalive, true);
        AddPrecondition(GoapKey.targethostile, true);
        if (holdPullStandoff)
        {
            AddPrecondition(GoapKey.withinpullrange, false);
            AddEffect(GoapKey.withinpullrange, true);
        }
        else
        {
            AddPrecondition(GoapKey.incombatrange, false);
            AddEffect(GoapKey.incombatrange, true);
        }
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e.GetType() == typeof(ResumeEvent))
        {
            approachStart = GetTimestamp();
        }
    }

    public override void OnEnter()
    {
        initialTargetGuid = initialTargetGuid == playerReader.TargetGuid
            ? -1
            : playerReader.TargetGuid;

        initialMinRange = playerReader.MinRange();

        _lastTabAttemptMs = 0;
        approachStart = GetTimestamp();
        consecutiveNoMovementChecks = 0;
        SetNextStuckTimeCheck();
    }

    public override void OnExit()
    {
        input.StopForward(false);
    }

    public override void Update()
    {
        wait.Update();

        if (IsGearTooBrokenToFight())
        {
            if ((DateTime.UtcNow - lastBrokenGearWarning).TotalSeconds >= 3)
            {
                Log("Gear durability critically low/broken; blocking approach and clearing target.");
                lastBrokenGearWarning = DateTime.UtcNow;
            }

            stopMoving.Stop();
            if (bits.Target())
            {
                input.ForceAggressiveClearTarget(wait, bits);
            }
            return;
        }

        // Multi-mob detection - early retreat
        if (!bits.Combat() && DetectMultiMobThreat())
        {
            HandleMultiMobThreat();
            return;
        }

        if (bits.Combat() && !bits.Target_Combat() &&
            !combatLog.ToPull.Contains(playerReader.TargetGuid))
        {
            stopMoving.Stop();

            LogPreventExtraPull(logger);

            input.ForceAggressiveClearTarget(wait, bits);

            combatTracker.AcquiredTarget(5000);
            return;
        }

        if (ShouldAdvanceTowardTarget(
            holdPullStandoff,
            playerReader.WithInPullRange(),
            playerReader.WithInCombatRange(),
            bits.Combat()) &&
            !input.Approach.OnCooldown() &&
            (!bits.SoftInteract() || HasValidSoftInteract()))
        {
            input.PressApproach();
            wait.Update();
        }

        if (!bits.Combat())
        {
            if (holdPullStandoff && playerReader.WithInPullRange())
            {
                stopMoving.Stop();
                wait.Update();
                return;
            }

            NonCombatApproach();
            RandomJump();
        }
    }

    #region Multi-Mob Detection

    private int multiMobThreatCount;
    private DateTime lastMultiMobCheck;

    /// <summary>
    /// Detects if approaching this target would pull multiple mobs.
    /// </summary>
    private bool DetectMultiMobThreat()
    {
        // Rate limit checks
        if ((DateTime.UtcNow - lastMultiMobCheck).TotalMilliseconds < 500)
        {
            return multiMobThreatCount > 1;
        }

        lastMultiMobCheck = DateTime.UtcNow;
        multiMobThreatCount = 0;

        // Count nearby hostiles using DamageTaken as proxy for mobs aware of us
        int nearbyHostiles = combatLog.DamageTaken.Count;

        // Also check if there are multiple mobs in ToPull (about to aggro)
        int toPullCount = combatLog.ToPull.Count;

        // Check target proximity - if target is close and we're not in combat,
        // there might be other mobs nearby
        if (playerReader.MinRange() < 15 && !bits.Combat())
        {
            // Estimate threat based on target type
            if (playerReader.TargetClassification == UnitClassification.Normal)
            {
                // Normal mobs often come in groups
                multiMobThreatCount = Math.Max(1, toPullCount) + nearbyHostiles;
            }
            else if (playerReader.TargetClassification == UnitClassification.Elite)
            {
                // Elites usually solo, but check anyway
                multiMobThreatCount = 1 + nearbyHostiles;
            }
        }

        if (multiMobThreatCount > 1 && debug)
        {
            Log($"Multi-mob threat detected: {multiMobThreatCount} (nearby: {nearbyHostiles}, topull: {toPullCount})");
        }

        return multiMobThreatCount > 1;
    }

    /// <summary>
    /// Handles detected multi-mob threat by aborting approach.
    /// </summary>
    private void HandleMultiMobThreat()
    {
        logger.LogWarning("[ApproachTarget] Multi-mob threat detected ({Count} mobs). Aborting approach!", multiMobThreatCount);

        stopMoving.Stop();

        // Clear target to avoid accidental pull
        input.ForceAggressiveClearTarget(wait, bits);

        // Turn away from the threat cluster
        input.TurnRandomDir(1000 + Random.Shared.Next(500));

        // Mark this target as temporary blacklist to avoid re-aggro
        if (playerReader.TargetGuid != 0)
        {
            Log($"Target {playerReader.TargetGuid} blacklisted temporarily due to multi-mob");
        }
    }

    #endregion

    private void NonCombatApproach()
    {
        if (ApproachDurationMs >= nextStuckCheckTime)
        {
            SetNextStuckTimeCheck();

            if (!bits.Moving())
            {
                if (playerReader.LastUIError is
                    UI_ERROR.ERR_AUTOFOLLOW_TOO_FAR or UI_ERROR.ERR_BADATTACKPOS)
                {
                    playerReader.LastUIError = UI_ERROR.NONE;

                    Log($"Target is too far({playerReader.MinRange()} yard) for interact, start moving forward!");
                    input.StartForward(false);
                    consecutiveNoMovementChecks = 0;

                    return;
                }
                // Handle pacified state - occurs when mounted and attempting combat actions
                // Dismount and retry interaction to resolve the pacified condition
                else if (playerReader.LastUIError == UI_ERROR.ERR_ATTACK_PACIFIED)
                {
                    playerReader.LastUIError = UI_ERROR.NONE;

                    if (mountHandler.IsMounted())
                    {
                        mountHandler.Dismount();

                        wait.While(bits.Falling);

                        input.PressInteract();
                        wait.Update();

                        consecutiveNoMovementChecks = 0;
                        SetNextStuckTimeCheck();

                        return;
                    }
                }

                if (ShouldRetryNoMovementApproach())
                {
                    consecutiveNoMovementChecks++;
                    Log(
                        $"Approach stalled; retrying face/approach ({consecutiveNoMovementChecks}/{ConsecutiveNoMovementRetriesBeforeClear})");

                    input.PressFastInteract();
                    wait.Update();

                    if (!input.Approach.OnCooldown() &&
                        (!bits.SoftInteract() || HasValidSoftInteract()))
                    {
                        input.PressApproach();
                        wait.Update();
                    }

                    return;
                }

                consecutiveNoMovementChecks = 0;
                Log($"Seems stuck! Clear Target.");

                input.ForceAggressiveClearTarget(wait, bits);
                input.TurnRandomDir(250 + Random.Shared.Next(250));

                return;
            }

            consecutiveNoMovementChecks = 0;
        }

        if (ApproachDurationMs > MAX_APPROACH_DURATION_MS)
        {
            logger.LogWarning("Too long time. Clear Target. Turn away.");

            consecutiveNoMovementChecks = 0;
            input.ForceAggressiveClearTarget(wait, bits);
            input.TurnRandomDir(250 + Random.Shared.Next(250));

            return;
        }

        if (playerReader.TargetGuid == initialTargetGuid &&
            !playerReader.IsInMeleeRange() &&
            !bits.Stealthed())
        {
            int initialTargetMinRange = playerReader.MinRange();
            if (!input.TargetNearestTarget.OnCooldown() &&
                (ApproachDurationMs - _lastTabAttemptMs) > 2000)
            {
                _lastTabAttemptMs = ApproachDurationMs;
                //logger.LogWarning($"Attempt to find closer target IsInMeleeRange:{playerReader.IsInMeleeRange()} - min:{playerReader.MinRange()} | max:{playerReader.MaxRange()} - initialMinRange:{initialTargetMinRange}");

                input.PressNearestTarget();
                wait.Update();

                if (bits.Target() && playerReader.TargetGuid != initialTargetGuid)
                {
                    if (targetBlacklist.Is())
                    {
                        logger.LogWarning($"Losing the target due blacklist!");
                        return;
                    }

                    int closerTargetRange = playerReader.MinRange();
                    int rangeImprovement = initialTargetMinRange - closerTargetRange;

                    if (rangeImprovement >= MIN_CLOSER_TARGET_RANGE_IMPROVEMENT)
                    {
                        logger.LogWarning(
                            "Found a meaningfully closer target! {NewRange} < {OldRange} (improvement {Improvement})",
                            closerTargetRange,
                            initialTargetMinRange,
                            rangeImprovement);

                        initialMinRange = closerTargetRange;
                    }
                    else
                    {
                        initialTargetGuid = -1;
                        logger.LogWarning(
                            "Stick to initial target (closer target improvement too small: {Improvement} < {Threshold})",
                            rangeImprovement,
                            MIN_CLOSER_TARGET_RANGE_IMPROVEMENT);

                        input.PressLastTargetAndWait(wait, bits.Target);
                    }
                }
            }
        }

        int rangeRegression = playerReader.MinRange() - (int)initialMinRange;
        if (ApproachDurationMs > MIN_TIME_TILL_IDLE &&
            rangeRegression >= ClearTargetRangeRegressionThreshold)
        {
            Log(
                $"Going away from the target! {initialMinRange} < {playerReader.MinRange()} (regression {rangeRegression}y)");

            consecutiveNoMovementChecks = 0;
            input.ForceAggressiveClearTarget(wait, bits);
        }
    }

    private void SetNextStuckTimeCheck()
    {
        nextStuckCheckTime = ApproachDurationMs + STUCK_INTERVAL_MS;
    }

    private void RandomJump()
    {
        if (ApproachDurationMs > MIN_TIME_TILL_IDLE &&
            input.Jump.SinceLastClickMs > Random.Shared.Next(5000, 25_000))
        {
            input.PressJump();
            wait.Update();
        }
    }

    private bool HasValidSoftInteract()
    {
        return
            bits.SoftInteract() &&
            !bits.SoftInteract_Dead() &&
            !bits.SoftInteract_Tagged() &&
            playerReader.SoftInteract_Type == GuidType.Creature;
    }

    private bool ShouldRetryNoMovementApproach()
    {
        return bits.Target() &&
            bits.Target_Alive() &&
            bits.Target_Hostile() &&
            consecutiveNoMovementChecks < ConsecutiveNoMovementRetriesBeforeClear;
    }

    private void Log(string text)
    {
        logger.LogDebug(text);
    }

    internal static bool ShouldHoldPullStandoff(UnitClass unitClass)
    {
        return unitClass == UnitClass.Warlock;
    }

    internal static bool ShouldAdvanceTowardTarget(
        bool holdPullStandoff,
        bool withinPullRange,
        bool inCombatRange,
        bool inCombat)
    {
        if (inCombat)
        {
            return false;
        }

        return holdPullStandoff
            ? !withinPullRange
            : !inCombatRange;
    }

    private bool IsGearTooBrokenToFight()
    {
        return bits.Items_Broken() || playerReader.AvgEquipDurability() <= 5;
    }


    #region Logging

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Warning,
        Message = "Clear current target as not in combat!")]
    static partial void LogPreventExtraPull(ILogger logger);

    #endregion
}
