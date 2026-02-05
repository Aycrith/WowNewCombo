using System;
using System.Numerics;

namespace Core.Hazard;

/// <summary>
/// Record of a hazard event with spatial and temporal metadata.
/// Immutable for thread-safe sharing across analytics pipeline.
/// </summary>
public sealed record HazardEvent
{
    /// <summary>World position (game coordinates).</summary>
    public required Vector3 WorldPosition { get; init; }

    /// <summary>Map position X (for UI display).</summary>
    public float MapX { get; init; }

    /// <summary>Map position Y (for UI display).</summary>
    public float MapY { get; init; }

    /// <summary>Map identifier (e.g., 0=Eastern Kingdoms, 1=Kalimdor).</summary>
    public required int MapId { get; init; }

    /// <summary>UI map identifier for zone-specific data.</summary>
    public required int UIMapId { get; init; }

    /// <summary>Type of hazard event.</summary>
    public required HazardEventType Type { get; init; }

    /// <summary>UTC timestamp when event occurred.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Zone name (for debugging).</summary>
    public string Zone { get; init; } = string.Empty;

    /// <summary>Duration stuck (milliseconds) - only for Stuck events.</summary>
    public int DurationMs { get; init; }

    /// <summary>Number of recovery attempts when this hazard was captured.</summary>
    public int AttemptCount { get; init; }

    /// <summary>Class name for filtering class-specific hazards.</summary>
    public string? PlayerClass { get; init; }

    /// <summary>Level at time of event for progression analysis.</summary>
    public int PlayerLevel { get; init; }
}

