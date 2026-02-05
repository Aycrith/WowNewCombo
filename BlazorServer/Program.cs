using Core;

using Frontend;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;

using SharedLib.Converters;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace BlazorServer;

public static class Program
{
    public static void Main(string[] args)
    {
        const int maxRestartsInWindow = 5;
        var restartWindow = TimeSpan.FromMinutes(10);
        List<DateTimeOffset> crashTimes = [];

        while (true)
        {
            bool shutdownRequested = false;

            try
            {
                Log.Information("[Program          ] Starting blazor server");
                var host = CreateApp(args);
                var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
                lifetime.ApplicationStopping.Register(() =>
                {
                    shutdownRequested = true;
                    Log.Warning("[Program          ] Graceful shutdown requested");
                });

                host.Run();
            }
            catch (Exception ex)
            {
                if (shutdownRequested)
                {
                    // We were stopping anyway; don't restart-loop just because Dispose threw.
                    Log.Error(ex, "[Program          ] Exception during shutdown; exiting without restart");
                    CrashReporter.TryWrite(ex, shutdownRequested: true);
                    break;
                }

                // Check if this is a "WoW not running" error - don't restart loop for this
                if (ex.Message.Contains("World of Warcraft process") || 
                    ex.InnerException?.Message.Contains("World of Warcraft process") == true)
                {
                    Log.Fatal("[Program          ] WoW is required to run the bot. Exiting.");
                    CrashReporter.TryWrite(ex, shutdownRequested: false);
                    break;
                }

                Log.Fatal(ex, "[Program          ] Host crashed – restarting in 3s");
                CrashReporter.TryWrite(ex, shutdownRequested: false);
                crashTimes.Add(DateTimeOffset.UtcNow);

                // Trim crashes outside the window
                crashTimes.RemoveAll(t => (DateTimeOffset.UtcNow - t) > restartWindow);

                if (crashTimes.Count > maxRestartsInWindow)
                {
                    Log.Fatal("[Program          ] Too many crashes ({Count}) within {Window}; exiting to avoid restart loop",
                        crashTimes.Count, restartWindow);
                    break;
                }

                // Exponential backoff (3s → 6s → 12s → 24s → 48s → 60s)
                int exponent = Math.Clamp(crashTimes.Count - 1, 0, 10);
                int delaySeconds = Math.Min(3 * (1 << exponent), 60);

                Log.Warning("[Program          ] Restarting in {DelaySeconds}s (crash {Crash}/{Max} in {Window})",
                    delaySeconds, crashTimes.Count, maxRestartsInWindow, restartWindow);

                Thread.Sleep(delaySeconds * 1000);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }

    private static WebApplication CreateApp(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // When running directly from `bin/Release/net10.0` (OneClickLauncher default),
        // static web assets from referenced projects (Frontend) are not copied to `wwwroot`.
        // Load the static-web-assets manifest so `_content/Frontend/...` scripts/styles resolve.
        try
        {
            builder.WebHost.UseStaticWebAssets();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Program          ] UseStaticWebAssets failed: {ex.Message}");
        }

        builder.Logging.ClearProviders().AddSerilog();

        // Ensure Kestrel binds to the configured Web UI port when no explicit URL binding is provided.
        // This keeps OneClickLauncher.ps1's Startup__WebUIPort override aligned with the actual server port.
        string? urls = builder.Configuration["ASPNETCORE_URLS"];
        if (string.IsNullOrWhiteSpace(urls))
        {
            int port = builder.Configuration.GetSection("Startup").GetValue<int>("WebUIPort", 5000);
            builder.WebHost.UseUrls($"http://localhost:{port}");
        }

        ConfigureServices(builder.Configuration, builder.Services);

        return ConfigureApp(builder, builder.Environment);
    }

    private static void ConfigureServices(IConfiguration configuration, IServiceCollection services)
    {
        ILoggerFactory logFactory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders().AddSerilog();
        });

        services.AddLogging(builder =>
        {
            LoggerSink sink = new();
            builder.Services.AddSingleton(sink);

            const string outputTemplate = "[{@t:HH:mm:ss:fff} {@l:u1}] {#if Length(SourceContext) > 0}[{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1),-17}] {#end}{@m}\n{@x}";
            //const string outputTemplate = "[{@t:HH:mm:ss:fff} {@l:u1}] {SourceContext}] {@m}\n{@x}";

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
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

        Microsoft.Extensions.Logging.ILogger log = logFactory.CreateLogger("Program");

        log.LogInformation(
            $"{Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName} " +
            $"{DateTimeOffset.Now}");

        services.AddStartupConfigurations(configuration);

        // Add startup orchestration first (handles WoW launch, nav server, etc.)
        services.AddStartupOrchestration(configuration);

        // AddWoWProcess now supports configuration mode - it won't throw if WoW isn't running
        // Instead, WowProcess will poll for WoW to be launched
        // wowIsRunning: true if WoW process is currently running (needed for screen capture, input, etc.)
        // configurationComplete: true if addon and frame config are valid
        services.AddWoWProcess(log, out bool wowIsRunning, out bool configurationComplete);

        // Register WoW-dependent services only if WoW is running
        services.AddCoreBase(wowIsRunning);

        if (configurationComplete)
        {
            services.AddCoreNormal(log);
        }
        else
        {
            services.AddCoreConfiguration(log);
        }

        services.AddFrontend();

        services.AddCoreFrontend();

        services.AddSingleton(provider =>
            provider.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions);

        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.Converters.Add(new Vector3Converter());
            options.SerializerOptions.Converters.Add(new Vector4Converter());
        });

        services.Configure<HostOptions>(o =>
        {
            o.ShutdownTimeout = TimeSpan.FromSeconds(1);
        });

        // Register mDNS advertising service for http://wowbot.local access
        if(Environment.GetEnvironmentVariable("USE_MDNS") != null)
        {
            services.AddHostedService<MdnsAdvertisingService>();
        }

        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            options.JsonSerializerOptions.Converters.Add(new Vector3Converter());
            options.JsonSerializerOptions.Converters.Add(new Vector4Converter());
        });

        services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private static WebApplication ConfigureApp(WebApplicationBuilder builder, IWebHostEnvironment env)
    {
        WebApplication app = builder.Build();

        // Required for `@Assets[...]` references in App.razor in newer ASP.NET Core releases.
        // This is safe to call even when the static-assets endpoints are not present.
        try
        {
            app.MapStaticAssets();
        }
        catch
        {
            // Older runtime or stripped build; fall back to classic static-file hosting.
        }

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();

        app.UseCustomStaticFiles(env);

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(typeof(Frontend._Imports).Assembly);

        app.UseRouting();

        app.UseAntiforgery();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });

        return app;
    }

}
