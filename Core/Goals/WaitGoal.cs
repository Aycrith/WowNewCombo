using Microsoft.Extensions.Logging;

namespace Core.Goals
{
    public class WaitGoal : GoapGoal
    {
        public override float Cost => 21;

        private readonly ILogger logger;
        private readonly Wait wait;

        public WaitGoal(ILogger logger, Wait wait)
            : base(nameof(WaitGoal))
        {
            this.logger = logger;
            this.wait = wait;
        }

        public override void OnEnter()
        {
            logger.LogInformation("Waiting");
        }

        public override void Update()
        {
            wait.Update();
        }
    }
}