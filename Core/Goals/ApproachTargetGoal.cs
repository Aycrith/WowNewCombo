using Core.GOAP;

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

    private long approachStart;

    private double nextStuckCheckTime;

    private int initialTargetGuid;
    private float initialMinRange;

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

        AddPrecondition(GoapKey.hastarget, true);
        AddPrecondition(GoapKey.targetisalive, true);
        AddPrecondition(GoapKey.targethostile, true);
        AddPrecondition(GoapKey.incombatrange, false);

        AddEffect(GoapKey.incombatrange, true);
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

        approachStart = GetTimestamp();
        SetNextStuckTimeCheck();
    }

    public override void OnExit()
    {
        input.StopForward(false);
    }

    public override void Update()
    {
        wait.Update();

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

        if (!input.Approach.OnCooldown() && (!bits.SoftInteract() || HasValidSoftInteract()))
        {
            input.PressApproach();
            wait.Update();
        }

        if (!bits.Combat())
        {
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

                        SetNextStuckTimeCheck();

                        return;
                    }
                }

                Log($"Seems stuck! Clear Target.");

                input.ForceAggressiveClearTarget(wait, bits);
                input.TurnRandomDir(250 + Random.Shared.Next(250));

                return;
            }
        }

        if (ApproachDurationMs > MAX_APPROACH_DURATION_MS)
        {
            logger.LogWarning("Too long time. Clear Target. Turn away.");

            input.ForceAggressiveClearTarget(wait, bits);
            input.TurnRandomDir(250 + Random.Shared.Next(250));

            return;
        }

        if (playerReader.TargetGuid == initialTargetGuid &&
            !playerReader.IsInMeleeRange())
        {
            int initialTargetMinRange = playerReader.MinRange();
            if (!input.TargetNearestTarget.OnCooldown())
            {
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

                    if (playerReader.MinRange() < initialTargetMinRange)
                    {
                        logger.LogWarning($"Found a closer target! {playerReader.MinRange()} < {initialTargetMinRange}");

                        initialMinRange = playerReader.MinRange();
                    }
                    else
                    {
                        initialTargetGuid = -1;
                        logger.LogWarning("Stick to initial target!");

                        input.PressLastTargetAndWait(wait, bits.Target);
                    }
                }
            }
        }

        if (ApproachDurationMs > MIN_TIME_TILL_IDLE && initialMinRange < playerReader.MinRange())
        {
            Log($"Going away from the target! {initialMinRange} < {playerReader.MinRange()}");

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

    private void Log(string text)
    {
        logger.LogDebug(text);
    }


    #region Logging

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Warning,
        Message = "Clear current target as not in combat!")]
    static partial void LogPreventExtraPull(ILogger logger);

    #endregion
}
