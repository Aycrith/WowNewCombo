using System.Collections.Specialized;

namespace Core
{
    public class SpellInRange
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

        // Warlock
        public bool Warlock_ShadowBolt => b[Mask._0];
        public bool Warlock_Shoot => b[Mask._1];

        // Shaman
        public bool Shaman_LightningBolt => b[Mask._0];
        public bool Shaman_EarthShock => b[Mask._1];

        // Death Knight
        public bool DeathKnight_IcyTouch => b[Mask._0];
        public bool DeathKnight_DeathCoil => b[Mask._1];
        public bool DeathKnight_DeathGrip => b[Mask._2];
        public bool DeathKnight_DarkCommand => b[Mask._3];
        public bool DeathKnight_RaiseDead => b[Mask._4];

        public bool WithinPullRange(PlayerReader playerReader, PlayerClassEnum playerClass) => playerClass switch
        {
            PlayerClassEnum.Warrior => (playerReader.Level.Value >= 4 && Warrior_Charge) || playerReader.IsInMeleeRange(),
            PlayerClassEnum.Rogue => Rogue_Throw,
            PlayerClassEnum.Priest => Priest_Smite,
            PlayerClassEnum.Druid => Druid_Wrath,
            PlayerClassEnum.Paladin => (playerReader.Level.Value >= 4 && Paladin_Judgement) || playerReader.IsInMeleeRange() ||
                                       (playerReader.Level.Value >= 20 && playerReader.MinRange() <= 20 && playerReader.MaxRange() <= 25),
            PlayerClassEnum.Mage => (playerReader.Level.Value >= 4 && Mage_Frostbolt) || Mage_Fireball,
            PlayerClassEnum.Hunter => (playerReader.Level.Value >= 4 && Hunter_SerpentSting) || Hunter_AutoShoot || playerReader.IsInMeleeRange(),
            PlayerClassEnum.Warlock => Warlock_ShadowBolt,
            PlayerClassEnum.Shaman => (playerReader.Level.Value >= 4 && Shaman_EarthShock) || Shaman_LightningBolt,
            PlayerClassEnum.DeathKnight => DeathKnight_DeathGrip,
            _ => true
        };

        public bool WithinCombatRange(PlayerReader playerReader, PlayerClassEnum playerClass) => playerClass switch
        {
            PlayerClassEnum.Warrior => (playerReader.Level.Value >= 4 && Warrior_Rend) || playerReader.IsInMeleeRange(),
            PlayerClassEnum.Rogue => Rogue_SinisterStrike,
            PlayerClassEnum.Priest => Priest_Smite,
            PlayerClassEnum.Druid => Druid_Wrath || playerReader.IsInMeleeRange(),
            PlayerClassEnum.Paladin => (playerReader.Level.Value >= 4 && Paladin_Judgement) || playerReader.IsInMeleeRange(),
            PlayerClassEnum.Mage => Mage_Frostbolt || Mage_Fireball,
            PlayerClassEnum.Hunter => (playerReader.Level.Value >= 4 && Hunter_SerpentSting) || Hunter_AutoShoot || playerReader.IsInMeleeRange(),
            PlayerClassEnum.Warlock => Warlock_ShadowBolt,
            PlayerClassEnum.Shaman => Shaman_LightningBolt,
            PlayerClassEnum.DeathKnight => DeathKnight_IcyTouch,
            _ => true
        };
    }
}