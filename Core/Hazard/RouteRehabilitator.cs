using Core.Analytics;
using Core.FeatureFlags;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Core.Hazard;

/// <summary>
/// Service for managing hazard zone rehabilitation and auto-escalation based on failure analytics.
/// </summary>
public sealed class RouteRehabilitator
{
    private readonly HazardZoneStore store;
    private readonly FailureAnalytics? failureAnalytics;
    private readonly ILogger<RouteRehabilitator> logger;
    private readonly FeatureFlagService featureFlagService;

    // Configuration for hot zone auto-escalation
    private const float HotZoneSeverityIncrease = 2.0f;
    private const int MinFailuresForHotZone = 2;
    private const float HotZoneRadius = 25f;
    private const float SingleEventRadius = 12f;
    private const int MaxAlternativeDetours = 3;
    private const float MinDetourSeparation = 12f;
    private static readonly TimeSpan RecentFallbackEventWindow = TimeSpan.FromHours(2);

    public RouteRehabilitator(
        HazardZoneStore store,
        ILogger<RouteRehabilitator> logger,
        FeatureFlagService featureFlagService,
        FailureAnalytics? failureAnalytics = null)
    {
        this.store = store;
        this.logger = logger;
        this.featureFlagService = featureFlagService;
        this.failureAnalytics = failureAnalytics;
    }

    /// <summary>
    /// Reports a successful traversal, reducing hazard severity in the area.
    /// </summary>
    public void ReportSuccessfulTraversal(
        Vector3 position,
        int mapId,
        float radius = 20f,
        float severityFactor = 0.95f)
    {
        if (!featureFlagService.IsHazardAvoidanceEnabled)
        {
            return;
        }

        float factor = Clamp(severityFactor, 0f, 1f);

        int adjusted = store.ReduceSeverityNear(position, mapId, radius, factor);
        if (adjusted <= 0)
        {
            return;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "[Rehabilitator     ] Reduced severity for {Count} clusters near {Pos} (mapId={MapId})",
                adjusted,
                position,
                mapId);
        }
    }

    /// <summary>
    /// Processes hot zones from failure analytics and increases hazard severity.
    /// Call this periodically (e.g., every 30 seconds) to auto-escalate problematic areas.
    /// </summary>
    public void ProcessHotZones()
    {
        if (!featureFlagService.IsHazardAvoidanceEnabled)
        {
            return;
        }

        if (failureAnalytics == null)
        {
            return;
        }

        try
        {
            FailureStatistics stats = failureAnalytics.GetSessionStatistics();
            List<FailureHotZone> hotZones = stats.HotZones;

            if (hotZones.Count == 0)
            {
                return;
            }

            foreach (FailureHotZone zone in hotZones)
            {
                if (zone.FailureCount < MinFailuresForHotZone)
                {
                    continue;
                }

                // Create or escalate hazard for this hot zone
                EscalateHazardForHotZone(zone);
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "[Rehabilitator     ] Processed {Count} hot zones from failure analytics",
                    hotZones.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Rehabilitator     ] Failed to process hot zones");
        }
    }

    /// <summary>
    /// Increases hazard severity for a known hot zone.
    /// </summary>
    private void EscalateHazardForHotZone(FailureHotZone zone)
    {
        // Get existing events in this area
        IReadOnlyList<HazardEvent> existingEvents = store.GetEventsSnapshot(zone.MapId);

        // Check if we already have a hazard event near this location
        bool hasExistingHazard = existingEvents.Any(e =>
            Vector3.Distance(e.WorldPosition, zone.Center) < HotZoneRadius);

        if (!hasExistingHazard)
        {
            // Create a new hazard event for this hot zone
            var hazardEvent = new HazardEvent
            {
                WorldPosition = zone.Center,
                MapId = zone.MapId,
                UIMapId = zone.MapId, // Use MapId as UIMapId fallback
                Type = ConvertFailureTypeToHazardType(zone.PrimaryType),
                Zone = $"Hot Zone - {zone.PrimaryType}"
            };

            store.AddEvent(hazardEvent);

            logger.LogInformation(
                "[Rehabilitator     ] Created hazard for hot zone at {Pos} (mapId={MapId}, type={Type}, failures={Count})",
                zone.Center,
                zone.MapId,
                zone.PrimaryType,
                zone.FailureCount);
        }
        else
        {
            // Existing hazard - severity will be updated by the clustering algorithm
            logger.LogDebug(
                "[Rehabilitator     ] Hot zone at {Pos} already has hazard coverage",
                zone.Center);
        }
    }

    /// <summary>
    /// Suggests alternative waypoints to avoid known hot zones.
    /// Returns a list of suggested detour points around high-risk areas.
    /// </summary>
    public List<Vector3> SuggestAlternativePath(
        Vector3 start,
        Vector3 end,
        int mapId,
        float safetyMargin = 30f)
    {
        if (!featureFlagService.IsHazardAvoidanceEnabled)
        {
            return [];
        }

        List<Vector3> alternatives = [];
        IReadOnlyList<HazardCluster> clusters = store.GetClustersSnapshot(mapId);
        IReadOnlyList<HazardEvent> events = store.GetEventsSnapshot(mapId);

        if (clusters.Count == 0 && events.Count == 0)
        {
            return alternatives;
        }

        // Prefer very recent failure events first so we can react immediately to fresh stuck loops.
        DateTime cutoff = DateTime.UtcNow - RecentFallbackEventWindow;
        for (int i = events.Count - 1; i >= 0 && alternatives.Count < MaxAlternativeDetours; i--)
        {
            HazardEvent hazardEvent = events[i];

            if (hazardEvent.Timestamp < cutoff)
            {
                break;
            }

            if (hazardEvent.Type is not (HazardEventType.Stuck or HazardEventType.Death or HazardEventType.PathfindingFailure))
            {
                continue;
            }

            if (!IsPathNearPoint(start, end, hazardEvent.WorldPosition, SingleEventRadius + safetyMargin))
            {
                continue;
            }

            Vector3 detour = CalculateDetourPoint(start, end, hazardEvent.WorldPosition, SingleEventRadius, safetyMargin);
            if (TryAddDistinctDetour(alternatives, detour))
            {
                logger.LogDebug(
                    "[Rehabilitator     ] Suggested detour around recent {Type} event at {Position}",
                    hazardEvent.Type,
                    hazardEvent.WorldPosition);
            }
        }

        List<HazardCluster> sortedClusters = clusters
            .OrderByDescending(static c => c.SeverityScore)
            .ToList();

        // Then add longer-term cluster detours to keep routing stable through known problem zones.
        for (int i = 0; i < sortedClusters.Count && alternatives.Count < MaxAlternativeDetours; i++)
        {
            HazardCluster cluster = sortedClusters[i];
            if (cluster.SeverityScore < 5f)
            {
                continue; // Only avoid high-severity clusters
            }

            if (IsPathNearCluster(start, end, cluster, safetyMargin))
            {
                Vector3 detour = CalculateDetourPoint(start, end, cluster, safetyMargin);
                if (TryAddDistinctDetour(alternatives, detour))
                {
                    logger.LogDebug(
                        "[Rehabilitator     ] Suggested detour around cluster at {Centroid} (severity={Severity})",
                        cluster.Centroid,
                        cluster.SeverityScore);
                }
            }
        }

        return alternatives;
    }

    /// <summary>
    /// Rehabilitates a specific hot zone, reducing its hazard severity.
    /// </summary>
    public bool RehabilitateHotZone(FailureHotZone zone, float severityFactor = 0.5f)
    {
        if (!featureFlagService.IsHazardAvoidanceEnabled)
        {
            return false;
        }

        int adjusted = store.ReduceSeverityNear(zone.Center, zone.MapId, HotZoneRadius, severityFactor);

        if (adjusted > 0)
        {
            logger.LogInformation(
                "[Rehabilitator     ] Rehabilitated hot zone at {Pos} (mapId={MapId}, reduced={Count})",
                zone.Center,
                zone.MapId,
                adjusted);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the top hot zones that should be avoided based on failure analytics.
    /// </summary>
    public List<FailureHotZone> GetPriorityHotZones(int count = 5)
    {
        if (failureAnalytics == null)
        {
            return [];
        }

        FailureStatistics stats = failureAnalytics.GetSessionStatistics();
        return stats.HotZones
            .OrderByDescending(z => z.FailureCount)
            .Take(count)
            .ToList();
    }

    #region Helper Methods

    private static float Clamp(float value, float min, float max)
    {
        return value < min ? min : value > max ? max : value;
    }

    private static string ConvertFailureTypeToReason(FailureType type)
    {
        return type switch
        {
            FailureType.Stuck => "Auto-generated from stuck detection",
            FailureType.Death => "Auto-generated from death location",
            FailureType.NoPlan => "Auto-generated from GOAP failure",
            FailureType.MultiMobRetreat => "Auto-generated from multi-mob retreat",
            FailureType.FailedPull => "Auto-generated from failed pull",
            _ => "Auto-generated from failure analytics"
        };
    }

    private static HazardEventType ConvertFailureTypeToHazardType(FailureType type)
    {
        return type switch
        {
            FailureType.Stuck => HazardEventType.Stuck,
            FailureType.Death => HazardEventType.Death,
            FailureType.NoPlan => HazardEventType.PathfindingFailure,
            FailureType.MultiMobRetreat => HazardEventType.UnexpectedAggro,
            FailureType.FailedPull => HazardEventType.TargetEvade,
            _ => HazardEventType.ManualMarker
        };
    }

    private static float GetSeverityForFailureType(FailureType type)
    {
        return type switch
        {
            FailureType.Death => 8f,
            FailureType.Stuck => 6f,
            FailureType.NoPlan => 5f,
            FailureType.MultiMobRetreat => 4f,
            FailureType.FailedPull => 3f,
            _ => 3f
        };
    }

    private static bool IsPathNearCluster(Vector3 start, Vector3 end, HazardCluster cluster, float margin)
    {
        return IsPathNearPoint(start, end, cluster.Centroid, cluster.Radius + margin);
    }

    private static bool IsPathNearPoint(Vector3 start, Vector3 end, Vector3 point, float radius)
    {
        if (Vector3.Distance(start, point) < radius ||
            Vector3.Distance(end, point) < radius)
        {
            return true;
        }

        // Check if point is between start and end (rough approximation)
        Vector3 pathDir = Vector3.Normalize(end - start);
        Vector3 toPoint = point - start;
        float projection = Vector3.Dot(toPoint, pathDir);

        if (projection > 0 && projection < Vector3.Distance(start, end))
        {
            Vector3 closestPoint = start + pathDir * projection;
            return Vector3.Distance(closestPoint, point) < radius;
        }

        return false;
    }

    private static Vector3 CalculateDetourPoint(Vector3 start, Vector3 end, HazardCluster cluster, float margin)
    {
        return CalculateDetourPoint(start, end, cluster.Centroid, cluster.Radius, margin);
    }

    private static Vector3 CalculateDetourPoint(
        Vector3 start,
        Vector3 end,
        Vector3 center,
        float hazardRadius,
        float margin)
    {
        Vector3 pathDir = Vector3.Normalize(end - start);
        Vector3 toCluster = center - start;

        // Calculate perpendicular direction
        Vector3 perpendicular = new(-pathDir.Y, pathDir.X, 0);
        if (perpendicular.Length() < 0.001f)
        {
            perpendicular = new Vector3(1, 0, 0);
        }
        else
        {
            perpendicular = Vector3.Normalize(perpendicular);
        }

        // Determine which side to detour based on current position
        float dot = Vector3.Dot(toCluster, perpendicular);
        if (dot < 0)
        {
            perpendicular = -perpendicular;
        }

        // Calculate detour point
        float detourDistance = hazardRadius + margin + 10f;
        Vector3 detour = center + perpendicular * detourDistance;
        detour.Z = center.Z; // Maintain same Z

        return detour;
    }

    private static bool TryAddDistinctDetour(List<Vector3> alternatives, Vector3 detour)
    {
        for (int i = 0; i < alternatives.Count; i++)
        {
            if (Vector3.Distance(alternatives[i], detour) < MinDetourSeparation)
            {
                return false;
            }
        }

        alternatives.Add(detour);
        return true;
    }

    #endregion
}
