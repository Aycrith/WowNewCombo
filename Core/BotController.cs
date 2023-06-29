using Core.Goals;
using Core.GOAP;
using Microsoft.Extensions.Logging;
using static Newtonsoft.Json.JsonConvert;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Game;
using WinAPI;
using SharedLib.NpcFinder;
using PPather.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Numerics;
using SharedLib;
using Core.Database;

namespace Core;

public sealed partial class BotController : IBotController, IDisposable
{
    private readonly IServiceProvider serviceProvider;

    private readonly ILogger<BotController> logger;
    private readonly ILogger globalLogger;
    private readonly IPPather pather;
    private readonly MinimapNodeFinder minimapNodeFinder;
    private readonly DataConfig dataConfig;

    private readonly CancellationTokenSource cts;
    private readonly INpcResetEvent npcNameFinderEvent;
    private readonly NpcNameFinder npcNameFinder;

    private readonly Thread addonThread;

    private readonly Thread screenshotThread;
    private const int screenshotTickMs = 200;

    private readonly Thread? remotePathing;
    private const int remotePathingTickMs = 500;

    public bool IsBotActive => GoapAgent != null && GoapAgent.Active;

    private readonly AddonReader addonReader;
    private readonly AddonBits bits;
    private readonly PlayerReader playerReader;
    private readonly WowScreen wowScreen;
    private readonly SessionStat sessionStat;
    private readonly ActionBarCostReader actionBarCostReader;

    private readonly WorldMapAreaDB worldMapAreaDb;
    private readonly AreaDB areaDb;

    public string SelectedClassFilename { get; private set; } = string.Empty;
    public string? SelectedPathFilename { get; private set; }
    public ClassConfiguration? ClassConfig { get; private set; }
    public GoapAgent? GoapAgent { get; private set; }
    public RouteInfo? RouteInfo { get; private set; }

    public event Action? ProfileLoaded;
    public event Action? StatusChanged;

    private const int SIZE = 32;
    private const int MOD = 5;
    private readonly double[] ScreenLatencys = new double[SIZE];
    private readonly double[] NPCLatencys = new double[SIZE];

    public double AvgScreenLatency => ScreenLatencys.Average();
    public double AvgNPCLatency => NPCLatencys.Average();

    public BotController(
        ILogger<BotController> logger, ILogger globalLogger,
        CancellationTokenSource cts,
        IPPather pather, SessionStat sessionStat, DataConfig dataConfig,
        WowScreen wowScreen,
        NpcNameFinder npcNameFinder, INpcResetEvent npcResetEvent,
        PlayerReader playerReader, AddonReader addonReader,
        AddonBits bits, Wait wait,
        WorldMapAreaDB worldMapAreaDb, AreaDB areaDb,
        ActionBarCostReader actionBarCostReader,
        MinimapNodeFinder minimapNodeFinder, IScreenCapture screenCapture,
        IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;

        this.globalLogger = globalLogger;
        this.logger = logger;
        this.pather = pather;
        this.dataConfig = dataConfig;
        this.sessionStat = sessionStat;
        this.worldMapAreaDb = worldMapAreaDb;
        this.areaDb = areaDb;

        this.actionBarCostReader = actionBarCostReader;

        this.wowScreen = wowScreen;

        this.addonReader = addonReader;
        this.playerReader = playerReader;
        this.bits = bits;

        this.minimapNodeFinder = minimapNodeFinder;

        this.cts = cts;
        npcNameFinderEvent = npcResetEvent;
        this.npcNameFinder = npcNameFinder;

        addonThread = new(AddonThread);
        addonThread.Start();

        do
        {
            if (!wait.Update(5000))
                logger.LogError("There is a problem, unable to read the players UnitClass and UnitRace!");
        } while (
            !Enum.IsDefined<UnitClass>(playerReader.Class) ||
            playerReader.Class == UnitClass.None);

        logger.LogInformation($"{playerReader.Race.ToStringF()} {playerReader.Class.ToStringF()}!");

        screenshotThread = new(ScreenshotThread);
        screenshotThread.Start();

        if (pather is RemotePathingAPI)
        {
            remotePathing = new(RemotePathingThread);
            remotePathing.Start();
        }
    }

    private void AddonThread()
    {
        while (!cts.IsCancellationRequested)
        {
            addonReader.Update();
        }
        logger.LogWarning("Addon thread stoppped!");
    }

    private void ScreenshotThread()
    {
        long timestamp;
        int tickCount = 0;

        while (!cts.IsCancellationRequested)
        {
            if (wowScreen.Enabled)
            {
                timestamp = Stopwatch.GetTimestamp();
                wowScreen.Update();
                ScreenLatencys[tickCount & MOD] = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

                timestamp = Stopwatch.GetTimestamp();
                npcNameFinder.Update();
                NPCLatencys[tickCount & MOD] = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

                if (wowScreen.EnablePostProcess)
                    wowScreen.PostProcess();
            }

            if (ClassConfig?.Mode == Mode.AttendedGather)
            {
                timestamp = Stopwatch.GetTimestamp();
                wowScreen.UpdateMinimapBitmap();
                minimapNodeFinder.Update();
                ScreenLatencys[tickCount & MOD] = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
            }

            tickCount++;

            cts.Token.WaitHandle.WaitOne(
                wowScreen.Enabled || ClassConfig?.Mode == Mode.AttendedGather
                ? screenshotTickMs
                : 4);
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Screenshot Thread stopped!");
    }

    private void RemotePathingThread()
    {
        while (!cts.IsCancellationRequested)
        {
            _ = pather.DrawSphere(
                new SphereArgs("Player",
                    playerReader.MapPos,
                    bits.PlayerInCombat() ? 1 : bits.HasTarget() ? 6 : 2,
                    playerReader.UIMapId.Value)
                );

            cts.Token.WaitHandle.WaitOne(remotePathingTickMs);
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("RemotePathing thread stopped!");
    }

    public void ToggleBotStatus()
    {
        if (GoapAgent == null)
            return;

        GoapAgent.Active = !GoapAgent.Active;

        StatusChanged?.Invoke();
    }

    private bool InitialiseFromFile(string classFile, string? pathFile)
    {
        long timestamp = Stopwatch.GetTimestamp();
        try
        {
            ClassConfig?.Dispose();
            ClassConfig = ReadClassConfiguration(classFile);

            RequirementFactory requirementFactory = new(serviceProvider, ClassConfig);

            ClassConfig.Initialise(dataConfig, addonReader, playerReader,
                addonReader.GlobalTime, actionBarCostReader, requirementFactory, globalLogger, pathFile);

            LogProfileLoaded(logger, classFile, ClassConfig.PathFilename);

        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
            return false;
        }

        Initialize(ClassConfig);

        LogProfileLoadedTime(logger, Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds);

        return true;
    }

    private void Initialize(ClassConfiguration config)
    {
        addonReader.SessionReset();
        sessionStat.Reset();

        IServiceScope profileLoadedScope =
            GoalFactory.CreateGoals(serviceProvider, config);

        IEnumerable<GoapGoal> availableActions = profileLoadedScope.ServiceProvider.GetServices<GoapGoal>();
        IEnumerable<IRouteProvider> pathProviders = availableActions.OfType<IRouteProvider>();

        Vector3[] mapRoute = profileLoadedScope.ServiceProvider.GetRequiredService<Vector3[]>();
        RouteInfo routeInfo = new(mapRoute, pathProviders, playerReader, areaDb, worldMapAreaDb);

        if (pather is RemotePathingAPI)
        {
            pather.DrawLines(new()
            {
                new LineArgs("grindpath", mapRoute, 2, playerReader.UIMapId.Value)
            }).AsTask().Wait();
        }

        RouteInfo?.Dispose();
        RouteInfo = routeInfo;

        GoapAgent?.Dispose();
        GoapAgent = new(profileLoadedScope, routeInfo);
    }

    private ClassConfiguration ReadClassConfiguration(string classFile)
    {
        string filePath = Path.Join(dataConfig.Class, classFile);
        return DeserializeObject<ClassConfiguration>(File.ReadAllText(filePath))!;
    }

    public void Dispose()
    {
        cts.Cancel();
        ClassConfig?.Dispose();
        RouteInfo?.Dispose();
        GoapAgent?.Dispose();

        npcNameFinderEvent.Dispose();
    }

    public void MinimapNodeFound()
    {
        GoapAgent?.NodeFound();
    }

    public void Shutdown()
    {
        cts.Cancel();
    }

    public IEnumerable<string> ClassFiles()
    {
        var root = Path.Join(dataConfig.Class, Path.DirectorySeparatorChar.ToString());
        var files = Directory.EnumerateFiles(root, "*.json*", SearchOption.AllDirectories)
            .Select(path => path.Replace(root, string.Empty))
            .OrderBy(x => x, new NaturalStringComparer())
            .ToList();

        files.Insert(0, "Press Init State first!");
        return files;
    }

    public IEnumerable<string> PathFiles()
    {
        var root = Path.Join(dataConfig.Path, Path.DirectorySeparatorChar.ToString());
        var files = Directory.EnumerateFiles(root, "*.json*", SearchOption.AllDirectories)
            .Select(path => path.Replace(root, string.Empty))
            .OrderBy(x => x, new NaturalStringComparer())
            .ToList();

        files.Insert(0, "Use Class Profile Default");
        return files;
    }

    public void LoadClassProfile(string classFilename)
    {
        if (InitialiseFromFile(classFilename, SelectedPathFilename))
        {
            SelectedClassFilename = classFilename;
        }

        ProfileLoaded?.Invoke();
    }

    public void LoadPathProfile(string pathFilename)
    {
        if (InitialiseFromFile(SelectedClassFilename, pathFilename))
        {
            SelectedPathFilename = pathFilename;
        }

        ProfileLoaded?.Invoke();
    }

    public void OverrideClassConfig(ClassConfiguration classConfig)
    {
        this.ClassConfig = classConfig;
        Initialize(this.ClassConfig);
    }

    #region logging

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Elapsed time: {time} ms")]
    static partial void LogProfileLoadedTime(ILogger logger, double time);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "ClassConfig: {profile} with Path: {path}")]
    static partial void LogProfileLoaded(ILogger logger, string profile, string path);

    #endregion
}