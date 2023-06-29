using CommandLine;

using Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;

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

        ILogger<Program> log = logFactory.CreateLogger<Program>();

        log.LogInformation(
            $"{Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName} " +
            $"{DateTimeOffset.Now}");

        ParserResult<RunOptions> options =
            Parser.Default.ParseArguments<RunOptions>(args).WithNotParsed(a =>
        {
            log.LogError("Missing Required command line argument!");
        });

        if (options.Tag == ParserResultType.NotParsed)
        {
            Console.ReadLine();
            return;
        }

        services.AddSingleton<RunOptions>(options.Value);

        services.AddStartupConfigFactories();

        if (!FrameConfig.Exists() || !AddonConfig.Exists())
        {
            log.LogError("Unable to run headless server as crucial configuration files missing!");
            log.LogWarning($"Please be sure, the following validated configuration files present next to the executable:");
            log.LogWarning($"{Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}");
            log.LogWarning($"* {DataConfigMeta.DefaultFileName}");
            log.LogWarning($"* {FrameConfigMeta.DefaultFilename}");
            log.LogWarning($"* {AddonConfigMeta.DefaultFileName}");
            Console.ReadLine();
            return;
        }

        if (ConfigureServices(log, services))
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

    private static bool ConfigureServices(
        Microsoft.Extensions.Logging.ILogger log,
        IServiceCollection services)
    {
        if (!services.AddWoWProcess(log))
            return false;

        services.AddCoreBase();
        services.AddCoreNormal(log);

        return true;
    }
}