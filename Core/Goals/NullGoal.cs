namespace Core.Goals
{
    public class NullGoal : GoapGoal
    {
        public NullGoal() : base(nameof(NullGoal))
        {
        }

        public override float CostOfPerformingAction => 0;

        public override void PerformAction()
        {
        }
    }
}