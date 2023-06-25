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

            ILoggerFactory logFactory = LoggerFactory.Create(builder =>
            {
                builder.ClearProviders().AddSerilog();
            });

            builder.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(logFactory.CreateLogger(string.Empty));
            builder.AddSerilog();
        });

        var log = Log.Logger.ForContext<Program>();

        log.Information($"{Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName} {DateTimeOffset.Now}");

        ParserResult<RunOptions> options = Parser.Default.ParseArguments<RunOptions>(args).WithNotParsed(a =>
        {
            log.Error("Missing Required command line argument!");
        });

        if (options.Tag == ParserResultType.NotParsed)
        {
            Console.ReadLine();
            return;
        }

        if (!FrameConfig.Exists() || !AddonConfig.Exists())
        {
            log.Error("Unable to run headless server as crucial configuration files missing!");
            log.Warning($"Please be sure, the following validated configuration files present next to the executable:");
            log.Warning($"{System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}");
            log.Warning($"* {DataConfigMeta.DefaultFileName}");
            log.Warning($"* {FrameConfigMeta.DefaultFilename}");
            log.Warning($"* {AddonConfigMeta.DefaultFileName}");
            Console.ReadLine();
            return;
        }

        while (WowProcess.Get(options.Value.Pid) == null)
        {
            log.Warning($"Unable to find any Wow process, is it running ?");
            Thread.Sleep(1000);
        }

        if (ConfigureServices(log, services, options))
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

    private static bool ConfigureServices(Serilog.ILogger log, IServiceCollection services, ParserResult<RunOptions> options)
    {
        if (!Validate(log, options.Value.Pid))
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
            log.Information(nameof(ScreenCapture));
        }
        else
        {
            services.AddSingleton<IScreenCapture, NoScreenCapture>();
            log.Information(nameof(NoScreenCapture));
        }

        services.AddSingleton<SessionStat>();
        services.AddSingleton<IGrindSessionDAO, LocalGrindSessionDAO>();
        services.AddSingleton<WorldMapAreaDB>();
        services.AddSingleton<IPPather>(x =>
            GetPather(log, x.GetRequiredService<Microsoft.Extensions.Logging.ILogger>(),
                x.GetRequiredService<DataConfig>(), x.GetRequiredService<WorldMapAreaDB>(), options));

        services.AddSingleton<AutoResetEvent>(x => new(false));
        services.AddSingleton<DataFrame[]>(x => FrameConfig.LoadFrames());
        services.AddSingleton<Wait>();


        switch (options.Value.Reader)
        {
            case AddonDataProviderType.GDI:
                services.AddSingleton<IAddonDataProvider, AddonDataProviderGDI>();
                log.Information(nameof(AddonDataProviderGDI));
                break;
            case AddonDataProviderType.GDIBlit:
                services.AddSingleton<IAddonDataProvider, AddonDataProviderBitBlt>();
                log.Information(nameof(AddonDataProviderBitBlt));
                break;
            case AddonDataProviderType.DXGI:
                services.AddSingleton<IAddonDataProvider, AddonDataProviderDXGI>();
                log.Information(nameof(AddonDataProviderDXGI));
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

    private static IPPather GetPather(Serilog.ILogger log, Microsoft.Extensions.Logging.ILogger logger, DataConfig dataConfig, WorldMapAreaDB worldMapAreaDB, ParserResult<RunOptions> options)
    {
        StartupConfigPathing scp = new(options.Value.Mode.ToString()!,
            options.Value.Hostv1!, options.Value.Portv1,
            options.Value.Hostv3!, options.Value.Portv3);

        bool failed = false;

        if (scp.Type == StartupConfigPathing.Types.RemoteV3)
        {
            RemotePathingAPIV3 api = new(logger, scp.hostv3, scp.portv3, worldMapAreaDB);
            if (api.PingServer())
            {
                log.Information($"Using {StartupConfigPathing.Types.RemoteV3}({api.GetType().Name}) {scp.hostv3}:{scp.portv3}");
                return api;
            }
            api.Dispose();
            failed = true;
        }

        if (scp.Type == StartupConfigPathing.Types.RemoteV1 || failed)
        {
            RemotePathingAPI api = new(logger, scp.hostv1, scp.portv1);
            Task<bool> pingTask = Task.Run(api.PingServer);
            pingTask.Wait();
            if (pingTask.Result)
            {
                if (scp.Type == StartupConfigPathing.Types.RemoteV3)
                {
                    log.Warning($"Unavailable {StartupConfigPathing.Types.RemoteV3} {scp.hostv3}:{scp.portv3} - Fallback to {StartupConfigPathing.Types.RemoteV1}");
                }

                log.Information($"Using {StartupConfigPathing.Types.RemoteV1}({api.GetType().Name}) {scp.hostv1}:{scp.portv1}");
                return api;
            }

            failed = true;
        }

        if (scp.Type != StartupConfigPathing.Types.Local)
        {
            log.Warning($"{scp.Type} not available!");
        }

        LocalPathingApi localApi = new(logger, new PPatherService(logger, dataConfig, worldMapAreaDB), dataConfig);
        log.Information($"Using {StartupConfigPathing.Types.Local}({localApi.GetType().Name})");
        return localApi;
    }

    private static bool Validate(Serilog.ILogger log, int pid)
    {
        WowProcess wowProcess = new(pid);
        log.Information($"Pid: {wowProcess.ProcessId}");
        log.Information($"Version: {wowProcess.FileVersion}");
        NativeMethods.GetWindowRect(wowProcess.Process.MainWindowHandle, out Rectangle rect);

        ILoggerFactory factory = new LoggerFactory().AddSerilog(Log.Logger);
        var logger = factory.CreateLogger<AddonConfigurator>();

        AddonConfigurator addonConfigurator = new(logger, wowProcess);
        Version? installVersion = addonConfigurator.GetInstallVersion();
        log.Information($"Addon version: {installVersion}");

        bool valid = true;

        if (addonConfigurator.IsDefault() || installVersion == null)
        {
            // At this point the webpage never loads so fallback to configuration page
            addonConfigurator.Delete();
            FrameConfig.Delete();
            valid = false;

            log.Error($"{nameof(AddonConfig)} doesn't exists or addon not installed yet!");
        }

        if (FrameConfig.Exists() && !FrameConfig.IsValid(rect, installVersion!))
        {
            // At this point the webpage never loads so fallback to configuration page
            FrameConfig.Delete();
            valid = false;

            log.Error($"{nameof(FrameConfig)} doesn't exists or window rect is different then config!");
        }

        wowProcess.Dispose();

        return valid;
    }
}