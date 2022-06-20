using Core.Database;
using Microsoft.Extensions.Logging;
using SharedLib;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Core
{
    public sealed class AddonReader : IAddonReader, IDisposable
    {
        private readonly ILogger logger;
        private readonly AddonDataProvider addonDataProvider;
        private readonly AutoResetEvent autoResetEvent;

        public bool Initialized { get; private set; }

        public PlayerReader PlayerReader { get; }

        public CreatureHistory CreatureHistory { get; }

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

        public WorldMapAreaDB WorldMapAreaDb { get; }
        public ItemDB ItemDb { get; }
        public CreatureDB CreatureDb { get; }
        public AreaDB AreaDb { get; }

        private readonly SpellDB spellDb;
        private readonly TalentDB talentDB;

        public RecordInt UIMapId { get; } = new(4);

        public RecordInt GlobalTime { get; } = new(98);

        public int CombatCreatureCount => CreatureHistory.DamageTaken.Count(c => c.HealthPercent > 0);

        public string TargetName { get; private set; } = string.Empty;
        public string TargetNameUpper { get; private set; } = string.Empty;

        public double AvgUpdateLatency { private set; get; }
        private readonly double[] UpdateLatencys = new double[16];
        private int LatencyIndex;

        public AddonReader(ILogger logger, DataConfig dataConfig, AddonDataProvider addonDataProvider, AutoResetEvent autoResetEvent)
        {
            this.logger = logger;
            this.addonDataProvider = addonDataProvider;
            this.autoResetEvent = autoResetEvent;

            this.AreaDb = new AreaDB(logger, dataConfig);
            this.WorldMapAreaDb = new WorldMapAreaDB(dataConfig);
            this.ItemDb = new ItemDB(dataConfig);
            this.CreatureDb = new CreatureDB(dataConfig);
            this.spellDb = new SpellDB(dataConfig);
            this.talentDB = new TalentDB(dataConfig, spellDb);

            this.CreatureHistory = new CreatureHistory(addonDataProvider, 64, 65, 66, 67);

            this.EquipmentReader = new EquipmentReader(addonDataProvider, ItemDb, 24, 25);
            this.BagReader = new BagReader(addonDataProvider, ItemDb, EquipmentReader, 20, 21, 22, 23);

            this.ActionBarCostReader = new ActionBarCostReader(addonDataProvider, 36);
            this.ActionBarCooldownReader = new ActionBarCooldownReader(addonDataProvider, 37);

            this.GossipReader = new GossipReader(addonDataProvider, 73);

            this.SpellBookReader = new SpellBookReader(addonDataProvider, 71, spellDb);

            this.PlayerReader = new PlayerReader(addonDataProvider);
            this.LevelTracker = new LevelTracker(this);
            this.TalentReader = new TalentReader(addonDataProvider, 72, PlayerReader, talentDB);

            this.CurrentAction = new(PlayerReader, addonDataProvider, 26, 27, 28, 29, 30);
            this.UsableAction = new(PlayerReader, addonDataProvider, 31, 32, 33, 34, 35);

            for (int i = 0; i < UpdateLatencys.Length; i++)
            {
                UpdateLatencys[i] = 0;
            }

            UIMapId.Changed += OnUIMapIdChanged;
            GlobalTime.Changed += GlobalTimeChanged;
        }

        public void Dispose()
        {
            BagReader.Dispose();
            LevelTracker.Dispose();

            UIMapId.Changed -= OnUIMapIdChanged;
            GlobalTime.Changed -= GlobalTimeChanged;
        }

        public void Update()
        {
            FetchData();

            if (GlobalTime.Updated(addonDataProvider) && (GlobalTime.Value <= 3 || !Initialized))
            {
                FullReset();
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
            CreatureHistory.Reset();
        }

        public void FullReset()
        {
            Initialized = false;

            PlayerReader.Reset();

            UIMapId.Reset();

            ActionBarCostReader.Reset();
            ActionBarCooldownReader.Reset();
            SpellBookReader.Reset();
            TalentReader.Reset();

            SessionReset();

            Initialized = true;
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

        private void OnUIMapIdChanged()
        {
            this.AreaDb.Update(WorldMapAreaDb.GetAreaId(UIMapId.Value));
            ZoneChanged?.Invoke();
        }

        private void GlobalTimeChanged()
        {
            UpdateLatencys[LatencyIndex++] = (DateTime.UtcNow - GlobalTime.LastChanged).TotalMilliseconds;
            if (LatencyIndex >= UpdateLatencys.Length)
                LatencyIndex = 0;

            AvgUpdateLatency = 0;
            for (int i = 0; i < UpdateLatencys.Length; i++)
            {
                AvgUpdateLatency += UpdateLatencys[i];
            }
            AvgUpdateLatency /= UpdateLatencys.Length;

            CurrentAction.SetDirty();
            UsableAction.SetDirty();

            PlayerReader.Update();

            TargetName = CreatureDb.Entries.TryGetValue(PlayerReader.TargetId, out Creature creature)
                ? creature.Name : addonDataProvider.GetString(16) + addonDataProvider.GetString(17);
            TargetNameUpper = TargetName.ToUpper();

            UIMapId.Update(addonDataProvider);

            CreatureHistory.Update(PlayerReader.TargetGuid, PlayerReader.TargetHealthPercentage());

            BagReader.Read();
            EquipmentReader.Read();

            ActionBarCostReader.Read();
            ActionBarCooldownReader.Read();

            GossipReader.Read();

            SpellBookReader.Read();
            TalentReader.Read();

            autoResetEvent.Set();
        }

        public void UpdateUI()
        {
            AddonDataChanged?.Invoke();
        }
    }
}