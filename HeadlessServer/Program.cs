using CommandLine;

using Core;
using Core.CombatRotation;
using Core.Extensions;
using Core.Hazard;
using Core.Humanization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;

using System.Collections.Generic;

namespace HeadlessServer;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        string runtimeFeatureFlagsPath = ResolveRuntimeFeatureFlagsPath("HeadlessServer");

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("headless_appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"headless_appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
            // Runtime overrides / feature flags (written by the Web UI in BlazorServer, or edited manually).
            .AddJsonFile(runtimeFeatureFlagsPath, optional: true, reloadOnChange: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:ConfigFilePath"] = runtimeFeatureFlagsPath
            })
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        IServiceCollection services = new ServiceCollection();

        ILoggerFactory logFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders().AddSerilog();
        });

        services.AddLogging(builder =>
        {
            const string outputTemplate = "[{@t:HH:mm:ss:fff} {@l:u1}] {#if Length(SourceContext) > 0}[{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1),-17}] {#end}{@m}\n{@x}";
            //const string outputTemplate = "[{@t:HH:mm:ss:fff} {@l:u1}] {SourceContext}] {@m}\n{@x}";

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
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

        ILogger<Program> log = logFactory.CreateLogger<Program>();

        log.LogInformation($"Hosting environment: {environmentName ?? "Production"}");

        log.LogInformation(
            $"{Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName} " +
            $"{DateTimeOffset.Now}");

        ParserResult<RunOptions> options =
            Parser.Default.ParseArguments<RunOptions>(args).WithNotParsed(errors =>
        {
            foreach (Error? e in errors)
            {
                log.LogError($"{e}");
            }
        });

        if (options.Tag == ParserResultType.NotParsed)
        {
            goto Exit;
        }

        services.AddSingleton<RunOptions>(options.Value);

        // Runtime feature flags (hot-reload via runtime_feature_flags.json)
        services.Configure<MountUnlockOptions>(configuration.GetSection(MountUnlockOptions.Position));

        services.AddStartupConfigFactories();

        if (!FrameConfig.Exists() || !AddonConfig.Exists())
        {
            log.LogError($"Unable to run {nameof(HeadlessServer)} as crucial configuration files were missing!");
            log.LogWarning($"Please be sure, the following validated configuration files present next to the executable:");
            log.LogWarning($"{Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}");
            log.LogWarning($"* {DataConfigMeta.DefaultFileName}");
            log.LogWarning($"* {FrameConfigMeta.DefaultFilename}");
            log.LogWarning($"* {AddonConfigMeta.DefaultFileName}");
            goto Exit;
        }

        if (!ConfigureServices(log, services, configuration))
        {
            goto Exit;
        }

        ServiceProvider provider = services
            .AddSingleton<HeadlessServer>()
            .BuildServiceProvider(new ServiceProviderOptions() { ValidateOnBuild = true });

        var logger =
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger>();

        AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs args) =>
        {
            Exception e = (Exception)args.ExceptionObject;
            logger.LogError(e, e.Message);
        };

        HeadlessServer headlessServer = provider.GetRequiredService<HeadlessServer>();

        if (options.Value.LoadOnly)
        {
            headlessServer.RunLoadOnly(options);
            Environment.Exit(0);
        }
        else
        {
            headlessServer.Run(options);
        }

    Exit:
        Console.ReadKey();
    }

    private static string ResolveRuntimeFeatureFlagsPath(string projectFolderName)
    {
        string[] candidates =
        [
            "runtime_feature_flags.json",
            Path.Combine(projectFolderName, "runtime_feature_flags.json"),
            Path.Combine(AppContext.BaseDirectory, "runtime_feature_flags.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "runtime_feature_flags.json")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", projectFolderName, "runtime_feature_flags.json"))
        ];

        for (int i = 0; i < candidates.Length; i++)
        {
            if (File.Exists(candidates[i]))
            {
                return candidates[i];
            }
        }

        return "runtime_feature_flags.json";
    }

    private static bool ConfigureServices(
        Microsoft.Extensions.Logging.ILogger log,
        IServiceCollection services,
        IConfiguration configuration)
    {
        if (!services.AddWoWProcess(log))
            return false;

        services.AddCoreBase();
        services.AddCoreNormal(log);

        // Phase 1/2 feature systems (disabled by default, opt-in via runtime_feature_flags.json)
        services.AddPhase1Features(configuration);
        services.AddHazardAvoidance();
        services.AddHumanizationServices();

        // Phase 2 (AI Profile Generator & Profile Marketplace) - feature-flagged, disabled by default.
        services.AddPhase2Features(configuration);

        // Phase 3 (Behavior Trees, Hybrid LLM) - feature-flagged, disabled by default.
        services.AddPhase3Features();

        // Combat Rotation Optimizer - disabled by default; safe to register always.
        services.AddCombatRotationOptimizer();

        return true;
    }
}
