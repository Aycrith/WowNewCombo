using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Core.Goals
{
    public class WaitGoal : GoapGoal
    {
        public override float Cost => 21;
        
        private readonly ILogger logger;
        private readonly Wait wait;

        private readonly Stopwatch stopWatch = new();

        public WaitGoal(ILogger logger, Wait wait)
            : base(nameof(WaitGoal))
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

        public override void Update()
        {
            wait.Update();
        }
    }
}