using System;
using System.Threading;

namespace Core.Addon;

public sealed class ConfigAddonReader : IAddonReader
{
    public PlayerReader PlayerReader => throw new NotImplementedException();
    public BagReader BagReader => throw new NotImplementedException();
    public EquipmentReader EquipmentReader => throw new NotImplementedException();
    public ActionBarCostReader ActionBarCostReader => throw new NotImplementedException();
    public ActionBarCooldownReader ActionBarCooldownReader => throw new NotImplementedException();

    public AuraTimeReader PlayerBuffTimeReader => throw new NotImplementedException();
    public AuraTimeReader TargetDebuffTimeReader => throw new NotImplementedException();

    public SpellBookReader SpellBookReader => throw new NotImplementedException();
    public TalentReader TalentReader => throw new NotImplementedException();

    public CombatLog CombatLog => throw new NotImplementedException();

    public double AvgUpdateLatency => throw new NotImplementedException();

    public int DamageTakenCount() => throw new NotImplementedException();

    public string TargetName => throw new NotImplementedException();

    public event Action? AddonDataChanged;

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