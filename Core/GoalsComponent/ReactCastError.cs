using Core.Goals;

using Microsoft.Extensions.Logging;

using System;
using System.Numerics;

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

    public ReactCastError(ILogger<ReactCastError> logger,
        PlayerReader playerReader,
        ActionBarBits<IUsableAction> usableAction,
        AddonBits bits, Wait wait, ConfigurableInput input, StopMoving stopMoving,
        PlayerDirection direction)
    {
        this.logger = logger;
        this.playerReader = playerReader;
        this.usableAction = usableAction;
        this.bits = bits;
        this.wait = wait;
        this.input = input;
        this.stopMoving = stopMoving;
        this.direction = direction;
    }

    public void Do(KeyAction item)
    {
        UI_ERROR value = (UI_ERROR)playerReader.CastEvent.Value;
        switch (value)
        {
            case UI_ERROR.NONE:
            case UI_ERROR.CAST_START:
            case UI_ERROR.CAST_SUCCESS:
            case UI_ERROR.SPELL_FAILED_TARGETS_DEAD:
                break;
            case UI_ERROR.ERR_SPELL_FAILED_INTERRUPTED:
                item.SetClicked();
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
                logger.LogInformation($"React to {value.ToStringF()} -- wait until its ready");
                int waitTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs);
                bool before = usableAction.Is(item);
                wait.Until(waitTime, () => before != usableAction.Is(item) || usableAction.Is(item));
                break;
            case UI_ERROR.ERR_ATTACK_PACIFIED:
            case UI_ERROR.ERR_SPELL_FAILED_STUNNED:
                int debuffCount = playerReader.AuraCount.PlayerDebuff;
                if (debuffCount != 0)
                {
                    logger.LogInformation($"React to {value.ToStringF()} -- Wait till losing debuff!");
                    wait.While(() => debuffCount == playerReader.AuraCount.PlayerDebuff);
                }
                else
                {
                    logger.LogInformation($"Didn't know how to react {value.ToStringF()} when PlayerDebuffCount: {debuffCount}");
                }

                break;
            case UI_ERROR.ERR_SPELL_OUT_OF_RANGE:

                if (!bits.HasTarget())
                    return;

                if (playerReader.Class == UnitClass.Hunter && playerReader.IsInMeleeRange())
                {
                    logger.LogInformation($"As a {UnitClass.Hunter.ToStringF()} didn't know how to react {value.ToStringF()}");
                    return;
                }

                int minRange = playerReader.MinRange();
                if (bits.PlayerInCombat() && bits.HasTarget() && !playerReader.IsTargetCasting())
                {
                    if (playerReader.TargetTarget == UnitsTarget.Me)
                    {
                        if (playerReader.InCloseMeleeRange())
                        {
                            logger.LogInformation($"React to {value.ToStringF()} -- ({minRange}) wait for close melee range.");
                            wait.Fixed(30);
                            wait.Update();
                            return;
                        }

                        logger.LogInformation($"React to {value.ToStringF()} -- ({minRange}) Just wait for the target to get in range.");

                        int duration = CastingHandler.GCD;
                        if (playerReader.MinRange() <= 5)
                            duration = CastingHandler.SPELL_QUEUE;

                        float e = wait.Until(duration,
                            () => minRange != playerReader.MinRange() || playerReader.IsTargetCasting()
                        );
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

                        float e = wait.Until(CastingHandler.GCD, () => minRange != playerReader.MinRange());

                        logger.LogInformation($"React to {value.ToStringF()} -- Approached target {minRange}->{playerReader.MinRange()}");
                    }
                    else if (!playerReader.WithInPullRange())
                    {
                        logger.LogInformation($"React to {value.ToStringF()} -- Start moving forward as outside of pull range.");
                        input.StartForward(true);
                    }
                    else
                    {
                        input.PressInteract();
                    }
                }
                break;
            case UI_ERROR.ERR_BADATTACKFACING:
                if (playerReader.IsInMeleeRange())
                {
                    logger.LogInformation($"React to {value.ToStringF()} -- Interact!");
                    input.PressInteract();
                    stopMoving.Stop();
                }
                else
                {
                    switch (playerReader.Class)
                    {
                        case UnitClass.None:
                            break;
                        case UnitClass.Monk:
                        case UnitClass.DemonHunter:
                        case UnitClass.Druid:
                        case UnitClass.DeathKnight:
                        case UnitClass.Warrior:
                        case UnitClass.Paladin:
                        case UnitClass.Rogue:
                            logger.LogInformation($"React to {value.ToStringF()} -- Interact!");
                            input.PressInteract();
                            stopMoving.Stop();
                            break;
                        case UnitClass.Hunter:
                        case UnitClass.Priest:
                        case UnitClass.Shaman:
                        case UnitClass.Mage:
                        case UnitClass.Warlock:
                            stopMoving.Stop();
                            logger.LogInformation($"React to {value.ToStringF()} -- Turning 180!");
                            float desiredDirection = playerReader.Direction + MathF.PI;
                            desiredDirection = desiredDirection > MathF.PI * 2 ? desiredDirection - (MathF.PI * 2) : desiredDirection;
                            direction.SetDirection(desiredDirection, Vector3.Zero);
                            break;
                    }

                    wait.Update();
                }
                break;
            case UI_ERROR.SPELL_FAILED_MOVING:
                logger.LogInformation($"React to {value.ToStringF()} -- Stop moving!");
                wait.While(bits.IsFalling);
                stopMoving.Stop();
                wait.Update();
                break;
            case UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS:
                logger.LogInformation($"React to {value.ToStringF()} -- Wait till casting!");
                wait.While(playerReader.IsCasting);
                break;
            case UI_ERROR.ERR_BADATTACKPOS:
                if (bits.SpellOn_AutoAttack())
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
                if (!bits.PlayerInCombat())
                {
                    logger.LogInformation($"React to {value.ToStringF()} -- Stop attack and clear target!");
                    input.PressStopAttack();
                    input.PressClearTarget();
                    wait.Update();
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

}
