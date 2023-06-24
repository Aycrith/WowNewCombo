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
using Core.Session;
using Game;
using WinAPI;
using SharedLib.NpcFinder;
using PPather.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Numerics;

namespace Core;

public sealed partial class BotController : IBotController, IDisposable
{
    private readonly WowProcess wowProcess;
    private readonly WowProcessInput wowProcessInput;
    private readonly ILogger<BotController> logger;
    private readonly ILogger globalLogger;
    private readonly IPPather pather;
    private readonly MinimapNodeFinder minimapNodeFinder;
    private readonly Wait wait;
    private readonly ExecGameCommand execGameCommand;
    private readonly DataConfig dataConfig;

    private readonly CancellationTokenSource cts;
    private readonly ManualResetEventSlim npcNameFinderEvent;
    private readonly NpcNameFinder npcNameFinder;
    private readonly NpcNameTargeting npcNameTargeting;
    private readonly IScreenCapture screenCapture;

    private readonly Thread addonThread;

    private readonly Thread screenshotThread;
    private const int screenshotTickMs = 200;

    private readonly Thread? remotePathing;
    private const int remotePathingTickMs = 500;

    public bool IsBotActive => GoapAgent != null && GoapAgent.Active;
    public AddonReader AddonReader { get; }
    public WowScreen WowScreen { get; }
    public IGrindSessionDAO GrindSessionDAO { get; }

    public SessionStat SessionStat { get; }

    public string SelectedClassFilename { get; private set; } = string.Empty;
    public string? SelectedPathFilename { get; private set; }
    public ClassConfiguration? ClassConfig { get; private set; }
    public GoapAgent? GoapAgent { get; private set; }
    public RouteInfo? RouteInfo { get; private set; }

    public event Action? ProfileLoaded;
    public event Action? StatusChanged;

    private const int SIZE = 32;
    private readonly double[] ScreenLatencys = new double[SIZE];
    private readonly double[] NPCLatencys = new double[SIZE];

    public double AvgScreenLatency => ScreenLatencys.Average();
    public double AvgNPCLatency => NPCLatencys.Average();

    public BotController(ILogger<BotController> logger, ILogger globalLogger, CancellationTokenSource cts,
        IPPather pather, SessionStat sessionStat, IGrindSessionDAO grindSessionDAO, DataConfig dataConfig,
        WowProcess wowProcess, WowScreen wowScreen, WowProcessInput wowProcessInput,
        ExecGameCommand execGameCommand, Wait wait, IAddonReader addonReader,
        MinimapNodeFinder minimapNodeFinder, IScreenCapture screenCapture)
    {
        this.globalLogger = globalLogger;
        this.logger = logger;
        this.pather = pather;
        this.dataConfig = dataConfig;
        GrindSessionDAO = grindSessionDAO;
        this.SessionStat = sessionStat;
        this.wowProcess = wowProcess;
        this.WowScreen = wowScreen;
        this.wowProcessInput = wowProcessInput;
        this.execGameCommand = execGameCommand;
        this.AddonReader = (addonReader as AddonReader)!;
        this.wait = wait;
        this.minimapNodeFinder = minimapNodeFinder;
        this.screenCapture = screenCapture;

        this.cts = cts;
        npcNameFinderEvent = new(false);

        addonThread = new(AddonThread);
        addonThread.Start();

        long timestamp = Stopwatch.GetTimestamp();
        do
        {
            wait.Update();

            if (Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds > 5000)
            {
                logger.LogWarning("There is a problem with the addon, I have been unable to read the player class. Is it running ?");
                timestamp = Stopwatch.GetTimestamp();
            }
        } while (!Enum.GetValues(typeof(UnitClass)).Cast<UnitClass>().Contains(AddonReader.PlayerReader.Class));

        logger.LogDebug($"Woohoo, I have read the player class. You are a {AddonReader.PlayerReader.Race.ToStringF()} {AddonReader.PlayerReader.Class.ToStringF()}.");

        npcNameFinder = new(globalLogger, WowScreen, npcNameFinderEvent);
        npcNameTargeting = new(globalLogger, cts, WowScreen, npcNameFinder, wowProcessInput, addonReader.PlayerReader, new NoBlacklist(), wait);
        WowScreen.AddDrawAction(npcNameFinder.ShowNames);
        WowScreen.AddDrawAction(npcNameTargeting.ShowClickPositions);

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
            AddonReader.Update();
        }
        logger.LogWarning("Addon thread stoppped!");
    }

    private void ScreenshotThread()
    {
        long timestamp;
        int tickCount = 0;

        while (!cts.IsCancellationRequested)
        {
            if (WowScreen.Enabled)
            {
                timestamp = Stopwatch.GetTimestamp();
                WowScreen.Update();
                ScreenLatencys[tickCount % SIZE] = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

                timestamp = Stopwatch.GetTimestamp();
                npcNameFinder.Update();
                NPCLatencys[tickCount % SIZE] = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

                if (WowScreen.EnablePostProcess)
                    WowScreen.PostProcess();
            }

            if (ClassConfig?.Mode == Mode.AttendedGather)
            {
                timestamp = Stopwatch.GetTimestamp();
                WowScreen.UpdateMinimapBitmap();
                minimapNodeFinder.Update();
                ScreenLatencys[tickCount % SIZE] = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
            }

            tickCount++;

            cts.Token.WaitHandle.WaitOne(
                WowScreen.Enabled || ClassConfig?.Mode == Mode.AttendedGather
                ? screenshotTickMs
                : 4);
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Screenshot thread stopped!");
    }

    private void RemotePathingThread()
    {
        while (!cts.IsCancellationRequested)
        {
            _ = pather.DrawSphere(
                new SphereArgs("Player",
                AddonReader.PlayerReader.MapPos,
                AddonReader.PlayerReader.Bits.PlayerInCombat() ? 1 : AddonReader.PlayerReader.Bits.HasTarget() ? 6 : 2,
                AddonReader.PlayerReader.UIMapId.Value
            ));

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

            RequirementFactory requirementFactory = new(globalLogger, AddonReader, SessionStat, npcNameFinder, ClassConfig.ImmunityBlacklist);
            ClassConfig.Initialise(dataConfig, AddonReader, requirementFactory, globalLogger, pathFile);

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
        AddonReader.SessionReset();
        SessionStat.Reset();

        IServiceScope profileLoadedScope =
            GoalFactory.CreateGoals(globalLogger, AddonReader, dataConfig, npcNameFinder,
                npcNameTargeting, pather, execGameCommand, wowProcessInput, config, cts, wait);

        npcNameTargeting.UpdateBlacklist(
            profileLoadedScope.ServiceProvider.GetService<MouseOverBlacklist>()
            ?? profileLoadedScope.ServiceProvider.GetService<IBlacklist>()!);

        IEnumerable<GoapGoal> availableActions = profileLoadedScope.ServiceProvider.GetServices<GoapGoal>();
        IEnumerable<IRouteProvider> pathProviders = availableActions.OfType<IRouteProvider>();

        Vector3[] mapRoute = profileLoadedScope.ServiceProvider.GetRequiredService<Vector3[]>();
        RouteInfo routeInfo = new(mapRoute, pathProviders, AddonReader.PlayerReader, AddonReader.AreaDb, AddonReader.WorldMapAreaDb);

        if (pather is RemotePathingAPI)
        {
            pather.DrawLines(new()
            {
                new LineArgs("grindpath", mapRoute, 2, AddonReader.PlayerReader.UIMapId.Value)
            }).AsTask().Wait();
        }

        RouteInfo?.Dispose();
        RouteInfo = routeInfo;

        GoapAgent?.Dispose();
        GoapAgent = new(profileLoadedScope, dataConfig, GrindSessionDAO, SessionStat, WowScreen, screenCapture, routeInfo);
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

        npcNameTargeting.Dispose();

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