using System.Collections.Specialized;

namespace Core
{
    public class TargetDebuffStatus
    {
        private readonly int cell;

        private BitVector32 v;

        public TargetDebuffStatus(int cell)
        {
            this.cell = cell;
        }

        public void Update(AddonDataProvider reader)
        {
            v = new(reader.GetInt(cell));
        }

        public override string ToString()
        {
            return string.Empty;
        }

        // Priest
        public bool ShadowWordPain() => v[Mask._0];

        // Druid
        public bool Roar() => v[Mask._0];
        public bool FaerieFire() => v[Mask._1];
        public bool Rip() => v[Mask._2];
        public bool Moonfire() => v[Mask._3];
        public bool EntanglingRoots() => v[Mask._4];
        public bool Rake() => v[Mask._5];

        // Paladin
        public bool JudgementoftheCrusader() => v[Mask._0];
        public bool HammerOfJustice() => v[Mask._1];
        public bool JudgementofWisdom() => v[Mask._2];

        // Mage
        public bool Frostbite() => v[Mask._0];
        public bool Slow() => v[Mask._1];

        // Rogue

        // Warrior
        public bool Rend() => v[Mask._0];
        public bool ThunderClap() => v[Mask._1];
        public bool Hamstring() => v[Mask._2];
        public bool ChargeStun() => v[Mask._3];

        // Warlock
        public bool Curseof() => v[Mask._0];
        public bool Corruption() => v[Mask._1];
        public bool Immolate() => v[Mask._2];
        public bool SiphonLife() => v[Mask._3];

        // Hunter
        public bool SerpentSting() => v[Mask._0];

        // Death Knight
        public bool BloodPlague() => v[Mask._0];
        public bool FrostFever() => v[Mask._1];
        public bool Strangulate() => v[Mask._2];
        public bool ChainsofIce() => v[Mask._3];
    }
}