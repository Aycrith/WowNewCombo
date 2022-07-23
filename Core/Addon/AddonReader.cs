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
        private readonly AddonDataProvider addonDataProvider;
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

        public int CombatCreatureCount => CombatLog.DamageTaken.Count;

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
            this.addonDataProvider = addonDataProvider;
            this.autoResetEvent = autoResetEvent;

            this.AreaDb = areaDB;
            this.WorldMapAreaDb = worldMapAreaDB;
            this.ItemDb = itemDB;
            this.CreatureDb = creatureDB;

            this.CombatLog = new(addonDataProvider, 64, 65, 66, 67);

            this.EquipmentReader = new(addonDataProvider, ItemDb, 24, 25);
            this.BagReader = new(addonDataProvider, ItemDb, EquipmentReader, 20, 21, 22, 23);

            this.ActionBarCostReader = new(addonDataProvider, 36);
            this.ActionBarCooldownReader = new(addonDataProvider, 37);

            this.GossipReader = new(addonDataProvider, 73);

            this.SpellBookReader = new(addonDataProvider, 71, spellDB);

            this.PlayerReader = new(addonDataProvider);
            this.LevelTracker = new(this);
            this.TalentReader = new(addonDataProvider, 72, PlayerReader, talentDB);

            this.CurrentAction = new(PlayerReader, addonDataProvider, 26, 27, 28, 29, 30);
            this.UsableAction = new(PlayerReader, addonDataProvider, 31, 32, 33, 34, 35);
        }

        public void Dispose()
        {
            BagReader.Dispose();
            LevelTracker.Dispose();
        }

        public void Update()
        {
            FetchData();

            if (GlobalTime.Updated(addonDataProvider))
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

                CurrentAction.Update();
                UsableAction.Update();

                PlayerReader.Update();

                TargetName = CreatureDb.Entries.TryGetValue(PlayerReader.TargetId, out Creature creature)
                    ? creature.Name : addonDataProvider.GetString(16) + addonDataProvider.GetString(17);

                CombatLog.Update(PlayerReader.Bits.PlayerInCombat());

                BagReader.Read();
                EquipmentReader.Read();

                ActionBarCostReader.Read();
                ActionBarCooldownReader.Read();

                GossipReader.Read();

                SpellBookReader.Read();
                TalentReader.Read();

                if (UIMapId.Updated(addonDataProvider))
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
            addonDataProvider.Update();
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
            return addonDataProvider.GetInt(index);
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