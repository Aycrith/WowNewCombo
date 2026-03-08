using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;

using Core.Autonomy;
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

    private static readonly TimeSpan IncidentDedupWindow = TimeSpan.FromMinutes(2);

    private readonly ILogger<FailureAnalyticsEngine> logger;
    private readonly FeatureFlagService featureFlags;
    private readonly PlayerReader playerReader;
    private readonly TimeProvider timeProvider;
    private readonly IScreenCapture? screenCapture;

    private readonly List<FailureEvent> sessionEvents = [];
    private readonly List<AutonomyIncident> sessionIncidents = [];
    private readonly object eventLock = new();

    private readonly string persistencePath;
    private readonly string incidentPersistencePath;
    private long totalFailures;

    public FailureAnalyticsEngine(
        ILogger<FailureAnalyticsEngine> logger,
        FeatureFlagService featureFlags,
        PlayerReader playerReader,
        TimeProvider? timeProvider = null,
        IScreenCapture? screenCapture = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        this.playerReader = playerReader ?? throw new ArgumentNullException(nameof(playerReader));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.screenCapture = screenCapture;

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string botDir = Path.Combine(appData, "WowClassicGrindBot");
        Directory.CreateDirectory(botDir);
        persistencePath = Path.Combine(botDir, "failure_analytics.json");
        incidentPersistencePath = Path.Combine(botDir, "autonomy_incidents.json");
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

    public AutonomyIncident RecordIncident(
        FailureType type,
        string reason,
        string subsystem,
        string source = "runtime",
        int? targetGuid = null,
        Dictionary<string, object>? additionalData = null,
        bool requestScreenshot = true,
        string? gate = null)
    {
        lock (eventLock)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            string fingerprint = BuildFingerprint(type, reason, subsystem, gate);
            AutonomyIncident incident = FindOrCreateIncident(type, reason, subsystem, source, gate, additionalData, fingerprint, now);
            incident.OccurrenceCount++;
            incident.LastSeenUtc = now;
            incident.Outcome = "Open";
            incident.Status = "Open";

            ScreenshotRef? screenshot = requestScreenshot
                ? TryCaptureScreenshot(incident, type, reason)
                : null;

            if (screenshot != null)
            {
                incident.Screenshot = screenshot;
                incident.Artifacts.Add(new ArtifactRef
                {
                    Kind = "Screenshot",
                    Path = screenshot.Path,
                    Source = source,
                    TimestampUtc = now,
                    Metadata = new Dictionary<string, object>
                    {
                        ["CaptureLatencyMs"] = screenshot.CaptureLatencyMs ?? 0d,
                        ["Success"] = screenshot.Success
                    }
                });
            }

            FailureEvent evt = new()
            {
                Timestamp = now.UtcDateTime,
                Type = type,
                Reason = reason,
                TargetGuid = targetGuid,
                Position = playerReader.WorldPos,
                MapId = playerReader.MapId,
                Zone = playerReader.WorldMapArea.AreaName,
                Level = playerReader.Level.Value,
                AdditionalData = additionalData ?? new Dictionary<string, object>(),
                CorrelationId = incident.CorrelationId,
                IncidentId = incident.Id,
                Source = source,
                Screenshot = screenshot
            };

            if (gate is not null)
            {
                evt.AdditionalData["Gate"] = gate;
            }

            sessionEvents.Add(evt);
            Interlocked.Increment(ref totalFailures);

            TrimInMemoryCollections();

            logger.LogDebug("[FailureAnalytics-Engine] Recorded {Type}: {Reason} at {Position} ({IncidentId})",
                type, reason, evt.Position, incident.Id);

            return incident;
        }
    }

    private void RecordFailure(
        FailureType type,
        string reason,
        int? targetGuid = null,
        Dictionary<string, object>? additionalData = null)
    {
        string subsystem = GetSubsystem(type);
        _ = RecordIncident(type, reason, subsystem, targetGuid: targetGuid, additionalData: additionalData);
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
                RecentEvents = sessionEvents.TakeLast(10).ToList(),
                RecentIncidents = sessionIncidents
                    .OrderByDescending(i => i.LastSeenUtc)
                    .Take(10)
                    .ToList()
            };
        }
    }

    public IReadOnlyList<AutonomyIncident> GetRecentIncidents()
    {
        lock (eventLock)
        {
            return sessionIncidents
                .OrderByDescending(i => i.LastSeenUtc)
                .Take(25)
                .ToList();
        }
    }

    /// <summary>
    /// Gets hotspot locations with high failure rates.
    /// </summary>
    private List<FailureHotZone> GetHotZones()
    {
        List<FailureHotZone> zones = [];

        IEnumerable<IGrouping<object, FailureEvent>> grouped = sessionEvents
            .GroupBy(e => new { e.MapId, GridX = (int)(e.Position.X / 10), GridY = (int)(e.Position.Y / 10) })
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Count())
            .Take(10);

        foreach (IGrouping<object, FailureEvent> group in grouped)
        {
            Vector3 center = new(
                group.Average(e => e.Position.X),
                group.Average(e => e.Position.Y),
                group.Average(e => e.Position.Z));

            zones.Add(new FailureHotZone
            {
                Center = center,
                MapId = group.First().MapId,
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
                if (sessionEvents.Count == 0 && sessionIncidents.Count == 0)
                {
                    return;
                }

                List<FailureEvent> allEvents = LoadExistingEvents();
                allEvents.AddRange(sessionEvents);

                DateTime cutoff = timeProvider.GetUtcNow().DateTime.AddDays(-featureFlags.Current.FailureAnalytics.RetentionDays);
                allEvents.RemoveAll(e => e.Timestamp < cutoff);

                if (allEvents.Count > featureFlags.Current.FailureAnalytics.MaxPersistedEvents)
                {
                    allEvents = allEvents.Skip(allEvents.Count - featureFlags.Current.FailureAnalytics.MaxPersistedEvents).ToList();
                }

                string json = JsonSerializer.Serialize(allEvents, JsonOptions);
                AtomicWrite(persistencePath, json);

                List<AutonomyIncident> allIncidents = LoadExistingIncidents();
                Dictionary<string, AutonomyIncident> byId = new(StringComparer.OrdinalIgnoreCase);
                foreach (AutonomyIncident incident in allIncidents)
                {
                    byId[incident.Id] = incident;
                }

                foreach (AutonomyIncident incident in sessionIncidents)
                {
                    byId[incident.Id] = incident;
                }

                List<AutonomyIncident> persistedIncidents = byId.Values
                    .Where(i => i.LastSeenUtc.UtcDateTime >= cutoff)
                    .OrderByDescending(i => i.LastSeenUtc)
                    .Take(featureFlags.Current.FailureAnalytics.MaxPersistedEvents)
                    .ToList();

                string incidentsJson = JsonSerializer.Serialize(persistedIncidents, JsonOptions);
                AtomicWrite(incidentPersistencePath, incidentsJson);

                logger.LogDebug("[FailureAnalytics-Engine] Flushed {EventCount} events and {IncidentCount} incidents to disk",
                    allEvents.Count, persistedIncidents.Count);

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
                return [];
            }

            string json = File.ReadAllText(persistencePath);
            return JsonSerializer.Deserialize<List<FailureEvent>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FailureAnalytics-Engine] Failed to load existing events");
            return [];
        }
    }

    private List<AutonomyIncident> LoadExistingIncidents()
    {
        try
        {
            if (!File.Exists(incidentPersistencePath))
            {
                return [];
            }

            string json = File.ReadAllText(incidentPersistencePath);
            return JsonSerializer.Deserialize<List<AutonomyIncident>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FailureAnalytics-Engine] Failed to load incident history");
            return [];
        }
    }

    private AutonomyIncident FindOrCreateIncident(
        FailureType type,
        string reason,
        string subsystem,
        string source,
        string? gate,
        Dictionary<string, object>? additionalData,
        string fingerprint,
        DateTimeOffset now)
    {
        AutonomyIncident? existing = sessionIncidents
            .OrderByDescending(i => i.LastSeenUtc)
            .FirstOrDefault(i =>
                string.Equals(i.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) &&
                now - i.LastSeenUtc <= IncidentDedupWindow);

        if (existing != null)
        {
            MergeMetadata(existing.Metadata, additionalData);
            return existing;
        }

        AutonomyIncident created = new()
        {
            Id = $"incident-{Guid.NewGuid():N}"[..17],
            Fingerprint = fingerprint,
            CorrelationId = Guid.NewGuid().ToString("N"),
            Category = GetCategory(type),
            Subsystem = subsystem,
            Severity = GetSeverity(type),
            Gate = gate ?? string.Empty,
            Reason = reason,
            Summary = reason,
            FirstSeenUtc = now,
            LastSeenUtc = now,
            Metadata = additionalData != null ? new Dictionary<string, object>(additionalData) : new Dictionary<string, object>()
        };

        created.Metadata["Source"] = source;
        created.Metadata["MapId"] = playerReader.MapId;
        created.Metadata["Zone"] = playerReader.WorldMapArea.AreaName;

        sessionIncidents.Add(created);
        return created;
    }

    private ScreenshotRef? TryCaptureScreenshot(AutonomyIncident incident, FailureType type, string reason)
    {
        if (screenCapture == null)
        {
            return null;
        }

        string captureReason = $"{type}_{reason}";
        ScreenCaptureResult result = screenCapture.Capture(captureReason, incident.CorrelationId, incident.Id);
        if (!result.Success && string.IsNullOrWhiteSpace(result.Path))
        {
            return result.ToScreenshotRef();
        }

        return result.ToScreenshotRef();
    }

    private void TrimInMemoryCollections()
    {
        int maxEvents = featureFlags.Current.FailureAnalytics.MaxEventsInMemory;
        if (sessionEvents.Count > maxEvents)
        {
            int excess = sessionEvents.Count - maxEvents;
            sessionEvents.RemoveRange(0, excess);
        }

        if (sessionIncidents.Count > maxEvents)
        {
            int excess = sessionIncidents.Count - maxEvents;
            sessionIncidents.RemoveRange(0, excess);
        }
    }

    private static string BuildFingerprint(FailureType type, string reason, string subsystem, string? gate)
    {
        return $"{type}|{subsystem}|{gate}|{reason}".ToLowerInvariant();
    }

    private static string GetCategory(FailureType type)
    {
        return type switch
        {
            FailureType.Stuck => "navigation",
            FailureType.Death => "survival",
            FailureType.NoPlan => "planning",
            FailureType.FailedPull => "combat",
            FailureType.MultiMobRetreat => "combat",
            FailureType.LaunchReadiness => "launch",
            FailureType.FocusRestore => "input",
            FailureType.CombatTargetLoss => "combat",
            FailureType.LootFailure => "loot",
            FailureType.BotInactive => "automation",
            FailureType.ServiceInterruption => "services",
            FailureType.ProcessExit => "services",
            _ => "runtime"
        };
    }

    private static string GetSubsystem(FailureType type)
    {
        return type switch
        {
            FailureType.Stuck => "navigation",
            FailureType.Death => "combat",
            FailureType.NoPlan => "goap",
            FailureType.FailedPull => "combat",
            FailureType.MultiMobRetreat => "combat",
            FailureType.LaunchReadiness => "launch-readiness",
            FailureType.FocusRestore => "focus",
            FailureType.CombatTargetLoss => "combat",
            FailureType.LootFailure => "loot",
            FailureType.BotInactive => "bootstrap",
            FailureType.ServiceInterruption => "services",
            FailureType.ProcessExit => "services",
            _ => "runtime"
        };
    }

    private static string GetSeverity(FailureType type)
    {
        return type switch
        {
            FailureType.Death => "Critical",
            FailureType.ProcessExit => "Critical",
            FailureType.ServiceInterruption => "Error",
            FailureType.LaunchReadiness => "Error",
            FailureType.NoPlan => "Error",
            FailureType.Stuck => "Error",
            _ => "Warning"
        };
    }

    private static void MergeMetadata(Dictionary<string, object> target, Dictionary<string, object>? source)
    {
        if (source == null)
        {
            return;
        }

        foreach ((string key, object value) in source)
        {
            target[key] = value;
        }
    }

    private static void AtomicWrite(string path, string json)
    {
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    public void Dispose()
    {
        FlushToDisk();
    }
}
