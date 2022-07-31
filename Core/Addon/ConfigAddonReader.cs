using System;
using System.Threading;

namespace Core.Addon
{
    public class ConfigAddonReader : IAddonReader
    {
        public PlayerReader PlayerReader => throw new NotImplementedException();
        public BagReader BagReader => throw new NotImplementedException();
        public EquipmentReader EquipmentReader => throw new NotImplementedException();
        public LevelTracker LevelTracker => throw new NotImplementedException();
        public ActionBarCostReader ActionBarCostReader => throw new NotImplementedException();
        public ActionBarCooldownReader ActionBarCooldownReader => throw new NotImplementedException();

        public AuraTimeReader PlayerBuffTimeReader => throw new NotImplementedException();
        public AuraTimeReader TargetDebuffTimeReader => throw new NotImplementedException();

        public SpellBookReader SpellBookReader => throw new NotImplementedException();
        public TalentReader TalentReader => throw new NotImplementedException();

        public double AvgUpdateLatency => throw new NotImplementedException();

        public int DamageTakenCount => throw new NotImplementedException();

        public string TargetName => throw new NotImplementedException();

        public RecordInt UIMapId => throw new NotImplementedException();

        public event Action? AddonDataChanged;

#pragma warning disable CS0067 // The event is never used
        public event Action? ZoneChanged;
        public event Action? PlayerDeath;
#pragma warning restore CS0067

        private readonly IAddonDataProvider reader;
        private readonly AutoResetEvent autoResetEvent;

        public ConfigAddonReader(IAddonDataProvider reader, AutoResetEvent autoResetEvent)
        {
            this.reader = reader;
            this.autoResetEvent = autoResetEvent;
        }

        public int GetInt(int index)
        {
            return reader.GetInt(index);
        }

        public void FetchData()
        {
            reader.Update();
        }

        public void FullReset()
        {
            throw new NotImplementedException();
        }

        public void Update()
        {
            FetchData();
            autoResetEvent.Set();
        }

        public void UpdateUI()
        {
            AddonDataChanged?.Invoke();
        }

        public void SessionReset()
        {
            throw new NotImplementedException();
        }
    }
}