using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Startup;

/// <summary>
/// Background service that monitors system health and can auto-restart crashed components.
/// Checks WoW process, navigation server, and bot state at regular intervals.
/// </summary>
public sealed class HealthMonitor : BackgroundService
{
    private readonly ILogger<HealthMonitor> _logger;
    private readonly StartupOptions _options;
    private readonly StartupState _state;
    private readonly NavigationServerManager _navManager;
    private readonly WoWProcessLauncher _wowLauncher;

    private DateTime _lastWoWWarning = DateTime.MinValue;
    private DateTime _lastNavWarning = DateTime.MinValue;

    public HealthMonitor(
        ILogger<HealthMonitor> logger,
        IOptions<StartupOptions> options,
        StartupState state,
        NavigationServerManager navManager,
        WoWProcessLauncher wowLauncher)
    {
        _logger = logger;
        _options = options.Value;
        _state = state;
        _navManager = navManager;
        _wowLauncher = wowLauncher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableHealthMonitoring)
        {
            _logger.LogInformation("[HealthMonitor] Health monitoring disabled");
            return;
        }

        _logger.LogInformation("[HealthMonitor] Starting health monitor (interval: {Interval}s)",
            _options.HealthCheckIntervalSeconds);

        // Wait for initial startup to complete before monitoring
        while (!_state.IsReady && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }

        // Main monitoring loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthCheckAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HealthMonitor] Error during health check");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("[HealthMonitor] Health monitor stopped");
    }

    private async Task PerformHealthCheckAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Check WoW process
        await CheckWoWHealthAsync(now);

        // Check navigation server
        await CheckNavigationServerHealthAsync(now, cancellationToken);
    }

    private Task CheckWoWHealthAsync(DateTime now)
    {
        if (!_wowLauncher.IsRunning())
        {
            // Only warn once per minute to avoid log spam
            if ((now - _lastWoWWarning).TotalMinutes >= 1)
            {
                _logger.LogWarning("[HealthMonitor] WoW process is not running");
                _lastWoWWarning = now;

                _state.StatusMessage = "WoW process not detected";
            }

            // Note: We don't auto-restart WoW as it requires user authentication
        }
        else
        {
            // WoW is running, clear any previous warning state
            if (_lastWoWWarning != DateTime.MinValue)
            {
                _logger.LogInformation("[HealthMonitor] WoW process restored");
                _lastWoWWarning = DateTime.MinValue;
            }
        }

        return Task.CompletedTask;
    }

    private async Task CheckNavigationServerHealthAsync(DateTime now, CancellationToken cancellationToken)
    {
        if (!_options.AutoStartNavigationServer)
            return;

        if (!_navManager.IsInstalled)
            return;

        // Don't keep hammering the restart if it's in a failed state - NavigationServerManager handles retry limiting
        if (_navManager.Status == NavigationServerStatus.Failed)
        {
            if ((now - _lastNavWarning).TotalMinutes >= 5)
            {
                _logger.LogWarning("[HealthMonitor] Navigation server is in failed state. Check configuration and logs.");
                _lastNavWarning = now;
            }
            return;
        }

        var isHealthy = await _navManager.IsHealthyAsync();

        if (!isHealthy)
        {
            if ((now - _lastNavWarning).TotalMinutes >= 1)
            {
                _logger.LogWarning("[HealthMonitor] Navigation server is not responding");
                _lastNavWarning = now;
            }

            // NavigationServerManager.EnsureRunningAsync handles restart limiting
            _logger.LogInformation("[HealthMonitor] Attempting to restart navigation server...");
            var success = await _navManager.EnsureRunningAsync(cancellationToken);

            if (success)
            {
                _logger.LogInformation("[HealthMonitor] Navigation server restarted successfully");
                _lastNavWarning = DateTime.MinValue;
            }
            // Don't log failure here - NavigationServerManager already logs it
        }
        else
        {
            if (_lastNavWarning != DateTime.MinValue)
            {
                _logger.LogInformation("[HealthMonitor] Navigation server restored");
                _lastNavWarning = DateTime.MinValue;
            }
        }
    }
}
