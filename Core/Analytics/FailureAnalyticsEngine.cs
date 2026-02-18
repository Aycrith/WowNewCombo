using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;

using Core.FeatureFlags;
using Core.GoalsComponent;

using Microsoft.Extensions.Logging;

namespace Core.Analytics;

/// <summary>
/// Core analytics engine for tracking bot failures and generating insights.
/// Separated from hosted service lifecycle for better testability and SRP compliance.
/// </summary>
public sealed class FailureAnalyticsEngine : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<FailureAnalyticsEngine> logger;
    private readonly FeatureFlagService featureFlags;
    private readonly PlayerReader playerReader;
    private readonly TimeProvider timeProvider;

    private readonly List<FailureEvent> sessionEvents = new();
    private readonly object eventLock = new();

    private readonly string persistencePath;
    private long totalFailures;

    public FailureAnalyticsEngine(
        ILogger<FailureAnalyticsEngine> logger,
        FeatureFlagService featureFlags,
        PlayerReader playerReader,
        TimeProvider? timeProvider = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        this.playerReader = playerReader ?? throw new ArgumentNullException(nameof(playerReader));
        this.timeProvider = timeProvider ?? TimeProvider.System;

        // Set persistence path
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string botDir = Path.Combine(appData, "WowClassicGrindBot");
        Directory.CreateDirectory(botDir);
        persistencePath = Path.Combine(botDir, "failure_analytics.json");
    }

    /// <summary>
    /// Records a failure event from GOAP abort.
    /// </summary>
    public void RecordNoPlanFailure(string reason)
    {
        RecordFailure(FailureType.NoPlan, reason);
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

    /// <summary>
    /// Records a stuck event.
    /// </summary>
    public void RecordStuckEvent(StuckEventData data)
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
            FailureEvent evt = new()
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

            logger.LogDebug("[FailureAnalytics-Engine] Recorded {Type}: {Reason} at {Position}",
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
            Vector3 center = new(
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

    /// <summary>
    /// Flushes session events to disk.
    /// </summary>
    public void FlushToDisk()
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

                logger.LogDebug("[FailureAnalytics-Engine] Flushed {Count} events to disk", allEvents.Count);

                // Clear session events after successful flush
                sessionEvents.Clear();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FailureAnalytics-Engine] Failed to flush to disk");
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
            logger.LogError(ex, "[FailureAnalytics-Engine] Failed to load existing events");
            return new List<FailureEvent>();
        }
    }

    public void Dispose()
    {
        FlushToDisk();
    }
}
