namespace Core.Goals
{
    public class BlacklistTargetGoal : GoapGoal
    {
        public override float CostOfPerformingAction => 2;

        private readonly PlayerReader playerReader;
        private readonly ConfigurableInput input;
        private readonly IBlacklist blacklist;

        public BlacklistTargetGoal(PlayerReader playerReader, ConfigurableInput input, IBlacklist blacklist)
        {
            this.playerReader = playerReader;
            this.input = input;
            this.blacklist = blacklist;
        }

        public override bool CheckIfActionCanRun()
        {
            return playerReader.HasTarget && blacklist.IsTargetBlacklisted();
        }

        public override void OnEnter()
        {
            input.StopAttack();
            input.ClearTarget();
        }

        public override void PerformAction()
        {

        }
    }
}
