using SharedLib;
using SharedLib.Data;
using SharedLib.Extensions;

using System;
using System.Collections.Generic;
using System.Numerics;

namespace Core;

public static class RerouteProbeResolver
{
    public static Vector3? TryResolveFromMapRoute(
        IReadOnlyList<Vector3> mapRoute,
        Vector3 playerMapPosition,
        Vector3 playerWorldPosition,
        WorldMapArea worldMapArea,
        int lookaheadPoints = 4,
        float minAnchorDistance = 8.0f)
    {
        if (mapRoute.Count == 0)
        {
            return null;
        }

        (int segmentStartIndex, Vector3 closestPoint) = FindClosestSegmentPoint(mapRoute, playerMapPosition);
        List<Vector3> routeTopFirst = new(mapRoute.Count - segmentStartIndex + 1)
        {
            NormalizeWorldPointZ(
                WorldMapAreaDB.ToWorld_FlipXY(closestPoint, worldMapArea),
                playerWorldPosition)
        };

        for (int i = segmentStartIndex + 1; i < mapRoute.Count; i++)
        {
            routeTopFirst.Add(NormalizeWorldPointZ(
                WorldMapAreaDB.ToWorld_FlipXY(mapRoute[i], worldMapArea),
                playerWorldPosition));
        }

        return TrySelectDetourAnchor(routeTopFirst, playerWorldPosition, lookaheadPoints, minAnchorDistance);
    }

    private static (int SegmentStartIndex, Vector3 ClosestPoint) FindClosestSegmentPoint(
        IReadOnlyList<Vector3> mapRoute,
        Vector3 playerMapPosition)
    {
        if (mapRoute.Count == 1)
        {
            return (0, mapRoute[0]);
        }

        Vector2 playerXY = playerMapPosition.AsVector2();
        int closestSegmentStartIndex = 0;
        Vector3 closestPoint = mapRoute[0];
        float bestDistance = float.MaxValue;

        for (int i = 0; i < mapRoute.Count - 1; i++)
        {
            Vector3 segmentStart = mapRoute[i];
            Vector3 segmentEnd = mapRoute[i + 1];
            Vector2 pointOnSegment = VectorExt.GetClosestPointOnLineSegment(
                segmentStart.AsVector2(),
                segmentEnd.AsVector2(),
                playerXY);
            float distance = Vector2.Distance(playerXY, pointOnSegment);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closestSegmentStartIndex = i;
                closestPoint = new Vector3(pointOnSegment.X, pointOnSegment.Y, 0);
            }
        }

        return (closestSegmentStartIndex, closestPoint);
    }

    private static Vector3 NormalizeWorldPointZ(Vector3 worldPoint, Vector3 playerWorldPosition)
    {
        if (MathF.Abs(worldPoint.Z) > 0.01f || MathF.Abs(playerWorldPosition.Z) <= 0.01f)
        {
            return worldPoint;
        }

        return new Vector3(worldPoint.X, worldPoint.Y, playerWorldPosition.Z);
    }

    private static Vector3? TrySelectDetourAnchor(
        IReadOnlyList<Vector3> routeTopFirst,
        Vector3 playerWorldPosition,
        int lookaheadPoints,
        float minAnchorDistance)
    {
        if (routeTopFirst.Count == 0)
        {
            return null;
        }

        int inspectCount = Math.Min(Math.Max(1, routeTopFirst.Count - 1), Math.Max(1, lookaheadPoints));
        float furthestDistance = -1f;
        Vector3 furthestPoint = default;

        for (int i = 0; i < inspectCount; i++)
        {
            Vector3 candidate = routeTopFirst[i];
            float distance = playerWorldPosition.WorldDistanceXYTo(candidate);

            if (distance > furthestDistance)
            {
                furthestDistance = distance;
                furthestPoint = candidate;
            }

            if (distance >= minAnchorDistance)
            {
                return candidate;
            }
        }

        return furthestDistance <= 0.01f
            ? null
            : furthestPoint;
    }
}
