using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

using Core.Addon;
using Core.Database;
using Core.Session;

using Game;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PPather;

using SharedLib;
using SharedLib.NpcFinder;

using WinAPI;

namespace Core;

public static class DependencyInjection
{
    public static IServiceCollection AddAddonComponents(
        this IServiceCollection s)
    {
        s.AddSingleton<CombatLog>();
        s.AddSingleton<EquipmentReader>();
        s.AddSingleton<BagReader>();
        s.AddSingleton<GossipReader>();
        s.AddSingleton<SpellBookReader>();
        s.AddSingleton<TalentReader>();

        s.AddSingleton<ActionBarCostReader>();
        s.AddSingleton<ActionBarCooldownReader>();

        s.AddSingleton<ActionBarBits<ICurrentAction>>(x => new(25, 26, 27, 28, 29));
        s.AddSingleton<ActionBarBits<IUsableAction>>(x => new(30, 31, 32, 33, 34));

        s.AddSingleton<AuraTimeReader<IPlayerBuffTimeReader>>(x => new(79, 80));
        s.AddSingleton<AuraTimeReader<ITargetDebuffTimeReader>>(x => new(81, 82));
        s.AddSingleton<AuraTimeReader<ITargetBuffTimeReader>>(x => new(83, 84));

        // Player Reader components
        s.AddSingleton<AddonBits>(x => new(8, 9));
        s.AddSingleton<SpellInRange>(x => new(40));
        s.AddSingleton<BuffStatus>(x => new(41));
        s.AddSingleton<TargetDebuffStatus>(x => new(42));
        s.AddSingleton<Stance>(x => new(48));

        return s;
    }

    public static IServiceCollection AddStartupIoC(
        this IServiceCollection s, IServiceProvider sp)
    {
        // StartUp Container
        s.AddSingleton(sp.GetRequiredService<CancellationTokenSource>());

        s.AddSingleton(sp.GetRequiredService<WowProcessInput>());
        s.AddSingleton(sp.GetRequiredService<IMouseInput>());
        s.AddSingleton(sp.GetRequiredService<IMouseOverReader>());

        s.AddSingleton(sp.GetRequiredService<NpcNameFinder>());
        s.AddSingleton(sp.GetRequiredService<IWowScreen>());
        s.AddSingleton(sp.GetRequiredService<WowScreen>());

        s.AddSingleton(sp.GetRequiredService<IPPather>());
        s.AddSingleton(sp.GetRequiredService<ExecGameCommand>());

        s.AddSingleton(sp.GetRequiredService<Wait>());

        s.AddSingleton(sp.GetRequiredService<DataConfig>());

        s.AddSingleton(sp.GetRequiredService<AreaDB>());
        s.AddSingleton(sp.GetRequiredService<WorldMapAreaDB>());
        s.AddSingleton(sp.GetRequiredService<ItemDB>());
        s.AddSingleton(sp.GetRequiredService<CreatureDB>());
        s.AddSingleton(sp.GetRequiredService<SpellDB>());
        s.AddSingleton(sp.GetRequiredService<TalentDB>());

        s.AddSingleton(sp.GetRequiredService<AddonReader>());
        s.AddSingleton(sp.GetRequiredService<PlayerReader>());

        s.AddSingleton<ConfigurableInput>();

        s.AddSingleton<IScreenCapture>(sp.GetRequiredService<IScreenCapture>());
        s.AddSingleton<SessionStat>(sp.GetRequiredService<SessionStat>());
        s.AddSingleton<IGrindSessionDAO>(sp.GetRequiredService<IGrindSessionDAO>());

        // Addon Components
        s.AddSingleton(sp.GetRequiredService<CombatLog>());
        s.AddSingleton(sp.GetRequiredService<EquipmentReader>());
        s.AddSingleton(sp.GetRequiredService<BagReader>());
        s.AddSingleton(sp.GetRequiredService<GossipReader>());
        s.AddSingleton(sp.GetRequiredService<SpellBookReader>());
        s.AddSingleton(sp.GetRequiredService<TalentReader>());

        s.AddSingleton(sp.GetRequiredService<ActionBarCostReader>());
        s.AddSingleton(sp.GetRequiredService<ActionBarCooldownReader>());

        s.AddSingleton(sp.GetRequiredService<ActionBarBits<ICurrentAction>>());
        s.AddSingleton(sp.GetRequiredService<ActionBarBits<IUsableAction>>());

        s.AddSingleton(sp.GetRequiredService<AuraTimeReader<IPlayerBuffTimeReader>>());
        s.AddSingleton(sp.GetRequiredService<AuraTimeReader<ITargetDebuffTimeReader>>());
        s.AddSingleton(sp.GetRequiredService<AuraTimeReader<ITargetBuffTimeReader>>());

        // Player Reader components
        s.AddSingleton(sp.GetRequiredService<AddonBits>());
        s.AddSingleton(sp.GetRequiredService<SpellInRange>());
        s.AddSingleton(sp.GetRequiredService<BuffStatus>());
        s.AddSingleton(sp.GetRequiredService<TargetDebuffStatus>());
        s.AddSingleton(sp.GetRequiredService<Stance>());

        return s;
    }

    public static IServiceCollection AddCoreFrontend(this IServiceCollection services)
    {
        services.AddSingleton<WApi>();
        services.AddSingleton<FrontendUpdate>();
        return services;
    }

    public static IServiceCollection AddCoreConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<IAddonDataProvider, AddonDataProviderGDIConfig>();
        services.AddSingleton<IBotController, ConfigBotController>();
        services.AddSingleton<IAddonReader, ConfigAddonReader>();

        return services;
    }

    public static IServiceCollection AddCoreNormal(this IServiceCollection services, ILogger logger)
    {
        services.AddSingleton<IScreenCapture>(x =>
            GetScreenCapture(x.GetRequiredService<IServiceProvider>(), logger));

        services.AddSingleton<IPPather>(x =>
            GetPather(x.GetRequiredService<IServiceProvider>(), logger));

        services.AddSingleton<IAddonDataProvider>(x =>
            GetAddonDataProvider(x.GetRequiredService<IServiceProvider>(), logger));

        services.AddSingleton<MinimapNodeFinder>();

        services.AddSingleton<SessionStat>();
        services.AddSingleton<IGrindSessionDAO, LocalGrindSessionDAO>();

        services.AddSingleton<AreaDB>();
        services.AddSingleton<WorldMapAreaDB>();
        services.AddSingleton<ItemDB>();
        services.AddSingleton<CreatureDB>();
        services.AddSingleton<SpellDB>();
        services.AddSingleton<TalentDB>();

        services.AddAddonComponents();

        services.AddSingleton<PlayerReader>();
        services.AddSingleton<IMouseOverReader>(
            x => x.GetRequiredService<PlayerReader>());

        services.AddSingleton<AddonReader>();
        services.AddSingleton<IAddonReader>(
            x => x.GetRequiredService<AddonReader>());

        services.AddSingleton<LevelTracker>();

        services.AddSingleton<IBotController, BotController>();

        return services;
    }

    public static IServiceCollection AddCoreBase(this IServiceCollection services)
    {
        services.AddSingleton<CancellationTokenSource>();
        services.AddSingleton<AutoResetEvent>(x => new(false));
        services.AddSingleton<Wait>();

        services.AddSingleton<StartupClientVersion>();
        services.AddSingleton<DataConfig>(x => DataConfig.Load(
            x.GetRequiredService<StartupClientVersion>().Path));

        services.AddSingleton<WowScreen>();
        services.AddSingleton<IBitmapProvider>(x => x.GetRequiredService<WowScreen>());
        services.AddSingleton<IWowScreen>(x => x.GetRequiredService<WowScreen>());

        services.AddSingleton<WowProcessInput>();
        services.AddSingleton<IMouseInput>(x => x.GetRequiredService<WowProcessInput>());

        services.AddSingleton<ExecGameCommand>();

        services.AddSingleton<DataFrame[]>(x => FrameConfig.LoadFrames());
        services.AddSingleton<FrameConfigurator>();

        services.AddSingleton<INpcResetEvent, NpcResetEvent>();
        services.AddSingleton<NpcNameFinder>();

        return services;
    }


    public static bool AddWoWProcess(this IServiceCollection services, ILogger log)
    {
        services.AddSingleton<WowProcess>();
        services.AddSingleton<AddonConfigurator>();

        var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        WowProcess wowProcess = sp.GetRequiredService<WowProcess>();
        log.LogInformation($"Pid: {wowProcess.ProcessId}");
        log.LogInformation($"Version: {wowProcess.FileVersion}");

        // TODO: this might need some massage
        services.AddSingleton<Version>(x => wowProcess.FileVersion);

        AddonConfigurator configurator = sp.GetRequiredService<AddonConfigurator>();
        Version? installVersion = configurator.GetInstallVersion();
        log.LogInformation($"Addon version: {installVersion}");

        if (configurator.IsDefault() || installVersion == null)
        {
            // At this point the webpage never loads so fallback to configuration page
            configurator.Delete();
            FrameConfig.Delete();

            log.LogError($"{nameof(AddonConfig)} doesn't exists or addon not installed yet!");
            return false;
        }

        NativeMethods.GetWindowRect(wowProcess.Process.MainWindowHandle, out Rectangle rect);
        if (FrameConfig.Exists() && !FrameConfig.IsValid(rect, installVersion))
        {
            // At this point the webpage never loads so fallback to configuration page
            FrameConfig.Delete();
            log.LogError($"{nameof(FrameConfig)} doesn't exists or window rect is different then config!");

            return false;
        }

        return true;
    }


    private static IScreenCapture GetScreenCapture(IServiceProvider sp, ILogger log)
    {
        //var log = sp.GetRequiredService<ILogger<Startup>>();
        var spd = sp.GetRequiredService<IOptions<StartupConfigDiagnostics>>();
        var globalLogger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger>();
        var logger = sp.GetRequiredService<ILogger<ScreenCapture>>();
        var dataConfig = sp.GetRequiredService<DataConfig>();
        var cts = sp.GetRequiredService<CancellationTokenSource>();
        var wowScreen = sp.GetRequiredService<WowScreen>();

        IScreenCapture value = spd.Value.Enabled
            ? new ScreenCapture(logger, dataConfig, cts, wowScreen)
            : new NoScreenCapture(globalLogger, dataConfig);

        log.LogInformation(value.GetType().Name);

        return value;
    }

    private static IAddonDataProvider GetAddonDataProvider(IServiceProvider sp, ILogger log)
    {
        //var log = sp.GetRequiredService<ILogger<Startup>>();
        var scr = sp.GetRequiredService<IOptions<StartupConfigReader>>();
        var wowScreen = sp.GetRequiredService<WowScreen>();
        var frames = sp.GetRequiredService<DataFrame[]>();

        IAddonDataProvider value = scr.Value.ReaderType switch
        {
            AddonDataProviderType.GDIConfig or
            AddonDataProviderType.GDI =>
                new AddonDataProviderGDI(wowScreen, frames),
            AddonDataProviderType.GDIBlit =>
                new AddonDataProviderBitBlt(wowScreen, frames),
            AddonDataProviderType.DXGI =>
                new AddonDataProviderDXGI(wowScreen, frames),
            _ => throw new NotImplementedException(),
        };

        log.LogInformation(value.GetType().Name);
        return value;
    }

    private static IPPather GetPather(IServiceProvider sp, ILogger logger)
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        //var logger = sp.GetRequiredService<ILogger<Startup>>();
        var oscp = sp.GetRequiredService<IOptions<StartupConfigPathing>>();
        var dataConfig = sp.GetRequiredService<DataConfig>();
        var worldMapAreaDB = sp.GetRequiredService<WorldMapAreaDB>();

        StartupConfigPathing scp = oscp.Value;

        bool failed = false;
        if (scp.Type == StartupConfigPathing.Types.RemoteV3)
        {
            var remoteLogger = loggerFactory.CreateLogger<RemotePathingAPIV3>();
            RemotePathingAPIV3 api = new(remoteLogger, scp.hostv3, scp.portv3, worldMapAreaDB);
            if (api.PingServer())
            {
                logger.LogInformation($"Using {StartupConfigPathing.Types.RemoteV3}({api.GetType().Name}) {scp.hostv3}:{scp.portv3}");
                return api;
            }
            api.Dispose();
            failed = true;
        }

        if (scp.Type == StartupConfigPathing.Types.RemoteV1 || failed)
        {
            var remoteLogger = loggerFactory.CreateLogger<RemotePathingAPI>();
            RemotePathingAPI api = new(remoteLogger, scp.hostv1, scp.portv1);
            Task<bool> pingTask = Task.Run(api.PingServer);
            pingTask.Wait();
            if (pingTask.Result)
            {
                if (scp.Type == StartupConfigPathing.Types.RemoteV3)
                {
                    logger.LogWarning($"Unavailable {StartupConfigPathing.Types.RemoteV3} {scp.hostv3}:{scp.portv3} - Fallback to {StartupConfigPathing.Types.RemoteV1}");
                }

                logger.LogInformation($"Using {StartupConfigPathing.Types.RemoteV1}({api.GetType().Name}) {scp.hostv1}:{scp.portv1}");
                return api;
            }

            failed = true;
        }

        if (scp.Type != StartupConfigPathing.Types.Local)
        {
            logger.LogWarning($"{scp.Type} not available!");
        }

        var pathingLogger = loggerFactory.CreateLogger<LocalPathingApi>();
        var serviceLogger = loggerFactory.CreateLogger<PPatherService>();
        LocalPathingApi localApi = new(pathingLogger, new(serviceLogger, dataConfig, worldMapAreaDB), dataConfig);
        logger.LogInformation($"Using {StartupConfigPathing.Types.Local}({localApi.GetType().Name})");

        return localApi;
    }

}
