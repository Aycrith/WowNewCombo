using Core.GOAP;

namespace Core.Goals
{
    public class TargetFocusTargetGoal : GoapGoal
    {
        public override float CostOfPerformingAction => 10f;

        private readonly ConfigurableInput input;
        private readonly PlayerReader playerReader;
        private readonly Wait wait;

        public TargetFocusTargetGoal(ConfigurableInput input, PlayerReader playerReader, Wait wait)
            : base(nameof(TargetFocusTargetGoal))
        {
            this.input = input;
            this.playerReader = playerReader;
            this.wait = wait;

            AddPrecondition(GoapKey.hasfocus, true);
            AddPrecondition(GoapKey.focustargetincombat, true);
        }

        public override void PerformAction()
        {
            input.TargetFocus();
            wait.Update();
            input.TargetOfTarget();
            wait.Update();

            if (!playerReader.Bits.TargetCanBeHostile())
            {
                input.ClearTarget();
                wait.Update();
            }
        }
    }
}
