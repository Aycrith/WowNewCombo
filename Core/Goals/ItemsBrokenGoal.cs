using Core.GOAP;
using Microsoft.Extensions.Logging;

namespace Core.Goals
{
    public class ItemsBrokenGoal : GoapGoal
    {
        public override float Cost => 0;

        private readonly ILogger logger;
        private readonly PlayerReader playerReader;

        public ItemsBrokenGoal(PlayerReader playerReader, ILogger logger)
            : base(nameof(ItemsBrokenGoal))
        {
            this.playerReader = playerReader;
            this.logger = logger;
        }

        public override bool CanRun()
        {
            return playerReader.Bits.ItemsAreBroken();
        }

        public override void Update()
        {
            logger.LogInformation("Items are broken");
            SendGoapEvent(new AbortEvent());
        }
    }
}