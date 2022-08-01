using System;
using System.Collections.Specialized;
using System.Numerics;

namespace Core
{
    public partial class PlayerReader
    {
        private readonly IAddonDataProvider reader;

        public PlayerReader(IAddonDataProvider reader)
        {
            this.reader = reader;
            Bits = new(8, 9);
            SpellInRange = new(40);
            Buffs = new(41);
            TargetDebuffs = new(42);
            Stance = new(48);
            CustomTrigger1 = new(reader.GetInt(74));
        }

        public Vector3 PlayerLocation => new(XCoord, YCoord, ZCoord);

        public float XCoord => reader.GetFixed(1) * 10;
        public float YCoord => reader.GetFixed(2) * 10;
        public float ZCoord { get; set; }
        public float Direction => reader.GetFixed(3);

        public RecordInt Level { get; } = new(5);

        public Vector3 CorpseLocation => new(CorpseX, CorpseY, 0);
        public float CorpseX => reader.GetFixed(6) * 10;
        public float CorpseY => reader.GetFixed(7) * 10;

        public AddonBits Bits { get; }

        public int HealthMax() => reader.GetInt(10);
        public int HealthCurrent() => reader.GetInt(11);
        public int HealthPercent() => HealthMax() == 0 || HealthCurrent() == 1 ? 0 : HealthCurrent() * 100 / HealthMax();

        public int PTMax() => reader.GetInt(12); // Maximum amount of Power Type (dynamic)
        public int PTCurrent() => reader.GetInt(13); // Current amount of Power Type (dynamic)
        public int PTPercentage() => PTMax() == 0 ? 0 : PTCurrent() * 100 / PTMax(); // Power Type (dynamic) in terms of a percentage

        public int ManaMax() => reader.GetInt(14);
        public int ManaCurrent() => reader.GetInt(15);
        public int ManaPercentage() => ManaMax() == 0 ? 0 : ManaCurrent() * 100 / ManaMax();

        public int MaxRune() => reader.GetInt(14);

        public int BloodRune() => reader.GetInt(15) / 100 % 10;
        public int FrostRune() => reader.GetInt(15) / 10 % 10;
        public int UnholyRune() => reader.GetInt(15) % 10;

        public int TargetMaxHealth() => reader.GetInt(18);
        public int TargetHealth() => reader.GetInt(19);
        public int TargetHealthPercentage() => TargetMaxHealth() == 0 || TargetHealth() == 1 ? 0 : TargetHealth() * 100 / TargetMaxHealth();


        public int PetMaxHealth() => reader.GetInt(38);
        public int PetHealth() => reader.GetInt(39);
        public int PetHealthPercentage() => PetMaxHealth() == 0 || PetHealth() == 1 ? 0 : PetHealth() * 100 / PetMaxHealth();


        public SpellInRange SpellInRange { get; }
        public bool WithInPullRange() => SpellInRange.WithinPullRange(this, Class);
        public bool WithInCombatRange() => SpellInRange.WithinCombatRange(this, Class);
        public bool OutOfCombatRange() => !SpellInRange.WithinCombatRange(this, Class);

        public BuffStatus Buffs { get; }
        public TargetDebuffStatus TargetDebuffs { get; }

        public int TargetLevel => reader.GetInt(43);

        public int Gold => reader.GetInt(44) + (reader.GetInt(45) * 1000000);

        public UnitRace Race => (UnitRace)(reader.GetInt(46) / 100f);

        public UnitClass Class => (UnitClass)(reader.GetInt(46) - ((int)Race * 100f));

        // 47 empty

        public Stance Stance { get; }
        public Form Form => Stance.Get(Class, Bits.IsStealthed());

        public int MinRange() => (int)(reader.GetInt(49) / 100000f);
        public int MaxRange() => (int)((reader.GetInt(49) - (MinRange() * 100000f)) / 100f);

        public bool IsInMeleeRange() => MinRange() == 0 && MaxRange() != 0 && MaxRange() <= 5;
        public bool IsInDeadZone() => MinRange() >= 5 && Bits.TargetInTradeRange(); // between 5-8 yard - hunter and warrior

        public RecordInt PlayerXp { get; } = new(50);

        public int PlayerMaxXp => reader.GetInt(51);
        public int PlayerXpPercentage => PlayerXp.Value * 100 / (PlayerMaxXp == 0 ? 1 : PlayerMaxXp);

        private UI_ERROR UIError => (UI_ERROR)reader.GetInt(52);
        public UI_ERROR LastUIError { get; set; }

        public int SpellBeingCast => reader.GetInt(53);
        public bool IsCasting() => SpellBeingCast != 0;

        public int ComboPoints() => reader.GetInt(54);

        public AuraCount AuraCount => new(reader, 55);

        public int TargetId => reader.GetInt(56);
        public int TargetGuid => reader.GetInt(57);

        public int SpellBeingCastByTarget => reader.GetInt(58);
        public bool IsTargetCasting() => SpellBeingCastByTarget != 0;

        public TargetTargetEnum TargetTarget => (TargetTargetEnum)reader.GetInt(59);
        public bool TargetsMe() => TargetTarget == TargetTargetEnum.Me;
        public bool TargetsPet() => TargetTarget == TargetTargetEnum.Pet;
        public bool TargetsNone() => TargetTarget == TargetTargetEnum.None;

        public RecordInt AutoShot { get; } = new(60);
        public RecordInt MainHandSwing { get; } = new(61);
        public RecordInt CastEvent { get; } = new(62);
        public RecordInt CastSpellId { get; } = new(63);

        public int PetGuid => reader.GetInt(68);
        public int PetTargetGuid => reader.GetInt(69);
        public bool PetHasTarget => PetTargetGuid != 0;

        public int CastCount => reader.GetInt(70);

        public BitVector32 CustomTrigger1;

        public int MainHandSpeedMs() => (int)(reader.GetInt(75) / 10000f) * 10;

        public int OffHandSpeed => (int)(reader.GetInt(75) - (MainHandSpeedMs() * 1000f));  // supposed to be 10000f - but theres a 10x

        public int RemainCastMs => reader.GetInt(76);

        public int LastCastGCD { get; set; }
        public void ReadLastCastGCD()
        {
            LastCastGCD = reader.GetInt(94);
        }

        public RecordInt GCD { get; } = new(95);

        public RecordInt NetworkLatency { get; } = new(96);

        public RecordInt LootEvent { get; } = new(97);

        public int FocusGuid => reader.GetInt(77);
        public int FocusTargetGuid => reader.GetInt(78);

        // https://wowpedia.fandom.com/wiki/Mob_experience
        public bool TargetYieldXP() => Level.Value switch
        {
            int n when n < 5 => true,
            int n when n is >= 6 and <= 39 => TargetLevel > (Level.Value - MathF.Floor(Level.Value / 10f) - 5),
            int n when n is >= 40 and <= 59 => TargetLevel > (Level.Value - MathF.Floor(Level.Value / 5f) - 5),
            int n when n is >= 60 and <= 80 => TargetLevel > Level.Value - 9,
            _ => false
        };

        public void Update(IAddonDataProvider reader)
        {
            Bits.Update(reader);
            SpellInRange.Update(reader);
            Buffs.Update(reader);
            TargetDebuffs.Update(reader);
            Stance.Update(reader);
            CustomTrigger1 = new(reader.GetInt(74));

            PlayerXp.Update(reader);
            Level.Update(reader);

            AutoShot.Update(reader);
            MainHandSwing.Update(reader);
            CastEvent.Update(reader);
            CastSpellId.Update(reader);

            LootEvent.Update(reader);

            GCD.Update(reader);
            NetworkLatency.Update(reader);

            if (UIError != UI_ERROR.NONE)
                LastUIError = UIError;
        }

        public void Reset()
        {
            // Reset all RecordInt
            AutoShot.Reset();
            MainHandSwing.Reset();
            CastEvent.Reset();
            CastSpellId.Reset();

            PlayerXp.Reset();
            Level.Reset();

            LootEvent.Reset();

            GCD.Reset();
            NetworkLatency.Reset();
        }
    }
}