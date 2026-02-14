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
    private readonly ILogger<RouteRerouter> _logger;
    private readonly RouteRehabilitator? _rehabilitator;
    private readonly HazardZoneStore? _hazardStore;
    private readonly FeatureFlagService? _featureFlags;

    private RerouteInfo? _activeReroute;
    private readonly object _rerouteLock = new();
    private bool _isEnabled = true;

    // Configuration
    private float _hotZoneSeverityThreshold = 5f;
    private float _safetyMargin = 30f;
    private readonly TimeSpan _rerouteCooldown = TimeSpan.FromSeconds(5);
    private DateTime _lastRerouteTime = DateTime.MinValue;

    /// <inheritdoc />
    public event Action<RerouteEventArgs>? OnRerouteTriggered;

    /// <inheritdoc />
    public event Action<DetourPathCalculatedEventArgs>? OnDetourCalculated;

    /// <inheritdoc />
    public bool IsEnabled => _isEnabled && (_featureFlags?.IsHazardAvoidanceEnabled ?? true);

    /// <inheritdoc />
    public float HotZoneSeverityThreshold
    {
        get => _hotZoneSeverityThreshold;
        set => _hotZoneSeverityThreshold = Math.Max(1f, Math.Min(10f, value));
    }

    /// <inheritdoc />
    public float SafetyMargin
    {
        get => _safetyMargin;
        set => _safetyMargin = Math.Max(10f, Math.Min(100f, value));
    }

    public RouteRerouter(
        ILogger<RouteRerouter> logger,
        RouteRehabilitator? rehabilitator = null,
        HazardZoneStore? hazardStore = null,
        FeatureFlagService? featureFlags = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rehabilitator = rehabilitator;
        _hazardStore = hazardStore;
        _featureFlags = featureFlags;

        _logger.LogInformation("[RouteRerouter  ] Initialized with threshold={Threshold}, margin={Margin}",
            _hotZoneSeverityThreshold, _safetyMargin);
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
            _logger.LogDebug("[RouteRerouter  ] Reroute skipped - disabled");
            return false;
        }

        // Check cooldown
        if (DateTime.UtcNow - _lastRerouteTime < _rerouteCooldown)
        {
            _logger.LogDebug("[RouteRerouter  ] Reroute skipped - cooldown active");
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
        if (maxSeverity < _hotZoneSeverityThreshold)
        {
            _logger.LogDebug("[RouteRerouter  ] Hot zones detected but severity {Severity} below threshold {Threshold}",
                maxSeverity, _hotZoneSeverityThreshold);
            return false;
        }

        // Calculate detour
        Vector3[] originalPath = [currentPosition, targetPosition];
        Vector3[]? detourPath = await CalculateDetourAsync(originalPath, mapId, cancellationToken);

        if (detourPath == null || detourPath.Length < 2)
        {
            _logger.LogWarning("[RouteRerouter  ] Failed to calculate detour path");
            return false;
        }

        // Create reroute info
        lock (_rerouteLock)
        {
            _activeReroute = new RerouteInfo
            {
                Id = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow,
                OriginalTarget = targetPosition,
                DetourWaypoints = detourPath,
                CurrentWaypointIndex = 0,
                IsCompleted = false
            };
            _lastRerouteTime = DateTime.UtcNow;
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

        _logger.LogInformation(
            "[RouteRerouter  ] Reroute triggered: {ZoneCount} hot zones, {WaypointCount} waypoints",
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

        if (!IsEnabled || _rehabilitator == null || originalPath.Length < 2)
        {
            return Task.FromResult<Vector3[]?>(null);
        }

        Vector3 start = originalPath[0];
        Vector3 end = originalPath[^1];

        // Get alternative path suggestions from RouteRehabilitator
        List<Vector3> detourPoints = _rehabilitator.SuggestAlternativePath(
            start, end, mapId, _safetyMargin);

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

        _logger.LogDebug(
            "[RouteRerouter  ] Detour calculated: {Distance:F1}m additional, {TimeMs:F1}ms",
            additionalDistance, stopwatch.Elapsed.TotalMilliseconds);

        return Task.FromResult<Vector3[]?>(detourPath.ToArray());
    }

    /// <inheritdoc />
    public RerouteInfo? GetActiveReroute()
    {
        lock (_rerouteLock)
        {
            return _activeReroute;
        }
    }

    /// <inheritdoc />
    public void ClearActiveReroute()
    {
        lock (_rerouteLock)
        {
            if (_activeReroute != null)
            {
                _activeReroute.IsCompleted = true;
                _logger.LogDebug("[RouteRerouter  ] Active reroute cleared");
            }
            _activeReroute = null;
        }
    }

    /// <inheritdoc />
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        _logger.LogInformation("[RouteRerouter  ] Auto-rerouting {Status}",
            enabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Advances to the next waypoint in the active reroute.
    /// Returns true if there are more waypoints.
    /// </summary>
    public bool AdvanceWaypoint()
    {
        lock (_rerouteLock)
        {
            if (_activeReroute == null)
            {
                return false;
            }

            _activeReroute.CurrentWaypointIndex++;

            if (_activeReroute.CurrentWaypointIndex >= _activeReroute.DetourWaypoints.Length)
            {
                _activeReroute.IsCompleted = true;
                _activeReroute = null;
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
        lock (_rerouteLock)
        {
            if (_activeReroute == null ||
                _activeReroute.CurrentWaypointIndex >= _activeReroute.DetourWaypoints.Length)
            {
                return null;
            }

            return _activeReroute.DetourWaypoints[_activeReroute.CurrentWaypointIndex];
        }
    }

    private async Task<List<HotZoneInfo>> DetectHotZonesAsync(
        Vector3 currentPosition,
        Vector3 targetPosition,
        int mapId,
        CancellationToken cancellationToken)
    {
        var hotZones = new List<HotZoneInfo>();

        if (_hazardStore == null)
        {
            return hotZones;
        }

        await Task.Yield(); // Allow cancellation

        IReadOnlyList<HazardCluster> clusters = _hazardStore.GetClustersSnapshot(mapId);

        foreach (HazardCluster cluster in clusters)
        {
            if (cluster.SeverityScore < _hotZoneSeverityThreshold)
            {
                continue;
            }

            // Check if cluster is on the path
            if (IsPointNearPath(currentPosition, targetPosition, cluster.Centroid, cluster.Radius + _safetyMargin))
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

        if (_hazardStore == null)
        {
            return zones;
        }

        IReadOnlyList<HazardCluster> clusters = _hazardStore.GetClustersSnapshot(mapId);

        foreach (HazardCluster cluster in clusters)
        {
            if (IsPointNearPath(start, end, cluster.Centroid, cluster.Radius + _safetyMargin))
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

        if (pathLength < 0.001f)
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
