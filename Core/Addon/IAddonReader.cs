using Core.Database;

namespace Core
{
    public interface IAddonReader
    {
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

        event EmptyEvent? AddonDataChanged;
        event EmptyEvent? ZoneChanged;
        event EmptyEvent? PlayerDeath;

        void Refresh();
        void Reset();

        int GetIntAt(int index);
    }
}