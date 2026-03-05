using Core.GOAP;

using Game;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Threading;

namespace Core;

public sealed class ConfigBotController : IBotController, IDisposable
{
    private readonly ILogger logger;
    private readonly CancellationTokenSource cts;

    private readonly Thread addonThread;
    private readonly IAddonReader addonReader;
    private readonly IWowScreen screen;

    public GoapAgent? GoapAgent => null;
    public RouteInfo? RouteInfo => null;
    public string SelectedClassFilename => string.Empty;
    public Dictionary<int, string> SelectedPathFilename => new();

    public ClassConfiguration? ClassConfig => null;

    public bool IsBotActive => false;
    public string? LastDeactivateReason => null;
    public DateTime? LastDeactivateUtc => null;

    public double AvgScreenLatency => 0;
    public double AvgNPCLatency => 0;

    public event Action? ProfileLoaded;
    public event Action? StatusChanged;

    public ConfigBotController(ILogger logger,
        IAddonReader addonReader,
        IWowScreen screen,
        CancellationTokenSource cts)
    {
        this.logger = logger;
        this.cts = cts;
        this.addonReader = addonReader;
        this.screen = screen;

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
            screen.Update();
            addonReader.Update();
            Thread.Sleep(20);
        }
        logger.LogWarning("Thread stopped!");
    }


    public void Shutdown()
    {
        cts.Cancel();
    }

    public void MinimapNodeFound()
    {
        // No-op in config mode
    }

    public void ToggleBotStatus(string? reason = null)
    {
        StatusChanged?.Invoke();
        logger.LogWarning("[ConfigBotController ] ToggleBotStatus called in configuration mode — ignored");
    }

    public void RecordDeactivateReason(string reason)
    {
        logger.LogWarning("[ConfigBotController ] RecordDeactivateReason called in configuration mode — ignored ({Reason})", reason);
    }

    public IEnumerable<string> ClassFiles()
    {
        return Array.Empty<string>();
    }

    public IEnumerable<string> PathFiles()
    {
        return Array.Empty<string>();
    }

    public void LoadClassProfile(string classFilename)
    {
        ProfileLoaded?.Invoke();
        logger.LogWarning("[ConfigBotController ] LoadClassProfile called in configuration mode — ignored");
    }

    public void LoadPathProfile(Dictionary<int, string> pathFilenames)
    {
        ProfileLoaded?.Invoke();
        logger.LogWarning("[ConfigBotController ] LoadPathProfile called in configuration mode — ignored");
    }

    public void OverrideClassConfig(ClassConfiguration classConfiguration)
    {
        logger.LogWarning("[ConfigBotController ] OverrideClassConfig called in configuration mode — ignored");
    }

    public ClassConfiguration ResolveLoadedProfile()
    {
        logger.LogWarning("[ConfigBotController ] ResolveLoadedProfile called in configuration mode — returning empty config");
        return new ClassConfiguration();
    }

    public void SaveClassConfig()
    {
        // No-op for config-only controller
    }
}
