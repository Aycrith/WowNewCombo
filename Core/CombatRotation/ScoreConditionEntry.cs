using System.Collections.Generic;

namespace Core.CombatRotation;

/// <summary>
/// JSON-serializable model for per-ability conditional score bonuses.
/// Used in KeyAction.ScoreConditions to define weighted scoring rules.
/// </summary>
/// <example>
/// JSON usage:
/// <code>
/// {
///   "Name": "Heroic Strike",
///   "Key": "2",
///   "Weight": 1.5,
///   "ScoreConditions": [
///     { "Condition": "Rage > 60", "Bonus": 0.5 },
///     { "Condition": "TargetHealth% &lt; 20", "Bonus": 1.0 }
///   ]
/// }
/// </code>
/// </example>
public sealed class ScoreConditionEntry
{
    /// <summary>
    /// Condition expression string, evaluated by RequirementFactory.
    /// Uses the same syntax as KeyAction.Requirements.
    /// </summary>
    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// Score bonus added when the condition evaluates to true.
    /// Can be negative to penalize.
    /// </summary>
    public float Bonus { get; set; }
}
