namespace Core.Goals
{
    public class NullGoal : GoapGoal
    {
        public override float Cost => 0;

        public NullGoal() : base(nameof(NullGoal)) { }
    }
}