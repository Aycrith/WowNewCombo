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
    private readonly NpcNameFinder npcNameFinder;
    private readonly AddonReader addonReader;
    private readonly AddonBits bits;
    private readonly PlayerReader playerReader;
    private readonly WowScreen wowScreen;
    private readonly ActionBarCostReader costReader;
    private readonly AuraTimeReader<IPlayerBuffTimeReader> playerBuff;
    private readonly AuraTimeReader<ITargetDebuffTimeReader> targetDebuff;
    private readonly AuraTimeReader<ITargetBuffTimeReader> targetBuff;

    public bool IsBotActive => GoapAgent != null && GoapAgent.Active;

    private readonly Thread addonThread;

    private readonly Thread screenshotThread;
    private const int screenshotTickMs = 200;

    private readonly Thread? remotePathing;
    private const int remotePathingTickMs = 500;

    public string SelectedClassFilename { get; private set; } = string.Empty;
    public string? SelectedPathFilename { get; private set; }
    public ClassConfiguration? ClassConfig { get; private set; }
    public GoapAgent? GoapAgent { get; private set; }
    public RouteInfo? RouteInfo { get; private set; }

    private IServiceScope? sessionScope;

    public event Action? ProfileLoaded;
    public event Action? StatusChanged;

    private const int SIZE = 32;
    private readonly double[] ScreenLatencys = new double[SIZE];
    private readonly double[] NPCLatencys = new double[SIZE];

    public double AvgScreenLatency => ScreenLatencys.Average();
    public double AvgNPCLatency => NPCLatencys.Average();

    public BotController(
        ILogger<BotController> logger, ILogger globalLogger,
        CancellationTokenSource cts,
        IPPather pather, DataConfig dataConfig,
        WowScreen wowScreen,
        NpcNameFinder npcNameFinder,
        PlayerReader playerReader, AddonReader addonReader,
        AddonBits bits, Wait wait,
        ActionBarCostReader costReader,
        MinimapNodeFinder minimapNodeFinder,
        IScreenCapture screenCapture,
        IServiceProvider serviceProvider,
        AuraTimeReader<IPlayerBuffTimeReader> playerBuff,
        AuraTimeReader<ITargetDebuffTimeReader> targetDebuff,
        AuraTimeReader<ITargetBuffTimeReader> targetBuff)
    {
        this.serviceProvider = serviceProvider;

        this.globalLogger = globalLogger;
        this.logger = logger;
        this.pather = pather;
        this.dataConfig = dataConfig;

        this.costReader = costReader;

        this.playerBuff = playerBuff;
        this.targetDebuff = targetDebuff;
        this.targetBuff = targetBuff;

        this.wowScreen = wowScreen;

        this.addonReader = addonReader;
        this.playerReader = playerReader;
        this.bits = bits;

        this.minimapNodeFinder = minimapNodeFinder;

        this.cts = cts;
        this.npcNameFinder = npcNameFinder;

        addonThread = new(AddonThread);
        addonThread.Start();

        do
        {
            if (!wait.Update(5000))
                logger.LogError("There is a problem, unable " +
                    "to read the players UnitClass and UnitRace!");
        } while (
            !Enum.IsDefined<UnitClass>(playerReader.Class) ||
            playerReader.Class == UnitClass.None);

        logger.LogInformation($"{playerReader.Race.ToStringF()} " +
            $"{playerReader.Class.ToStringF()}!");

        screenshotThread = new(ScreenshotThread);
        screenshotThread.Start();

        if (pather is RemotePathingAPI)
        {
            remotePathing = new(RemotePathingThread);
            remotePathing.Start();
        }
    }

    public ClassConfiguration ResolveLoadedProfile()
    {
        return ClassConfig!;
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
        long time;
        int tickCount = 0;

        while (!cts.IsCancellationRequested)
        {
            if (wowScreen.Enabled)
            {
                time = Stopwatch.GetTimestamp();
                wowScreen.Update();
                ScreenLatencys[tickCount % SIZE] =
                    Stopwatch.GetElapsedTime(time).TotalMilliseconds;

                time = Stopwatch.GetTimestamp();
                npcNameFinder.Update();
                NPCLatencys[tickCount % SIZE] =
                    Stopwatch.GetElapsedTime(time).TotalMilliseconds;

                if (wowScreen.EnablePostProcess)
                    wowScreen.PostProcess();
            }

            if (ClassConfig?.Mode == Mode.AttendedGather)
            {
                time = Stopwatch.GetTimestamp();
                wowScreen.UpdateMinimapBitmap();
                minimapNodeFinder.Update();
                ScreenLatencys[tickCount % SIZE] =
                    Stopwatch.GetElapsedTime(time).TotalMilliseconds;
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
        bool newLoaded = false;
        ProfileLoaded += () =>
        {
            newLoaded = true;
        };

        Vector3 oldPos = Vector3.Zero;

        while (!cts.IsCancellationRequested)
        {
            cts.Token.WaitHandle.WaitOne(remotePathingTickMs);

            if (sessionScope == null)
                continue;

            if (newLoaded)
            {
                Vector3[] mapRoute = sessionScope
                    .ServiceProvider.GetRequiredService<Vector3[]>();

                pather.DrawLines(new()
                {
                    new LineArgs("grindpath",
                        mapRoute, 2, playerReader.UIMapId.Value)
                }).AsTask().Wait(cts.Token);

                oldPos = Vector3.Zero;
                newLoaded = false;
            }

            if (playerReader.MapPos != oldPos)
            {
                oldPos = playerReader.MapPos;

                pather.DrawSphere(
                    new SphereArgs("Player", playerReader.MapPos,
                    bits.PlayerInCombat() ? 1 : bits.HasTarget() ? 6 : 2,
                    playerReader.UIMapId.Value))
                    .AsTask().Wait(cts.Token);
            }
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug($"{nameof(RemotePathingThread)} stopped!");
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
        long startTime = Stopwatch.GetTimestamp();
        try
        {
            ClassConfig = ReadClassConfiguration(classFile);

            RequirementFactory requirementFactory = new(serviceProvider, ClassConfig);

            ClassConfig.Initialise(dataConfig, addonReader, playerReader,
                addonReader.GlobalTime, costReader,
                requirementFactory, globalLogger,
                playerBuff, targetDebuff, targetBuff,
                pathFile);

            LogProfileLoaded(logger, classFile, ClassConfig.PathFilename);

            Initialize(ClassConfig);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            return false;
        }

        LogProfileLoadedTime(logger,
            Stopwatch.GetElapsedTime(startTime).TotalMilliseconds);

        return true;
    }

    private void Initialize(ClassConfiguration config)
    {
        IServiceCollection sessionServices = new ServiceCollection();

        sessionServices.AddScoped<ClassConfiguration>(x =>
            serviceProvider.GetRequiredService<IBotController>()
            .ResolveLoadedProfile());

        GoalFactory.Create(sessionServices, serviceProvider, config);

        sessionServices.AddScoped<IEnumerable<IRouteProvider>>(GetPathProviders);
        sessionServices.AddScoped<RouteInfo>();
        sessionServices.AddScoped<GoapAgent>();

        ServiceProvider provider = sessionServices.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        sessionScope?.Dispose();
        sessionScope = provider.CreateScope();

        GoapAgent = sessionScope.
            ServiceProvider.GetService<GoapAgent>();

        RouteInfo = sessionScope.
            ServiceProvider.GetService<RouteInfo>();
    }

    private static IEnumerable<IRouteProvider> GetPathProviders(IServiceProvider sp)
    {
        return sp.GetServices<GoapGoal>()
            .OfType<IRouteProvider>();
    }

    private ClassConfiguration ReadClassConfiguration(string classFile)
    {
        string filePath = Path.Join(dataConfig.Class, classFile);
        return DeserializeObject<ClassConfiguration>(File.ReadAllText(filePath))!;
    }

    public void Dispose()
    {
        cts.Cancel();

        sessionScope?.Dispose();
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