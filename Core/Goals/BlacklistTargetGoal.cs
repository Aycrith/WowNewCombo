namespace Core.Goals
{
    public sealed class BlacklistTargetGoal : GoapGoal
    {
        public override float Cost => 2;

        private readonly PlayerReader playerReader;
        private readonly ConfigurableInput input;
        private readonly IBlacklist targetBlacklist;

        public BlacklistTargetGoal(PlayerReader playerReader, ConfigurableInput input, IBlacklist blacklist)
            : base(nameof(BlacklistTargetGoal))
        {
            this.playerReader = playerReader;
            this.input = input;
            this.targetBlacklist = blacklist;
        }

        public override bool CanRun()
        {
            return playerReader.Bits.HasTarget() && targetBlacklist.Is();
        }

        public override void OnEnter()
        {
            if (playerReader.PetHasTarget ||
                playerReader.IsCasting() ||
                playerReader.Bits.SpellOn_AutoAttack() || playerReader.Bits.SpellOn_AutoShot() ||
                playerReader.Bits.SpellOn_Shoot())
                input.StopAttack();

            input.ClearTarget();
        }
    }
}
