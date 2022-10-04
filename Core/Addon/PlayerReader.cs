using System.Collections.Specialized;
using System.Numerics;

using SharedLib;

namespace Core
{
    public sealed partial class PlayerReader : IMouseOverReader
    {
        private readonly IAddonDataProvider reader;
        private readonly WorldMapAreaDB worldMapAreaDB;

        public PlayerReader(IAddonDataProvider reader, WorldMapAreaDB mapAreaDB)
        {
            this.worldMapAreaDB = mapAreaDB;

            this.reader = reader;
            Bits = new(8, 9);
            SpellInRange = new(40);
            Buffs = new(41);
            TargetDebuffs = new(42);
            Stance = new(48);
            CustomTrigger1 = new(reader.GetInt(74));
        }

        public WorldMapArea WorldMapArea { get; private set; }

        public Vector3 MapPos => new(MapX, MapY, WorldPosZ);
        public Vector3 WorldPos => worldMapAreaDB.ToWorld_FlipXY(UIMapId.Value, MapPos);

        public float WorldPosZ { get; set; } // MapZ not exists. Alias for WorldLoc.Z

        public float MapX => reader.GetFixed(1) * 10;
        public float MapY => reader.GetFixed(2) * 10;

        public float Direction => reader.GetFixed(3);

        public RecordInt UIMapId { get; } = new(4);

        public int MapId { get; private set; }

        public RecordInt Level { get; } = new(5);

        public Vector3 CorpseMapPos => new(CorpseMapX, CorpseMapY, 0);
        public float CorpseMapX => reader.GetFixed(6) * 10;
        public float CorpseMapY => reader.GetFixed(7) * 10;

        public AddonBits Bits { get; }

        public int HealthMax() => reader.GetInt(10);
        public int HealthCurrent() => reader.GetInt(11);
        public int HealthPercent() => HealthCurrent() * 100 / HealthMax();

        public int PTMax() => reader.GetInt(12); // Maximum amount of Power Type (dynamic)
        public int PTCurrent() => reader.GetInt(13); // Current amount of Power Type (dynamic)
        public int PTPercentage() => PTCurrent() * 100 / PTMax(); // Power Type (dynamic) in terms of a percentage

        public int ManaMax() => reader.GetInt(14);
        public int ManaCurrent() => reader.GetInt(15);
        public int ManaPercentage() => (1 + ManaCurrent()) * 100 / (1 + ManaMax());

        public int MaxRune() => reader.GetInt(14);

        public int BloodRune() => reader.GetInt(15) / 100 % 10;
        public int FrostRune() => reader.GetInt(15) / 10 % 10;
        public int UnholyRune() => reader.GetInt(15) % 10;

        public int TargetMaxHealth() => reader.GetInt(18);
        public int TargetHealth() => reader.GetInt(19);
        public int TargetHealthPercentage() => (1 + TargetHealth()) * 100 / (1 + TargetMaxHealth());

        public int PetMaxHealth() => reader.GetInt(38);
        public int PetHealth() => reader.GetInt(39);
        public int PetHealthPercentage() => (1 + PetHealth()) * 100 / (1 + PetMaxHealth());


        public SpellInRange SpellInRange { get; }
        public bool WithInPullRange() => SpellInRange.WithinPullRange(this, Class);
        public bool WithInCombatRange() => SpellInRange.WithinCombatRange(this, Class);
        public bool OutOfCombatRange() => !SpellInRange.WithinCombatRange(this, Class);

        public BuffStatus Buffs { get; }
        public TargetDebuffStatus TargetDebuffs { get; }

        // TargetLevel * 100 + TargetClass
        public int TargetLevel => reader.GetInt(43) / 100;
        public UnitClassification TargetClassification => (UnitClassification)(reader.GetInt(43) % 100);

        public int Money => reader.GetInt(44) + (reader.GetInt(45) * 1000000);

        // RACE_ID * 10000 + CLASS_ID * 100 + ClientVersion
        public UnitRace Race => (UnitRace)(reader.GetInt(46) / 10000);
        public UnitClass Class => (UnitClass)(reader.GetInt(46) / 100 % 100);
        public ClientVersion Version => (ClientVersion)(reader.GetInt(46) % 10);

        // 47 empty

        public Stance Stance { get; }
        public Form Form => Stance.Get(Class, Bits.IsStealthed(), Version);

        public int MinRange() => reader.GetInt(49) % 1000;
        public int MaxRange() => reader.GetInt(49) / 1000 % 1000;

        public bool IsInMeleeRange() => MinRange() == 0 && MaxRange() != 0 && MaxRange() <= 5;
        public bool InCloseMeleeRange() => MinRange() == 0 && MaxRange() <= 2;

        public bool IsInDeadZone() => MinRange() >= 5 && SpellInRange.Target_Trade; // between 5-8 yard - hunter and warrior

        public RecordInt PlayerXp { get; } = new(50);

        public int PlayerMaxXp => reader.GetInt(51);
        public int PlayerXpPercentage => PlayerXp.Value * 100 / PlayerMaxXp;

        private UI_ERROR UIError => (UI_ERROR)reader.GetInt(52);
        public UI_ERROR LastUIError { get; set; }

        public int SpellBeingCast => reader.GetInt(53);
        public bool IsCasting() => SpellBeingCast != 0;

        // avgEquipDurability * 100 + target combo points
        public int ComboPoints() => reader.GetInt(54) % 100;
        public int AvgEquipDurability() => reader.GetInt(54) / 100; // 0-99

        public AuraCount AuraCount => new(reader, 55);

        public int TargetId => reader.GetInt(56);
        public int TargetGuid => reader.GetInt(57);

        public int SpellBeingCastByTarget => reader.GetInt(58);
        public bool IsTargetCasting() => SpellBeingCastByTarget != 0;

        // 10 * MouseOverTarget + TargetTarget
        public UnitsTarget MouseOverTarget => (UnitsTarget)(reader.GetInt(59) / 10 % 10);
        public UnitsTarget TargetTarget => (UnitsTarget)(reader.GetInt(59) % 10);
        public bool TargetsMe() => TargetTarget == UnitsTarget.Me;
        public bool TargetsPet() => TargetTarget == UnitsTarget.Pet;
        public bool TargetsNone() => TargetTarget == UnitsTarget.None;

        public RecordInt AutoShot { get; } = new(60);
        public RecordInt MainHandSwing { get; } = new(61);
        public RecordInt CastEvent { get; } = new(62);
        public UI_ERROR CastState => (UI_ERROR)CastEvent.Value;
        public RecordInt CastSpellId { get; } = new(63);

        public int PetGuid => reader.GetInt(68);
        public int PetTargetGuid => reader.GetInt(69);
        public bool PetHasTarget() => PetTargetGuid != 0;

        public int CastCount => reader.GetInt(70);

        public BitVector32 CustomTrigger1;

        // 10000 * off * 100 + main * 100
        public int MainHandSpeedMs() => reader.GetInt(75) % 10000 * 10;
        public int OffHandSpeed => reader.GetInt(75) / 10000 * 10;

        public int RemainCastMs => reader.GetInt(76);


        // MouseOverLevel * 100 + MouseOverClassification
        public int MouseOverLevel => reader.GetInt(85) / 100;
        public UnitClassification MouseOverClassification => (UnitClassification)(reader.GetInt(85) % 100);
        public int MouseOverId => reader.GetInt(86);
        public int MouseOverGuid => reader.GetInt(87);


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

        public void Update(IAddonDataProvider reader)
        {
            if (UIMapId.Updated(reader) && UIMapId.Value != 0)
            {
                if (worldMapAreaDB.TryGet(UIMapId.Value, out var wma))
                {
                    WorldMapArea = wma;
                    MapId = wma.MapID;
                }
            }

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
            UIMapId.Reset();

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