using Core.GOAP;
using System;
using System.Collections.Generic;
using Core.Session;
using Game;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Core;

public sealed class ConfigBotController : IBotController, IDisposable
{
    public IAddonReader AddonReader { get; }
    public GoapAgent? GoapAgent => throw new NotImplementedException();
    public RouteInfo? RouteInfo => throw new NotImplementedException();
    public WowScreen WowScreen => throw new NotImplementedException();
    public IGrindSessionDAO GrindSessionDAO => throw new NotImplementedException();
    public string SelectedClassFilename => throw new NotImplementedException();
    public string? SelectedPathFilename => throw new NotImplementedException();

    public ClassConfiguration? ClassConfig => null;

    public bool IsBotActive => false;

    public double AvgScreenLatency => throw new NotImplementedException();
    public double AvgNPCLatency => throw new NotImplementedException();

    AddonReader IBotController.AddonReader => throw new NotImplementedException();

    public event Action? ProfileLoaded;
    public event Action? StatusChanged;

    private readonly ILogger logger;
    private readonly CancellationTokenSource cts;

    private readonly Thread addonThread;

    public ConfigBotController(ILogger logger, IAddonReader addonReader, CancellationTokenSource cts)
    {
        this.logger = logger;
        this.cts = cts;
        this.AddonReader = addonReader;

        addonThread = new(AddonThread);
        addonThread.Start();
    }

    public void Dispose()
    {
        cts.Cancel();
    }

    private void AddonThread()
    {
        while (!cts.IsCancellationRequested)
        {
            AddonReader.Update();
        }
        logger.LogWarning("Addon thread stoppped!");
    }


    public void Shutdown()
    {
        cts.Cancel();
    }

    public void MinimapNodeFound()
    {
        throw new NotImplementedException();
    }

    public void ToggleBotStatus()
    {
        StatusChanged?.Invoke();
        throw new NotImplementedException();
    }

    public IEnumerable<string> ClassFiles()
    {
        throw new NotImplementedException();
    }

    public IEnumerable<string> PathFiles()
    {
        throw new NotImplementedException();
    }

    public void LoadClassProfile(string classFilename)
    {
        ProfileLoaded?.Invoke();
        throw new NotImplementedException();
    }

    public void LoadPathProfile(string pathFilename)
    {
        ProfileLoaded?.Invoke();
        throw new NotImplementedException();
    }

    public void OverrideClassConfig(ClassConfiguration classConfiguration)
    {
        throw new NotImplementedException();
    }
}