using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using BlazorTable;

using Core;
using Core.Addon;
using Core.Database;
using Core.Session;

using Game;

using MatBlazor;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PPather;

using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

using SharedLib;

using WinAPI;

namespace BlazorServer;

public sealed class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            LoggerSink sink = new();
            builder.Services.AddSingleton(sink);

            const string outputTemplate = "[{Timestamp:HH:mm:ss:fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .WriteTo.Sink(sink)
                .WriteTo.File("out.log",
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

        Log.Information($"[{nameof(Startup)}] {Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName} {DateTimeOffset.Now}");

        StartupConfigPid StartupConfigPid = new();
        Configuration.GetSection(StartupConfigPid.Position).Bind(StartupConfigPid);
        while (WowProcess.Get(StartupConfigPid.Id) == null)
        {
            Log.Warning($"[{nameof(Startup)}] Unable to find any Wow process, is it running ?");
            Thread.Sleep(1000);
        }

        WowProcess wowProcess = new(StartupConfigPid.Id);
        Log.Information($"[{nameof(Startup)}] Pid: {wowProcess.ProcessId}");
        Log.Information($"[{nameof(Startup)}] Version: {wowProcess.FileVersion}");

        NativeMethods.GetWindowRect(wowProcess.Process.MainWindowHandle, out Rectangle rect);

        var logger = new SerilogLoggerProvider(Log.Logger, true).CreateLogger(nameof(AddonConfigurator));
        AddonConfigurator addonConfigurator = new(logger, wowProcess);
        Version? installVersion = addonConfigurator.GetInstallVersion();
        Log.Information($"[{nameof(Program)}] Addon version: {installVersion}");

        if (addonConfigurator.IsDefault() || installVersion == null)
        {
            // At this point the webpage never loads so fallback to configuration page
            addonConfigurator.Delete();
            FrameConfig.Delete();

            Log.Error($"[{nameof(Startup)}] {nameof(AddonConfig)} doesn't exists or addon not installed yet!");
        }

        if (FrameConfig.Exists() && !FrameConfig.IsValid(rect, installVersion!))
        {
            // At this point the webpage never loads so fallback to configuration page
            FrameConfig.Delete();
            Log.Error($"[{nameof(Startup)}] {nameof(FrameConfig)} doesn't exists or window rect is different then config!");
        }

        wowProcess.Dispose();

        services.AddSingleton<CancellationTokenSource>();
        services.AddSingleton<ManualResetEventSlim>(x => new(false));
        services.AddSingleton<Wait>();

        services.AddSingleton<WowProcess>(x => new(StartupConfigPid.Id));
        services.AddSingleton<StartupClientVersion>();
        services.AddSingleton<DataConfig>(x => DataConfig.Load(x.GetRequiredService<StartupClientVersion>().Path));
        services.AddSingleton<WowScreen>();
        services.AddSingleton<WowProcessInput>();
        services.AddSingleton<ExecGameCommand>();
        services.AddSingleton<AddonConfigurator>();

        services.AddSingleton<DataFrame[]>(x => FrameConfig.LoadFrames());
        services.AddSingleton<FrameConfigurator>();

        if (AddonConfig.Exists() && FrameConfig.Exists())
        {
            services.AddSingleton<MinimapNodeFinder>();

            StartupConfigDiagnostics StartupDiagnostics = new();
            Configuration.GetSection(StartupConfigDiagnostics.Position).Bind(StartupDiagnostics);

            if (StartupDiagnostics.Enabled)
            {
                services.AddSingleton<IScreenCapture, ScreenCapture>();
                Log.Information($"[{nameof(Startup)}] {nameof(ScreenCapture)}");
            }
            else
            {
                services.AddSingleton<IScreenCapture, NoScreenCapture>();
                Log.Information($"[{nameof(Startup)}] {nameof(NoScreenCapture)}");
            }

            services.AddSingleton<IGrindSessionDAO, LocalGrindSessionDAO>();
            services.AddSingleton<WorldMapAreaDB>();
            services.AddSingleton<IPPather>(x =>
                GetPather(x.GetRequiredService<Microsoft.Extensions.Logging.ILogger>(),
                x.GetRequiredService<DataConfig>(), x.GetRequiredService<WorldMapAreaDB>()));

            StartupConfigReader scr = new();
            Configuration.GetSection(StartupConfigReader.Position).Bind(scr);

            if (scr.ReaderType == AddonDataProviderType.DXGI)
            {
                services.AddSingleton<IAddonDataProvider, AddonDataProviderDXGI>();
                Log.Information($"[{nameof(Startup)}] {nameof(AddonDataProviderDXGI)}");
            }
            if (scr.ReaderType == AddonDataProviderType.DXGISwapChain)
            {
                services.AddSingleton<IAddonDataProvider, AddonDataProviderDXGISwapChain>();
                Log.Information($"[{nameof(Startup)}] {nameof(AddonDataProviderDXGISwapChain)}");
            }
            else if (scr.ReaderType is AddonDataProviderType.GDI)
            {
                services.AddSingleton<IAddonDataProvider, AddonDataProviderGDI>();
                Log.Information($"[{nameof(Startup)}] {nameof(AddonDataProviderGDI)}");
            }

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
            services.AddSingleton<IAddonDataProvider, AddonDataProviderGDIConfig>();
            services.AddSingleton<IBotController, ConfigBotController>();
            services.AddSingleton<IAddonReader, ConfigAddonReader>();
        }

        services.AddSingleton<WApi>();
        services.AddSingleton<FrontendUpdate>();

        services.AddMatBlazor();
        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddBlazorTable();

        services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private IPPather GetPather(Microsoft.Extensions.Logging.ILogger logger, DataConfig dataConfig, WorldMapAreaDB worldMapAreaDB)
    {
        StartupConfigPathing scp = new();
        Configuration.GetSection(StartupConfigPathing.Position).Bind(scp);

        bool failed = false;

        if (scp.Type == StartupConfigPathing.Types.RemoteV3)
        {
            RemotePathingAPIV3 api = new(logger, scp.hostv3, scp.portv3, worldMapAreaDB);
            if (api.PingServer())
            {
                Log.Information($"[{nameof(Startup)}] Using {StartupConfigPathing.Types.RemoteV3}({api.GetType().Name}) {scp.hostv3}:{scp.portv3}");
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
                    Log.Warning($"[{nameof(Startup)}] Unavailable {StartupConfigPathing.Types.RemoteV3} {scp.hostv3}:{scp.portv3} - Fallback to {StartupConfigPathing.Types.RemoteV1}");
                }

                Log.Information($"[{nameof(Startup)}] Using {StartupConfigPathing.Types.RemoteV1}({api.GetType().Name}) {scp.hostv1}:{scp.portv1}");
                return api;
            }

            failed = true;
        }

        if (scp.Type != StartupConfigPathing.Types.Local)
        {
            Log.Warning($"[{nameof(Startup)}] {scp.Type} not available!");
        }

        LocalPathingApi localApi = new(logger, new PPatherService(logger, dataConfig, worldMapAreaDB), dataConfig);
        Log.Information($"[{nameof(Startup)}] Using {StartupConfigPathing.Types.Local}({localApi.GetType().Name})");

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

        var dataConfig = DataConfig.Load();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, dataConfig.Path)),
            RequestPath = "/path"
        });

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapBlazorHub();
            endpoints.MapFallbackToPage("/_Host");
        });
    }
}