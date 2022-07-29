using BlazorTable;
using Core;
using Core.Addon;
using Core.Database;
using Core.Environment;
using Core.Session;
using Game;
using MatBlazor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PPather;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using WinAPI;

namespace BlazorServer
{
    public class Startup
    {
        private static StartupConfigPid StartupConfigPid = new();

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            var logfile = "out.log";
            var config = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .WriteTo.LoggerSink()
                .WriteTo.File(logfile, rollingInterval: RollingInterval.Day)
                .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss:fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss:fff} {Level:u3}] {Message:lj}{NewLine}{Exception}");

            Log.Logger = config.CreateLogger();
            Log.Logger.Information("Startup()");

            Configuration.GetSection(StartupConfigPid.Position).Bind(StartupConfigPid);

            while (WowProcess.Get(StartupConfigPid.Id) == null)
            {
                Log.Information("Unable to find any Wow process, is it running ?");
                Thread.Sleep(1000);
            }

            if (StartupConfigPid.Id > -1)
                Log.Logger.Information($"Startup() Attached pid={StartupConfigPid.Id}");
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var logger = new SerilogLoggerProvider(Log.Logger).CreateLogger(nameof(Program));
            services.AddSingleton(logger);

            WowProcess wowProcess = new(StartupConfigPid.Id);
            NativeMethods.GetWindowRect(wowProcess.Process.MainWindowHandle, out Rectangle rect);

            AddonConfigurator addonConfigurator = new(logger, wowProcess);
            Version? installVersion = addonConfigurator.GetInstallVersion();

            if (addonConfigurator.IsDefault() || installVersion == null)
            {
                // At this point the webpage never loads so fallback to configuration page
                addonConfigurator.Delete();
                FrameConfig.Delete();
            }

            if (FrameConfig.Exists() && !FrameConfig.IsValid(rect, installVersion!))
            {
                // At this point the webpage never loads so fallback to configuration page
                FrameConfig.Delete();
            }

            wowProcess.Dispose();

            services.AddSingleton<CancellationTokenSource>();

            services.AddSingleton(DataConfig.Load());
            services.AddSingleton<WowProcess>(x => new(StartupConfigPid.Id));
            services.AddSingleton<WowScreen>();
            services.AddSingleton<WowProcessInput>();
            services.AddSingleton<ExecGameCommand>();
            services.AddSingleton<AddonConfigurator>();
            services.AddSingleton<FrameConfigurator>();

            services.AddSingleton<IEnvironment, BlazorFrontend>();

            if (AddonConfig.Exists() && FrameConfig.Exists())
            {
                services.AddSingleton<MinimapNodeFinder>();

                services.AddSingleton<IGrindSessionDAO, LocalGrindSessionDAO>();
                services.AddSingleton<IPPather>(x => GetPather(logger, x.GetRequiredService<DataConfig>()));

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
            else
            {
                services.AddSingleton<IBotController, ConfigBotController>();
                services.AddSingleton<IAddonReader, ConfigAddonReader>();
            }

            services.AddMatBlazor();
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddBlazorTable();
        }

        private IPPather GetPather(Microsoft.Extensions.Logging.ILogger logger, DataConfig dataConfig)
        {
            StartupConfigPathing scp = new();
            Configuration.GetSection(StartupConfigPathing.Position).Bind(scp);

            bool failed = false;

            if (scp.Type == StartupConfigPathing.Types.RemoteV3)
            {
                var api = new RemotePathingAPIV3(logger, scp.hostv3, scp.portv3, new WorldMapAreaDB(dataConfig));
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
                var api = new RemotePathingAPI(logger, scp.hostv1, scp.portv1);
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

            var localApi = new LocalPathingApi(logger, new PPatherService(logger, dataConfig), dataConfig);
            Log.Information($"Using {StartupConfigPathing.Types.Local}({localApi.GetType().Name}) pathing API.");
            Log.Information("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
            return localApi;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }
    }
}