using Core.GOAP;
using Microsoft.Extensions.Logging;

namespace Core.Goals
{
    public class LastTargetLootGoal : GoapGoal
    {
        public override float Cost => 4.3f;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly PlayerReader playerReader;
        private readonly Wait wait;
        private readonly StopMoving stopMoving;
        private readonly BagReader bagReader;
        private readonly CombatUtil combatUtil;

        private const bool debug = true;
        private int lastLoot;

        public LastTargetLootGoal(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, StopMoving stopMoving, CombatUtil combatUtil)
            : base(nameof(LastTargetLootGoal))
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.playerReader = addonReader.PlayerReader;
            this.stopMoving = stopMoving;
            this.bagReader = addonReader.BagReader;

            this.combatUtil = combatUtil;

            AddPreconditions();
        }

        public virtual void AddPreconditions()
        {
            AddPrecondition(GoapKey.shouldloot, true);
            AddEffect(GoapKey.shouldloot, false);
        }

        public override void OnEnter()
        {
            if (bagReader.BagsFull)
            {
                logger.LogWarning("Inventory is full");
                SendGoapEvent(new GoapStateEvent(GoapKey.shouldloot, false));
            }

            lastLoot = playerReader.LastLootTime;

            stopMoving.Stop();
            combatUtil.Update();

            input.LastTarget();
            wait.Update();
            if (playerReader.Bits.HasTarget())
            {
                if (playerReader.Bits.TargetIsDead())
                {
                    Log("Found last dead target");
                    input.Interact();

                    (bool foundTarget, bool moved) = combatUtil.FoundTargetWhileMoved();
                    if (foundTarget)
                    {
                        Log("Goal interrupted!");
                        return;
                    }

                    if (moved)
                    {
                        Log("Last dead target double");
                        input.Interact();
                    }

                    if (!foundTarget && !moved)
                    {
                        Log("Just for safety Interact once more.");
                        input.Interact();
                    }
                }
                else
                {
                    Log("Don't attack the target!");
                    input.ClearTarget();
                }
            }

            GoalExit();
        }

        private void GoalExit()
        {
            if (!wait.Till(1000, LootChanged))
            {
                Log("Loot Successfull");
            }
            else
            {
                Log("Loot Failed");
            }

            lastLoot = playerReader.LastLootTime;

            SendGoapEvent(new GoapStateEvent(GoapKey.shouldloot, false));

            if (playerReader.Bits.HasTarget() && playerReader.Bits.TargetIsDead())
            {
                input.ClearTarget();
                wait.Update();
            }
        }

        private bool LootChanged()
        {
            return lastLoot != playerReader.LastLootTime;
        }

        private void Log(string text)
        {
            if (debug)
            {
                logger.LogInformation($"{nameof(LastTargetLootGoal)}: {text}");
            }
        }
    }
}