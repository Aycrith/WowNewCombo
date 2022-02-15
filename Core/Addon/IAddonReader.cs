using Core.Database;
using System;

namespace Core
{
    public interface IAddonReader
    {
        bool Active { get; set; }

        PlayerReader PlayerReader { get; }

        BagReader BagReader { get; }

        EquipmentReader EquipmentReader { get; }

        ActionBarCostReader ActionBarCostReader { get; }

        SpellBookReader SpellBookReader { get; }

        TalentReader TalentReader { get; }

        LevelTracker LevelTracker { get; }

        WorldMapAreaDB WorldMapAreaDb { get; }

        double AvgUpdateLatency { get; }

        int CombatCreatureCount { get; }

        string TargetName { get; }

        RecordInt UIMapId { get; }

        event EventHandler? AddonDataChanged;
        event EventHandler? ZoneChanged;
        event EventHandler? PlayerDeath;

        void Refresh();
        void Reset();

        int GetIntAt(int index);
    }
}