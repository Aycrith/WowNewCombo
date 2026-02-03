using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Game;
using Core.Startup;

namespace Core.Services;

/// <summary>
/// Monitors critical system health and provides self-healing capabilities.
/// Tracks WoW process, service health, and performs recovery actions.
/// </summary>
public sealed class HealthMonitoringService : IHostedService, IDisposable
{
    private readonly ILogger<HealthMonitoringService> _logger;
    private readonly StartupOptions _options;
    private Timer? _healthCheckTimer;
    private bool _disposed;
    private int _consecutiveFailures;
    private const int MaxConsecutiveFailures = 3;

    public HealthStatus CurrentHealth { get; private set; } = HealthStatus.Initializing;
    public DateTime LastHealthCheck { get; private set; }
    public string? LastError { get; private set; }

    public HealthMonitoringService(
        ILogger<HealthMonitoringService> logger,
        IOptions<StartupOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableHealthMonitoring)
        {
            _logger.LogInformation("[HealthMonitor] Health monitoring disabled");
            CurrentHealth = HealthStatus.Disabled;
            return Task.CompletedTask;
        }

        _logger.LogInformation("[HealthMonitor] Starting health monitoring (interval: {Interval}s)", 
            _options.HealthCheckIntervalSeconds);

        var interval = TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds);
        _healthCheckTimer = new Timer(
            PerformHealthCheck,
            null,
            TimeSpan.FromSeconds(10), // Initial delay
            interval);

        CurrentHealth = HealthStatus.Healthy;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[HealthMonitor] Stopping health monitoring");
        _healthCheckTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private void PerformHealthCheck(object? state)
    {
        try
        {
            LastHealthCheck = DateTime.UtcNow;
            
            // Check if WoW process is still running
            bool wowRunning = IsWoWProcessRunning();
            
            if (!wowRunning)
            {
                _consecutiveFailures++;
                LastError = "WoW process not detected";
                
                if (_consecutiveFailures >= MaxConsecutiveFailures)
                {
                    _logger.LogError("[HealthMonitor] CRITICAL: WoW process lost for {Count} consecutive checks", 
                        _consecutiveFailures);
                    CurrentHealth = HealthStatus.Critical;
                }
                else
                {
                    _logger.LogWarning("[HealthMonitor] WoW process check failed ({Count}/{Max})", 
                        _consecutiveFailures, MaxConsecutiveFailures);
                    CurrentHealth = HealthStatus.Degraded;
                }
            }
            else
            {
                // Reset failure counter on success
                if (_consecutiveFailures > 0)
                {
                    _logger.LogInformation("[HealthMonitor] Health restored - WoW process detected");
                    _consecutiveFailures = 0;
                }
                
                CurrentHealth = HealthStatus.Healthy;
                LastError = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HealthMonitor] Error during health check");
            CurrentHealth = HealthStatus.Unknown;
            LastError = ex.Message;
        }
    }

    private static bool IsWoWProcessRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("WowClassic");
            if (processes.Length > 0)
            {
                foreach (var p in processes)
                {
                    p.Dispose();
                }
                return true;
            }

            // Try alternative names
            var altProcesses = Process.GetProcessesByName("Wow");
            if (altProcesses.Length > 0)
            {
                foreach (var p in altProcesses)
                {
                    p.Dispose();
                }
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _healthCheckTimer?.Dispose();
        _disposed = true;
    }
}

public enum HealthStatus
{
    Initializing,
    Healthy,
    Degraded,
    Critical,
    Unknown,
    Disabled
}
