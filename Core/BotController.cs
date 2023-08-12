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

namespace Core;

public sealed partial class BotController : IBotController, IDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<BotController> logger;
    private readonly IPPather pather;
    private readonly MinimapNodeFinder minimapNodeFinder;
    private readonly DataConfig dataConfig;
    private readonly CancellationTokenSource cts;
    private readonly NpcNameFinder npcNameFinder;
    private readonly INpcResetEvent npcResetEvent;
    private readonly AddonReader addonReader;
    private readonly AddonBits bits;
    private readonly PlayerReader playerReader;
    private readonly WowScreen wowScreen;

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

    public double AvgScreenLatency { get; private set; }
    public double AvgNPCLatency { get; private set; }

    public BotController(
        ILogger<BotController> logger,
        CancellationTokenSource cts,
        IPPather pather, DataConfig dataConfig,
        WowScreen wowScreen,
        NpcNameFinder npcNameFinder,
        INpcResetEvent npcResetEvent,
        PlayerReader playerReader, AddonReader addonReader,
        AddonBits bits, Wait wait,
        MinimapNodeFinder minimapNodeFinder,
        IScreenCapture screenCapture,
        IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;

        this.logger = logger;
        this.pather = pather;
        this.dataConfig = dataConfig;

        this.wowScreen = wowScreen;

        this.addonReader = addonReader;
        this.playerReader = playerReader;
        this.bits = bits;

        this.minimapNodeFinder = minimapNodeFinder;

        this.cts = cts;
        this.npcNameFinder = npcNameFinder;
        this.npcResetEvent = npcResetEvent;

        addonThread = new(AddonThread);
        addonThread.Priority = ThreadPriority.AboveNormal;
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
        logger.LogWarning("Addon thread stopped!");
    }

    private void ScreenshotThread()
    {
        long time;
        int tickCount = 0;

        const int SIZE = 8;
        const int MOD = SIZE - 1;
        Span<double> screen = stackalloc double[SIZE];
        Span<double> npc = stackalloc double[SIZE];

        WaitHandle[] waitHandles = new[] {
            cts.Token.WaitHandle,
            npcResetEvent.WaitHandle,
        };

        while (true)
        {
            if (wowScreen.Enabled)
            {
                time = Stopwatch.GetTimestamp();
                wowScreen.Update();
                screen[tickCount & MOD] =
                    Stopwatch.GetElapsedTime(time).TotalMilliseconds;

                time = Stopwatch.GetTimestamp();
                npcNameFinder.Update();
                npc[tickCount & MOD] =
                    Stopwatch.GetElapsedTime(time).TotalMilliseconds;

                if (wowScreen.EnablePostProcess)
                    wowScreen.PostProcess();

                AvgScreenLatency = Average(screen);
                AvgNPCLatency = Average(npc);
            }

            if (ClassConfig?.Mode == Mode.AttendedGather)
            {
                time = Stopwatch.GetTimestamp();
                wowScreen.UpdateMinimapBitmap();
                minimapNodeFinder.Update();
                screen[tickCount & MOD] =
                    Stopwatch.GetElapsedTime(time).TotalMilliseconds;

                AvgScreenLatency = Average(screen);
            }

            int waitResult =
                WaitHandle.WaitAny(waitHandles,
                Math.Max(
                    screenshotTickMs -
                    (int)screen[tickCount & MOD] -
                    (int)npc[tickCount & MOD],
                    20));

            tickCount++;

            if (waitResult == 0)
                break;
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Screenshot Thread stopped!");

        static double Average(Span<double> span)
        {
            double sum = 0;
            for (int i = 0; i < SIZE; i++)
            {
                sum += span[i];
            }
            return sum / SIZE;
        }
    }

    private void RemotePathingThread()
    {
        bool newLoaded = false;
        ProfileLoaded += OnProfileLoaded;
        void OnProfileLoaded() => newLoaded = true;

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
                    bits.Combat() ? 1 : bits.Target() ? 6 : 2,
                    playerReader.UIMapId.Value))
                    .AsTask().Wait(cts.Token);
            }
        }

        ProfileLoaded -= OnProfileLoaded;

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
            ClassConfig.Initialise(serviceProvider, pathFile);

            LogProfileLoaded(logger, classFile, ClassConfig.PathFilename);

            CreateSession(ClassConfig);
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

    private void CreateSession(ClassConfiguration config)
    {
        IServiceCollection s = new ServiceCollection();

        s.AddSingleton<IBotController>(this);
        s.AddScoped<ClassConfiguration>(GetConfig);
        static ClassConfiguration GetConfig(IServiceProvider sp) =>
            sp.GetRequiredService<IBotController>().ResolveLoadedProfile();

        GoalFactory.Create(s, serviceProvider, config);

        s.AddScoped<IEnumerable<IRouteProvider>>(GetPathProviders);
        s.AddScoped<RouteInfo>();
        s.AddScoped<GoapAgent>();

        ServiceProvider provider = s.BuildServiceProvider(
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
        CreateSession(this.ClassConfig);
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