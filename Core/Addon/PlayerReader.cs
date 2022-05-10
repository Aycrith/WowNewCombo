using System;
using System.Collections.Generic;
using System.Numerics;

namespace Core
{
    public partial class PlayerReader
    {
        private readonly SquareReader reader;
        public PlayerReader(SquareReader reader)
        {
            this.reader = reader;
            Bits = new(reader, 8, 9);
            SpellInRange = new(reader, 40);
            Buffs = new(reader, 41);
            TargetDebuffs = new(reader, 42);
            Stance = new(reader, 48);
            CustomTrigger1 = new(reader.GetInt(74));
        }

        public Dictionary<Form, int> FormCost { get; } = new();

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

        public int HealthMax => reader.GetInt(10);
        public int HealthCurrent => reader.GetInt(11);
        public int HealthPercent => HealthMax == 0 || HealthCurrent == 1 ? 0 : (HealthCurrent * 100) / HealthMax;

        public int PTMax => reader.GetInt(12); // Maximum amount of Power Type (dynamic)
        public int PTCurrent => reader.GetInt(13); // Current amount of Power Type (dynamic)
        public int PTPercentage => PTMax == 0 ? 0 : (PTCurrent * 100) / PTMax; // Power Type (dynamic) in terms of a percentage

        public int ManaMax => reader.GetInt(14);
        public int ManaCurrent => reader.GetInt(15);
        public int ManaPercentage => ManaMax == 0 ? 0 : (ManaCurrent * 100) / ManaMax;

        public bool HasTarget => Bits.HasTarget;// || TargetHealth > 0;

        public int TargetMaxHealth => reader.GetInt(18);
        public int TargetHealth => reader.GetInt(19);
        public int TargetHealthPercentage => TargetMaxHealth == 0 || TargetHealth == 1 ? 0 : (TargetHealth * 100) / TargetMaxHealth;


        public int PetMaxHealth => reader.GetInt(38);
        public int PetHealth => reader.GetInt(39);
        public int PetHealthPercentage => PetMaxHealth == 0 || PetHealth == 1 ? 0 : (PetHealth * 100) / PetMaxHealth;


        public SpellInRange SpellInRange { get; }
        public bool WithInPullRange => SpellInRange.WithinPullRange(this, Class);
        public bool WithInCombatRange => SpellInRange.WithinCombatRange(this, Class);

        public BuffStatus Buffs { get; }
        public TargetDebuffStatus TargetDebuffs { get; }

        public int TargetLevel => reader.GetInt(43);

        public int Gold => reader.GetInt(44) + (reader.GetInt(45) * 1000000);

        public RaceEnum Race => (RaceEnum)(reader.GetInt(46) / 100f);

        public PlayerClassEnum Class => (PlayerClassEnum)(reader.GetInt(46) - ((int)Race * 100f));

        // 47 empty

        public Stance Stance { get; }
        public Form Form => Stance.Get(this, Class);

        public int MinRange => (int)(reader.GetInt(49) / 100000f);
        public int MaxRange => (int)((reader.GetInt(49) - (MinRange * 100000f)) / 100f);

        public bool IsInMeleeRange => MinRange == 0 && MaxRange != 0 && MaxRange <= 5;
        public bool IsInDeadZone => MinRange >= 5 && Bits.IsInDeadZoneRange; // between 5-8 yard - hunter and warrior

        public RecordInt PlayerXp { get; } = new(50);

        public int PlayerMaxXp => reader.GetInt(51);
        public int PlayerXpPercentage => (PlayerXp.Value * 100) / (PlayerMaxXp == 0 ? 1 : PlayerMaxXp);

        private UI_ERROR UIErrorMessage => (UI_ERROR)reader.GetInt(52);
        public UI_ERROR LastUIErrorMessage { get; set; }

        public int SpellBeingCast => reader.GetInt(53);
        public bool IsCasting => SpellBeingCast != 0;

        public int ComboPoints => reader.GetInt(54);

        public AuraCount AuraCount => new(reader, 55);

        public int TargetId => reader.GetInt(56);
        public int TargetGuid => reader.GetInt(57);

        public int SpellBeingCastByTarget => reader.GetInt(58);
        public bool IsTargetCasting => SpellBeingCastByTarget != 0;

        public TargetTargetEnum TargetTarget => (TargetTargetEnum)reader.GetInt(59);

        public RecordInt AutoShot { get; } = new(60);
        public RecordInt MainHandSwing { get; } = new(61);
        public RecordInt CastEvent { get; } = new(62);
        public RecordInt CastSpellId { get; } = new(63);

        public int PetGuid => reader.GetInt(68);
        public int PetTargetGuid => reader.GetInt(69);
        public bool PetHasTarget => PetTargetGuid != 0;

        public int CastCount => reader.GetInt(70);

        public BitStatus CustomTrigger1 { get; }

        public int MainHandSpeedMs => (int)(reader.GetInt(75) / 10000f) * 10;

        public int OffHandSpeed => (int)(reader.GetInt(75) - (MainHandSpeedMs * 1000f));  // supposed to be 10000f - but theres a 10x

        public int LastLootTime => reader.GetInt(97);

        // https://wowpedia.fandom.com/wiki/Mob_experience
        public bool TargetYieldXP => Level.Value switch
        {
            int n when n < 5 => true,
            int n when n >= 6 && n <= 39 => TargetLevel > (Level.Value - MathF.Floor(Level.Value / 10f) - 5),
            int n when n >= 40 && n <= 59 => TargetLevel > (Level.Value - MathF.Floor(Level.Value / 5f) - 5),
            int n when n >= 60 && n <= 70 => TargetLevel > Level.Value - 9,
            _ => false
        };

        public void Update()
        {
            Bits.SetDirty();
            SpellInRange.SetDirty();
            Buffs.SetDirty();
            TargetDebuffs.SetDirty();
            Stance.SetDirty();
            CustomTrigger1.Update(reader.GetInt(74));

            if (UIErrorMessage != UI_ERROR.NONE)
            {
                LastUIErrorMessage = (UI_ERROR)UIErrorMessage;
            }

            PlayerXp.Update(reader);
            Level.Update(reader);

            AutoShot.Update(reader);
            MainHandSwing.Update(reader);
            CastEvent.Update(reader);
            CastSpellId.Update(reader);
        }

        public void Reset()
        {
            FormCost.Clear();

            // Reset all RecordInt
            AutoShot.Reset();
            MainHandSwing.Reset();
            CastEvent.Reset();
            CastSpellId.Reset();

            PlayerXp.Reset();
            Level.Reset();
        }
    }
}