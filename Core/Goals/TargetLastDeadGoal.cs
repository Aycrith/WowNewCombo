using Core.GOAP;
using Microsoft.Extensions.Logging;

namespace Core.Goals
{
    public class TargetLastDeadGoal : GoapGoal
    {
        public override float CostOfPerformingAction => 4.2f;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        public TargetLastDeadGoal(ILogger logger, ConfigurableInput input)
            : base(nameof(TargetLastDeadGoal))
        {
            this.logger = logger;
            this.input = input;

            AddPrecondition(GoapKey.hastarget, false);
            AddPrecondition(GoapKey.producedcorpse, true);
        }

        public override void PerformAction()
        {
            input.LastTarget();
        }
    }
}