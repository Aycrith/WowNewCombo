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

namespace HeadlessServer
{
    public class Options
    {
        [Value(0, MetaName = "ClassConfig file", Required = true, HelpText = "ClassConfiguration file example: Warrior_1.json")]
        public string? ClassConfig { get; set; }

        [Option('m', "mode", Default = nameof(StartupConfigPathing.Types.Local), Required = false,
            HelpText = $"PPather service: {nameof(StartupConfigPathing.Types.Local)} | {nameof(StartupConfigPathing.Types.RemoteV1)} | {nameof(StartupConfigPathing.Types.RemoteV3)}")]
        public string? Mode { get; set; }

        [Option("hostv1", Default = "localhost", Required = false, HelpText = $"PPather Remote V1 host")]
        public string? Hostv1 { get; set; }

        [Option("portv1", Default = 5001, Required = false, HelpText = $"PPather Remote V1 port")]
        public int Portv1 { get; set; }

        [Option("hostv3", Default = "127.0.0.1", Required = false, HelpText = $"PPather Remote V3 host")]
        public string? Hostv3 { get; set; }

        [Option("portv3", Default = 47111, Required = false, HelpText = $"PPather Remote V3 port")]
        public int Portv3 { get; set; }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            string logfile = "out.log";
            var config = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .WriteTo.File(logfile, rollingInterval: RollingInterval.Day)
                .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss:fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff} {Level:u3}] {Message:lj}{NewLine}{Exception}");

            Log.Logger = config.CreateLogger();
            Log.Logger.Debug("Main()");

            ParserResult<Options> options = Parser.Default.ParseArguments<Options>(args).WithNotParsed(a =>
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

            while (WowProcess.Get() == null)
            {
                Log.Information("Unable to find the Wow process is it running ?");
                Thread.Sleep(2000);
            }

            ServiceCollection services = new();
            ConfigureServices(services, options);

            services
                .AddSingleton<HeadlessServer, HeadlessServer>()
                .BuildServiceProvider()
                .GetService<HeadlessServer>()?
                .Run(options);

            Console.ReadLine();
        }

        private static void ConfigureServices(IServiceCollection services, ParserResult<Options> options)
        {
            var logger = new SerilogLoggerProvider(Log.Logger).CreateLogger(nameof(Program));
            services.AddSingleton(logger);

            services.AddSingleton(DataConfig.Load());
            services.AddSingleton<WowProcess>();
            services.AddSingleton<WowScreen>();
            services.AddSingleton<WowProcessInput>();
            services.AddSingleton<ExecGameCommand>();
            services.AddSingleton<AddonConfigurator>();

            services.AddSingleton<MinimapNodeFinder>();

            services.AddSingleton<IGrindSessionDAO, LocalGrindSessionDAO>();
            services.AddSingleton<IPPather>(x => GetPather(logger, x.GetRequiredService<DataConfig>(), options));

            services.AddSingleton<AutoResetEvent>(x => new(false));
            services.AddSingleton<DataFrame[]>(x => FrameConfig.LoadFrames());
            services.AddSingleton<AddonDataProvider>();
            services.AddSingleton<Wait>();

            services.AddSingleton<AreaDB>();
            services.AddSingleton<WorldMapAreaDB>();
            services.AddSingleton<ItemDB>();
            services.AddSingleton<CreatureDB>();
            services.AddSingleton<SpellDB>();
            services.AddSingleton<TalentDB>();

            services.AddSingleton<IAddonReader, AddonReader>();
            services.AddSingleton<IBotController, BotController>();
        }

        private static IPPather GetPather(Microsoft.Extensions.Logging.ILogger logger, DataConfig dataConfig, ParserResult<Options> options)
        {
            StartupConfigPathing scp = new(options.Value.Mode!,
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
    }
}