namespace Core.CombatRotation;

/// <summary>
/// Pluggable scoring strategy for role-specific ability evaluation.
/// Implement this interface to add scoring logic for different roles
/// (DPS, Tank, Healer).
/// </summary>
public interface IRoleStrategy
{
    /// <summary>
    /// Computes a priority score for the given ability based on current game state.
    /// Higher scores indicate higher priority.
    /// </summary>
    /// <param name="action">The ability to score.</param>
    /// <param name="state">Current combat state snapshot (stack-allocated, zero-copy).</param>
    /// <param name="sequenceIndex">Original position in the JSON profile sequence (for tiebreaking).</param>
    /// <returns>Priority score. <see cref="float.MinValue"/> means skip entirely.</returns>
    float ScoreAbility(KeyAction action, in GameStateSnapshot state, int sequenceIndex);

    /// <summary>
    /// Human-readable name for this role strategy (e.g., "DPS", "Tank", "Healer").
    /// </summary>
    string RoleName { get; }
}
