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
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PPather;

using Serilog;
using Serilog.Events;
using Serilog.Templates.Themes;
using Serilog.Templates;

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
        ILoggerFactory logFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders().AddSerilog();
        });

        services.AddLogging(builder =>
        {
            LoggerSink sink = new();
            builder.Services.AddSingleton(sink);

            const string outputTemplate = "[{@t:HH:mm:ss:fff} {@l:u1}] {#if Length(SourceContext) > 0}[{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1),-15}] {#end}{@m}\n{@x}";

            Log.Logger = new LoggerConfiguration()
                //.MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Sink(sink)
                .WriteTo.File(new ExpressionTemplate(outputTemplate),
                    "out.log",
                    rollingInterval: RollingInterval.Day)
                .WriteTo.Debug(new ExpressionTemplate(outputTemplate))
                .WriteTo.Console(new ExpressionTemplate(outputTemplate, theme: TemplateTheme.Literate))
                .CreateLogger();

            builder.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(logFactory.CreateLogger(string.Empty));
        });

        var log = Log.Logger.ForContext<Startup>();

        log.Information($"{Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName} {DateTimeOffset.Now}");

        StartupConfigPid StartupConfigPid = new();
        Configuration.GetSection(StartupConfigPid.Position).Bind(StartupConfigPid);
        while (WowProcess.Get(StartupConfigPid.Id) == null)
        {
            log.Warning($"Unable to find any Wow process, is it running ?");
            Thread.Sleep(1000);
        }

        WowProcess wowProcess = new(StartupConfigPid.Id);
        log.Information($"Pid: {wowProcess.ProcessId}");
        log.Information($"Version: {wowProcess.FileVersion}");

        NativeMethods.GetWindowRect(wowProcess.Process.MainWindowHandle, out Rectangle rect);

        var logger = logFactory.CreateLogger<AddonConfigurator>();

        AddonConfigurator addonConfigurator = new(logger, wowProcess);
        Version? installVersion = addonConfigurator.GetInstallVersion();
        log.Information($"Addon version: {installVersion}");

        if (addonConfigurator.IsDefault() || installVersion == null)
        {
            // At this point the webpage never loads so fallback to configuration page
            addonConfigurator.Delete();
            FrameConfig.Delete();

            log.Error($"{nameof(AddonConfig)} doesn't exists or addon not installed yet!");
        }

        if (FrameConfig.Exists() && !FrameConfig.IsValid(rect, installVersion!))
        {
            // At this point the webpage never loads so fallback to configuration page
            FrameConfig.Delete();
            log.Error($"{nameof(FrameConfig)} doesn't exists or window rect is different then config!");
        }

        wowProcess.Dispose();

        services.AddSingleton<CancellationTokenSource>();
        services.AddSingleton<AutoResetEvent>(x => new(false));
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
                GetPather(logFactory, x.GetRequiredService<Microsoft.Extensions.Logging.ILogger>(),
                x.GetRequiredService<DataConfig>(), x.GetRequiredService<WorldMapAreaDB>()));

            StartupConfigReader scr = new();
            Configuration.GetSection(StartupConfigReader.Position).Bind(scr);

            switch (scr.ReaderType)
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
            services.AddSingleton<WorldMapAreaDB>();
            services.AddSingleton<ItemDB>();
            services.AddSingleton<CreatureDB>();
            services.AddSingleton<SpellDB>();
            services.AddSingleton<TalentDB>();

            services.AddSingleton<PlayerReader>();
            services.AddSingleton<IAddonReader, AddonReader>();

            services.AddSingleton<LevelTracker>();

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

    private IPPather GetPather(ILoggerFactory loggerFactory, Microsoft.Extensions.Logging.ILogger logger,
        DataConfig dataConfig, WorldMapAreaDB worldMapAreaDB)
    {
        StartupConfigPathing scp = new();
        Configuration.GetSection(StartupConfigPathing.Position).Bind(scp);

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