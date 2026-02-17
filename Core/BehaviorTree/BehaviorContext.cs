namespace Core.BehaviorTree;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Core;
using Core.GOAP;
using Core.Goals;

using Microsoft.Extensions.Logging;

/// <summary>
/// Shared context passed to behavior tree nodes during execution.
/// Contains all game state and services needed for decision making.
/// </summary>
public sealed class BehaviorContext
{
    /// <summary>
    /// Player state reader.
    /// </summary>
    public required PlayerReader Player { get; init; }

    /// <summary>
    /// Casting handler for spell execution.
    /// </summary>
    public required CastingHandler Casting { get; init; }

    /// <summary>
    /// Stop moving action.
    /// </summary>
    public required StopMoving StopMoving { get; init; }

    /// <summary>
    /// Configurable input for key presses.
    /// </summary>
    public required ConfigurableInput Input { get; init; }

    /// <summary>
    /// Logger for debugging.
    /// </summary>
    public required ILogger Logger { get; init; }

    /// <summary>
    /// Cancellation token for async operations.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Current target information (if any).
    /// </summary>
    public TargetInfo? CurrentTarget { get; set; }

    /// <summary>
    /// Number of nearby enemies detected.
    /// </summary>
    public int NearbyEnemies { get; set; }

    /// <summary>
    /// Time elapsed since last action in milliseconds.
    /// </summary>
    public double ElapsedMs { get; set; }

    /// <summary>
    /// Custom state bag for node-specific data.
    /// </summary>
    public Dictionary<string, object> State { get; } = new();

    /// <summary>
    /// Last action that was executed.
    /// </summary>
    public KeyAction? LastAction { get; set; }

    /// <summary>
    /// Current combat sequence configuration.
    /// </summary>
    public IReadOnlyList<KeyAction> CombatSequence { get; init; } = Array.Empty<KeyAction>();
}

/// <summary>
/// Information about current target.
/// </summary>
public sealed class TargetInfo
{
    /// <summary>
    /// Target health percentage (0-100).
    /// </summary>
    public float HealthPercent { get; init; }

    /// <summary>
    /// Target level.
    /// </summary>
    public int Level { get; init; }

    /// <summary>
    /// Whether target is dead.
    /// </summary>
    public bool IsDead { get; init; }

    /// <summary>
    /// Whether target is elite.
    /// </summary>
    public bool IsElite { get; init; }

    /// <summary>
    /// Distance to target.
    /// </summary>
    public float Distance { get; init; }

    /// <summary>
    /// Target position in world coordinates.
    /// </summary>
    public Vector3 Position { get; init; }
}
