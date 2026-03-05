using System;
using System.Numerics;

namespace Core.Goals;

/// <summary>
/// Diagnostic-only tracker for route progression invariants.
/// Detects likely regressions when a rebuilt route head re-enters a recently consumed segment.
/// </summary>
public struct RouteSegmentTracker
{
    private const float RegressionDistanceThreshold = 10f;

    private bool hasLastConsumedPoint;
    private Vector3 lastConsumedPoint;

    public int RouteRebuildCount { get; private set; }
    public int WaypointTransitionCount { get; private set; }
    public int SubSegmentTransitionCount { get; private set; }
    public int RegressionCount { get; private set; }
    public string? LastRegressionReason { get; private set; }
    public DateTime? LastRegressionUtc { get; private set; }

    public void Reset()
    {
        hasLastConsumedPoint = false;
        lastConsumedPoint = default;
        RouteRebuildCount = 0;
        WaypointTransitionCount = 0;
        SubSegmentTransitionCount = 0;
        RegressionCount = 0;
        LastRegressionReason = null;
        LastRegressionUtc = null;
    }

    public void ObserveSubSegmentConsumed(Vector3 consumedPoint)
    {
        hasLastConsumedPoint = true;
        lastConsumedPoint = consumedPoint;
        SubSegmentTransitionCount++;
    }

    public void ObserveWaypointConsumed()
    {
        WaypointTransitionCount++;
    }

    public bool ObserveRouteRebuild(ReadOnlySpan<Vector3> routeTopFirst, string source, out string? warning)
    {
        RouteRebuildCount++;
        warning = null;

        if (!hasLastConsumedPoint || routeTopFirst.Length == 0)
        {
            return false;
        }

        Vector3 newHead = routeTopFirst[0];
        if (DistanceXY(newHead, lastConsumedPoint) > RegressionDistanceThreshold)
        {
            return false;
        }

        RegressionCount++;
        LastRegressionUtc = DateTime.UtcNow;
        LastRegressionReason = $"{source}: route head revisited consumed segment ({newHead.X:F2},{newHead.Y:F2})";
        warning = LastRegressionReason;
        return true;
    }

    private static float DistanceXY(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
