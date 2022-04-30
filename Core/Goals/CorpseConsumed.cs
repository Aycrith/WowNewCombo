using System;
using System.Threading.Tasks;
using Core.GOAP;
using Microsoft.Extensions.Logging;

namespace Core.Goals
{
    public partial class CorpseConsumed : GoapGoal
    {
        public override float CostOfPerformingAction => 4.7f;

        private readonly ILogger logger;
        private readonly GoapAgentState goapAgentState;

        public CorpseConsumed(ILogger logger, GoapAgentState goapAgentState)
        {
            this.logger = logger;
            this.goapAgentState = goapAgentState;

            AddPrecondition(GoapKey.dangercombat, false);
            AddPrecondition(GoapKey.consumecorpse, true);

            AddEffect(GoapKey.consumecorpse, false);
        }

        public override ValueTask OnEnter()
        {
            goapAgentState.LastCombatKillCount = Math.Max(goapAgentState.LastCombatKillCount - 1, 0);
            LogConsumed(logger, goapAgentState.LastCombatKillCount);

            SendActionEvent(new ActionEventArgs(GoapKey.consumecorpse, false));
            SendActionEvent(new ActionEventArgs(GoapKey.wowscreen, false));

            return base.OnEnter();
        }

        public override ValueTask PerformAction()
        {
            return ValueTask.CompletedTask;
        }


        [LoggerMessage(
            EventId = 101,
            Level = LogLevel.Information,
            Message = "----- Corpse consumed. Remaining: {remains}")]
        static partial void LogConsumed(ILogger logger, int remains);
    }
}