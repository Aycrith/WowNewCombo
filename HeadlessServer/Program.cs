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
using Core.Environment;
using WinAPI;
using System.Drawing;

namespace HeadlessServer
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            string logfile = "headless_out.log";
            var config = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .WriteTo.File(logfile, rollingInterval: RollingInterval.Day)
                .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss:fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff} {Level:u3}] {Message:lj}{NewLine}{Exception}");

            Log.Logger = config.CreateLogger();
            Log.Logger.Debug("Main()");

            ParserResult<RunOptions> options = Parser.Default.ParseArguments<RunOptions>(args).WithNotParsed(a =>
            {
                Log.Logger.Error("Missing Required command line argument!");
            });

            if (options.Tag == ParserResultType.NotParsed)
            {
                Console.ReadLine();
                return;
            }

            if (!FrameConfig.Exists() || !AddonConfig.Exists())
            {
                Log.Logger.Error("Unable to run headless server as crucial configuration files missing!");
                Log.Logger.Warning($"Please be sure, the following validated configuration files present next to the executable:");
                Log.Logger.Warning($"* {DataConfigMeta.DefaultFileName}");
                Log.Logger.Warning($"* {FrameConfigMeta.DefaultFilename}");
                Log.Logger.Warning($"* {AddonConfigMeta.DefaultFileName}");
                Console.ReadLine();
                return;
            }

            while (WowProcess.Get(options.Value.Pid) == null)
            {
                Log.Logger.Information("Unable to find any Wow process, is it running ?");
                Thread.Sleep(1000);
            }

            ServiceCollection services = new();
            if (ConfigureServices(services, options))
            {
                services
                    .AddSingleton<HeadlessServer, HeadlessServer>()
                    .BuildServiceProvider()
                    .GetService<HeadlessServer>()?
                    .Run(options);
            }

            Console.ReadLine();
        }

        private static bool ConfigureServices(IServiceCollection services, ParserResult<RunOptions> options)
        {
            var logger = new SerilogLoggerProvider(Log.Logger).CreateLogger(nameof(Program));

            if (!Validate(logger, options.Value.Pid))
                return false;

            services.AddSingleton(logger);

            services.AddSingleton<CancellationTokenSource>();

            services.AddSingleton<IEnvironment, Headless>();

            services.AddSingleton(DataConfig.Load());
            services.AddSingleton<WowProcess>(x => new(options.Value.Pid));
            services.AddSingleton<WowScreen>();
            services.AddSingleton<WowProcessInput>();
            services.AddSingleton<ExecGameCommand>();
            services.AddSingleton<AddonConfigurator>();

            services.AddSingleton<MinimapNodeFinder>();

            services.AddSingleton<IGrindSessionDAO, LocalGrindSessionDAO>();
            services.AddSingleton<IPPather>(x => GetPather(logger, x.GetRequiredService<DataConfig>(), options));

            services.AddSingleton<AutoResetEvent>(x => new(false));
            services.AddSingleton<DataFrame[]>(x => FrameConfig.LoadFrames());
            services.AddSingleton<Wait>();

            if (options.Value.Reader == AddonDataProviderType.DXGI)
            {
                services.AddSingleton<IAddonDataProvider, AddonDataProviderDXGI>();
                Log.Logger.Information($"Using {nameof(AddonDataProviderDXGI)}");
            }
            else
            {
                services.AddSingleton<IAddonDataProvider, AddonDataProviderGDI>();
                Log.Logger.Information($"Using {nameof(AddonDataProviderGDI)}");
            }

            services.AddSingleton<AreaDB>();
            services.AddSingleton<WorldMapAreaDB>();
            services.AddSingleton<ItemDB>();
            services.AddSingleton<CreatureDB>();
            services.AddSingleton<SpellDB>();
            services.AddSingleton<TalentDB>();

            services.AddSingleton<IAddonReader, AddonReader>();
            services.AddSingleton<IBotController, BotController>();

            return true;
        }

        private static IPPather GetPather(Microsoft.Extensions.Logging.ILogger logger, DataConfig dataConfig, ParserResult<RunOptions> options)
        {
            StartupConfigPathing scp = new(options.Value.Mode.ToString()!,
                options.Value.Hostv1!, options.Value.Portv1,
                options.Value.Hostv3!, options.Value.Portv3);

            bool failed = false;

            if (scp.Type == StartupConfigPathing.Types.RemoteV3)
            {
                RemotePathingAPIV3 api = new(logger, scp.hostv3, scp.portv3, new WorldMapAreaDB(dataConfig));
                if (api.PingServer())
                {
                    Log.Information("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
                    Log.Debug($"Using {StartupConfigPathing.Types.RemoteV3}({api.GetType().Name}) {scp.hostv3}:{scp.portv3}");
                    Log.Information("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
                    return api;
                }
                api.Dispose();
                failed = true;
            }

            if (scp.Type == StartupConfigPathing.Types.RemoteV1 || failed)
            {
                RemotePathingAPI api = new(logger, scp.hostv1, scp.portv1);
                var pingTask = Task.Run(api.PingServer);
                pingTask.Wait();
                if (pingTask.Result)
                {
                    Log.Information("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");

                    if (scp.Type == StartupConfigPathing.Types.RemoteV3)
                    {
                        Log.Debug($"Unavailable {StartupConfigPathing.Types.RemoteV3} {scp.hostv3}:{scp.portv3} - Fallback to {StartupConfigPathing.Types.RemoteV1}");
                    }

                    Log.Debug($"Using {StartupConfigPathing.Types.RemoteV1}({api.GetType().Name}) {scp.hostv1}:{scp.portv1}");
                    Log.Information("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
                    return api;
                }

                failed = true;
            }

            Log.Information("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
            if (scp.Type != StartupConfigPathing.Types.Local)
            {
                Log.Debug($"{scp.Type} not available!");
            }
            Log.Information("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");

            LocalPathingApi localApi = new(logger, new PPatherService(logger, dataConfig), dataConfig);
            Log.Information($"Using {StartupConfigPathing.Types.Local}({localApi.GetType().Name}) pathing API.");
            Log.Information("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
            return localApi;
        }

        private static bool Validate(Microsoft.Extensions.Logging.ILogger logger, int pid)
        {
            WowProcess wowProcess = new(pid);
            NativeMethods.GetWindowRect(wowProcess.Process.MainWindowHandle, out Rectangle rect);

            AddonConfigurator addonConfigurator = new(logger, wowProcess);
            Version? installVersion = addonConfigurator.GetInstallVersion();

            bool valid = true;

            if (addonConfigurator.IsDefault() || installVersion == null)
            {
                // At this point the webpage never loads so fallback to configuration page
                addonConfigurator.Delete();
                FrameConfig.Delete();
                valid = false;

                Log.Error($"{nameof(AddonConfig)} dosent exists or addon dosent installed!");
            }

            if (FrameConfig.Exists() && !FrameConfig.IsValid(rect, installVersion!))
            {
                // At this point the webpage never loads so fallback to configuration page
                FrameConfig.Delete();
                valid = false;

                Log.Error($"{nameof(FrameConfig)} dosent exists or window rect is different then config!");
            }

            wowProcess.Dispose();

            return valid;
        }
    }
}