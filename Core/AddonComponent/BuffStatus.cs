using System.Collections.Specialized;

namespace Core
{
    public class BuffStatus
    {
        private readonly int cell;

        private BitVector32 v;

        public BuffStatus(int cell)
        {
            this.cell = cell;
        }

        public void Update(IAddonDataProvider reader)
        {
            v = new(reader.GetInt(cell));
        }

        // All
        public bool Food() => v[Mask._0];

        public bool Drink() => v[Mask._1];

        public bool Well_Fed() => v[Mask._2];

        public bool Mana_Regeneration() => v[Mask._3];

        public bool Clearcasting() => v[Mask._4];

        // Priest
        public bool Fortitude() => v[Mask._10];
        public bool InnerFire() => v[Mask._11];
        public bool Renew() => v[Mask._12];
        public bool Shield() => v[Mask._13];
        public bool DivineSpirit() => v[Mask._14];

        // Druid
        public bool MarkOfTheWild() => v[Mask._10];
        public bool Thorns() => v[Mask._11];
        public bool TigersFury() => v[Mask._12];
        public bool Prowl() => v[Mask._13];
        public bool Rejuvenation() => v[Mask._14];
        public bool Regrowth() => v[Mask._15];
        public bool OmenOfClarity() => v[Mask._16];

        // Paladin
        public bool SealofRighteousness() => v[Mask._5];
        public bool SealoftheCrusader() => v[Mask._6];
        public bool SealofCommand() => v[Mask._7];
        public bool SealofWisdom() => v[Mask._8];
        public bool SealofLight() => v[Mask._9];
        public bool SealofBlood() => v[Mask._10];
        public bool SealofVengeance() => v[Mask._11];

        public bool BlessingofMight() => v[Mask._12];
        public bool BlessingofProtection() => v[Mask._13];
        public bool BlessingofWisdom() => v[Mask._14];
        public bool BlessingofKings() => v[Mask._15];
        public bool BlessingofSalvation() => v[Mask._16];
        public bool BlessingofSanctuary() => v[Mask._17];
        public bool BlessingofLight() => v[Mask._18];

        public bool RighteousFury() => v[Mask._19];
        public bool DivineProtection() => v[Mask._20];
        public bool AvengingWrath() => v[Mask._21];
        public bool HolyShield() => v[Mask._22];

        // Mage
        public bool FrostArmor() => v[Mask._10];
        public bool ArcaneIntellect() => v[Mask._11];
        public bool IceBarrier() => v[Mask._12];
        public bool Ward() => v[Mask._13];
        public bool FirePower() => v[Mask._14];
        public bool ManaShield() => v[Mask._15];
        public bool PresenceOfMind() => v[Mask._16];
        public bool ArcanePower() => v[Mask._17];

        // Rogue
        public bool SliceAndDice() => v[Mask._10];
        public bool Stealth() => v[Mask._11];

        // Warrior
        public bool BattleShout() => v[Mask._10];
        public bool Bloodrage() => v[Mask._11];

        // Warlock
        public bool Demon() => v[Mask._10]; //Skin and Armor
        public bool SoulLink() => v[Mask._11];
        public bool SoulstoneResurrection() => v[Mask._12];
        public bool ShadowTrance() => v[Mask._13];
        public bool FelArmor() => v[Mask._14];
        public bool FelDomination() => v[Mask._15];
        public bool DemonicSacrifice() => v[Mask._16];

        // Shaman
        public bool LightningShield() => v[Mask._10];
        public bool WaterShield() => v[Mask._11];
        public bool ShamanisticFocus() => v[Mask._12];
        public bool Stoneskin() => v[Mask._13];

        // Hunter
        public bool AspectoftheCheetah() => v[Mask._10];
        public bool AspectofthePack() => v[Mask._11];
        public bool AspectoftheHawk() => v[Mask._12];
        public bool AspectoftheMonkey() => v[Mask._13];
        public bool AspectoftheViper() => v[Mask._14];
        public bool RapidFire() => v[Mask._15];
        public bool QuickShots() => v[Mask._16];

        // Death Knight
        public bool BloodTap() => v[Mask._10];
        public bool HornofWinter() => v[Mask._11];
        public bool IceboundFortitude() => v[Mask._12];
        public bool PathofFrost() => v[Mask._13];
        public bool AntiMagicShell() => v[Mask._14];
        public bool ArmyoftheDead() => v[Mask._15];
        public bool VampiricBlood() => v[Mask._16];
        public bool DancingRuneWeapon() => v[Mask._17];
        public bool UnbreakableArmor() => v[Mask._18];
        public bool BoneShield() => v[Mask._19];
        public bool SummonGargoyle() => v[Mask._20];
        public bool FreezingFog() => v[Mask._21];
    }
}