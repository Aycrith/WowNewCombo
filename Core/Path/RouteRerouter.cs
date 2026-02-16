using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Core.FeatureFlags;
using Core.Hazard;

using Microsoft.Extensions.Logging;

namespace Core;

/// <summary>
/// Service that automatically reroutes around hot zones based on failure analytics.
/// Integrates with RouteVisualizationService and RouteRehabilitator to provide dynamic path adjustment.
/// </summary>
public sealed class RouteRerouter : IRouteRerouter, IDisposable
{
    private readonly ILogger<RouteRerouter> logger;
    private readonly RouteRehabilitator? rehabilitator;
    private readonly HazardZoneStore? hazardStore;
    private readonly FeatureFlagService? featureFlags;

    private RerouteInfo? activeReroute;
    private readonly object rerouteLock = new();
    private bool isEnabled = true;

    // Configuration
    private float hotZoneSeverityThreshold = DefaultHotZoneSeverityThreshold;
    private float safetyMargin = DefaultSafetyMargin;
    private readonly TimeSpan rerouteCooldown = TimeSpan.FromSeconds(RerouteCooldownSeconds);
    private DateTime lastRerouteTime = DateTime.MinValue;

    /// <summary>
    /// Default hot zone severity threshold.
    /// Minimum severity score required to trigger a reroute around a hazard zone.
    /// </summary>
    private const float DefaultHotZoneSeverityThreshold = 5f;

    /// <summary>
    /// Minimum allowed hot zone severity threshold.
    /// </summary>
    private const float MinHotZoneSeverityThreshold = 1f;

    /// <summary>
    /// Maximum allowed hot zone severity threshold.
    /// </summary>
    private const float MaxHotZoneSeverityThreshold = 10f;

    /// <summary>
    /// Default safety margin in units (yards/meters).
    /// Distance to maintain from hazard zones when calculating detours.
    /// </summary>
    private const float DefaultSafetyMargin = 30f;

    /// <summary>
    /// Minimum allowed safety margin.
    /// </summary>
    private const float MinSafetyMargin = 10f;

    /// <summary>
    /// Maximum allowed safety margin.
    /// </summary>
    private const float MaxSafetyMargin = 100f;

    /// <summary>
    /// Cooldown period in seconds between reroute attempts.
    /// Prevents excessive rerouting in quick succession.
    /// </summary>
    private const int RerouteCooldownSeconds = 5;

    /// <summary>
    /// Minimum number of waypoints required for a valid path.
    /// </summary>
    private const int MinPathWaypoints = 2;

    /// <summary>
    /// Epsilon value for floating-point path length comparisons.
    /// Used to detect zero-length paths.
    /// </summary>
    private const float PathLengthEpsilon = 0.001f;

    /// <inheritdoc />
    public event Action<RerouteEventArgs>? OnRerouteTriggered;

    /// <inheritdoc />
    public event Action<DetourPathCalculatedEventArgs>? OnDetourCalculated;

    /// <inheritdoc />
    public bool IsEnabled => isEnabled && (featureFlags?.IsHazardAvoidanceEnabled ?? true);

    /// <inheritdoc />
    public float HotZoneSeverityThreshold
    {
        get => hotZoneSeverityThreshold;
        set => hotZoneSeverityThreshold = Math.Max(MinHotZoneSeverityThreshold, Math.Min(MaxHotZoneSeverityThreshold, value));
    }

    /// <inheritdoc />
    public float SafetyMargin
    {
        get => safetyMargin;
        set => safetyMargin = Math.Max(MinSafetyMargin, Math.Min(MaxSafetyMargin, value));
    }

    public RouteRerouter(
        ILogger<RouteRerouter> loggerParam,
        RouteRehabilitator? rehabilitatorParam = null,
        HazardZoneStore? hazardStoreParam = null,
        FeatureFlagService? featureFlagsParam = null)
    {
        logger = loggerParam ?? throw new ArgumentNullException(nameof(loggerParam));
        rehabilitator = rehabilitatorParam;
        hazardStore = hazardStoreParam;
        featureFlags = featureFlagsParam;

        logger.LogInformation("[RouteRerouter ] Initialized with threshold={Threshold}, margin={Margin}",
            hotZoneSeverityThreshold, safetyMargin);
    }

    /// <inheritdoc />
    public async Task<bool> TriggerRerouteAsync(
        Vector3 currentPosition,
        Vector3 targetPosition,
        int mapId,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            logger.LogDebug("[RouteRerouter ] Reroute skipped - disabled");
            return false;
        }

        // Check cooldown
        if (DateTime.UtcNow - lastRerouteTime < rerouteCooldown)
        {
            logger.LogDebug("[RouteRerouter ] Reroute skipped - cooldown active");
            return false;
        }

        // Check for hot zones ahead
        List<HotZoneInfo> hotZones = await DetectHotZonesAsync(
            currentPosition,
            targetPosition,
            mapId,
            cancellationToken);

        if (hotZones.Count == 0)
        {
            return false;
        }

        // Check if severity threshold is met
        float maxSeverity = hotZones.Max(z => z.Severity);
        if (maxSeverity < hotZoneSeverityThreshold)
        {
            logger.LogDebug("[RouteRerouter ] Hot zones detected but severity {Severity} below threshold {Threshold}",
                maxSeverity, hotZoneSeverityThreshold);
            return false;
        }

        // Calculate detour
        Vector3[] originalPath = [currentPosition, targetPosition];
        Vector3[]? detourPath = await CalculateDetourAsync(originalPath, mapId, cancellationToken);

            if (detourPath == null || detourPath.Length < MinPathWaypoints)
        {
            logger.LogWarning("[RouteRerouter ] Failed to calculate detour path");
            return false;
        }

        // Create reroute info
        lock (rerouteLock)
        {
            activeReroute = new RerouteInfo
            {
                Id = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow,
                OriginalTarget = targetPosition,
                DetourWaypoints = detourPath,
                CurrentWaypointIndex = 0,
                IsCompleted = false
            };
            lastRerouteTime = DateTime.UtcNow;
        }

        // Notify listeners
        var rerouteArgs = new RerouteEventArgs
        {
            TriggerPosition = currentPosition,
            Reason = $"Hot zone detected (severity: {maxSeverity:F1})",
            TriggeringZones = hotZones,
            Timestamp = DateTime.UtcNow
        };
        OnRerouteTriggered?.Invoke(rerouteArgs);

        logger.LogInformation(
            "[RouteRerouter ] Reroute triggered: {ZoneCount} hot zones, {WaypointCount} waypoints",
            hotZones.Count, detourPath.Length);

        return true;
    }

    /// <inheritdoc />
    public Task<Vector3[]?> CalculateDetourAsync(
        Vector3[] originalPath,
        int mapId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

            if (!IsEnabled || rehabilitator == null || originalPath.Length < MinPathWaypoints)
        {
            return Task.FromResult<Vector3[]?>(null);
        }

        Vector3 start = originalPath[0];
        Vector3 end = originalPath[^1];

        // Get alternative path suggestions from RouteRehabilitator
        List<Vector3> detourPoints = rehabilitator.SuggestAlternativePath(
            start, end, mapId, safetyMargin);

        if (detourPoints.Count == 0)
        {
            return Task.FromResult<Vector3[]?>(null);
        }

        // Build detour path: start -> detour points -> end
        List<Vector3> detourPath = [start];
        detourPath.AddRange(detourPoints);
        detourPath.Add(end);

        // Calculate additional distance
        float originalDistance = CalculatePathDistance(originalPath);
        float detourDistance = CalculatePathDistance(detourPath.ToArray());
        float additionalDistance = detourDistance - originalDistance;

        stopwatch.Stop();

        // Get hot zones being avoided
        List<HotZoneInfo> avoidedZones = GetHotZonesOnPath(start, end, mapId);

        // Notify listeners
        var args = new DetourPathCalculatedEventArgs
        {
            OriginalPath = originalPath,
            DetourPath = detourPath.ToArray(),
            AvoidedZones = avoidedZones,
            AdditionalDistance = additionalDistance,
            CalculationTime = stopwatch.Elapsed
        };
        OnDetourCalculated?.Invoke(args);

        logger.LogDebug(
            "[RouteRerouter ] Detour calculated: {Distance:F1}m additional, {TimeMs:F1}ms",
            additionalDistance, stopwatch.Elapsed.TotalMilliseconds);

        return Task.FromResult<Vector3[]?>(detourPath.ToArray());
    }

    /// <inheritdoc />
    public RerouteInfo? GetActiveReroute()
    {
        lock (rerouteLock)
        {
            return activeReroute;
        }
    }

    /// <inheritdoc />
    public void ClearActiveReroute()
    {
        lock (rerouteLock)
        {
            if (activeReroute != null)
            {
                activeReroute.IsCompleted = true;
                logger.LogDebug("[RouteRerouter ] Active reroute cleared");
            }
            activeReroute = null;
        }
    }

    /// <inheritdoc />
    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        logger.LogInformation("[RouteRerouter ] Auto-rerouting {Status}",
            enabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Advances to the next waypoint in the active reroute.
    /// Returns true if there are more waypoints.
    /// </summary>
    public bool AdvanceWaypoint()
    {
        lock (rerouteLock)
        {
            if (activeReroute == null)
            {
                return false;
            }

            activeReroute.CurrentWaypointIndex++;

            if (activeReroute.CurrentWaypointIndex >= activeReroute.DetourWaypoints.Length)
            {
                activeReroute.IsCompleted = true;
                activeReroute = null;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Gets the current waypoint to navigate to.
    /// </summary>
    public Vector3? GetCurrentWaypoint()
    {
        lock (rerouteLock)
        {
            if (activeReroute == null ||
                activeReroute.CurrentWaypointIndex >= activeReroute.DetourWaypoints.Length)
            {
                return null;
            }

            return activeReroute.DetourWaypoints[activeReroute.CurrentWaypointIndex];
        }
    }

    private async Task<List<HotZoneInfo>> DetectHotZonesAsync(
        Vector3 currentPosition,
        Vector3 targetPosition,
        int mapId,
        CancellationToken cancellationToken)
    {
        var hotZones = new List<HotZoneInfo>();

        if (hazardStore == null)
        {
            return hotZones;
        }

        await Task.Yield(); // Allow cancellation

        IReadOnlyList<HazardCluster> clusters = hazardStore.GetClustersSnapshot(mapId);

        foreach (HazardCluster cluster in clusters)
        {
            if (cluster.SeverityScore < hotZoneSeverityThreshold)
            {
                continue;
            }

            // Check if cluster is on the path
            if (IsPointNearPath(currentPosition, targetPosition, cluster.Centroid, cluster.Radius + safetyMargin))
            {
                hotZones.Add(new HotZoneInfo
                {
                    Center = cluster.Centroid,
                    Radius = cluster.Radius,
                    FailureCount = (int)cluster.SeverityScore,
                    Severity = cluster.SeverityScore,
                    FailureType = GetPrimaryFailureType(cluster)
                });
            }
        }

        return hotZones;
    }

    private List<HotZoneInfo> GetHotZonesOnPath(Vector3 start, Vector3 end, int mapId)
    {
        var zones = new List<HotZoneInfo>();

        if (hazardStore == null)
        {
            return zones;
        }

        IReadOnlyList<HazardCluster> clusters = hazardStore.GetClustersSnapshot(mapId);

        foreach (HazardCluster cluster in clusters)
        {
            if (IsPointNearPath(start, end, cluster.Centroid, cluster.Radius + safetyMargin))
            {
                zones.Add(new HotZoneInfo
                {
                    Center = cluster.Centroid,
                    Radius = cluster.Radius,
                    FailureCount = (int)cluster.SeverityScore,
                    Severity = cluster.SeverityScore,
                    FailureType = GetPrimaryFailureType(cluster)
                });
            }
        }

        return zones;
    }

    private static bool IsPointNearPath(Vector3 start, Vector3 end, Vector3 point, float threshold)
    {
        Vector3 pathDir = end - start;
        float pathLength = pathDir.Length();

            if (pathLength < PathLengthEpsilon)
        {
            return Vector3.Distance(start, point) < threshold;
        }

        pathDir = Vector3.Normalize(pathDir);
        Vector3 toPoint = point - start;
        float projection = Vector3.Dot(toPoint, pathDir);

        if (projection < 0 || projection > pathLength)
        {
            // Point is outside the segment
            float distToStart = Vector3.Distance(start, point);
            float distToEnd = Vector3.Distance(end, point);
            return Math.Min(distToStart, distToEnd) < threshold;
        }

        Vector3 closestPoint = start + pathDir * projection;
        return Vector3.Distance(closestPoint, point) < threshold;
    }

    private static float CalculatePathDistance(Vector3[] path)
    {
        float distance = 0f;
        for (int i = 1; i < path.Length; i++)
        {
            distance += Vector3.Distance(path[i - 1], path[i]);
        }
        return distance;
    }

    private static string GetPrimaryFailureType(HazardCluster cluster)
    {
        if (cluster.Events.Count == 0)
        {
            return "Unknown";
        }

        // Get most common event type
        return cluster.Events
            .GroupBy(e => e.Type)
            .OrderByDescending(g => g.Count())
            .First()
            .Key
            .ToString();
    }

    public void Dispose()
    {
        ClearActiveReroute();
    }
}
