using Core.GOAP;

namespace Core.Goals
{
    public class FollowFocusGoal : GoapGoal
    {
        public override float Cost => 19f;

        private readonly ConfigurableInput input;
        private readonly PlayerReader playerReader;
        private readonly Wait wait;

        public FollowFocusGoal(ConfigurableInput input, PlayerReader playerReader, Wait wait)
            : base(nameof(FollowFocusGoal))
        {
            this.input = input;
            this.playerReader = playerReader;
            this.wait = wait;

            AddPrecondition(GoapKey.hasfocus, true);
            AddPrecondition(GoapKey.dangercombat, false);
            AddPrecondition(GoapKey.damagedone, false);
            AddPrecondition(GoapKey.damagetaken, false);
            AddPrecondition(GoapKey.producedcorpse, false);
            AddPrecondition(GoapKey.consumecorpse, false);
        }

        public override void Update()
        {
            if (playerReader.TargetGuid != playerReader.FocusGuid)
            {
                input.TargetFocus();
                wait.Update();
            }

            input.FollowTarget();

            wait.Update();
        }
    }
}
