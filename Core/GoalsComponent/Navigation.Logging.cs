using Microsoft.Extensions.Logging;

using System.Numerics;

namespace Core.Goals;

public sealed partial class Navigation
{
    [LoggerMessage(
        EventId = 0040,
        Level = LogLevel.Warning,
        Message = "Unable to find path {start} -> {end}. Character may stuck! {elapsedMs}ms")]
    static partial void LogPathfinderFailed(ILogger logger, Vector3 start, Vector3 end, double elapsedMs);

    [LoggerMessage(
        EventId = 0041,
        Level = LogLevel.Information,
        Message = "Pathfinder - {distance} - {start} -> {end} {elapsedMs}ms")]
    static partial void LogPathfinderSuccess(ILogger logger, float distance, Vector3 start, Vector3 end, double elapsedMs);

    [LoggerMessage(
        EventId = 0042,
        Level = LogLevel.Information,
        Message = "Clear route to waypoint! Stucked for {elapsedMs}ms")]
    static partial void LogClearRouteToWaypointStuck(ILogger logger, double elapsedMs);

    [LoggerMessage(
        EventId = 0043,
        Level = LogLevel.Information,
        Message = "[{name}] distance from nearlest point is {distance}. Have to clear RouteToWaypoint.")]
    static partial void LogV1ClearRouteToWaypoint(ILogger logger, string name, float distance);

    [LoggerMessage(
        EventId = 0044,
        Level = LogLevel.Information,
        Message = "[{name}] distance is close {distance}. Keep RouteToWaypoint.")]
    static partial void LogV1KeepRouteToWaypoint(ILogger logger, string name, float distance);

    [LoggerMessage(
        EventId = 0045,
        Level = LogLevel.Information,
        Message = "[{name}] total distance {totalDistance} > {maxDistancehalf}. Have to clear RouteToWaypoint.")]
    static partial void LogV1ClearRouteToWaypointTooFar(ILogger logger, string name, float totalDistance, float maxDistancehalf);
}
