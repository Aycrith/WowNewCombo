using System;
using Core.Database;

namespace Core.Addon
{
    public class ConfigAddonReader : IAddonReader
    {
        public PlayerReader PlayerReader { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public BagReader BagReader { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public EquipmentReader EquipmentReader { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public LevelTracker LevelTracker { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ActionBarCostReader ActionBarCostReader { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public SpellBookReader SpellBookReader { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TalentReader TalentReader { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public WorldMapAreaDB WorldMapAreaDb { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public double AvgUpdateLatency => throw new NotImplementedException();

        public int CombatCreatureCount => throw new NotImplementedException();

        public string TargetName => throw new NotImplementedException();

        public RecordInt UIMapId => throw new NotImplementedException();

#pragma warning disable CS0067 // The event is never used
        public event EventHandler? AddonDataChanged;
        public event EventHandler? ZoneChanged;
        public event EventHandler? PlayerDeath;
#pragma warning restore CS0067

        public int GetIntAt(int index)
        {
            throw new NotImplementedException();
        }

        public void Refresh()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }
    }
}