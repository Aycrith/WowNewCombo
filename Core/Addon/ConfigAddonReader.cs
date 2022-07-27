using System;

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

#pragma warning disable CS0067 // The event is never used
        public event Action? AddonDataChanged;
        public event Action? ZoneChanged;
        public event Action? PlayerDeath;
#pragma warning restore CS0067

        public int GetInt(int index)
        {
            throw new NotImplementedException();
        }

        public void FetchData()
        {
            throw new NotImplementedException();
        }

        public void FullReset()
        {
            throw new NotImplementedException();
        }
    }
}