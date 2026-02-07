using System.Runtime.CompilerServices;

namespace Core.CombatRotation;

/// <summary>
/// Shared helper methods for role strategy implementations.
/// Extracts common functionality to eliminate code duplication across strategies.
/// </summary>
public static class RoleStrategyHelpers
{
    /// <summary>
    /// Evaluates the ScoreConditions list on the KeyAction.
    /// Each condition whose requirement is met adds its Bonus to the total.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float EvaluateScoreConditions(KeyAction action)
    {
        if (action.ScoreConditionsRuntime is not { Length: > 0 } conditions)
        {
            return 0f;
        }

        float bonus = 0f;
        for (int i = 0; i < conditions.Length; i++)
        {
            ScoreConditionRuntime condition = conditions[i];
            if (condition.Requirement.HasRequirement())
            {
                bonus += condition.Bonus;
            }
        }

        return bonus;
    }
}
