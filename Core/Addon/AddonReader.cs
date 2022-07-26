using Core.Database;
using Microsoft.Extensions.Logging;
using SharedLib;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Core
{
    public sealed class AddonReader : IAddonReader, IDisposable
    {
        private readonly ILogger logger;
        private readonly AddonDataProvider reader;
        private readonly AutoResetEvent autoResetEvent;

        public PlayerReader PlayerReader { get; }

        public CombatLog CombatLog { get; }

        public BagReader BagReader { get; }
        public EquipmentReader EquipmentReader { get; }

        public ActionBarCostReader ActionBarCostReader { get; }

        public ActionBarCooldownReader ActionBarCooldownReader { get; }

        public ActionBarBits CurrentAction { get; }
        public ActionBarBits UsableAction { get; }

        public GossipReader GossipReader { get; }

        public SpellBookReader SpellBookReader { get; }
        public TalentReader TalentReader { get; }

        public LevelTracker LevelTracker { get; }

        public event Action? AddonDataChanged;
        public event Action? ZoneChanged;
        public event Action? PlayerDeath;

        private readonly WorldMapAreaDB WorldMapAreaDb;

        public ItemDB ItemDb { get; }
        public CreatureDB CreatureDb { get; }
        public AreaDB AreaDb { get; }

        public RecordInt UIMapId { get; } = new(4);

        public RecordInt GlobalTime { get; } = new(98);

        public int DamageTakenCount => CombatLog.DamageTaken.Count;
        public int DamageDoneCount => CombatLog.DamageDone.Count;

        private int lastTargetId = -1;
        public string TargetName { get; private set; } = string.Empty;

        public double AvgUpdateLatency { private set; get; }
        private readonly double[] UpdateLatencys = new double[16];
        private int LatencyIndex;
        private DateTime lastUpdate;

        public AddonReader(ILogger logger, AddonDataProvider addonDataProvider, AutoResetEvent autoResetEvent,
            AreaDB areaDB, WorldMapAreaDB worldMapAreaDB, ItemDB itemDB,
            CreatureDB creatureDB, SpellDB spellDB, TalentDB talentDB)
        {
            this.logger = logger;
            this.reader = addonDataProvider;
            this.autoResetEvent = autoResetEvent;

            this.AreaDb = areaDB;
            this.WorldMapAreaDb = worldMapAreaDB;
            this.ItemDb = itemDB;
            this.CreatureDb = creatureDB;

            this.CombatLog = new(64, 65, 66, 67);

            this.EquipmentReader = new(ItemDb, 23, 24);
            this.BagReader = new(ItemDb, EquipmentReader, 20, 21, 22);

            this.ActionBarCostReader = new(35, 36);
            this.ActionBarCooldownReader = new(37);

            this.GossipReader = new(73);

            this.SpellBookReader = new(71, spellDB);

            this.PlayerReader = new(addonDataProvider);
            this.LevelTracker = new(this);
            this.TalentReader = new(72, PlayerReader, talentDB);

            this.CurrentAction = new(PlayerReader, 25, 26, 27, 28, 29);
            this.UsableAction = new(PlayerReader, 30, 31, 32, 33, 34);
        }

        public void Dispose()
        {
            BagReader.Dispose();
            LevelTracker.Dispose();
        }

        public void Update()
        {
            FetchData();

            if (GlobalTime.UpdatedNoEvent(reader))
            {
                if (GlobalTime.Value <= 3)
                {
                    FullReset();
                    return;
                }

                UpdateLatencys[LatencyIndex++] = (DateTime.UtcNow - lastUpdate).TotalMilliseconds;
                lastUpdate = DateTime.UtcNow;
                if (LatencyIndex >= UpdateLatencys.Length)
                    LatencyIndex = 0;

                AvgUpdateLatency = 0;
                for (int i = 0; i < UpdateLatencys.Length; i++)
                {
                    AvgUpdateLatency += UpdateLatencys[i];
                }
                AvgUpdateLatency /= UpdateLatencys.Length;

                AddonDataProvider reader = this.reader;

                CurrentAction.Update(reader);
                UsableAction.Update(reader);

                PlayerReader.Update();

                if (lastTargetId != PlayerReader.TargetId)
                {
                    lastTargetId = PlayerReader.TargetId;

                    TargetName = CreatureDb.Entries.TryGetValue(PlayerReader.TargetId, out Creature creature)
                    ? creature.Name : reader.GetString(16) + reader.GetString(17);
                }

                CombatLog.Update(reader, PlayerReader.Bits.PlayerInCombat());

                BagReader.Read(reader);
                EquipmentReader.Read(reader);

                ActionBarCostReader.Read(reader);
                ActionBarCooldownReader.Read(reader);

                GossipReader.Read(reader);

                SpellBookReader.Read(reader);
                TalentReader.Read(reader);

                if (UIMapId.Updated(reader))
                {
                    AreaDb.Update(WorldMapAreaDb.GetAreaId(UIMapId.Value));
                    ZoneChanged?.Invoke();
                }

                autoResetEvent.Set();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FetchData()
        {
            reader.Update();
        }

        public void SessionReset()
        {
            LevelTracker.Reset();
            CombatLog.Reset();
        }

        public void FullReset()
        {
            PlayerReader.Reset();

            UIMapId.Reset();

            ActionBarCostReader.Reset();
            ActionBarCooldownReader.Reset();
            SpellBookReader.Reset();
            TalentReader.Reset();

            SessionReset();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetInt(int index)
        {
            return reader.GetInt(index);
        }

        public void PlayerDied()
        {
            PlayerDeath?.Invoke();
        }

        public void UpdateUI()
        {
            AddonDataChanged?.Invoke();
        }
    }
}