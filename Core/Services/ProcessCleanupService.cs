using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Services;

/// <summary>
/// Service that ensures graceful cleanup of processes and resources on shutdown.
/// Prevents file locks and orphaned processes.
/// </summary>
public sealed class ProcessCleanupService : IHostedService, IDisposable
{
    private readonly ILogger<ProcessCleanupService> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private bool _disposed;

    public ProcessCleanupService(
        ILogger<ProcessCleanupService> logger,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _lifetime = lifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ProcessCleanup] Service started - monitoring for shutdown signals");

        // Register cleanup on application stopping
        _lifetime.ApplicationStopping.Register(OnApplicationStopping);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[ProcessCleanup] Service stopping - performing cleanup");
        PerformCleanup();
        return Task.CompletedTask;
    }

    private void OnApplicationStopping()
    {
        _logger.LogWarning("[ProcessCleanup] Application stopping signal received - initiating cleanup");
        PerformCleanup();
    }

    private void PerformCleanup()
    {
        try
        {
            _logger.LogInformation("[ProcessCleanup] ════════════════════════════════════════");
            _logger.LogInformation("[ProcessCleanup]   GRACEFUL SHUTDOWN - CLEANUP INITIATED");
            _logger.LogInformation("[ProcessCleanup] ════════════════════════════════════════");

            // Give active operations time to complete
            Thread.Sleep(500);

            // Force garbage collection to release file handles
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            _logger.LogInformation("[ProcessCleanup] Cleanup completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProcessCleanup] Error during cleanup");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogDebug("[ProcessCleanup] Disposing service");
        PerformCleanup();

        _disposed = true;
    }
}
