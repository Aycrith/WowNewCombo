using Core.GOAP;

namespace Core.Goals
{
    public class DismountSubGoal : GoapGoal
    {
        public override float CostOfPerformingAction => 0.5f;

        private readonly MountHandler mountHandler;

        public DismountSubGoal(MountHandler mountHandler)
        {
            this.mountHandler = mountHandler;

            AddPrecondition(GoapKey.ismounted, true);
            AddEffect(GoapKey.ismounted, false);
        }

        public override void PerformAction()
        {
            mountHandler.Dismount();
        }
    }
}
