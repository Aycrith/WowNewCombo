using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

using Core.Addon;
using Core.Database;
using Core.Extensions;
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
        s.ForwardSingleton<PlayerReader, IReader>();
        s.ForwardSingleton<PlayerReader, IMouseOverReader>();

        s.ForwardSingleton<AddonReader, IAddonReader>();

        s.ForwardSingleton<AddonBits, IReader>();
        s.ForwardSingleton<SpellInRange, IReader>();
        s.ForwardSingleton<BuffStatus, IReader>();
        s.ForwardSingleton<TargetDebuffStatus, IReader>();
        s.ForwardSingleton<Stance, IReader>();

        s.ForwardSingleton<CombatLog, IReader>();
        s.ForwardSingleton<EquipmentReader, IReader>();
        s.ForwardSingleton<BagReader, IReader>();
        s.ForwardSingleton<GossipReader, IReader>();
        s.ForwardSingleton<SpellBookReader, IReader>();
        s.ForwardSingleton<TalentReader, IReader>();

        s.ForwardSingleton<ActionBarCostReader, IReader>();
        s.ForwardSingleton<ActionBarCooldownReader, IReader>();

        s.ForwardSingleton<ActionBarBits<ICurrentAction>, IReader>(
            x => new(25, 26, 27, 28, 29));
        s.ForwardSingleton<ActionBarBits<IUsableAction>, IReader>(
            x => new(30, 31, 32, 33, 34));

        s.ForwardSingleton<AuraTimeReader<IPlayerBuffTimeReader>, IReader>(
            x => new(79, 80));
        s.ForwardSingleton<AuraTimeReader<ITargetDebuffTimeReader>, IReader>(
            x => new(81, 82));
        s.ForwardSingleton<AuraTimeReader<ITargetBuffTimeReader>, IReader>(
            x => new(83, 84));

        return s;
    }

    public static IServiceCollection AddStartupIoC(
        this IServiceCollection s, IServiceProvider sp)
    {
        // StartUp Container
        s.ForwardSingleton<CancellationTokenSource>(sp);

        s.ForwardSingleton<WowProcessInput>(sp);
        s.ForwardSingleton<IMouseInput>(sp);
        s.ForwardSingleton<IMouseOverReader>(sp);

        s.ForwardSingleton<NpcNameFinder>(sp);
        s.ForwardSingleton<IWowScreen>(sp);
        s.ForwardSingleton<WowScreen>(sp);

        s.ForwardSingleton<IPPather>(sp);
        s.ForwardSingleton<ExecGameCommand>(sp);

        s.ForwardSingleton<Wait>(sp);

        s.ForwardSingleton<DataConfig>(sp);

        s.ForwardSingleton<AreaDB>(sp);
        s.ForwardSingleton<WorldMapAreaDB>(sp);
        s.ForwardSingleton<ItemDB>(sp);
        s.ForwardSingleton<CreatureDB>(sp);
        s.ForwardSingleton<SpellDB>(sp);
        s.ForwardSingleton<TalentDB>(sp);

        s.ForwardSingleton<AddonReader>(sp);
        s.ForwardSingleton<PlayerReader>(sp);

        s.ForwardSingleton<AddonBits>(sp);
        s.ForwardSingleton<SpellInRange>(sp);
        s.ForwardSingleton<BuffStatus>(sp);
        s.ForwardSingleton<TargetDebuffStatus>(sp);
        s.ForwardSingleton<Stance>(sp);

        s.ForwardSingleton<IScreenCapture>(sp);
        s.ForwardSingleton<SessionStat>(sp);
        s.ForwardSingleton<IGrindSessionDAO>(sp);

        // Addon Components
        s.ForwardSingleton<CombatLog>(sp);
        s.ForwardSingleton<EquipmentReader>(sp);
        s.ForwardSingleton<BagReader>(sp);
        s.ForwardSingleton<GossipReader>(sp);
        s.ForwardSingleton<SpellBookReader>(sp);
        s.ForwardSingleton<TalentReader>(sp);

        s.ForwardSingleton<ActionBarCostReader>(sp);
        s.ForwardSingleton<ActionBarCooldownReader>(sp);

        s.ForwardSingleton<ActionBarBits<ICurrentAction>>(sp);
        s.ForwardSingleton<ActionBarBits<IUsableAction>>(sp);

        s.ForwardSingleton<AuraTimeReader<IPlayerBuffTimeReader>>(sp);
        s.ForwardSingleton<AuraTimeReader<ITargetDebuffTimeReader>>(sp);
        s.ForwardSingleton<AuraTimeReader<ITargetBuffTimeReader>>(sp);

        return s;
    }

    public static IServiceCollection AddCoreFrontend(this IServiceCollection services)
    {
        services.AddSingleton<WApi>();
        services.AddSingleton<FrontendUpdate>();

        services.AddSingleton<LevelTracker>();

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
        var scp = sp.GetRequiredService<IOptions<StartupConfigPathing>>().Value;
        var dataConfig = sp.GetRequiredService<DataConfig>();
        var worldMapAreaDB = sp.GetRequiredService<WorldMapAreaDB>();

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
            if (api.PingServer())
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
        LocalPathingApi localApi = new(pathingLogger, new(serviceLogger, dataConfig, worldMapAreaDB));
        logger.LogInformation($"Using {StartupConfigPathing.Types.Local}({localApi.GetType().Name})");

        return localApi;
    }

}
