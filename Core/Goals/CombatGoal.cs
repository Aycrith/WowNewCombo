using Core.GOAP;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Numerics;

namespace Core.Goals
{
    public class CombatGoal : GoapGoal
    {
        public override float CostOfPerformingAction => 4f;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly CastingHandler castingHandler;
        private readonly MountHandler mountHandler;

        private float lastDirectionForTurnAround;

        private float lastKnwonPlayerDirection;
        private float lastKnownMinDistance;
        private float lastKnownMaxDistance;

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
            AddPrecondition(GoapKey.targethostile, true);
            AddPrecondition(GoapKey.targetisalive, true);
            //AddPrecondition(GoapKey.targettargetsus, true);
            AddPrecondition(GoapKey.incombatrange, true);

            AddEffect(GoapKey.producedcorpse, true);
            AddEffect(GoapKey.targetisalive, false);
            AddEffect(GoapKey.hastarget, false);

            Keys = classConfiguration.Combat.Sequence;
        }

        protected void Fight()
        {
            if (playerReader.Bits.HasPet() && !playerReader.PetHasTarget)
            {
                if (input.ClassConfig.PetAttack.GetCooldownRemaining() == 0)
                    input.PetAttack();
            }

            for (int i = 0; i < Keys.Length; i++)
            {
                if (playerReader.Bits.TargetIsDead() || !playerReader.Bits.HasTarget())
                {
                    logger.LogInformation("Lost Target!");
                    stopMoving.Stop();
                    return;
                }
                else
                {
                    lastKnwonPlayerDirection = playerReader.Direction;
                    lastKnownMinDistance = playerReader.MinRange();
                    lastKnownMaxDistance = playerReader.MaxRange();
                }

                if (castingHandler.CastIfReady(Keys[i]))
                {
                    break;
                }
            }
        }

        public override void OnActionEvent(object sender, ActionEventArgs e)
        {
            if (e.Key == GoapKey.newtarget)
            {
                logger.LogInformation("Reset cooldowns");

                ResetCooldowns();
            }

            if (e.Key == GoapKey.producedcorpse && (bool)e.Value)
            {
                // have to check range
                // ex. target died far away have to consider the range and approximate
                //logger.LogInformation($"--- Target is killed! Record death location.");
                float distance = (lastKnownMaxDistance + lastKnownMinDistance) / 2f;
                SendActionEvent(new ActionEventArgs(GoapKey.corpselocation, new CorpseLocation(GetCorpseLocation(distance), distance)));
            }
        }

        private void ResetCooldowns()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                var item = Keys[i];
                if (item.ResetOnNewTarget)
                {
                    logger.LogInformation($"Reset cooldown on {item.Name}");
                    item.ResetCooldown();
                    item.ResetCharges();
                }
            }
        }

        protected bool HasPickedUpAnAdd
        {
            get
            {
                return playerReader.Bits.PlayerInCombat() &&
                    !playerReader.Bits.TargetOfTargetIsPlayerOrPet() &&
                    playerReader.TargetHealthPercentage() == 100;
            }
        }

        public override void OnEnter()
        {
            if (mountHandler.IsMounted())
            {
                mountHandler.Dismount();
            }

            lastDirectionForTurnAround = playerReader.Direction;
        }

        public override void OnExit()
        {
            if (addonReader.CombatCreatureCount > 0 && !playerReader.Bits.HasTarget())
            {
                stopMoving.Stop();
            }
        }

        public override void PerformAction()
        {
            if (MathF.Abs(lastDirectionForTurnAround - playerReader.Direction) > MathF.PI / 2)
            {
                logger.LogInformation("Turning too fast!");
                stopMoving.Stop();

                lastDirectionForTurnAround = playerReader.Direction;
            }

            if (playerReader.Bits.IsDrowning())
            {
                StopDrowning();
                return;
            }

            if (playerReader.Bits.HasTarget())
            {
                Fight();
            }

            if (!playerReader.Bits.HasTarget() && addonReader.CombatCreatureCount > 0)
            {
                CreatureTargetMeOrMyPet();
            }

            wait.Update();
        }

        private void CreatureTargetMeOrMyPet()
        {
            wait.Update();
            if (playerReader.PetHasTarget && addonReader.CreatureHistory.CombatDeadGuid.Value != playerReader.PetTargetGuid)
            {
                logger.LogWarning("---- My pet has a target!");
                ResetCooldowns();

                input.TargetPet();
                input.TargetOfTarget();
                wait.Update();
                return;
            }

            if (addonReader.CombatCreatureCount > 1)
            {
                logger.LogInformation("Checking target in front of me");
                input.NearestTarget();
                wait.Update();
                if (playerReader.Bits.HasTarget())
                {
                    if (playerReader.Bits.TargetInCombat() && playerReader.Bits.TargetOfTargetIsPlayerOrPet())
                    {
                        ResetCooldowns();

                        logger.LogWarning("Somebody is attacking me! Found new target to attack");
                        input.Interact();
                        stopMoving.Stop();
                        wait.Update();
                        return;
                    }

                    input.ClearTarget();
                    wait.Update();
                }
                else
                {
                    // threat must be behind me
                    var anyDamageTakens = addonReader.CreatureHistory.DamageTaken.Where(x => (DateTime.UtcNow - x.LastEvent).TotalSeconds < 10 && x.HealthPercent > 0);
                    if (anyDamageTakens.Any())
                    {
                        logger.LogWarning($"---- Possible threats found behind {anyDamageTakens.Count()}. Waiting for my target to change!");
                        wait.Till(2000, playerReader.Bits.HasTarget);
                    }
                }
            }
        }

        private void StopDrowning()
        {
            logger.LogWarning("Drowning! Swim up");
            input.Jump();
            wait.Update();
        }

        private Vector3 GetCorpseLocation(float distance)
        {
            return PointEsimator.GetPoint(playerReader.PlayerLocation, playerReader.Direction, distance);
        }
    }
}