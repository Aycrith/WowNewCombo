using System;
using System.Threading;

namespace Core.Addon;

public sealed class ConfigAddonReader : IAddonReader
{
    private readonly IAddonDataProvider reader;
    private readonly AutoResetEvent autoResetEvent;

    public double AvgUpdateLatency => throw new NotImplementedException();
    public string TargetName => throw new NotImplementedException();

    public event Action? AddonDataChanged;

    public ConfigAddonReader(IAddonDataProvider reader, AutoResetEvent autoResetEvent)
    {
        this.reader = reader;
        this.autoResetEvent = autoResetEvent;
    }

    public void FullReset()
    {
        throw new NotImplementedException();
    }

    public void Update()
    {
        reader.UpdateData();
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