namespace Core.Goals
{
    public class NullGoal : GoapGoal
    {
        public override float CostOfPerformingAction => 0;

        public override void PerformAction()
        {
        }
    }
}