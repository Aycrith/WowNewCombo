using Core.GOAP;

namespace Core.Goals
{
    public class TargetPetTargetGoal : GoapGoal
    {
        public override float Cost => 4.01f;

        private readonly ConfigurableInput input;
        private readonly PlayerReader playerReader;
        private readonly Wait wait;

        public TargetPetTargetGoal(ConfigurableInput input, PlayerReader playerReader, Wait wait)
            : base(nameof(TargetPetTargetGoal))
        {
            this.input = input;
            this.playerReader = playerReader;
            this.wait = wait;

            AddPrecondition(GoapKey.hastarget, false);

            if (input.ClassConfig.KeyboardOnly)
            {
                AddPrecondition(GoapKey.consumablecorpsenearby, false);
            }
            else
            {
                AddPrecondition(GoapKey.damagetakenordone, true);
            }

            AddPrecondition(GoapKey.pethastarget, true);

            AddEffect(GoapKey.hastarget, true);
        }

        public override void Update()
        {
            input.TargetPet();
            input.TargetOfTarget();
            wait.Update();

            if (playerReader.Bits.HasTarget() && (playerReader.Bits.TargetIsDead() || playerReader.TargetGuid == playerReader.PetGuid))
            {
                input.ClearTarget();
                wait.Update();
            }
        }
    }
}
