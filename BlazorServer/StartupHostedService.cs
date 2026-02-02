using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Core.Startup;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorServer;

/// <summary>
/// Background service that runs the startup orchestration when the application starts.
/// Handles WoW discovery, addon installation, navigation server, and frame configuration.
/// </summary>
public sealed class StartupHostedService : BackgroundService
{
    private readonly ILogger<StartupHostedService> _logger;
    private readonly StartupOrchestrator _orchestrator;
    private readonly StartupOptions _options;
    private readonly IHostApplicationLifetime _lifetime;

    public StartupHostedService(
        ILogger<StartupHostedService> logger,
        StartupOrchestrator orchestrator,
        IOptions<StartupOptions> options,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _orchestrator = orchestrator;
        _options = options.Value;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a moment for the web server to fully start
        await Task.Delay(1000, stoppingToken);

        _logger.LogInformation("[StartupHostedService] ========================================");
        _logger.LogInformation("[StartupHostedService] Starting orchestration service");
        _logger.LogInformation("[StartupHostedService] ========================================");

        try
        {
            // Open browser if configured (do this before orchestration so user sees progress)
            if (_options.AutoOpenBrowser)
            {
                OpenBrowser();
            }

            // Run the startup orchestration
            var result = await _orchestrator.RunAsync(stoppingToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "[StartupHostedService] Startup orchestration completed successfully in {Elapsed:mm\\:ss\\.fff}",
                    result.TotalDuration);
            }
            else
            {
                _logger.LogWarning(
                    "[StartupHostedService] Startup orchestration completed with issues: {Message}",
                    result.Message);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[StartupHostedService] Startup orchestration cancelled due to shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StartupHostedService] Startup orchestration failed with exception");

            // Don't crash the whole app, just log the error
            // The user can still use the WebUI to manually configure things
        }

        _logger.LogInformation("[StartupHostedService] Orchestration service completed, entering idle state");

        // Keep the service running (it's monitoring via HealthMonitor)
        // This service just kicks off the orchestration and then idles
    }

    private void OpenBrowser()
    {
        try
        {
            var url = $"http://localhost:{_options.WebUIPort}";
            _logger.LogInformation("[StartupHostedService] Opening browser to {Url}", url);

            // Use the default browser to open the URL
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[StartupHostedService] Failed to open browser automatically");
            // Non-fatal, user can navigate manually
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[StartupHostedService] Stopping startup hosted service");
        await base.StopAsync(cancellationToken);
    }
}
