using Core.Launch;
using Core.Startup;
using Core.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SharedLib;

namespace BlazorServer;

public static class DependencyInjection
{
    public static void AddStartupConfigurations(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StartupConfigPid>
            (configuration.GetSection(StartupConfigPid.Position));

        services.Configure<StartupConfigDiagnostics>
            (configuration.GetSection(StartupConfigDiagnostics.Position));

        services.Configure<StartupConfigPathing>
            (configuration.GetSection(StartupConfigPathing.Position));

        services.Configure<StartupConfigReader>
            (configuration.GetSection(StartupConfigReader.Position));

        services.Configure<StartupConfigNpcOverlay>
            (configuration.GetSection(StartupConfigNpcOverlay.Position));
    }

    /// <summary>
    /// Adds the startup orchestration services for automatic WoW launch,
    /// addon installation, and frame configuration.
    /// </summary>
    public static IServiceCollection AddStartupOrchestration(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Bind StartupOptions from configuration
        services.Configure<StartupOptions>(configuration.GetSection("Startup"));
        services.Configure<LaunchOptions>(configuration.GetSection("Launch"));

        // Core startup services
        services.AddSingleton<StartupState>();
        services.AddSingleton<WoWPathFinder>();
        services.AddSingleton<NavigationServerManager>();
        services.AddSingleton<WoWProcessLauncher>();
        services.AddSingleton<StartupOrchestrator>();

        // Background services
        services.AddHostedService<ProcessCleanupService>();
        services.AddHostedService<HealthMonitor>();
        services.AddHostedService<StartupHostedService>();

        return services;
    }
}
