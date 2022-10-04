using Core.Goals;

using Microsoft.Extensions.Logging;

using System;
using System.Numerics;

namespace Core
{
    public sealed class ReactCastError
    {
        private readonly ILogger logger;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly Wait wait;
        private readonly ConfigurableInput input;
        private readonly StopMoving stopMoving;
        private readonly PlayerDirection direction;

        public ReactCastError(ILogger logger, AddonReader addonReader, Wait wait, ConfigurableInput input, StopMoving stopMoving, PlayerDirection direction)
        {
            this.logger = logger;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.wait = wait;
            this.input = input;
            this.stopMoving = stopMoving;
            this.direction = direction;
        }

        public void Do(KeyAction item, string source)
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
                logger.LogInformation($"{source} React to {value.ToStringF()} -- wait for GCD {waitTime}ms");
                if (waitTime > 0)
                    wait.Fixed(waitTime);
                break;
                */
                case UI_ERROR.ERR_SPELL_COOLDOWN:
                    logger.LogInformation($"{source} React to {value.ToStringF()} -- wait until its ready");
                    int waitTime = Math.Max(playerReader.GCD.Value, playerReader.RemainCastMs);
                    bool before = addonReader.UsableAction.Is(item);
                    wait.Until(waitTime, () => before != addonReader.UsableAction.Is(item) || addonReader.UsableAction.Is(item));
                    break;
                case UI_ERROR.ERR_ATTACK_PACIFIED:
                case UI_ERROR.ERR_SPELL_FAILED_STUNNED:
                    int debuffCount = playerReader.AuraCount.PlayerDebuff;
                    if (debuffCount != 0)
                    {
                        logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Wait till losing debuff!");
                        wait.While(() => debuffCount == playerReader.AuraCount.PlayerDebuff);
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to react {value.ToStringF()} when PlayerDebuffCount: {debuffCount}");
                    }

                    break;
                case UI_ERROR.ERR_SPELL_OUT_OF_RANGE:

                    if (!playerReader.Bits.HasTarget())
                        return;

                    if (playerReader.Class == UnitClass.Hunter && playerReader.IsInMeleeRange())
                    {
                        logger.LogInformation($"{source} -- As a Hunter didn't know how to react {value.ToStringF()}");
                        return;
                    }

                    int minRange = playerReader.MinRange();
                    if (playerReader.Bits.PlayerInCombat() && playerReader.Bits.HasTarget() && !playerReader.IsTargetCasting())
                    {
                        if (playerReader.TargetTarget == UnitsTarget.Me)
                        {
                            if (playerReader.InCloseMeleeRange())
                            {
                                logger.LogInformation($"{source} -- React to {value.ToStringF()} -- ({minRange}) wait for close melee range.");
                                wait.Fixed(30);
                                wait.Update();
                                return;
                            }

                            logger.LogInformation($"{source} -- React to {value.ToStringF()} -- ({minRange}) Just wait for the target to get in range.");

                            int duration = CastingHandler.GCD;
                            if (playerReader.MinRange() <= 5)
                                duration = CastingHandler.SpellQueueTimeMs;

                            (bool t, double e) = wait.Until(duration,
                                () => minRange != playerReader.MinRange() || playerReader.IsTargetCasting()
                            );
                            wait.Update();
                        }
                    }
                    else
                    {
                        double beforeDirection = playerReader.Direction;
                        input.Interact();
                        input.StopAttack();
                        stopMoving.Stop();
                        wait.Update();

                        if (beforeDirection != playerReader.Direction)
                        {
                            input.Interact();

                            (bool t, double e) = wait.Until(CastingHandler.GCD, () => minRange != playerReader.MinRange());

                            logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Approached target {minRange}->{playerReader.MinRange()}");
                        }
                        else if (!playerReader.WithInPullRange())
                        {
                            logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Start moving forward as outside of pull range.");
                            input.Proc.SetKeyState(input.Proc.ForwardKey, true);
                        }
                        else
                        {
                            input.Interact();
                        }
                    }
                    break;
                case UI_ERROR.ERR_BADATTACKFACING:
                    if (playerReader.IsInMeleeRange())
                    {
                        logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Interact!");
                        input.Interact();
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
                                logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Interact!");
                                input.Interact();
                                stopMoving.Stop();
                                break;
                            case UnitClass.Hunter:
                            case UnitClass.Priest:
                            case UnitClass.Shaman:
                            case UnitClass.Mage:
                            case UnitClass.Warlock:
                                stopMoving.Stop();
                                logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Turning 180!");
                                float desiredDirection = playerReader.Direction + MathF.PI;
                                desiredDirection = desiredDirection > MathF.PI * 2 ? desiredDirection - (MathF.PI * 2) : desiredDirection;
                                direction.SetDirection(desiredDirection, Vector3.Zero);
                                break;
                        }

                        wait.Update();
                    }
                    break;
                case UI_ERROR.SPELL_FAILED_MOVING:
                    logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Stop moving!");
                    wait.While(playerReader.Bits.IsFalling);
                    stopMoving.Stop();
                    wait.Update();
                    break;
                case UI_ERROR.ERR_SPELL_FAILED_ANOTHER_IN_PROGRESS:
                    logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Wait till casting!");
                    wait.While(playerReader.IsCasting);
                    break;
                case UI_ERROR.ERR_BADATTACKPOS:
                    if (playerReader.Bits.SpellOn_AutoAttack())
                    {
                        logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Interact!");
                        input.Interact();
                        stopMoving.Stop();
                        wait.Update();
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to React to {value.ToStringF()}");
                    }
                    break;
                case UI_ERROR.SPELL_FAILED_LINE_OF_SIGHT:
                    if (!playerReader.Bits.PlayerInCombat())
                    {
                        logger.LogInformation($"{source} -- React to {value.ToStringF()} -- Stop attack and clear target!");
                        input.StopAttack();
                        input.ClearTarget();
                        wait.Update();
                    }
                    else
                    {
                        logger.LogInformation($"{source} -- Didn't know how to React to {value.ToStringF()}");
                    }
                    break;
                default:
                    logger.LogInformation($"{source} -- Didn't know how to React to {value.ToStringF()}");
                    break;
            }
        }

    }
}
