using Core.GOAP;

namespace Core.Goals
{
    public class TargetPetTarget : GoapGoal
    {
        public override float CostOfPerformingAction => 4.01f;

        private readonly ConfigurableInput input;
        private readonly PlayerReader playerReader;

        public TargetPetTarget(ConfigurableInput input, PlayerReader playerReader)
        {
            this.input = input;
            this.playerReader = playerReader;

            AddPrecondition(GoapKey.dangercombat, true);
            AddPrecondition(GoapKey.hastarget, false);
            AddPrecondition(GoapKey.pethastarget, true);

            AddEffect(GoapKey.hastarget, true);
        }

        public override void PerformAction()
        {
            input.TargetPet();
            input.TargetOfTarget();
            if (playerReader.Bits.HasTarget() && (playerReader.Bits.TargetIsDead() || playerReader.TargetGuid == playerReader.PetGuid))
            {
                input.ClearTarget();
            }
        }
    }
}
