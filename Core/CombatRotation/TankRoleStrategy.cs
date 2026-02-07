using System;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

namespace Core.CombatRotation;

/// <summary>
/// Tank-focused scoring strategy prioritizing survival, threat generation, and crowd control.
///
/// Scoring formula:
/// Score = BaseWeight
/// + SurvivalCooldownBonus (emergency defensive abilities when health critical)
/// + ThreatGenerationBonus (taunts, high-threat abilities when not targeting us)
/// + AoEThreatBonus (multi-target threat when MobCount > 1)
/// + InterruptBonus (interrupt casting when target targets party member)
/// + ResourceEfficiencyBonus (rage generation when low)
/// + ScoreConditionsBonus
/// + SequencePositionTiebreaker
///
/// Design principles:
/// - Survival first: Defensive cooldowns have highest priority when health is critical
/// - Threat maintenance: Taunt immediately if target is not on us
/// - AoE awareness: Prioritize AoE threat abilities for multi-target
/// - Interrupt awareness: Prioritize interrupts on dangerous casts
/// - Sustainable tanking: Balance rage generation and consumption
/// </summary>
public sealed class TankRoleStrategy : IRoleStrategy
{
    // Survival thresholds - when to panic
    private const float CriticalHealthThreshold = 30f;
    private const float LowHealthThreshold = 50f;
    private const float CriticalHealthBonus = 10.0f; // Massive priority for survival
    private const float LowHealthBonus = 5.0f;

    // Threat thresholds
    private const float TauntPriorityBonus = 8.0f; // Immediate taunt
    private const float ThreatMaintenanceBonus = 2.0f; // Maintain threat

    // AoE threat scaling
    private const float AoEThreatBaseBonus = 1.5f;
    private const float AoEThreatPerMob = 1.0f; // Additional per mob above 1

    // Interrupt priority
    private const float InterruptCastingBonus = 6.0f; // Target casting dangerous spell

    // Resource management
    private const float RageGenerationBonus = 1.5f; // Bloodrage when low rage
    private const float RageLowThreshold = 30f;

    // Movement bonuses
    private const float MovementGapCloseBonus = 2.0f; // Charge/Intercept for gap closing

    // Stance bonuses
    private const float DefensiveStanceBonus = 1.0f; // In defensive stance

    private const float TiebreakerDivisor = 1000f;

    private readonly ILogger<TankRoleStrategy> logger;

    public string RoleName => "Tank";

    public TankRoleStrategy(ILogger<TankRoleStrategy> logger)
    {
        this.logger = logger;
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

            // Use cached ability type classification from KeyAction
            AbilityType abilityType = action.AbilityType;

            // Survival cooldowns - highest priority when health is critical
            if (abilityType == AbilityType.SurvivalCooldown)
            {
                if (state.HealthPercent <= CriticalHealthThreshold)
                {
                    score += CriticalHealthBonus;
                }
                else if (state.HealthPercent <= LowHealthThreshold)
                {
                    score += LowHealthBonus;
                }
            }

            // Taunt/Threat abilities - immediate if not targeting us
            if (abilityType == AbilityType.Taunt)
            {
                // Note: state doesn't have TargetTargetsMe, use generic threat bonus
                // In real implementation, this would check if we have aggro
                score += ThreatMaintenanceBonus;

                // Higher priority with more mobs (tab-taunt)
                if (state.MobCount > 1)
                {
                    score += TauntPriorityBonus * (state.MobCount - 1) * 0.3f;
                }
            }

            // Single-target threat maintenance
            if (abilityType == AbilityType.SingleTargetThreat)
            {
                score += ThreatMaintenanceBonus;
            }

            // AoE threat for multi-target
            if (abilityType == AbilityType.AoEThreat)
            {
                if (state.MobCount > 1)
                {
                    score += AoEThreatBaseBonus;
                    score += AoEThreatPerMob * (state.MobCount - 1);
                }
            }

            // Interrupt priority for casters
            if (abilityType == AbilityType.Interrupt && state.IsTargetCasting)
            {
                score += InterruptCastingBonus;
            }

            // Rage generation when low
            if (abilityType == AbilityType.RageGeneration && state.ResourcePercent < RageLowThreshold)
            {
                score += RageGenerationBonus;
            }

            // Movement abilities - gap closing
            if (abilityType == AbilityType.Movement)
            {
                score += MovementGapCloseBonus;
            }

            // Defensive stance bonus
            if (abilityType == AbilityType.DefensiveStance)
            {
                score += DefensiveStanceBonus;
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
            logger.LogError(ex, "[TankRoleStrategy] Failed to score action {Action} at index {Index}",
                action.Name, sequenceIndex);
            return float.MinValue; // Skip this ability on error
        }
    }
}
