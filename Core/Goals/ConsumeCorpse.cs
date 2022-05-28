using Microsoft.Extensions.Logging;
using Core.GOAP;

namespace Core.Goals
{
    public partial class ConsumeCorpse : GoapGoal
    {
        public override float CostOfPerformingAction => 4.1f;

        private readonly ILogger logger;
        private readonly ClassConfiguration classConfig;

        public ConsumeCorpse(ILogger logger, ClassConfiguration classConfig)
        {
            this.logger = logger;
            this.classConfig = classConfig;

            AddPrecondition(GoapKey.dangercombat, false);

            AddPrecondition(GoapKey.producedcorpse, true);
            AddPrecondition(GoapKey.consumecorpse, false);

            AddEffect(GoapKey.producedcorpse, false);
            AddEffect(GoapKey.consumecorpse, true);

            if (classConfig.Loot)
            {
                AddEffect(GoapKey.shouldloot, true);

                if (classConfig.Skin)
                {
                    AddEffect(GoapKey.shouldskin, true);
                }
            }
        }

        public override void OnEnter()
        {
            LogConsume(logger);
            SendActionEvent(new ActionEventArgs(GoapKey.consumecorpse, true));

            if (classConfig.Loot)
            {
                SendActionEvent(new ActionEventArgs(GoapKey.shouldloot, true));
            }
        }

        public override void PerformAction()
        {
        }

        [LoggerMessage(
            EventId = 100,
            Level = LogLevel.Information,
            Message = "----- Safe to consume a corpse.")]
        static partial void LogConsume(ILogger logger);
    }
}
