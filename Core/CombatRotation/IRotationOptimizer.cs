using System;

namespace Core.CombatRotation;

/// <summary>
/// Interface for the combat rotation optimizer.
/// Injected into CombatGoal to provide per-tick ability scoring and reordering.
/// </summary>
public interface IRotationOptimizer
{
    /// <summary>
    /// Scores and sorts abilities by priority for the current tick.
    /// Populates <paramref name="sortedIndices"/> with indices into <paramref name="keys"/>
    /// in descending score order.
    /// </summary>
    /// <param name="keys">The ability sequence from the class profile.</param>
    /// <param name="state">Current combat state snapshot.</param>
    /// <param name="sortedIndices">
    /// Caller-provided span to receive sorted indices.
    /// Must be at least <paramref name="keys"/>.Length in size.
    /// </param>
    /// <returns>Number of valid entries written to <paramref name="sortedIndices"/>.</returns>
    int Optimize(ReadOnlySpan<KeyAction> keys, in GameStateSnapshot state, Span<int> sortedIndices);

    /// <summary>
    /// Records the result of a cast attempt for metrics tracking.
    /// </summary>
    /// <param name="action">The ability that was attempted.</param>
    /// <param name="success">Whether the cast succeeded.</param>
    void RecordCastResult(KeyAction action, bool success);

    /// <summary>
    /// Whether the optimizer is currently enabled via feature flags.
    /// When false, CombatGoal should use the original iteration order.
    /// </summary>
    bool IsEnabled { get; }
}
