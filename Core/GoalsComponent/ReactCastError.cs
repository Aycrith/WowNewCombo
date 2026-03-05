using Core.Goals;

using Microsoft.Extensions.Logging;

using System;
using System.Numerics;

using static System.MathF;

namespace Core;

public sealed class ReactCastError
{
    private readonly ILogger<ReactCastError> logger;
    private readonly PlayerReader playerReader;
    private readonly ActionBarBits<IUsableAction> usableAction;
    private readonly AddonBits bits;
    private readonly Wait wait;
    private readonly ConfigurableInput input;
    private readonly StopMoving stopMoving;
    private readonly PlayerDirection direction;
    private readonly AddonReader addonReader;
    private readonly SessionStat sessionStat;
    private readonly ExecGameCommand execGameCommand;

    public ReactCastError(ILogger<ReactCastError> logger,
        PlayerReader playerReader,
        AddonReader addonReader,
        ActionBarBits<IUsableAction> usableAction,
        AddonBits bits, Wait wait, ConfigurableInput input, StopMoving stopMoving,
        SessionStat sessionStat,
        PlayerDirection direction,
        ExecGameCommand execGameCommand)
    {
        this.logger = logger;
        this.playerReader = playerReader;
        this.addonReader = addonReader;
        this.usableAction = usableAction;
        this.bits = bits;
        this.wait = wait;
        this.input = input;
        this.stopMoving = stopMoving;
        this.direction = direction;
        this.sessionStat = sessionStat;
        this.execGameCommand = execGameCommand;
    }

    public void Do(KeyAction item)
    {
        UI_ERROR value = (UI_ERROR)playerReader.CastEvent.Value;
        switch (value)
        {
            case UI_ERROR.CAST_SUCCESS:
                WaitForCooldown(item, value);
                break;
            case UI_ERROR.CAST_SENT:
                UI_ERROR currentCastState = playerReader.CastState;
                int maxTime = Math.Min(playerReader.DoubleNetworkLatency, playerReader.RemainCastMs);
                logger.LogInformation($"React to {value.ToStringF()} -- by waiting {maxTime}ms!");

                wait.Until(maxTime,
                    () => currentCastState != playerReader.CastState);
                break;
            case UI_ERROR.NONE:
            case UI_ERROR.CAST_START:
            case UI_ERROR.SPELL_FAILED_TARGETS_DEAD:
                break;
            case UI_ERROR.ERR_SPELL_FAILED_INTERRUPTED:
                int retryDelayMs = Math.Max(
                    playerReader.GCD.Value,
                    Math.Max(playerReader.HalfSpellQueueTimeMs, CastingHandler.SPELL_QUEUE));
                item.SetClicked(retryDelayMs);
                wait.Fixed(Math.Min(retryDelayMs, playerReader.NetworkLatency));
                break;
            case UI_ERROR.SPELL_FAILED_NOT_READY:
            /*
            int waitTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs);
            logger.LogInformation($"React to {value.ToStringF()} -- wait for GCD {waitTime}ms");
            if (waitTime > 0)
                wait.Fixed(waitTime);
            break;
            */
            case UI_ERROR.ERR_SPELL_COOLDOWN:
                WaitForCooldown(item, value);
                break;
            case UI_ERROR.ERR_ATTACK_PACIFIED:
            case UI_ERROR.ERR_SPELL_FAILED_STUNNED:
                int debuffCount = playerReader.AuraCount.PlayerDebuff;
                if (debuffCount != 0)
                {
                    logger.LogInformation($"React to {value.ToStringF()} -- Wait till losing debuff!");

                    WaitDebuffChange(wait, debuffCount, playerReader);
                    static void WaitDebuffChange(Wait wait,
                        int debuffCount, PlayerReader playerReader) =>
                        wait.While(() =>
                        debuffCount == playerReader.AuraCount.PlayerDebuff);
                }
                else
                {
                    logger.LogInformation($"Didn't know how to react {value.ToStringF()} when PlayerDebuffCount: {debuffCount}");
                }

                break;
            case UI_ERROR.ERR_SPELL_OUT_OF_RANGE:

                if (!bits.Target())
                    return;

                if (playerReader.Class == UnitClass.Hunter && playerReader.IsInMeleeRange())
                {
                    logger.LogInformation($"As a {UnitClass.Hunter.ToStringF()} didn't know how to react {value.ToStringF()}");
                    return;
                }

                int minRange = playerReader.MinRange();
                if (bits.Combat() && bits.Target() && !playerReader.IsTargetCasting())
                {
                    if (playerReader.TargetTarget == UnitsTarget.Me)
                    {
                        if (playerReader.InCloseMeleeRange())
                        {
                            logger.LogInformation($"React to {value.ToStringF()} -- ({minRange}) wait for close melee range.");
                            wait.Update();
                            wait.Update();
                            return;
                        }

                        logger.LogInformation($"React to {value.ToStringF()} -- ({minRange}) Just wait for the target to get in range.");

                        int duration = CastingHandler.GCD;
                        if (playerReader.MinRange() <= 5)
                            duration = CastingHandler.SPELL_QUEUE;

                        OutOfRange(duration, wait, minRange, playerReader);
                        static void OutOfRange(int duration, Wait wait,
                            int minRange, PlayerReader playerReader) =>
                            wait.Until(duration, () =>
                            minRange != playerReader.MinRange() || playerReader.IsTargetCasting());

                        wait.Update();
                    }
                }
                else
                {
                    double beforeDirection = playerReader.Direction;
                    input.PressInteract();
                    input.PressStopAttack();
                    stopMoving.Stop();
                    wait.Update();

                    if (beforeDirection != playerReader.Direction)
                    {
                        input.PressInteract();

                        MinRangeChanges(CastingHandler.GCD, wait, minRange, playerReader);
                        static void MinRangeChanges(int duration, Wait wait,
                            int minRange, PlayerReader playerReader) =>
                            wait.Until(duration, () =>
                            minRange != playerReader.MinRange());

                        logger.LogInformation($"React to {value.ToStringF()} -- Approached target {minRange}->{playerReader.MinRange()}");
                    }
                    else if (!playerReader.WithInPullRange())
                    {
                        // Re-face target and let PullTargetGoal's approach fallback
                        // handle forward movement — StartForward here races with the
                        // pull loop's own stopped state and causes uncontrolled body-pulls.
                        logger.LogInformation($"React to {value.ToStringF()} -- Outside pull range, re-facing target.");
                        input.PressInteract();
                    }
                    else
                    {
                        input.PressInteract();
                    }
                }
                break;
            case UI_ERROR.ERR_BADATTACKFACING:

                bool wasAnyAuto = bits.Any_AutoAttack();

                float beforeDir = playerReader.Direction;

                input.PressFastInteract();
                stopMoving.StopForward();

                const int updateCount = 2;
                float e = wait.AfterEquals(playerReader.SpellQueueTimeMs,
                    updateCount, playerReader._Direction);

                float sampleTimeMs =
                    updateCount * (float)addonReader.AvgUpdateLatency;
                bool directionChanged = DidDirectionChangeEnough(beforeDir, playerReader.Direction);

                if (e > sampleTimeMs || directionChanged)
                {
                    stopMoving.Stop();
                    logger.LogInformation(
                        $"React to {value.ToStringF()} - " +
                        $"Fast turn with Interact {e}ms");
                }
                else
                {
                    logger.LogWarning(
                        $"Unable to react to {value.ToStringF()} - " +
                        $"Fast turn with Interact {e}ms");
                }

                if (!wasAnyAuto)
                    input.PressStopAttack();

                if (e <= sampleTimeMs && !directionChanged && bits.Target() && bits.Target_Alive())
                {
                    wait.Fixed(Math.Max(playerReader.HalfNetworkLatency, 25));
                    input.PressFastInteract();
                    stopMoving.StopForward();
                    wait.Update();
                    directionChanged = DidDirectionChangeEnough(beforeDir, playerReader.Direction);
                }

                if (e <= sampleTimeMs &&
                    !directionChanged &&
                    bits.Target() &&
                    bits.Target_Alive() &&
                    !playerReader.WithInCombatRange())
                {
                    input.PressApproachOnCooldown();
                    wait.Update();
                    stopMoving.StopForward();
                    directionChanged = DidDirectionChangeEnough(beforeDir, playerReader.Direction);
                }

                if (e <= sampleTimeMs && !directionChanged)
                {
                    stopMoving.Stop();
                    logger.LogInformation($"React to {value.ToStringF()} - " +
                        $"Slow turn 180deg");
                    float targetDir = playerReader.Direction + PI;
                    if (targetDir > Tau)
                        targetDir = -Tau;
                    direction.SetDirection(targetDir, Vector3.Zero);
                }
                break;
            case UI_ERROR.SPELL_FAILED_MOVING:
                logger.LogInformation($"React to {value.ToStringF()} -- Stop moving!");
                wait.While(bits.Falling);
                stopMoving.Stop();
                wait.Update();
                break;
            case UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS:
                logger.LogInformation($"React to {value.ToStringF()} -- Wait till casting!");
                wait.While(playerReader.IsCasting);
                break;
            case UI_ERROR.ERR_BADATTACKPOS:
                sessionStat.MarkBadAttackPosition();
                if (bits.Auto_Attack())
                {
                    logger.LogInformation($"React to {value.ToStringF()} -- Interact!");
                    input.PressInteract();
                    stopMoving.Stop();
                    wait.Update();
                }
                else
                {
                    goto default;
                }
                break;
            case UI_ERROR.SPELL_FAILED_LINE_OF_SIGHT:
                if (!bits.Combat())
                {
                    logger.LogInformation($"React to {value.ToStringF()} -- Stop attack and clear target!");
                    input.PressStopAttack();
                    input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
                }
                else
                {
                    goto default;
                }
                break;
            default:
                logger.LogInformation($"Didn't know how to React to {value.ToStringF()}");
                break;
        }
    }

    private void WaitForCooldown(KeyAction item, UI_ERROR value)
    {
        logger.LogInformation($"React to {value.ToStringF()} -- wait until its ready");
        int waitTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs);
        bool before = usableAction.Is(item);

        WaitCooldown(waitTime, before, wait, usableAction, item);
        static void WaitCooldown(int duration, bool before, Wait wait,
            ActionBarBits<IUsableAction> usableAction, KeyAction item) =>
            wait.Until(duration, () =>
            before != usableAction.Is(item) || usableAction.Is(item));

    }

    private static bool DidDirectionChangeEnough(float before, float after)
    {
        float diff = Abs(after - before);
        if (diff > PI)
        {
            diff = Tau - diff;
        }

        return diff >= (PI / 36f); // ~5 degrees
    }
}
