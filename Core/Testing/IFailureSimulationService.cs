using System;
using System.Numerics;

using Core.Analytics;

namespace Core.Testing;

/// <summary>
/// Interface for failure simulation used by RouteVisualizationService.
/// Implemented by MockWoWClient to provide testing capabilities.
/// </summary>
public interface IFailureSimulationService
{
    event Action<SimulatedStuckEvent>? OnStuckSimulated;
    event Action<SimulatedDeathEvent>? OnDeathSimulated;
    event Action<SimulatedRehabEvent>? OnRehabSimulated;
    event Action<SimulatedHotZone>? OnHotZoneCreated;

    void SimulateStuck(UnstuckState stuckState, int durationMs = 3000, int attemptCount = 1);
    void SimulateDeath(string cause = "Simulated death");
    void SimulateHotZone(FailureType failureType, int failureCount = 3, float radius = 10f);
    void SimulateRehabilitation(Vector3 position, float radius = 25f);
}

/// <summary>
/// Represents a simulated stuck event.
/// </summary>
public sealed class SimulatedStuckEvent
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public Vector3 Position { get; set; }
    public int MapId { get; set; }
    public int UIMapId { get; set; }
    public float Direction { get; set; }
    public UnstuckState State { get; set; }
    public int DurationMs { get; set; }
    public bool IsSpinning { get; set; }
    public int AttemptCount { get; set; }
    public bool IsFlashingMarker { get; set; } = true;
}

/// <summary>
/// Represents a simulated death event.
/// </summary>
public sealed class SimulatedDeathEvent
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public Vector3 Position { get; set; }
    public int MapId { get; set; }
    public int UIMapId { get; set; }
    public string Cause { get; set; } = string.Empty;
    public int Level { get; set; }
}

/// <summary>
/// Represents a simulated rehabilitation event.
/// </summary>
public sealed class SimulatedRehabEvent
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public Vector3 Position { get; set; }
    public float Radius { get; set; }
    public float SeverityReduction { get; set; }
}

/// <summary>
/// Represents a simulated hot zone.
/// </summary>
public sealed class SimulatedHotZone
{
    public Guid Id { get; set; }
    public Vector3 Center { get; set; }
    public int MapId { get; set; }
    public int FailureCount { get; set; }
    public FailureType PrimaryType { get; set; }
    public DateTime CreatedAt { get; set; }
    public float Radius { get; set; }
}
