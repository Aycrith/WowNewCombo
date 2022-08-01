using Core.GOAP;
using Microsoft.Extensions.Logging;
using System;
using System.Numerics;

namespace Core.Goals
{
    public class CombatGoal : GoapGoal, IGoapEventListener
    {
        public override float Cost => 4f;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly CastingHandler castingHandler;
        private readonly MountHandler mountHandler;

        private float lastDirection;
        private float lastMinDistance;
        private float lastMaxDistance;

        public CombatGoal(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, StopMoving stopMoving, ClassConfiguration classConfiguration, CastingHandler castingHandler, MountHandler mountHandler)
            : base(nameof(CombatGoal))
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.stopMoving = stopMoving;
            this.castingHandler = castingHandler;
            this.mountHandler = mountHandler;

            AddPrecondition(GoapKey.incombat, true);
            AddPrecondition(GoapKey.hastarget, true);
            AddPrecondition(GoapKey.targetisalive, true);
            AddPrecondition(GoapKey.targethostile, true);
            //AddPrecondition(GoapKey.targettargetsus, true);
            AddPrecondition(GoapKey.incombatrange, true);

            AddEffect(GoapKey.producedcorpse, true);
            AddEffect(GoapKey.targetisalive, false);
            AddEffect(GoapKey.hastarget, false);

            Keys = classConfiguration.Combat.Sequence;
        }

        public void OnGoapEvent(GoapEventArgs e)
        {
            if (e is GoapStateEvent s)
            {
                if (s.Key == GoapKey.newtarget)
                {
                    logger.LogInformation("Reset cooldowns");

                    ResetCooldowns();
                }
                else if (s.Key == GoapKey.producedcorpse)
                {
                    // have to check range
                    // ex. target died far away have to consider the range and approximate
                    float distance = (lastMaxDistance + lastMinDistance) / 2f;
                    SendGoapEvent(new CorpseEvent(GetCorpseLocation(distance), distance));
                }
            }
        }

        private void ResetCooldowns()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                KeyAction keyAction = Keys[i];
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

            castingHandler.UpdateGCD(true);

            lastDirection = playerReader.Direction;
        }

        public override void OnExit()
        {
            if (addonReader.DamageTakenCount() > 0 && !playerReader.Bits.HasTarget())
            {
                stopMoving.Stop();
            }
        }

        public override void Update()
        {
            wait.Update();

            if (MathF.Abs(lastDirection - playerReader.Direction) > MathF.PI / 2)
            {
                logger.LogInformation("Turning too fast!");
                stopMoving.Stop();

                lastDirection = playerReader.Direction;
            }

            lastDirection = playerReader.Direction;
            lastMinDistance = playerReader.MinRange();
            lastMaxDistance = playerReader.MaxRange();

            if (playerReader.Bits.IsDrowning())
            {
                input.Jump();
                return;
            }

            if (playerReader.Bits.HasTarget())
            {
                if (playerReader.Bits.HasPet() &&
                    (!playerReader.PetHasTarget || playerReader.PetTargetGuid != playerReader.TargetGuid) &&
                    input.ClassConfig.PetAttack.GetCooldownRemaining() == 0)
                {
                    input.PetAttack();
                }

                for (int i = 0; i < Keys.Length; i++)
                {
                    KeyAction keyAction = Keys[i];

                    if (castingHandler.SpellInQueue() && !keyAction.BaseAction)
                    {
                        continue;
                    }

                    if (castingHandler.CastIfReady(keyAction))
                    {
                        break;
                    }
                }
            }

            if (!playerReader.Bits.HasTarget())
            {
                stopMoving.Stop();
                logger.LogInformation("Lost target!");

                if (addonReader.DamageTakenCount() > 0 && !input.ClassConfig.KeyboardOnly)
                {
                    FindNewTarget();
                }
            }
        }

        private void FindNewTarget()
        {
            if (playerReader.PetHasTarget && addonReader.CombatLog.DeadGuid.Value != playerReader.PetTargetGuid)
            {
                ResetCooldowns();

                input.TargetPet();
                input.TargetOfTarget();
                wait.Update();

                if (!playerReader.Bits.TargetIsDead())
                {
                    logger.LogWarning("---- New targe from Pet target!");
                    return;
                }

                input.ClearTarget();
            }

            if (addonReader.DamageTakenCount() > 1)
            {
                logger.LogInformation("Checking target in front...");
                input.NearestTarget();
                wait.Update();

                if (playerReader.Bits.HasTarget() && !playerReader.Bits.TargetIsDead())
                {
                    if (playerReader.Bits.TargetInCombat() && playerReader.Bits.TargetOfTargetIsPlayerOrPet())
                    {
                        stopMoving.Stop();
                        ResetCooldowns();

                        logger.LogWarning("Found new target!");
                        input.Interact();

                        return;
                    }

                    input.ClearTarget();
                    wait.Update();
                }
                else if (addonReader.DamageTakenCount() > 0)
                {
                    logger.LogWarning($"---- Possible threats from behind {addonReader.DamageTakenCount()}. Waiting target by damage taken!");
                    wait.Till(2500, playerReader.Bits.HasTarget);
                }
            }
        }

        private Vector3 GetCorpseLocation(float distance)
        {
            return PointEstimator.GetPoint(playerReader.PlayerLocation, playerReader.Direction, distance);
        }
    }
}