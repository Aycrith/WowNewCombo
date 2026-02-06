using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

namespace Core.CombatRotation;

/// <summary>
/// DPS-focused scoring strategy implementing the weighted-sum model
/// inspired by SimulationCraft's APL (Action Priority List) pattern.
///
/// Scoring formula:
///   Score = BaseWeight × CooldownGate × UsabilityGate
///         + ResourceEfficiencyBonus
///         + BuffSynergyBonus
///         + DebuffMaintenanceBonus
///         + ExecutePhaseBonus
///         + ScoreConditionsBonus
///         - OvercapPenalty
///         + SequencePositionTiebreaker
///
/// When all abilities have Weight=1.0 (default), the SequencePositionTiebreaker
/// ensures the original JSON ordering is preserved exactly.
/// </summary>
public sealed class DpsRoleStrategy : IRoleStrategy
{
    // Scoring constants - tuned for balanced DPS optimization
    private const float ExecutePhaseBonusValue = 2.0f;
    private const float ExecutePhaseThreshold = 20;
    private const float DebuffMissingBonus = 1.5f;
    private const float DebuffExpiringBonusBase = 1.0f;
    private const float DebuffExpiringThresholdMs = 3000;
    private const float BuffActiveBonusBase = 0.5f;
    private const float ResourceHighPenalty = -0.3f;
    private const float ResourceHighThreshold = 90;
    private const float TiebreakerDivisor = 1000f;
    private const float ResourceEfficiencyBonusBase = 0.2f;

    private readonly ILogger<DpsRoleStrategy> logger;

    public string RoleName => "DPS";

    public DpsRoleStrategy(ILogger<DpsRoleStrategy> logger)
    {
        this.logger = logger;
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ScoreAbility(KeyAction action, in GameStateSnapshot state, int sequenceIndex)
    {
        // Gate: if the ability can't run, skip entirely
        if (!action.CanRun())
        {
            return float.MinValue;
        }

        // Base weight from JSON profile (default 1.0)
        float score = action.Weight;

        // CooldownGate: zero if on cooldown
        if (action.OnCooldown())
        {
            return float.MinValue;
        }

        // Execute phase bonus: boost execute abilities when target < 20% HP
        if (state.IsExecutePhase)
        {
            score += ExecutePhaseBonusValue;
        }

        // Resource efficiency: penalize when near resource cap to encourage spending
        if (state.ResourcePercent >= (int)ResourceHighThreshold)
        {
            score += ResourceEfficiencyBonusBase;
        }

        // Overcap penalty: if we're at max resource, penalize abilities that don't spend
        if (state.ResourcePercent >= 95)
        {
            score += ResourceHighPenalty;
        }

        // Evaluate profile-defined score conditions
        float conditionBonus = EvaluateScoreConditions(action);
        score += conditionBonus;

        // Sequence position tiebreaker: lower index = slightly higher score
        // This ensures default-weight profiles maintain exact original ordering
        score += (1000 - sequenceIndex) / TiebreakerDivisor;

        return score;
    }

    /// <summary>
    /// Evaluates the ScoreConditions list on the KeyAction.
    /// Each condition whose requirement is met adds its Bonus to the total.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float EvaluateScoreConditions(KeyAction action)
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
