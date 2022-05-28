using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Core.Goals
{
    public class WaitGoal : GoapGoal
    {
        private readonly ILogger logger;
        private readonly Wait wait;

        private readonly Stopwatch stopWatch = new();

        public override float CostOfPerformingAction => 21;

        public WaitGoal(ILogger logger, Wait wait)
        {
            this.logger = logger;
            this.wait = wait;
        }

        public override void OnEnter()
        {
            logger.LogInformation("Waiting");

            stopWatch.Restart();
            while (stopWatch.ElapsedMilliseconds < 1000)
            {
                wait.Update();
            }
            stopWatch.Stop();
        }

        public override void PerformAction() { }
    }
}