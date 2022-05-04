using Core.GOAP;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Core.Goals
{
    public class LastTargetLoot : GoapGoal
    {
        public override float CostOfPerformingAction { get => 4.3f; }

        private ILogger logger;
        private readonly ConfigurableInput input;

        private readonly PlayerReader playerReader;
        private readonly Wait wait;
        private readonly StopMoving stopMoving;
        private readonly BagReader bagReader;
        private readonly CombatUtil combatUtil;

        private const bool debug = true;
        private int lastLoot;

        public LastTargetLoot(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, StopMoving stopMoving, CombatUtil combatUtil)
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.playerReader = addonReader.PlayerReader;
            this.stopMoving = stopMoving;
            this.bagReader = addonReader.BagReader;

            this.combatUtil = combatUtil;
        }

        public virtual void AddPreconditions()
        {
            AddPrecondition(GoapKey.shouldloot, true);
            AddEffect(GoapKey.shouldloot, false);
        }

        public override ValueTask OnEnter()
        {
            if (bagReader.BagsFull)
            {
                logger.LogWarning("Inventory is full");
                SendActionEvent(new ActionEventArgs(GoapKey.shouldloot, false));
            }

            int lastHealth = playerReader.HealthCurrent;
            var lastPosition = playerReader.PlayerLocation;
            lastLoot = playerReader.LastLootTime;

            stopMoving.Stop();
            combatUtil.Update();

            Log("No corpse name found - check last dead target exists");
            input.LastTarget();
            wait.Update();
            if (playerReader.HasTarget)
            {
                if (playerReader.Bits.TargetIsDead)
                {
                    Log("Found last dead target");
                    input.Interact();
                    wait.Update();

                    (bool foundTarget, bool moved) = combatUtil.FoundTargetWhileMoved();
                    if (foundTarget)
                    {
                        Log("Goal interrupted!");
                        return ValueTask.CompletedTask;
                    }

                    if (moved)
                    {
                        Log("Last dead target double");
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

            return ValueTask.CompletedTask;
        }

        public override ValueTask PerformAction()
        {
            return ValueTask.CompletedTask;
        }

        private void GoalExit()
        {
            if (!wait.Till(1000, () => lastLoot != playerReader.LastLootTime))
            {
                Log($"Loot Successfull");
            }
            else
            {
                Log($"Loot Failed");
            }

            lastLoot = playerReader.LastLootTime;

            SendActionEvent(new ActionEventArgs(GoapKey.shouldloot, false));

            if (playerReader.HasTarget && playerReader.Bits.TargetIsDead)
            {
                //$"{nameof(LastTargetLoot)}: Exit Goal"
                input.ClearTarget();
                wait.Update();
            }
        }

        private void Log(string text)
        {
            if (debug)
            {
                logger.LogInformation($"{nameof(LastTargetLoot)}: {text}");
            }
        }
    }
}