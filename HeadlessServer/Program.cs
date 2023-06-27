using Core;
using Core.Database;
using Core.Session;
using Game;
using Microsoft.Extensions.DependencyInjection;
using PPather;
using Serilog;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;
using CommandLine;
using WinAPI;
using System.Drawing;
using SharedLib;
using Microsoft.Extensions.Logging;

namespace HeadlessServer;

internal sealed class Program
{
    private static void Main(string[] args)
    {
        IServiceCollection services = new ServiceCollection();

        ILoggerFactory logFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders().AddSerilog();
        });

        services.AddLogging(builder =>
        {
            const string outputTemplate = "[{@t:HH:mm:ss:fff} {@l:u1}] {#if Length(SourceContext) > 0}[{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1),-15}] {#end}{@m}\n{@x}";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.File(new ExpressionTemplate(outputTemplate),
                    path: "headless_out.log",
                    rollingInterval: RollingInterval.Day)
                .WriteTo.Debug(new ExpressionTemplate(outputTemplate))
                .WriteTo.Console(new ExpressionTemplate(outputTemplate, theme: TemplateTheme.Literate))
                .CreateLogger();

            builder.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(logFactory.CreateLogger(string.Empty));
            builder.AddSerilog();
        });

        var log = logFactory.CreateLogger<Program>();

        log.LogInformation($"{Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName} {DateTimeOffset.Now}");

        ParserResult<RunOptions> options = Parser.Default.ParseArguments<RunOptions>(args).WithNotParsed(a =>
        {
            log.LogError("Missing Required command line argument!");
        });

        if (options.Tag == ParserResultType.NotParsed)
        {
            Console.ReadLine();
            return;
        }

        if (!FrameConfig.Exists() || !AddonConfig.Exists())
        {
            log.LogError("Unable to run headless server as crucial configuration files missing!");
            log.LogWarning($"Please be sure, the following validated configuration files present next to the executable:");
            log.LogWarning($"{System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}");
            log.LogWarning($"* {DataConfigMeta.DefaultFileName}");
            log.LogWarning($"* {FrameConfigMeta.DefaultFilename}");
            log.LogWarning($"* {AddonConfigMeta.DefaultFileName}");
            Console.ReadLine();
            return;
        }

        while (WowProcess.Get(options.Value.Pid) == null)
        {
            log.LogWarning($"Unable to find any Wow process, is it running ?");
            Thread.Sleep(1000);
        }

        if (ConfigureServices(logFactory, log, services, options))
        {
            ServiceProvider provider = services
                .AddSingleton<HeadlessServer>()
                .BuildServiceProvider(new ServiceProviderOptions() { ValidateOnBuild = true });

            Microsoft.Extensions.Logging.ILogger logger =
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger>();

            AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs args) =>
            {
                Exception e = (Exception)args.ExceptionObject;
                logger.LogError(e, e.Message);
            };

            provider
                .GetRequiredService<HeadlessServer>()
                .Run(options);
        }

        Console.ReadLine();
    }

    private static bool ConfigureServices(ILoggerFactory loggerFactory, Microsoft.Extensions.Logging.ILogger log, IServiceCollection services, ParserResult<RunOptions> options)
    {
        if (!Validate(loggerFactory, log, options.Value.Pid))
            return false;

        services.AddSingleton<CancellationTokenSource>();

        services.AddSingleton<WowProcess>(x => new(options.Value.Pid));
        services.AddSingleton<StartupClientVersion>();
        services.AddSingleton<DataConfig>(x => DataConfig.Load(x.GetRequiredService<StartupClientVersion>().Path));
        services.AddSingleton<WowScreen>();
        services.AddSingleton<WowProcessInput>();
        services.AddSingleton<ExecGameCommand>();
        services.AddSingleton<AddonConfigurator>();

        services.AddSingleton<MinimapNodeFinder>();

        if (options.Value.Diagnostics)
        {
            services.AddSingleton<IScreenCapture, ScreenCapture>();
            log.LogInformation(nameof(ScreenCapture));
        }
        else
        {
            services.AddSingleton<IScreenCapture, NoScreenCapture>();
            log.LogInformation(nameof(NoScreenCapture));
        }

        services.AddSingleton<SessionStat>();
        services.AddSingleton<IGrindSessionDAO, LocalGrindSessionDAO>();
        services.AddSingleton<WorldMapAreaDB>();
        services.AddSingleton<IPPather>(x =>
            GetPather(loggerFactory, x.GetRequiredService<Microsoft.Extensions.Logging.ILogger>(),
                x.GetRequiredService<DataConfig>(), x.GetRequiredService<WorldMapAreaDB>(), options));

        services.AddSingleton<AutoResetEvent>(x => new(false));
        services.AddSingleton<DataFrame[]>(x => FrameConfig.LoadFrames());
        services.AddSingleton<Wait>();


        switch (options.Value.Reader)
        {
            case AddonDataProviderType.GDI:
                services.AddSingleton<IAddonDataProvider, AddonDataProviderGDI>();
                log.LogInformation(nameof(AddonDataProviderGDI));
                break;
            case AddonDataProviderType.GDIBlit:
                services.AddSingleton<IAddonDataProvider, AddonDataProviderBitBlt>();
                log.LogInformation(nameof(AddonDataProviderBitBlt));
                break;
            case AddonDataProviderType.DXGI:
                services.AddSingleton<IAddonDataProvider, AddonDataProviderDXGI>();
                log.LogInformation(nameof(AddonDataProviderDXGI));
                break;
        }

        services.AddSingleton<AreaDB>();
        services.AddSingleton<ItemDB>();
        services.AddSingleton<CreatureDB>();
        services.AddSingleton<SpellDB>();
        services.AddSingleton<TalentDB>();

        services.AddSingleton<PlayerReader>();
        services.AddSingleton<IAddonReader, AddonReader>();

        services.AddSingleton<IBotController, BotController>();

        return true;
    }

    private static IPPather GetPather(ILoggerFactory logFactory,
        Microsoft.Extensions.Logging.ILogger logger, DataConfig dataConfig,
        WorldMapAreaDB worldMapAreaDB, ParserResult<RunOptions> options)
    {
        StartupConfigPathing scp = new(options.Value.Mode.ToString()!,
            options.Value.Hostv1!, options.Value.Portv1,
            options.Value.Hostv3!, options.Value.Portv3);

        bool failed = false;

        if (scp.Type == StartupConfigPathing.Types.RemoteV3)
        {
            var remoteLogger = logFactory.CreateLogger<RemotePathingAPIV3>();
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
            var remoteLogger = logFactory.CreateLogger<RemotePathingAPI>();
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

        var pathingLogger = logFactory.CreateLogger<LocalPathingApi>();
        var serviceLogger = logFactory.CreateLogger<PPatherService>();
        LocalPathingApi localApi = new(pathingLogger, new(serviceLogger, dataConfig, worldMapAreaDB), dataConfig);
        logger.LogInformation($"Using {StartupConfigPathing.Types.Local}({localApi.GetType().Name})");
        return localApi;
    }

    private static bool Validate(ILoggerFactory logFactory, Microsoft.Extensions.Logging.ILogger log, int pid)
    {
        WowProcess wowProcess = new(pid);
        log.LogInformation($"Pid: {wowProcess.ProcessId}");
        log.LogInformation($"Version: {wowProcess.FileVersion}");
        NativeMethods.GetWindowRect(wowProcess.Process.MainWindowHandle, out Rectangle rect);

        var logger = logFactory.CreateLogger<AddonConfigurator>();

        AddonConfigurator addonConfigurator = new(logger, wowProcess);
        Version? installVersion = addonConfigurator.GetInstallVersion();
        log.LogInformation($"Addon version: {installVersion}");

        bool valid = true;

        if (addonConfigurator.IsDefault() || installVersion == null)
        {
            // At this point the webpage never loads so fallback to configuration page
            addonConfigurator.Delete();
            FrameConfig.Delete();
            valid = false;

            log.LogError($"{nameof(AddonConfig)} doesn't exists or addon not installed yet!");
        }

        if (FrameConfig.Exists() && !FrameConfig.IsValid(rect, installVersion!))
        {
            // At this point the webpage never loads so fallback to configuration page
            FrameConfig.Delete();
            valid = false;

            log.LogError($"{nameof(FrameConfig)} doesn't exists or window rect is different then config!");
        }

        wowProcess.Dispose();

        return valid;
    }
}