using Core.GOAP;

namespace Core.Goals
{
    public class TargetFocusTargetGoal : GoapGoal
    {
        public override float Cost => 10f;

        private readonly ConfigurableInput input;
        private readonly PlayerReader playerReader;
        private readonly Wait wait;

        public TargetFocusTargetGoal(ConfigurableInput input, PlayerReader playerReader, Wait wait)
            : base(nameof(TargetFocusTargetGoal))
        {
            this.input = input;
            this.playerReader = playerReader;
            this.wait = wait;

            if (input.ClassConfig.Loot)
            {
                AddPrecondition(GoapKey.incombat, false);
            }

            AddPrecondition(GoapKey.hasfocus, true);
            AddPrecondition(GoapKey.focushastarget, true);
        }

        public override void OnEnter()
        {
            input.TargetFocus();
            wait.Update();
        }

        public override void Update()
        {
            if (playerReader.Bits.FocusTargetCanBeHostile())
            {
                if (playerReader.Bits.FocusTargetInCombat())
                {
                    input.TargetFocus();
                    input.TargetOfTarget();
                    wait.Update();
                }
            }
            else if (playerReader.Bits.FocusTargetInTradeRange())
            {
                input.TargetFocus();
                input.TargetOfTarget();
                input.Interact();
                wait.Update();
            }

            wait.Update();
        }

        public override void OnExit()
        {
            if (!playerReader.Bits.FocusHasTarget())
            {
                input.ClearTarget();
                wait.Update();
            }
        }
    }
}
