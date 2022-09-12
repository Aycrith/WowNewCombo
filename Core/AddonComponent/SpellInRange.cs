using System.Collections.Specialized;

namespace Core
{
    public sealed class SpellInRange
    {
        private readonly int cell;

        public bool this[int index] => b[index];

        private BitVector32 b;

        public SpellInRange(int cell)
        {
            this.cell = cell;
        }

        public void Update(IAddonDataProvider reader)
        {
            b = new(reader.GetInt(cell));
        }

        // Warrior
        public bool Warrior_Charge => b[Mask._0];
        public bool Warrior_Rend => b[Mask._1];
        public bool Warrior_ShootGun => b[Mask._2];
        public bool Warrior_Throw => b[Mask._3];

        // Rogue
        public bool Rogue_SinisterStrike => b[Mask._0];
        public bool Rogue_Throw => b[Mask._1];
        public bool Rogue_ShootGun => b[Mask._2];

        // Priest
        public bool Priest_ShadowWordPain => b[Mask._0];
        public bool Priest_Shoot => b[Mask._1];
        public bool Priest_MindFlay => b[Mask._2];
        public bool Priest_MindBlast => b[Mask._3];
        public bool Priest_Smite => b[Mask._4];

        // Druid
        public bool Druid_Wrath => b[Mask._0];
        public bool Druid_Bash => b[Mask._1];
        public bool Druid_Rip => b[Mask._2];
        public bool Druid_Maul => b[Mask._3];

        //Paladin
        public bool Paladin_Judgement => b[Mask._0];

        //Mage
        public bool Mage_Fireball => b[Mask._0];
        public bool Mage_Shoot => b[Mask._1];
        public bool Mage_Pyroblast => b[Mask._2];
        public bool Mage_Frostbolt => b[Mask._3];
        public bool Mage_Fireblast => b[Mask._4];

        //Hunter
        public bool Hunter_RaptorStrike => b[Mask._0];
        public bool Hunter_AutoShoot => b[Mask._1];
        public bool Hunter_SerpentSting => b[Mask._2];
        public bool Hunter_FeedPet => b[Mask._3];

        // Warlock
        public bool Warlock_ShadowBolt => b[Mask._0];
        public bool Warlock_Shoot => b[Mask._1];
        public bool Warlock_HealthFunnel => b[Mask._2];

        // Shaman
        public bool Shaman_LightningBolt => b[Mask._0];
        public bool Shaman_EarthShock => b[Mask._1];

        // Death Knight
        public bool DeathKnight_IcyTouch => b[Mask._0];
        public bool DeathKnight_DeathCoil => b[Mask._1];
        public bool DeathKnight_DeathGrip => b[Mask._2];
        public bool DeathKnight_DarkCommand => b[Mask._3];
        public bool DeathKnight_RaiseDead => b[Mask._4];


        // Unit based Non spell Ranges
        public bool FocusTarget_Inspect => b[Mask._12];
        public bool FocusTarget_Trade => b[Mask._13];
        public bool FocusTarget_Duel => b[Mask._14];

        public bool Focus_Inspect => b[Mask._15];
        public bool Focus_Trade => b[Mask._16];
        public bool Focus_Duel => b[Mask._17];

        public bool Pet_Inspect => b[Mask._18];
        public bool Pet_Trade => b[Mask._19];
        public bool Pet_Duel => b[Mask._20];

        public bool Target_Inspect => b[Mask._21];
        public bool Target_Trade => b[Mask._22];
        public bool Target_Duel => b[Mask._23];

        public bool WithinPullRange(PlayerReader playerReader, UnitClass @class) => @class switch
        {
            UnitClass.Warrior => (playerReader.Level.Value >= 4 && Warrior_Charge) || playerReader.IsInMeleeRange(),
            UnitClass.Rogue => Rogue_Throw,
            UnitClass.Priest => Priest_Smite,
            UnitClass.Druid => Druid_Wrath,
            UnitClass.Paladin => (playerReader.Level.Value >= 4 && Paladin_Judgement) || playerReader.IsInMeleeRange() ||
                                       (playerReader.Level.Value >= 20 && playerReader.MinRange() <= 20 && playerReader.MaxRange() <= 25),
            UnitClass.Mage => (playerReader.Level.Value >= 4 && Mage_Frostbolt) || Mage_Fireball,
            UnitClass.Hunter => (playerReader.Level.Value >= 4 && Hunter_SerpentSting) || Hunter_AutoShoot || playerReader.IsInMeleeRange(),
            UnitClass.Warlock => Warlock_ShadowBolt,
            UnitClass.Shaman => (playerReader.Level.Value >= 4 && Shaman_EarthShock) || Shaman_LightningBolt,
            UnitClass.DeathKnight => DeathKnight_DeathGrip,
            _ => true
        };

        public bool WithinCombatRange(PlayerReader playerReader, UnitClass @class) => @class switch
        {
            UnitClass.Warrior => (playerReader.Level.Value >= 4 && Warrior_Rend) || playerReader.IsInMeleeRange(),
            UnitClass.Rogue => Rogue_SinisterStrike,
            UnitClass.Priest => Priest_Smite,
            UnitClass.Druid => Druid_Wrath || playerReader.IsInMeleeRange(),
            UnitClass.Paladin => (playerReader.Level.Value >= 4 && Paladin_Judgement) || playerReader.IsInMeleeRange(),
            UnitClass.Mage => Mage_Frostbolt || Mage_Fireball,
            UnitClass.Hunter => (playerReader.Level.Value >= 4 && Hunter_SerpentSting) || Hunter_AutoShoot || playerReader.IsInMeleeRange(),
            UnitClass.Warlock => Warlock_ShadowBolt,
            UnitClass.Shaman => Shaman_LightningBolt,
            UnitClass.DeathKnight => DeathKnight_IcyTouch,
            _ => true
        };
    }
}