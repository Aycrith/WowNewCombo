using Core.AddonComponent;
using Core.GOAP;

using Microsoft.Extensions.Logging;

using System;
using System.Numerics;

#pragma warning disable 162

namespace Core.Goals;

public sealed class ApproachTargetGoal : GoapGoal, IGoapEventListener
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
    private readonly CombatUtil combatUtil;
    private readonly IMountHandler mountHandler;
    private readonly IBlacklist targetBlacklist;

    private DateTime approachStart;

    private double nextStuckCheckTime;
    private Vector3 playerMap;

    private int initialTargetGuid;
    private float initialMinRange;

    private double ApproachDurationMs =>
        (DateTime.UtcNow - approachStart).TotalMilliseconds;

    public ApproachTargetGoal(ILogger<ApproachTargetGoal> logger,
        ConfigurableInput input, Wait wait,
        PlayerReader playerReader, AddonBits addonBits,
        StopMoving stopMoving, CombatUtil combatUtil, IBlacklist blacklist,
        IMountHandler mountHandler)
        : base(nameof(ApproachTargetGoal))
    {
        this.logger = logger;
        this.input = input;

        this.wait = wait;
        this.playerReader = playerReader;
        this.bits = addonBits;

        this.stopMoving = stopMoving;
        this.combatUtil = combatUtil;
        this.mountHandler = mountHandler;
        this.targetBlacklist = blacklist;

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
            approachStart = DateTime.UtcNow;
        }
    }

    public override void OnEnter()
    {
        initialTargetGuid = playerReader.TargetGuid;
        initialMinRange = playerReader.MinRange();
        playerMap = playerReader.MapPos;

        combatUtil.Update();

        approachStart = DateTime.UtcNow;
        SetNextStuckTimeCheck();
    }

    public override void OnExit()
    {
        input.StopForward(false);
    }

    public override void Update()
    {
        wait.Update();

        if (combatUtil.EnteredCombat() && !bits.Target_Combat())
        {
            stopMoving.Stop();

            input.PressClearTarget();
            wait.Update();

            combatUtil.AcquiredTarget(5000);
            return;
        }

        if (!input.Approach.OnCooldown() && HasValidSoftInteract())
        {
            input.PressApproach();
        }

        if (!bits.Combat())
        {
            NonCombatApproach();
            RandomJump();
        }
    }

    private void NonCombatApproach()
    {
        if (ApproachDurationMs >= nextStuckCheckTime)
        {
            SetNextStuckTimeCheck();

            Vector3 last = playerMap;
            playerMap = playerReader.MapPos;
            if (!bits.Moving())
            {
                if (playerReader.LastUIError == UI_ERROR.ERR_AUTOFOLLOW_TOO_FAR)
                {
                    playerReader.LastUIError = UI_ERROR.NONE;

                    if (debug)
                        Log($"Too far ({playerReader.MinRange()} yard), start moving forward!");

                    input.StartForward(false);
                    return;
                }
                else if (playerReader.LastUIError == UI_ERROR.ERR_ATTACK_PACIFIED)
                {
                    playerReader.LastUIError = UI_ERROR.NONE;

                    if (mountHandler.IsMounted())
                    {
                        mountHandler.Dismount();
                        wait.Fixed(playerReader.DoubleNetworkLatency);

                        wait.While(bits.Falling);

                        input.PressInteract();

                        SetNextStuckTimeCheck();

                        return;
                    }
                }

                if (debug)
                    Log($"Seems stuck! Clear Target.");

                input.PressClearTarget();
                wait.Update();
                input.TurnRandomDir(250 + Random.Shared.Next(250));

                return;
            }
        }

        if (ApproachDurationMs > MAX_APPROACH_DURATION_MS)
        {
            if (debug)
                Log("Too long time. Clear Target. Turn away.");

            input.PressClearTarget();
            wait.Update();
            input.TurnRandomDir(250 + Random.Shared.Next(250));

            return;
        }

        if (playerReader.TargetGuid == initialTargetGuid)
        {
            int initialTargetMinRange = playerReader.MinRange();
            if (!input.TargetNearestTarget.OnCooldown())
            {
                input.PressNearestTarget();
                wait.Update();
            }

            if (playerReader.TargetGuid != initialTargetGuid)
            {
                if (bits.Target() && !targetBlacklist.Is())
                {
                    if (playerReader.MinRange() < initialTargetMinRange)
                    {
                        if (debug)
                            Log($"Found a closer target! {playerReader.MinRange()} < {initialTargetMinRange}");

                        initialMinRange = playerReader.MinRange();
                    }
                    else
                    {
                        initialTargetGuid = -1;
                        if (debug)
                            Log("Stick to initial target!");

                        input.PressLastTarget();
                        wait.Update();
                    }
                }
                else
                {
                    if (debug)
                        Log($"Lost the target due blacklist!");
                }
            }
        }

        if (ApproachDurationMs > MIN_TIME_TILL_IDLE && initialMinRange < playerReader.MinRange())
        {
            if (debug)
                Log($"Going away from the target! {initialMinRange} < {playerReader.MinRange()}");

            input.PressClearTarget();
            wait.Update();
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
        }
    }

    private bool HasValidSoftInteract()
    {
        return
            !bits.SoftInteract() ||
            (bits.SoftInteract() &&
            !bits.SoftInteract_Dead() &&
            !bits.SoftInteract_Tagged() &&
            playerReader.SoftInteract_Type == GuidType.Creature);
    }

    private void Log(string text)
    {
        logger.LogDebug(text);
    }
}
