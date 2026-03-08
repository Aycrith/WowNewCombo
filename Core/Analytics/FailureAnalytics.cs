using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Core.Autonomy;
using Core.FeatureFlags;
using Core.GOAP;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Analytics;

/// <summary>
/// Hosted service that manages the FailureAnalytics engine lifecycle.
/// Previously contained all business logic - now delegates to FailureAnalyticsEngine.
/// </summary>
public sealed class FailureAnalytics : IHostedService, IDisposable
{
    private readonly ILogger<FailureAnalytics> logger;
    private readonly FeatureFlagService featureFlags;
    private readonly FailureAnalyticsEngine engine;

    private Timer? flushTimer;

    public FailureAnalytics(
        ILogger<FailureAnalytics> logger,
        FeatureFlagService featureFlags,
        FailureAnalyticsEngine engine)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!featureFlags.Current.FailureAnalytics.Enabled)
        {
            logger.LogInformation("[FailureAnalytics  ] Disabled by feature flag");
            return Task.CompletedTask;
        }

        // Start flush timer
        int intervalMs = featureFlags.Current.FailureAnalytics.FlushIntervalMinutes * 60 * 1000;
        flushTimer = new Timer(_ => engine.FlushToDisk(), null, intervalMs, intervalMs);

        logger.LogInformation("[FailureAnalytics  ] Started with {Interval}min flush interval",
            featureFlags.Current.FailureAnalytics.FlushIntervalMinutes);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        flushTimer?.Change(Timeout.Infinite, 0);
        engine.FlushToDisk();

        logger.LogInformation("[FailureAnalytics  ] Stopped. Final data flushed.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets failure statistics for the current session (delegates to engine).
    /// </summary>
    public FailureStatistics GetSessionStatistics()
    {
        return engine.GetSessionStatistics();
    }

    public IReadOnlyList<AutonomyIncident> GetRecentIncidents()
    {
        return engine.GetRecentIncidents();
    }

    /// <summary>
    /// Records a death event (delegates to engine).
    /// </summary>
    public void RecordDeath(string? cause = null)
    {
        engine.RecordDeath(cause);
    }

    /// <summary>
    /// Records a failed pull attempt (delegates to engine).
    /// </summary>
    public void RecordFailedPull(int targetGuid, string reason)
    {
        engine.RecordFailedPull(targetGuid, reason);
    }

    /// <summary>
    /// Records a multi-mob retreat (delegates to engine).
    /// </summary>
    public void RecordMultiMobRetreat(int mobCount)
    {
        engine.RecordMultiMobRetreat(mobCount);
    }

    public AutonomyIncident RecordOperationalFailure(
        FailureType type,
        string reason,
        string subsystem,
        string source = "runtime",
        Dictionary<string, object>? additionalData = null)
    {
        return engine.RecordIncident(type, reason, subsystem, source, additionalData: additionalData);
    }


    public void Dispose()
    {
        flushTimer?.Dispose();
        engine?.Dispose();
    }
}

/// <summary>
/// Types of failures tracked by analytics.
/// </summary>
public enum FailureType
{
    Stuck,
    Death,
    FailedPull,
    NoPlan,
    MultiMobRetreat,
    Disconnect,
    Timeout,
    LaunchReadiness,
    FocusRestore,
    CombatTargetLoss,
    LootFailure,
    BotInactive,
    ServiceInterruption,
    ProcessExit
}

/// <summary>
/// A single failure event.
/// </summary>
public sealed class FailureEvent
{
    public DateTime Timestamp { get; set; }
    public FailureType Type { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? TargetGuid { get; set; }
    public System.Numerics.Vector3 Position { get; set; }
    public int MapId { get; set; }
    public string Zone { get; set; } = string.Empty;
    public int Level { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
    public string? CorrelationId { get; set; }
    public string? IncidentId { get; set; }
    public string Source { get; set; } = "runtime";
    public ScreenshotRef? Screenshot { get; set; }
}

/// <summary>
/// A geographic hotspot of failures.
/// </summary>
public sealed class FailureHotZone
{
    public System.Numerics.Vector3 Center { get; set; }
    public int MapId { get; set; }
    public int FailureCount { get; set; }
    public FailureType PrimaryType { get; set; }
    public DateTime LastFailure { get; set; }
}

/// <summary>
/// Failure statistics for a session.
/// </summary>
public sealed class FailureStatistics
{
    public int TotalFailures { get; set; }
    public Dictionary<FailureType, int> EventsByType { get; set; } = new();
    public List<FailureHotZone> HotZones { get; set; } = new();
    public List<FailureEvent> RecentEvents { get; set; } = new();
    public List<AutonomyIncident> RecentIncidents { get; set; } = new();
}
