using Core.GOAP;
using Microsoft.Extensions.Logging;

namespace Core.Goals
{
    public class PostKillLootGoal : LootGoal
    {
        public override float CostOfPerformingAction { get => 4.5f; }

        public PostKillLootGoal(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, StopMoving stopMoving, ClassConfiguration classConfiguration, NpcNameTargeting npcNameTargeting, CombatUtil combatUtil, PlayerDirection playerDirection)
            : base(logger, input, wait, addonReader, stopMoving, classConfiguration, npcNameTargeting, combatUtil, playerDirection)
        {
        }

        public override void AddPreconditions()
        {
            AddPrecondition(GoapKey.incombat, false);
            AddPrecondition(GoapKey.hastarget, false);
            AddPrecondition(GoapKey.shouldloot, true);
        }

        public override void PerformAction()
        {
            SendActionEvent(new ActionEventArgs(GoapKey.shouldloot, false));
            base.PerformAction();
        }
    }
}