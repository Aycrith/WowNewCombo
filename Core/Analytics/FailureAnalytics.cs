using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Core.FeatureFlags;
using Core.GOAP;
using Core.GoalsComponent;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Analytics;

/// <summary>
/// Analytics service that tracks bot failures and generates insights.
/// Persists data to disk for long-term analysis.
/// </summary>
public sealed class FailureAnalytics : IHostedService, IDisposable, IGoapEventListener
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<FailureAnalytics> logger;
    private readonly FeatureFlagService featureFlags;
    private readonly PlayerReader playerReader;
    private readonly StuckDetector? stuckDetector;
    private readonly GoapAgent? goapAgent;
    private readonly TimeProvider timeProvider;

    private readonly List<FailureEvent> sessionEvents = new();
    private readonly object eventLock = new();

    private readonly string persistencePath;
    private Timer? flushTimer;
    private long totalFailures;

    public FailureAnalytics(
        ILogger<FailureAnalytics> logger,
        FeatureFlagService featureFlags,
        PlayerReader playerReader,
        StuckDetector? stuckDetector = null,
        GoapAgent? goapAgent = null,
        TimeProvider? timeProvider = null)
    {
        this.logger = logger;
        this.featureFlags = featureFlags;
        this.playerReader = playerReader;
        this.stuckDetector = stuckDetector;
        this.goapAgent = goapAgent;
        this.timeProvider = timeProvider ?? TimeProvider.System;

        // Set persistence path
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string botDir = Path.Combine(appData, "WowClassicGrindBot");
        Directory.CreateDirectory(botDir);
        persistencePath = Path.Combine(botDir, "failure_analytics.json");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!featureFlags.Current.FailureAnalytics.Enabled)
        {
            logger.LogInformation("[FailureAnalytics  ] Disabled by feature flag");
            return Task.CompletedTask;
        }

        // Subscribe to stuck events if detector available
        if (stuckDetector != null)
        {
            stuckDetector.OnStuckDetected += OnStuckDetected;
        }

        // Start flush timer
        int intervalMs = featureFlags.Current.FailureAnalytics.FlushIntervalMinutes * 60 * 1000;
        flushTimer = new Timer(_ => FlushToDisk(), null, intervalMs, intervalMs);

        logger.LogInformation("[FailureAnalytics  ] Started with {Interval}min flush interval",
            featureFlags.Current.FailureAnalytics.FlushIntervalMinutes);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (stuckDetector != null)
        {
            stuckDetector.OnStuckDetected -= OnStuckDetected;
        }

        flushTimer?.Change(Timeout.Infinite, 0);
        FlushToDisk();

        logger.LogInformation("[FailureAnalytics  ] Stopped. Final data flushed.");
        return Task.CompletedTask;
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (!featureFlags.Current.FailureAnalytics.Enabled)
            return;

        switch (e)
        {
            case AbortEvent:
                RecordFailure(FailureType.NoPlan, "GOAP could not find valid plan");
                break;
        }
    }

    /// <summary>
    /// Records a death event.
    /// </summary>
    public void RecordDeath(string? cause = null)
    {
        RecordFailure(FailureType.Death, cause ?? "Player died");
    }

    /// <summary>
    /// Records a failed pull attempt.
    /// </summary>
    public void RecordFailedPull(int targetGuid, string reason)
    {
        RecordFailure(FailureType.FailedPull, reason, targetGuid);
    }

    /// <summary>
    /// Records a multi-mob retreat.
    /// </summary>
    public void RecordMultiMobRetreat(int mobCount)
    {
        RecordFailure(FailureType.MultiMobRetreat, $"Retreated from {mobCount} mobs");
    }

    private void OnStuckDetected(StuckEventData data)
    {
        RecordFailure(
            FailureType.Stuck,
            $"Stuck in state {data.State}",
            additionalData: new Dictionary<string, object>
            {
                ["DurationMs"] = data.DurationMs,
                ["IsSpinning"] = data.IsSpinning,
                ["AttemptCount"] = data.AttemptCount
            });
    }

    private void RecordFailure(FailureType type, string reason, int? targetGuid = null, Dictionary<string, object>? additionalData = null)
    {
        lock (eventLock)
        {
            var evt = new FailureEvent
            {
                Timestamp = timeProvider.GetUtcNow().DateTime,
                Type = type,
                Reason = reason,
                TargetGuid = targetGuid,
                Position = playerReader.WorldPos,
                MapId = playerReader.MapId,
                Zone = playerReader.WorldMapArea.AreaName,
                Level = playerReader.Level.Value,
                AdditionalData = additionalData ?? new Dictionary<string, object>()
            };

            sessionEvents.Add(evt);
            Interlocked.Increment(ref totalFailures);

            // Enforce memory bound: trim oldest events when exceeding limit
            int maxEvents = featureFlags.Current.FailureAnalytics.MaxEventsInMemory;
            if (sessionEvents.Count > maxEvents)
            {
                int excess = sessionEvents.Count - maxEvents;
                sessionEvents.RemoveRange(0, excess);
            }

            logger.LogDebug("[FailureAnalytics  ] Recorded {Type}: {Reason} at {Position}",
                type, reason, evt.Position);
        }
    }

    /// <summary>
    /// Gets failure statistics for the current session.
    /// </summary>
    public FailureStatistics GetSessionStatistics()
    {
        lock (eventLock)
        {
            return new FailureStatistics
            {
                TotalFailures = (int)Interlocked.Read(ref totalFailures),
                EventsByType = sessionEvents.GroupBy(e => e.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                HotZones = GetHotZones(),
                RecentEvents = sessionEvents.TakeLast(10).ToList()
            };
        }
    }

    /// <summary>
    /// Gets hotspot locations with high failure rates.
    /// </summary>
    private List<FailureHotZone> GetHotZones()
    {
        var zones = new List<FailureHotZone>();

        // Group failures by approximate location (10-yard radius)
        var grouped = sessionEvents
            .GroupBy(e => new { e.MapId, GridX = (int)(e.Position.X / 10), GridY = (int)(e.Position.Y / 10) })
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Count())
            .Take(10);

        foreach (var group in grouped)
        {
            var center = new Vector3(
                group.Average(e => e.Position.X),
                group.Average(e => e.Position.Y),
                group.Average(e => e.Position.Z));

            zones.Add(new FailureHotZone
            {
                Center = center,
                MapId = group.Key.MapId,
                FailureCount = group.Count(),
                PrimaryType = group.GroupBy(e => e.Type).OrderByDescending(g => g.Count()).First().Key,
                LastFailure = group.Max(e => e.Timestamp)
            });
        }

        return zones;
    }

    private void FlushToDisk()
    {
        try
        {
            lock (eventLock)
            {
                if (sessionEvents.Count == 0) return;

                // Load existing data
                List<FailureEvent> allEvents = LoadExistingEvents();

                // Add new events
                allEvents.AddRange(sessionEvents);

                // Prune old events
                DateTime cutoff = timeProvider.GetUtcNow().DateTime.AddDays(-featureFlags.Current.FailureAnalytics.RetentionDays);
                allEvents.RemoveAll(e => e.Timestamp < cutoff);

                // Keep max events
                if (allEvents.Count > featureFlags.Current.FailureAnalytics.MaxPersistedEvents)
                {
                    allEvents = allEvents.Skip(allEvents.Count - featureFlags.Current.FailureAnalytics.MaxPersistedEvents).ToList();
                }

                // Serialize
                string json = JsonSerializer.Serialize(allEvents, JsonOptions);

                // Atomic write
                string tempPath = persistencePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, persistencePath, overwrite: true);

                logger.LogDebug("[FailureAnalytics  ] Flushed {Count} events to disk", allEvents.Count);

                // Clear session events after successful flush
                sessionEvents.Clear();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FailureAnalytics  ] Failed to flush to disk");
        }
    }

    private List<FailureEvent> LoadExistingEvents()
    {
        try
        {
            if (!File.Exists(persistencePath))
            {
                return new List<FailureEvent>();
            }

            string json = File.ReadAllText(persistencePath);
            return JsonSerializer.Deserialize<List<FailureEvent>>(json, JsonOptions) ?? new List<FailureEvent>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FailureAnalytics  ] Failed to load existing events");
            return new List<FailureEvent>();
        }
    }

    public void Dispose()
    {
        flushTimer?.Dispose();
        FlushToDisk();
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
    Timeout
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
    public Vector3 Position { get; set; }
    public int MapId { get; set; }
    public string Zone { get; set; } = string.Empty;
    public int Level { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

/// <summary>
/// A geographic hotspot of failures.
/// </summary>
public sealed class FailureHotZone
{
    public Vector3 Center { get; set; }
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
}
