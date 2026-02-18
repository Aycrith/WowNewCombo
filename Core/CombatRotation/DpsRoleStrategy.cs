using System;
using System.Runtime.CompilerServices;

using Core.FeatureFlags;

using Microsoft.Extensions.Logging;

namespace Core.CombatRotation;

/// <summary>
/// DPS-focused scoring strategy implementing the weighted-sum model
/// inspired by SimulationCraft's APL (Action Priority List) pattern.
///
/// Scoring formula:
/// Score = BaseWeight × CooldownGate × UsabilityGate
/// + ResourceEfficiencyBonus
/// + BuffSynergyBonus
/// + DebuffMaintenanceBonus
/// + ExecutePhaseBonus
/// + ScoreConditionsBonus
/// - OvercapPenalty
/// + SequencePositionTiebreaker
///
/// When all abilities have Weight=1.0 (default), the SequencePositionTiebreaker
/// ensures the original JSON ordering is preserved exactly.
/// </summary>
public sealed class DpsRoleStrategy : IRoleStrategy
{
    // Scoring constants - tuned for balanced DPS optimization
    private const float ExecutePhaseBonusValue = 2.0f;
    private const float ExecutePhaseThreshold = 20;
    private const float ResourceHighPenalty = -0.3f;
    private const float ResourceHighThreshold = 90;
    private const float TiebreakerDivisor = 1000f;
    private const float ResourceEfficiencyBonusBase = 0.2f;

    // Swing timer alignment constants
    private const float SwingAlignmentBonus = 0.5f; // Bonus for weaving between swings
    private const float SwingClipPenalty = -1.0f; // Penalty for clipping auto-attack
    private const int SwingClipThresholdMs = 500; // Time window where instants clip
    private const int SwingSafetyBufferMs = 200; // Safe window to cast instant

    // Movement bonus for gap closing
    private const float MovementGapCloseBonus = 1.0f;

    private readonly ILogger<DpsRoleStrategy> logger;
    private readonly FeatureFlagService? featureFlags;

    public string RoleName => "DPS";

    public DpsRoleStrategy(ILogger<DpsRoleStrategy> logger, FeatureFlagService? featureFlags = null)
    {
        this.logger = logger;
        this.featureFlags = featureFlags;
    }

    [SkipLocalsInit]
    public float ScoreAbility(KeyAction action, in GameStateSnapshot state, int sequenceIndex)
    {
        try
        {
            // Gate: if the ability can't run, skip entirely
            if (!action.CanRun())
            {
                return float.MinValue;
            }

            // CooldownGate: zero if on cooldown
            if (action.OnCooldown())
            {
                return float.MinValue;
            }

            // Base weight from JSON profile (default 1.0)
            float score = action.Weight;

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

            // Use cached ability type from KeyAction
            AbilityType abilityType = action.AbilityType;

            // Swing timer alignment: weave instant abilities between auto-attacks
            // to avoid clipping the swing timer (e.g., Heroic Strike queueing, Rogue weaving)
            if (featureFlags?.Current.CombatRotationOptimizer.EnableSwingTimerAlignment == true &&
                state.MainHandSpeedMs > 0)
            {
                int swingRemainingMs = state.MainHandSpeedMs - state.MainHandSwingElapsedMs;

                // Instant abilities (no cast bar, not on-next-swing)
                if (IsInstantAbility(action, abilityType))
                {
                    if (swingRemainingMs < SwingSafetyBufferMs)
                    {
                        // Too close to swing - potential clip, penalize
                        score += SwingClipPenalty;
                    }
                    else if (swingRemainingMs < SwingClipThresholdMs)
                    {
                        // Safe weaving window - bonus
                        score += SwingAlignmentBonus;
                    }
                    // else: plenty of time, no adjustment needed
                }
                else if (abilityType == AbilityType.OnNextSwing)
                {
                    // Abilities like Heroic Strike queue for next swing
                    // Bonus when rage available and swing ready soon
                    if (state.ResourcePercent > 40 && swingRemainingMs < SwingClipThresholdMs)
                    {
                        score += SwingAlignmentBonus;
                    }
                }
            }

            // Movement abilities - gap closing bonus
            if (abilityType == AbilityType.Movement)
            {
                score += MovementGapCloseBonus;
            }

            // Evaluate profile-defined score conditions
            float conditionBonus = RoleStrategyHelpers.EvaluateScoreConditions(action);
            score += conditionBonus;

            // Sequence position tiebreaker: lower index = slightly higher score
            // This ensures default-weight profiles maintain exact original ordering
            score += (1000 - sequenceIndex) / TiebreakerDivisor;

            return score;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DpsRoleStrategy] Failed to score action {Action} at index {Index}",
                action.Name, sequenceIndex);
            return float.MinValue; // Skip this ability on error
        }
    }

    /// <summary>
    /// Determines if an ability is instant (no cast bar, doesn't trigger GCD in the same way).
    /// Instant abilities can be weaved between auto-attacks.
    /// Uses cached AbilityType to avoid string allocations.
    /// </summary>
    private static bool IsInstantAbility(KeyAction action, AbilityType abilityType)
    {
        // Instant abilities have no cast bar and aren't "on next swing"
        if (action.HasCastBar)
            return false;

        // Check using cached ability type (no string allocation)
        return abilityType != AbilityType.OnNextSwing;
    }
}
