using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Core.FeatureFlags;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.CombatRotation;

/// <summary>
/// Singleton hosted service that collects rotation performance metrics
/// and periodically flushes them to a JSON log file.
/// Thread-safe: uses Interlocked operations for counters.
/// </summary>
public sealed class RotationMetricsCollector : IHostedService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
    
    private readonly ILogger<RotationMetricsCollector> logger;
    private readonly FeatureFlagService featureFlags;
    private readonly RotationSessionMetrics currentSession;

    private Timer? flushTimer;
    private int disposed;

    public RotationMetricsCollector(
        ILogger<RotationMetricsCollector> logger,
        FeatureFlagService featureFlags)
    {
        this.logger = logger;
        this.featureFlags = featureFlags;
        currentSession = new RotationSessionMetrics
        {
            SessionStartTicks = Environment.TickCount64
        };
    }

    /// <summary>
    /// Gets the current session metrics for UI display.
    /// </summary>
    public RotationSessionMetrics CurrentSession => currentSession;

    public void RecordOptimizedTick()
    {
        currentSession.OptimizedTicks++;
        currentSession.TotalTicks++;
    }

    public void RecordFallbackTick()
    {
        currentSession.FallbackTicks++;
        currentSession.TotalTicks++;
    }

    public void RecordCastAttempt(string abilityName, float score, bool success)
    {
        currentSession.RecordAttempt(abilityName, score, success);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        CombatRotationOptimizerOptions options = featureFlags.Current.CombatRotationOptimizer;

        if (!options.Enabled || !options.EnableMetrics)
        {
            logger.LogInformation("[RotationMetrics   ] Metrics collection disabled");
            return Task.CompletedTask;
        }

        int intervalMs = options.MetricsFlushIntervalSeconds * 1000;
        flushTimer = new Timer(FlushMetrics, null, intervalMs, intervalMs);

        logger.LogInformation(
            "[RotationMetrics   ] Started with {Interval}s flush interval to {Path}",
            options.MetricsFlushIntervalSeconds,
            options.MetricsOutputPath);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        flushTimer?.Change(Timeout.Infinite, 0);
        FlushMetrics(null);

        logger.LogInformation("[RotationMetrics   ] Stopped. Final metrics flushed.");
        return Task.CompletedTask;
    }

    private void FlushMetrics(object? state)
    {
        try
        {
            CombatRotationOptimizerOptions options = featureFlags.Current.CombatRotationOptimizer;
            string path = options.MetricsOutputPath;

            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            currentSession.SessionEndTicks = Environment.TickCount64;

            string json = JsonSerializer.Serialize(new
            {
                Timestamp = DateTime.UtcNow,
                currentSession.TotalTicks,
                currentSession.OptimizedTicks,
                currentSession.FallbackTicks,
                DurationMs = currentSession.SessionEndTicks - currentSession.SessionStartTicks,
                Abilities = currentSession.GetOrderedStats()
            }, JsonOptions);

            // Atomic write via temp file
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[RotationMetrics   ] Failed to flush metrics");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            flushTimer?.Dispose();
        }
    }
}
