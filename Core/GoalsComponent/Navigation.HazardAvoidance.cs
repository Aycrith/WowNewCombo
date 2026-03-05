using Core.Hazard;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Extensions;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using static System.MathF;

namespace Core.Goals;

public sealed partial class Navigation
{
    private void TryScheduleRouteAwareReroute(Vector3 playerPosition, CancellationToken token)
    {
        IRouteRerouter? rerouter = routeRerouter;
        if (rerouter == null ||
            featureFlagService?.Current.HazardAvoidance.Enabled != true ||
            routeToNextWaypoint.Count < 2 ||
            rerouter.GetActiveReroute() != null)
        {
            return;
        }

        Vector3[] routeSnapshot = routeToNextWaypoint.ToArray();
        if (!TrySelectDetourAnchor(routeSnapshot, playerPosition, out Vector3 detourAnchor))
        {
            return;
        }

        float anchorDistance = playerPosition.WorldDistanceXYTo(detourAnchor);
        _ = TriggerRouteAwareRerouteAsync(
            rerouter,
            playerPosition,
            detourAnchor,
            anchorDistance,
            playerReader.UIMapId.Value,
            token);
    }

    private async Task TriggerRouteAwareRerouteAsync(
        IRouteRerouter rerouter,
        Vector3 playerPosition,
        Vector3 detourAnchor,
        float anchorDistance,
        int mapId,
        CancellationToken token)
    {
        try
        {
            bool triggered = await rerouter.TriggerRerouteAsync(playerPosition, detourAnchor, mapId, token)
                .ConfigureAwait(false);

            if (triggered)
            {
                RecordRerouteTriggered(anchorDistance);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Navigation       ] Route-aware reroute scheduling failed");
        }
    }

    internal static bool TrySelectDetourAnchor(
        IReadOnlyList<Vector3> routeTopFirst,
        Vector3 playerPosition,
        out Vector3 detourAnchor,
        int lookaheadPoints = RouteAwareDetourLookaheadPoints,
        float minAnchorDistance = RouteAwareDetourMinAnchorDistance)
    {
        detourAnchor = default;
        if (routeTopFirst.Count == 0)
        {
            return false;
        }

        int inspectCount = Math.Min(Math.Max(1, routeTopFirst.Count - 1), Math.Max(1, lookaheadPoints));
        float furthestDistance = -1f;
        Vector3 furthestPoint = default;

        for (int i = 0; i < inspectCount; i++)
        {
            Vector3 candidate = routeTopFirst[i];
            float distance = playerPosition.WorldDistanceXYTo(candidate);

            if (distance > furthestDistance)
            {
                furthestDistance = distance;
                furthestPoint = candidate;
            }

            if (distance >= minAnchorDistance)
            {
                detourAnchor = candidate;
                return true;
            }
        }

        if (furthestDistance <= 0.01f)
        {
            return false;
        }

        detourAnchor = furthestPoint;
        return true;
    }

    internal static bool ShouldApplyPendingReroute(
        RerouteInfo pendingReroute,
        IReadOnlyList<Vector3> routeTopFirst,
        DateTime nowUtc,
        int maxAgeSeconds = PendingRerouteMaxAgeSeconds,
        float targetMatchDistance = PendingRerouteTargetMatchDistance)
    {
        if (routeTopFirst.Count == 0)
        {
            return false;
        }

        TimeSpan age = nowUtc - pendingReroute.StartedAt;
        if (age < TimeSpan.Zero || age > TimeSpan.FromSeconds(maxAgeSeconds))
        {
            return false;
        }

        for (int i = 0; i < routeTopFirst.Count; i++)
        {
            if (routeTopFirst[i].WorldDistanceXYTo(pendingReroute.OriginalTarget) <= targetMatchDistance)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearPendingReroute()
    {
        routeRerouter?.ClearActiveReroute();
    }

    private void RecordRerouteTriggered(float anchorDistance)
    {
        rerouteTriggerCount++;
        lastRerouteAnchorDistance = anchorDistance;
        lastRerouteDropReason = null;
        OnRerouteTriggered?.Invoke();
    }

    private void RecordRerouteApplied()
    {
        rerouteApplyCount++;
        OnRerouteApplied?.Invoke();
    }

    private void RecordRerouteDropped(string reason, float? anchorDistance = null)
    {
        rerouteDropCount++;
        lastRerouteDropReason = reason;
        if (anchorDistance.HasValue)
        {
            lastRerouteAnchorDistance = anchorDistance.Value;
        }

        OnRerouteDropped?.Invoke(reason);
    }

    private void RecordDetourOnlyCollapse()
    {
        detourOnlyCollapseCount++;
        OnDetourOnlyCollapseDetected?.Invoke();
    }

    internal static Vector3[] BuildInlineDetourRoute(
        Vector3[] detourWaypoints,
        IReadOnlyList<Vector3> preservedRouteTopFirst,
        Vector3 playerPosition,
        float reconnectDistance = DetourReconnectDistance,
        float duplicateDistance = DetourDuplicateDistance)
    {
        int continuationStartIndex = 0;
        if (detourWaypoints.Length > 0 && preservedRouteTopFirst.Count > 0)
        {
            Vector3 reconnectPoint = detourWaypoints[^1];
            int reconnectIndex = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < preservedRouteTopFirst.Count; i++)
            {
                float distance = preservedRouteTopFirst[i].WorldDistanceXYTo(reconnectPoint);
                if (distance <= reconnectDistance &&
                    (distance < bestDistance || (Abs(distance - bestDistance) <= 0.001f && i > reconnectIndex)))
                {
                    reconnectIndex = i;
                    bestDistance = distance;
                }
            }

            if (reconnectIndex >= 0)
            {
                continuationStartIndex = reconnectIndex + 1;
            }
            else
            {
                int destinationIndex = preservedRouteTopFirst.Count - 1;
                if (preservedRouteTopFirst[destinationIndex].WorldDistanceXYTo(reconnectPoint) <= reconnectDistance * 4f)
                {
                    continuationStartIndex = preservedRouteTopFirst.Count;
                }
            }
        }

        List<Vector3> mergedTopFirst = new(detourWaypoints.Length + Math.Max(0, preservedRouteTopFirst.Count - continuationStartIndex));

        static void AddIfDistinct(List<Vector3> points, Vector3 candidate, float minDistance)
        {
            if (points.Count > 0 &&
                Vector3.Distance(points[points.Count - 1], candidate) <= minDistance)
            {
                return;
            }

            points.Add(candidate);
        }

        for (int i = 0; i < detourWaypoints.Length; i++)
        {
            Vector3 candidate = detourWaypoints[i];
            if (mergedTopFirst.Count == 0 &&
                playerPosition.WorldDistanceXYTo(candidate) <= duplicateDistance)
            {
                continue;
            }

            AddIfDistinct(mergedTopFirst, candidate, duplicateDistance);
        }

        for (int i = continuationStartIndex; i < preservedRouteTopFirst.Count; i++)
        {
            AddIfDistinct(mergedTopFirst, preservedRouteTopFirst[i], duplicateDistance);
        }

        return mergedTopFirst.ToArray();
    }

    /// <summary>
    /// Prepends detour waypoints to the front of the current route without replacing it.
    /// After the detour is traversed, normal route progression resumes.
    /// </summary>
    private void InsertDetourIntoRoute(Vector3[] detourWaypoints)
    {
        if (detourWaypoints.Length == 0)
        {
            return;
        }

        // Drain current route into a temporary list (stack order: top-first = nearest-first)
        List<Vector3> preserved = new(routeToNextWaypoint.Count);
        int preservedCount = routeToNextWaypoint.Count;
        while (routeToNextWaypoint.Count > 0)
        {
            preserved.Add(routeToNextWaypoint.Pop());
        }

        Vector3[] mergedRoute = BuildInlineDetourRoute(
            detourWaypoints,
            preserved,
            playerReader.WorldPos);

        for (int i = mergedRoute.Length - 1; i >= 0; i--)
        {
            routeToNextWaypoint.Push(mergedRoute[i]);
        }

        stuckDetector.Reset();
        if (routeToNextWaypoint.Count > 0)
        {
            stuckDetector.SetTargetLocation(routeToNextWaypoint.Peek());
        }

        if (routeToNextWaypoint.Count == 0)
        {
            RecordDetourOnlyCollapse();
            RecordRerouteDropped("detour-collapsed");
            return;
        }

        RecordRerouteApplied();
        OnDynamicDetourApplied?.Invoke();
        if (DebugEnabled)
        {
            LogDebug($"[HazardAvoidance] Inserted detour ({detourWaypoints.Length} points) into route ({preservedCount} points) => {routeToNextWaypoint.Count} points");
        }
    }
}
