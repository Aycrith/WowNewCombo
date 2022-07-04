using Core.GOAP;

namespace Core.Goals
{
    public class DismountSubGoal : GoapGoal
    {
        public override float Cost => 0.5f;

        private readonly MountHandler mountHandler;

        public DismountSubGoal(MountHandler mountHandler)
            : base(nameof(DismountSubGoal))
        {
            this.mountHandler = mountHandler;

            AddPrecondition(GoapKey.ismounted, true);
            AddEffect(GoapKey.ismounted, false);
        }

        public override void Update()
        {
            mountHandler.Dismount();
        }
    }
}
