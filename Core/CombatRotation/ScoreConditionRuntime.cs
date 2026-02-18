namespace Core.CombatRotation;

/// <summary>
/// Compiled runtime representation of a ScoreConditionEntry.
/// The Condition string has been parsed into a Requirement delegate.
/// </summary>
public readonly record struct ScoreConditionRuntime(
    Requirement Requirement,
    float Bonus);
