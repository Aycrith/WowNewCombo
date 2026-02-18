using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Core;

/// <summary>
/// Interface for auto-rerouting service that detects hot zones ahead and calculates detour paths.
/// </summary>
public interface IRouteRerouter
{
    /// <summary>
    /// Triggered when a reroute is initiated due to hot zone detection.
    /// </summary>
    event Action<RerouteEventArgs>? OnRerouteTriggered;

    /// <summary>
    /// Triggered when a detour path is calculated successfully.
    /// </summary>
    event Action<DetourPathCalculatedEventArgs>? OnDetourCalculated;

    /// <summary>
    /// Checks if auto-rerouting is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets the minimum hot zone severity threshold for triggering reroutes.
    /// </summary>
    float HotZoneSeverityThreshold { get; set; }

    /// <summary>
    /// Gets the safety margin distance for detours around hot zones.
    /// </summary>
    float SafetyMargin { get; set; }

    /// <summary>
    /// Triggers a reroute check asynchronously.
    /// Returns true if a reroute was initiated.
    /// </summary>
    Task<bool> TriggerRerouteAsync(
        Vector3 currentPosition,
        Vector3 targetPosition,
        int mapId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates a detour path around hot zones.
    /// Returns null if no detour is needed.
    /// </summary>
    Task<Vector3[]?> CalculateDetourAsync(
        Vector3[] originalPath,
        int mapId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets any active reroute in progress.
    /// </summary>
    RerouteInfo? GetActiveReroute();

    /// <summary>
    /// Clears any active reroute.
    /// </summary>
    void ClearActiveReroute();

    /// <summary>
    /// Enables or disables auto-rerouting.
    /// </summary>
    void SetEnabled(bool enabled);
}

/// <summary>
/// Event args for reroute triggering.
/// </summary>
public sealed class RerouteEventArgs : EventArgs
{
    /// <summary>
    /// The position where the reroute was triggered.
    /// </summary>
    public Vector3 TriggerPosition { get; init; }

    /// <summary>
    /// The reason for the reroute.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// The hot zones that triggered the reroute.
    /// </summary>
    public IReadOnlyList<HotZoneInfo> TriggeringZones { get; init; } = Array.Empty<HotZoneInfo>();

    /// <summary>
    /// Timestamp when the reroute was triggered.
    /// </summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Event args for detour path calculation.
/// </summary>
public sealed class DetourPathCalculatedEventArgs : EventArgs
{
    /// <summary>
    /// The original path that was being followed.
    /// </summary>
    public Vector3[] OriginalPath { get; init; } = Array.Empty<Vector3>();

    /// <summary>
    /// The calculated detour path.
    /// </summary>
    public Vector3[] DetourPath { get; init; } = Array.Empty<Vector3>();

    /// <summary>
    /// The hot zones being avoided.
    /// </summary>
    public IReadOnlyList<HotZoneInfo> AvoidedZones { get; init; } = Array.Empty<HotZoneInfo>();

    /// <summary>
    /// Additional distance added by the detour.
    /// </summary>
    public float AdditionalDistance { get; init; }

    /// <summary>
    /// Time taken to calculate the detour.
    /// </summary>
    public TimeSpan CalculationTime { get; init; }
}

/// <summary>
/// Information about a hot zone.
/// </summary>
public sealed class HotZoneInfo
{
    /// <summary>
    /// Center position of the hot zone.
    /// </summary>
    public Vector3 Center { get; init; }

    /// <summary>
    /// Radius of the hot zone.
    /// </summary>
    public float Radius { get; init; }

    /// <summary>
    /// Number of failures in this zone.
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// Severity score of the zone (0-10).
    /// </summary>
    public float Severity { get; init; }

    /// <summary>
    /// Primary failure type in this zone.
    /// </summary>
    public string FailureType { get; init; } = string.Empty;
}

/// <summary>
/// Information about an active reroute.
/// </summary>
public sealed class RerouteInfo
{
    /// <summary>
    /// Unique identifier for the reroute.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// When the reroute was initiated.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// The original target position.
    /// </summary>
    public Vector3 OriginalTarget { get; init; }

    /// <summary>
    /// The detour waypoints.
    /// </summary>
    public Vector3[] DetourWaypoints { get; init; } = Array.Empty<Vector3>();

    /// <summary>
    /// Current waypoint index.
    /// </summary>
    public int CurrentWaypointIndex { get; set; }

    /// <summary>
    /// Whether the reroute has been completed.
    /// </summary>
    public bool IsCompleted { get; set; }
}
