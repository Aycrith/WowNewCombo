using Core.Hazard;

using SharedLib.Extensions;

using System;
using System.Numerics;

namespace Core.Goals;

/// <summary>
/// Coordinates route rehabilitation by reporting successful traversals to the RouteRehabilitator.
/// Helps optimize pathfinding by learning from successful routes.
/// </summary>
public sealed class RouteRehabilitationCoordinator
{
    private readonly RouteRehabilitator routeRehabilitator;

    private const float REHAB_MIN_DISTANCE = 25f;
    private static readonly TimeSpan REHAB_MIN_INTERVAL = TimeSpan.FromSeconds(20);

    private DateTime lastRehabUtc;
    private Vector3 lastRehabWorldPos;
    private bool hasLastRehab;

    public RouteRehabilitationCoordinator(RouteRehabilitator routeRehabilitator)
    {
        this.routeRehabilitator = routeRehabilitator;
    }

    /// <summary>
    /// Reports a successful traversal for route rehabilitation if conditions are met.
    /// </summary>
    /// <param name="playerPos">The player's current position</param>
    /// <param name="mapId">The current map ID</param>
    /// <param name="radius">The radius for rehabilitation (default: 20f)</param>
    /// <param name="severityFactor">The severity factor (default: 0.95f)</param>
    /// <returns>True if rehabilitation was reported, false if skipped due to cooldown or proximity</returns>
    public bool TryRehabilitate(Vector3 playerPos, int mapId, float radius = 20f, float severityFactor = 0.95f)
    {
        DateTime now = DateTime.UtcNow;

        if (hasLastRehab)
        {
            if (now - lastRehabUtc < REHAB_MIN_INTERVAL &&
                playerPos.WorldDistanceXYTo(lastRehabWorldPos) < REHAB_MIN_DISTANCE)
            {
                return false;
            }
        }

        routeRehabilitator.ReportSuccessfulTraversal(
            playerPos,
            mapId,
            radius: radius,
            severityFactor: severityFactor);

        lastRehabUtc = now;
        lastRehabWorldPos = playerPos;
        hasLastRehab = true;

        return true;
    }

    /// <summary>
    /// Resets the rehabilitation state
    /// </summary>
    public void Reset()
    {
        hasLastRehab = false;
        lastRehabUtc = default;
        lastRehabWorldPos = default;
    }
}
