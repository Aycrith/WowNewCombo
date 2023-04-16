using Core;
using Core.Database;
using Core.Session;
using Game;
using Microsoft.Extensions.DependencyInjection;
using PPather;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
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
            const string outputTemplate = "[{Timestamp:HH:mm:ss:fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .WriteTo.File("headless_out.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: outputTemplate)
                .WriteTo.Debug(outputTemplate: outputTemplate)
                .WriteTo.Console(outputTemplate: outputTemplate)
                .CreateLogger();

            ILoggerFactory logFactory = LoggerFactory.Create(builder =>
            {
                builder.ClearProviders().AddSerilog();
            });

            builder.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(logFactory.CreateLogger(nameof(Program)));
        });

        Log.Information($"[{nameof(Program)}] {Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName} {DateTimeOffset.Now}");

        ParserResult<RunOptions> options = Parser.Default.ParseArguments<RunOptions>(args).WithNotParsed(a =>
        {
            Log.Error("Missing Required command line argument!");
        });

        if (options.Tag == ParserResultType.NotParsed)
        {
            Console.ReadLine();
            return;
        }

        if (!FrameConfig.Exists() || !AddonConfig.Exists())
        {
            Log.Error("Unable to run headless server as crucial configuration files missing!");
            Log.Warning($"Please be sure, the following validated configuration files present next to the executable:");
            Log.Warning($"{System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}");
            Log.Warning($"* {DataConfigMeta.DefaultFileName}");
            Log.Warning($"* {FrameConfigMeta.DefaultFilename}");
            Log.Warning($"* {AddonConfigMeta.DefaultFileName}");
            Console.ReadLine();
            return;
        }

        while (WowProcess.Get(options.Value.Pid) == null)
        {
            Log.Warning($"[{nameof(Program)}] Unable to find any Wow process, is it running ?");
            Thread.Sleep(1000);
        }

        if (ConfigureServices(services, options))
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

    private static bool ConfigureServices(IServiceCollection services, ParserResult<RunOptions> options)
    {
        if (!Validate(options.Value.Pid))
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
            Log.Information($"[{nameof(Program)}] {nameof(ScreenCapture)}");
        }
        else
        {
            services.AddSingleton<IScreenCapture, NoScreenCapture>();
            Log.Information($"[{nameof(Program)}] {nameof(NoScreenCapture)}");
        }

        services.AddSingleton<SessionStat>();
        services.AddSingleton<IGrindSessionDAO, LocalGrindSessionDAO>();
        services.AddSingleton<WorldMapAreaDB>();
        services.AddSingleton<IPPather>(x =>
            GetPather(x.GetRequiredService<Microsoft.Extensions.Logging.ILogger>(),
                x.GetRequiredService<DataConfig>(), x.GetRequiredService<WorldMapAreaDB>(), options));

        services.AddSingleton<AutoResetEvent>(x => new(false));
        services.AddSingleton<DataFrame[]>(x => FrameConfig.LoadFrames());
        services.AddSingleton<Wait>();

        if (options.Value.Reader == AddonDataProviderType.DXGI)
        {
            services.AddSingleton<IAddonDataProvider, AddonDataProviderDXGI>();
            Log.Information($"[{nameof(Program)}] {nameof(AddonDataProviderDXGI)}");
        }
        if (options.Value.Reader == AddonDataProviderType.DXGISwapChain)
        {
            services.AddSingleton<IAddonDataProvider, AddonDataProviderDXGISwapChain>();
            Log.Information($"[{nameof(Program)}] {nameof(AddonDataProviderDXGISwapChain)}");
        }
        else if (options.Value.Reader is AddonDataProviderType.GDI)
        {
            services.AddSingleton<IAddonDataProvider, AddonDataProviderGDI>();
            Log.Information($"[{nameof(Program)}] {nameof(AddonDataProviderGDI)}");
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

    private static IPPather GetPather(Microsoft.Extensions.Logging.ILogger logger, DataConfig dataConfig, WorldMapAreaDB worldMapAreaDB, ParserResult<RunOptions> options)
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
                Log.Information($"[{nameof(Program)}] Using {StartupConfigPathing.Types.RemoteV3}({api.GetType().Name}) {scp.hostv3}:{scp.portv3}");
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
                    Log.Warning($"[{nameof(Program)}] Unavailable {StartupConfigPathing.Types.RemoteV3} {scp.hostv3}:{scp.portv3} - Fallback to {StartupConfigPathing.Types.RemoteV1}");
                }

                Log.Information($"[{nameof(Program)}] Using {StartupConfigPathing.Types.RemoteV1}({api.GetType().Name}) {scp.hostv1}:{scp.portv1}");
                return api;
            }

            failed = true;
        }

        if (scp.Type != StartupConfigPathing.Types.Local)
        {
            Log.Warning($"[{nameof(Program)}] {scp.Type} not available!");
        }

        LocalPathingApi localApi = new(logger, new PPatherService(logger, dataConfig, worldMapAreaDB), dataConfig);
        Log.Information($"[{nameof(Program)}] Using {StartupConfigPathing.Types.Local}({localApi.GetType().Name})");
        return localApi;
    }

    private static bool Validate(int pid)
    {
        WowProcess wowProcess = new(pid);
        Log.Information($"[{nameof(Program)}] Pid: {wowProcess.ProcessId}");
        Log.Information($"[{nameof(Program)}] Version: {wowProcess.FileVersion}");
        NativeMethods.GetWindowRect(wowProcess.Process.MainWindowHandle, out Rectangle rect);

        var logger = new SerilogLoggerProvider(Log.Logger, true).CreateLogger(nameof(AddonConfigurator));
        AddonConfigurator addonConfigurator = new(logger, wowProcess);
        Version? installVersion = addonConfigurator.GetInstallVersion();
        Log.Information($"[{nameof(Program)}] Addon version: {installVersion}");

        bool valid = true;

        if (addonConfigurator.IsDefault() || installVersion == null)
        {
            // At this point the webpage never loads so fallback to configuration page
            addonConfigurator.Delete();
            FrameConfig.Delete();
            valid = false;

            Log.Error($"[{nameof(Program)}] {nameof(AddonConfig)} doesn't exists or addon not installed yet!");
        }

        if (FrameConfig.Exists() && !FrameConfig.IsValid(rect, installVersion!))
        {
            // At this point the webpage never loads so fallback to configuration page
            FrameConfig.Delete();
            valid = false;

            Log.Error($"[{nameof(Program)}] {nameof(FrameConfig)} doesn't exists or window rect is different then config!");
        }

        wowProcess.Dispose();

        return valid;
    }
}