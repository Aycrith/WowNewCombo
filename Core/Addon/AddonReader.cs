using Core.Database;
using Microsoft.Extensions.Logging;
using System;
using Cyotek.Collections.Generic;
using System.Linq;

namespace Core
{
    public sealed class AddonReader : IAddonReader, IDisposable
    {
        private readonly ILogger logger;
        private readonly ISquareReader squareReader;
        private readonly IAddonDataProvider addonDataProvider;

        public bool Initialized { get; private set; }

        public PlayerReader PlayerReader { get; }

        public CreatureHistory CreatureHistory { get; }

        public BagReader BagReader { get; }
        public EquipmentReader EquipmentReader { get; }

        public ActionBarCostReader ActionBarCostReader { get; }

        public ActionBarCooldownReader ActionBarCooldownReader { get; }

        public ActionBarBits CurrentAction => new(PlayerReader, squareReader, 26, 27, 28, 29, 30);
        public ActionBarBits UsableAction => new(PlayerReader, squareReader, 31, 32, 33, 34, 35);

        public GossipReader GossipReader { get; }

        public SpellBookReader SpellBookReader { get; }
        public TalentReader TalentReader { get; }

        public LevelTracker LevelTracker { get; }

        public event EventHandler? AddonDataChanged;
        public event EventHandler? ZoneChanged;
        public event EventHandler? PlayerDeath;

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
                    : squareReader.GetStringAtCell(16) + squareReader.GetStringAtCell(17);
            }
        }

        public double AvgUpdateLatency { private set; get; } = 5;
        private readonly CircularBuffer<double> UpdateLatencys;

        private DateTime lastFrontendUpdate;
        private readonly int FrontendUpdateIntervalMs = 250;

        public AddonReader(ILogger logger, DataConfig dataConfig, IAddonDataProvider addonDataProvider)
        {
            this.logger = logger;
            this.addonDataProvider = addonDataProvider;

            this.squareReader = new SquareReader(this);

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

            UpdateLatencys = new CircularBuffer<double>(10);

            UIMapId.Changed -= OnUIMapIdChanged;
            UIMapId.Changed += OnUIMapIdChanged;

            GlobalTime.Changed -= GlobalTimeChanged;
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

        public void AddonRefresh()
        {
            Refresh();

            CreatureHistory.Update(PlayerReader.TargetGuid, PlayerReader.TargetHealthPercentage);

            BagReader.Read();
            EquipmentReader.Read();

            ActionBarCostReader.Read();
            ActionBarCooldownReader.Read();

            GossipReader.Read();

            SpellBookReader.Read();
            TalentReader.Read();

            if ((DateTime.UtcNow - lastFrontendUpdate).TotalMilliseconds >= FrontendUpdateIntervalMs)
            {
                AddonDataChanged?.Invoke(this, EventArgs.Empty);
                lastFrontendUpdate = DateTime.UtcNow;
            }
        }

        public void Refresh()
        {
            addonDataProvider.Update();

            if (GlobalTime.Updated(squareReader) && (GlobalTime.Value <= 3 || !Initialized))
            {
                Reset();
            }

            PlayerReader.Updated();

            UIMapId.Update(squareReader);
        }

        public void SoftReset()
        {
            LevelTracker.Reset();
            CreatureHistory.Reset();
        }

        public void Reset()
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

        public int GetIntAt(int index)
        {
            return addonDataProvider.GetInt(index);
        }

        public void PlayerDied()
        {
            PlayerDeath?.Invoke(this, EventArgs.Empty);
        }

        private void OnUIMapIdChanged(object? s, EventArgs e)
        {
            this.AreaDb.Update(WorldMapAreaDb.GetAreaId(UIMapId.Value));
            ZoneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void GlobalTimeChanged(object? s, EventArgs e)
        {
            UpdateLatencys.Put((DateTime.UtcNow - GlobalTime.LastChanged).TotalMilliseconds);
            AvgUpdateLatency = 0;
            for (int i = 0; i < UpdateLatencys.Size; i++)
            {
                AvgUpdateLatency += UpdateLatencys.PeekAt(i);
            }
            AvgUpdateLatency /= UpdateLatencys.Size;
        }
    }
}