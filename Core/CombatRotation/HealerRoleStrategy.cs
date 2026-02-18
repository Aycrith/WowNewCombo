using System;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

namespace Core.CombatRotation;

/// <summary>
/// Healer-focused scoring strategy prioritizing triage, mana efficiency, and HoT management.
///
/// Scoring formula:
/// Score = BaseWeight
/// + TriageBonus (emergency heals at critical health)
/// + ManaEfficiencyBonus (efficient heals when mana low)
/// + HoTRefreshBonus (maintain HoTs before they expire)
/// + CooldownAlignment (use cooldowns effectively)
/// + DispelPriority (remove dangerous debuffs)
/// + ScoreConditionsBonus
/// + SequencePositionTiebreaker
///
/// Design principles:
/// - Triage first: Critical targets get immediate attention
/// - Mana sustainability: Balance throughput with efficiency
/// - HoT maintenance: Keep healing over time effects rolling
/// - Prevention: Shield/damage prevention when appropriate
/// - Cooldown management: Use powerful abilities strategically
///
/// Note: This is currently a self-heal implementation only. In a full implementation,
/// party health data would be integrated from party frames.
/// </summary>
public sealed class HealerRoleStrategy : IRoleStrategy
{
    // Triage thresholds - health levels requiring different responses
    private const float CriticalHealthThreshold = 25f; // <25% - emergency
    private const float LowHealthThreshold = 50f; // <50% - urgent
    private const float MediumHealthThreshold = 70f; // <70% - normal

    // Triage bonuses
    private const float CriticalHealthBonus = 15.0f; // Massive priority
    private const float LowHealthBonus = 8.0f; // High priority
    private const float MediumHealthBonus = 3.0f; // Normal priority
    private const float FullHealthPenalty = -5.0f; // Don't heal full health

    // Mana efficiency
    private const float CriticalManaThreshold = 20f; // <20% - panic
    private const float LowManaThreshold = 40f; // <40% - conserve
    private const float ManaEfficiencyBonus = 3.0f; // Efficient heal bonus
    private const float ManaRegenerationBonus = 5.0f; // Use mana regen abilities

    // Emergency abilities
    private const float EmergencyHealBonus = 10.0f; // Swiftmend, Nature's Swiftness
    private const float CombatResurrectionBonus = 12.0f; // Brez when ally dead

    // Prevention
    private const float PreventiveShieldBonus = 2.0f; // Shield before damage
    private const float IncomingDamageMultiplier = 1.5f; // Boost when damage incoming

    // Cooldown alignment
    private const float CooldownReadyBonus = 1.0f; // Use CDs efficiently
    private const float BuffActiveBonus = 1.5f; // Healing buffs active

    // Dispel
    private const float DispelBonus = 5.0f; // Remove magic/curse/disease

    private const float TiebreakerDivisor = 1000f;

    private readonly ILogger<HealerRoleStrategy> logger;

    public string RoleName => "Healer";

    public HealerRoleStrategy(ILogger<HealerRoleStrategy> logger)
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

            // Base weight from JSON profile
            float score = action.Weight;

            // Use cached ability type classification
            AbilityType abilityType = action.AbilityType;

            // Triage-based scoring based on target health
            // Note: In a real implementation, we'd need target health from party frames
            // For now, we use ScoreConditions to check specific health thresholds
            score += CalculateTriageBonus(abilityType, state);

            // Mana efficiency considerations
            score += CalculateManaEfficiencyBonus(abilityType, state);

            // HoT management - refresh before expiration
            score += CalculateHoTBonus(abilityType, action, state);

            // Emergency abilities
            if (abilityType == AbilityType.EmergencyHeal)
            {
                score += EmergencyHealBonus;
            }

            // Combat resurrection
            if (abilityType == AbilityType.CombatResurrection && !state.TargetAlive)
            {
                score += CombatResurrectionBonus;
            }

            // Prevention shields
            if (abilityType == AbilityType.DamagePrevention)
            {
                score += CalculatePreventionBonus(state);
            }

            // Dispel priority
            if (abilityType == AbilityType.Dispel)
            {
                score += DispelBonus;
            }

            // Healing buffs (Tree of Life, Inner Focus, etc.)
            if (abilityType == AbilityType.HealingBuff)
            {
                score += BuffActiveBonus;
            }

            // Cooldown alignment - use powerful cooldowns effectively
            if (IsPowerfulCooldown(abilityType))
            {
                score += CooldownReadyBonus;
            }

            // Evaluate profile-defined score conditions
            float conditionBonus = RoleStrategyHelpers.EvaluateScoreConditions(action);
            score += conditionBonus;

            // Sequence position tiebreaker
            score += (1000 - sequenceIndex) / TiebreakerDivisor;

            return score;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[HealerRoleStrategy] Failed to score action {Action} at index {Index}",
                action.Name, sequenceIndex);
            return float.MinValue;
        }
    }

    /// <summary>
    /// Calculate triage bonus based on ability type and health context.
    /// </summary>
    private static float CalculateTriageBonus(AbilityType abilityType, in GameStateSnapshot state)
    {
        // Direct heals get triage bonuses
        if (abilityType == AbilityType.DirectHeal ||
            abilityType == AbilityType.EmergencyHeal)
        {
            // Use player's health as proxy (in real implementation, use party health)
            if (state.HealthPercent < CriticalHealthThreshold)
            {
                return CriticalHealthBonus;
            }

            if (state.HealthPercent < LowHealthThreshold)
            {
                return LowHealthBonus;
            }

            if (state.HealthPercent < MediumHealthThreshold)
            {
                return MediumHealthBonus;
            }

            if (state.HealthPercent >= 100)
            {
                return FullHealthPenalty;
            }
        }

        return 0f;
    }

    /// <summary>
    /// Calculate mana efficiency bonus.
    /// </summary>
    private static float CalculateManaEfficiencyBonus(AbilityType abilityType, in GameStateSnapshot state)
    {
        float bonus = 0f;

        // Mana regeneration when critically low
        if (abilityType == AbilityType.ManaRegeneration)
        {
            if (state.ResourcePercent < CriticalManaThreshold)
            {
                bonus += ManaRegenerationBonus * 2; // Urgent
            }
            else if (state.ResourcePercent < LowManaThreshold)
            {
                bonus += ManaRegenerationBonus; // Needed
            }
        }

        // Prefer efficient heals when mana low
        if (abilityType == AbilityType.DirectHeal ||
            abilityType == AbilityType.HoT)
        {
            if (state.ResourcePercent < CriticalManaThreshold)
            {
                bonus += ManaEfficiencyBonus * 1.5f;
            }
            else if (state.ResourcePercent < LowManaThreshold)
            {
                bonus += ManaEfficiencyBonus;
            }
        }

        return bonus;
    }

    /// <summary>
    /// Calculate HoT maintenance bonus.
    /// </summary>
    private static float CalculateHoTBonus(AbilityType abilityType, KeyAction action, in GameStateSnapshot state)
    {
        if (abilityType != AbilityType.HoT)
        {
            return 0f;
        }

        // Check if HoT is about to expire via ScoreConditions
        // The profile should use conditions like:
        // - "!Renew" (not present)
        // - "Renew RemainingMs < 3000" (about to expire)

        // Base HoT bonus for maintaining rolling HoTs
        return 1.0f;
    }

    /// <summary>
    /// Calculate prevention shield bonus.
    /// </summary>
    private static float CalculatePreventionBonus(in GameStateSnapshot state)
    {
        // Shield before anticipated damage
        float bonus = PreventiveShieldBonus;

        // Boost if target health already dropping
        if (state.HealthPercent < 80)
        {
            bonus *= IncomingDamageMultiplier;
        }

        return bonus;
    }

    /// <summary>
    /// Determines if an ability is a powerful cooldown.
    /// </summary>
    private static bool IsPowerfulCooldown(AbilityType type)
    {
        return type == AbilityType.HealingBuff ||
               type == AbilityType.SurvivalCooldown;
    }
}
