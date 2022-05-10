using Core.Database;
using Microsoft.Extensions.Logging;
using System;
using Cyotek.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Core
{
    public sealed class AddonReader : IAddonReader, IDisposable
    {
        private readonly ILogger logger;
        private readonly SquareReader squareReader;
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

        public string TargetName
        {
            get
            {
                return CreatureDb.Entries.TryGetValue(PlayerReader.TargetId, out SharedLib.Creature creature)
                    ? creature.Name
                    : squareReader.GetString(16) + squareReader.GetString(17);
            }
        }

        public double AvgUpdateLatency { private set; get; }
        private readonly CircularBuffer<double> UpdateLatencys;

        public AddonReader(ILogger logger, DataConfig dataConfig, AddonDataProvider addonDataProvider, AutoResetEvent autoResetEvent)
        {
            this.logger = logger;
            this.addonDataProvider = addonDataProvider;
            this.squareReader = new SquareReader(this);
            this.autoResetEvent = autoResetEvent;

            this.AreaDb = new AreaDB(logger, dataConfig);
            this.WorldMapAreaDb = new WorldMapAreaDB(dataConfig);
            this.ItemDb = new ItemDB(dataConfig);
            this.CreatureDb = new CreatureDB(dataConfig);
            this.spellDb = new SpellDB(dataConfig);
            this.talentDB = new TalentDB(dataConfig, spellDb);

            this.CreatureHistory = new CreatureHistory(squareReader, 64, 65, 66, 67);

            this.EquipmentReader = new EquipmentReader(squareReader, 24, 25);
            this.BagReader = new BagReader(squareReader, ItemDb, EquipmentReader, 20, 21, 22, 23);

            this.ActionBarCostReader = new ActionBarCostReader(squareReader, 36);
            this.ActionBarCooldownReader = new ActionBarCooldownReader(squareReader, 37);

            this.GossipReader = new GossipReader(squareReader, 73);

            this.SpellBookReader = new SpellBookReader(squareReader, 71, spellDb);

            this.PlayerReader = new PlayerReader(squareReader);
            this.LevelTracker = new LevelTracker(this);
            this.TalentReader = new TalentReader(squareReader, 72, PlayerReader, talentDB);

            this.CurrentAction = new(PlayerReader, squareReader, 26, 27, 28, 29, 30);
            this.UsableAction = new(PlayerReader, squareReader, 31, 32, 33, 34, 35);

            UpdateLatencys = new(16);

            UIMapId.Changed += OnUIMapIdChanged;
            GlobalTime.Changed += GlobalTimeChanged;
        }

        public void Dispose()
        {
            BagReader.Dispose();
            LevelTracker.Dispose();

            UIMapId.Changed -= OnUIMapIdChanged;
            GlobalTime.Changed -= GlobalTimeChanged;

            addonDataProvider?.Dispose();
        }

        public void Update()
        {
            FetchData();

            if (GlobalTime.Updated(squareReader) && (GlobalTime.Value <= 3 || !Initialized))
            {
                FullReset();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FetchData()
        {
            addonDataProvider.Update();
        }

        public void SoftReset()
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

            SoftReset();

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
            UpdateLatencys.Put((DateTime.UtcNow - GlobalTime.LastChanged).TotalMilliseconds);
            AvgUpdateLatency = 0;
            for (int i = 0; i < UpdateLatencys.Size; i++)
            {
                AvgUpdateLatency += UpdateLatencys.PeekAt(i);
            }
            AvgUpdateLatency /= UpdateLatencys.Size;

            CurrentAction.SetDirty();
            UsableAction.SetDirty();

            PlayerReader.Update();

            UIMapId.Update(squareReader);

            CreatureHistory.Update(PlayerReader.TargetGuid, PlayerReader.TargetHealthPercentage);

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