using System;
using System.Reflection;

using Core;
using Core.CombatRotation;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace CoreUnitTests.CombatRotation;

/// <summary>
/// Tests for TankRoleStrategy scoring behavior.
/// </summary>
public sealed class TankRoleStrategyTests
{
    private readonly TankRoleStrategy strategy = new(NullLogger<TankRoleStrategy>.Instance);

    [Fact]
    public void RoleName_ReturnsTank()
    {
        Assert.Equal("Tank", strategy.RoleName);
    }

    [Fact]
    public void Strategy_CanBeConstructed()
    {
        Assert.NotNull(strategy);
    }

    [Fact]
    public void Strategy_ImplementsIRoleStrategy()
    {
        Assert.IsAssignableFrom<IRoleStrategy>(strategy);
    }

    [Fact]
    public void ScoreAbility_UninitializedKeyAction_ReturnsMinValue()
    {
        // Without Init(), globalTime is null so CanRun() throws NRE,
        // which the strategy catches and returns float.MinValue
        KeyAction action = new();
        GameStateSnapshot state = CreateDefaultSnapshot();

        float score = strategy.ScoreAbility(action, in state, 0);

        Assert.Equal(float.MinValue, score);
    }

    // === Scoring behavior tests using initialized KeyAction ===

    [Fact]
    public void SurvivalCooldown_CriticalHealth_GetsHighBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.SurvivalCooldown);
        GameStateSnapshot criticalHealth = CreateSnapshot(healthPercent: 25);

        float criticalScore = strategy.ScoreAbility(action, in criticalHealth, 0);

        // At critical health (<=30%), SurvivalCooldown gets +10.0 bonus
        // Base weight is 1.0, tiebreaker adds (1000-0)/1000 = 1.0
        Assert.True(criticalScore > 10.0f, $"Expected > 10.0 but got {criticalScore}");
    }

    [Fact]
    public void SurvivalCooldown_LowHealth_GetsModerateBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.SurvivalCooldown);
        GameStateSnapshot lowHealth = CreateSnapshot(healthPercent: 45);

        float lowScore = strategy.ScoreAbility(action, in lowHealth, 0);

        // At low health (<=50%), SurvivalCooldown gets +5.0 bonus
        Assert.True(lowScore > 5.0f, $"Expected > 5.0 but got {lowScore}");
    }

    [Fact]
    public void SurvivalCooldown_CriticalHealth_HigherThanLowHealth()
    {
        KeyAction actionCritical = CreateTestableKeyAction(AbilityType.SurvivalCooldown);
        KeyAction actionLow = CreateTestableKeyAction(AbilityType.SurvivalCooldown);
        GameStateSnapshot critical = CreateSnapshot(healthPercent: 20);
        GameStateSnapshot low = CreateSnapshot(healthPercent: 45);

        float criticalScore = strategy.ScoreAbility(actionCritical, in critical, 0);
        float lowScore = strategy.ScoreAbility(actionLow, in low, 0);

        Assert.True(criticalScore > lowScore,
            $"Critical score ({criticalScore}) should be greater than low score ({lowScore})");
    }

    [Fact]
    public void SurvivalCooldown_FullHealth_NoBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.SurvivalCooldown);
        GameStateSnapshot fullHealth = CreateSnapshot(healthPercent: 100);

        float score = strategy.ScoreAbility(action, in fullHealth, 0);

        // At full health, no survival bonus; base weight 1.0 + tiebreaker 1.0
        Assert.True(score < 5.0f, $"Expected < 5.0 at full health but got {score}");
    }

    [Fact]
    public void Taunt_MultipleMobs_GetsBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.Taunt);
        GameStateSnapshot multiMob = CreateSnapshot(mobCount: 3);

        float score = strategy.ScoreAbility(action, in multiMob, 0);

        // Taunt gets +2.0 threat maintenance + 8.0 * (3-1) * 0.3 = 4.8 multi-mob bonus
        // Total: 1.0 (weight) + 2.0 + 4.8 + 1.0 (tiebreaker) = 8.8
        Assert.True(score > 7.0f, $"Expected > 7.0 with 3 mobs but got {score}");
    }

    [Fact]
    public void Taunt_SingleMob_GetsBaseBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.Taunt);
        GameStateSnapshot singleMob = CreateSnapshot(mobCount: 1);

        float score = strategy.ScoreAbility(action, in singleMob, 0);

        // Taunt gets +2.0 threat maintenance only (no multi-mob bonus)
        // Total: 1.0 (weight) + 2.0 + 1.0 (tiebreaker) = 4.0
        float expectedMin = 3.5f;
        Assert.True(score > expectedMin, $"Expected > {expectedMin} but got {score}");
    }

    [Fact]
    public void AoEThreat_MultipleMobs_GetsScaledBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.AoEThreat);
        GameStateSnapshot multiMob = CreateSnapshot(mobCount: 4);

        float score = strategy.ScoreAbility(action, in multiMob, 0);

        // AoEThreat: +1.5 base + 1.0 * (4-1) = 4.5 AoE bonus
        // Total: 1.0 (weight) + 4.5 + 1.0 (tiebreaker) = 6.5
        Assert.True(score > 6.0f, $"Expected > 6.0 with 4 mobs but got {score}");
    }

    [Fact]
    public void AoEThreat_SingleMob_NoBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.AoEThreat);
        GameStateSnapshot singleMob = CreateSnapshot(mobCount: 1);

        float score = strategy.ScoreAbility(action, in singleMob, 0);

        // No AoE bonus with single mob; base weight 1.0 + tiebreaker 1.0
        Assert.True(score < 3.0f, $"Expected < 3.0 with 1 mob but got {score}");
    }

    [Fact]
    public void Interrupt_TargetCasting_GetsBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.Interrupt);
        GameStateSnapshot casting = CreateSnapshot(isTargetCasting: true);

        float score = strategy.ScoreAbility(action, in casting, 0);

        // Interrupt gets +6.0 when target is casting
        Assert.True(score > 6.0f, $"Expected > 6.0 when target casting but got {score}");
    }

    [Fact]
    public void Interrupt_TargetNotCasting_NoBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.Interrupt);
        GameStateSnapshot notCasting = CreateSnapshot(isTargetCasting: false);

        float score = strategy.ScoreAbility(action, in notCasting, 0);

        // No interrupt bonus when target not casting
        Assert.True(score < 3.0f, $"Expected < 3.0 when target not casting but got {score}");
    }

    [Fact]
    public void RageGeneration_LowResource_GetsBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.RageGeneration);
        GameStateSnapshot lowRage = CreateSnapshot(resourcePercent: 20);

        float score = strategy.ScoreAbility(action, in lowRage, 0);

        // RageGeneration gets +1.5 when resource < 30%
        Assert.True(score > 3.0f, $"Expected > 3.0 with low rage but got {score}");
    }

    [Fact]
    public void RageGeneration_HighResource_NoBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.RageGeneration);
        GameStateSnapshot highRage = CreateSnapshot(resourcePercent: 80);

        float score = strategy.ScoreAbility(action, in highRage, 0);

        // No rage generation bonus when resource >= 30%
        Assert.True(score < 3.0f, $"Expected < 3.0 with high rage but got {score}");
    }

    [Fact]
    public void Movement_GetsGapCloseBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.Movement);
        GameStateSnapshot state = CreateDefaultSnapshot();

        float score = strategy.ScoreAbility(action, in state, 0);

        // Movement gets +2.0 gap close bonus
        Assert.True(score > 3.0f, $"Expected > 3.0 for movement but got {score}");
    }

    [Fact]
    public void DefensiveStance_GetsBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.DefensiveStance);
        GameStateSnapshot state = CreateDefaultSnapshot();

        float score = strategy.ScoreAbility(action, in state, 0);

        // DefensiveStance gets +1.0 bonus
        Assert.True(score > 2.5f, $"Expected > 2.5 for defensive stance but got {score}");
    }

    [Fact]
    public void SingleTargetThreat_GetsThreatMaintenanceBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.SingleTargetThreat);
        GameStateSnapshot state = CreateDefaultSnapshot();

        float score = strategy.ScoreAbility(action, in state, 0);

        // SingleTargetThreat gets +2.0 threat maintenance bonus
        Assert.True(score > 3.5f, $"Expected > 3.5 for single target threat but got {score}");
    }

    [Fact]
    public void SequenceIndex_AffectsTiebreaker()
    {
        KeyAction actionFirst = CreateTestableKeyAction(AbilityType.Other);
        KeyAction actionLater = CreateTestableKeyAction(AbilityType.Other);
        GameStateSnapshot state = CreateDefaultSnapshot();

        float scoreFirst = strategy.ScoreAbility(actionFirst, in state, 0);
        float scoreLater = strategy.ScoreAbility(actionLater, in state, 10);

        // Lower sequence index should get slightly higher tiebreaker score
        Assert.True(scoreFirst > scoreLater,
            $"Index 0 score ({scoreFirst}) should be > index 10 score ({scoreLater})");
    }

    [Theory]
    [InlineData(AbilityType.Other)]
    [InlineData(AbilityType.Damage)]
    [InlineData(AbilityType.Execute)]
    [InlineData(AbilityType.DoT)]
    [InlineData(AbilityType.SurvivalCooldown)]
    [InlineData(AbilityType.Taunt)]
    [InlineData(AbilityType.AoEThreat)]
    [InlineData(AbilityType.Interrupt)]
    [InlineData(AbilityType.RageGeneration)]
    [InlineData(AbilityType.DirectHeal)]
    [InlineData(AbilityType.Movement)]
    [InlineData(AbilityType.DefensiveStance)]
    [InlineData(AbilityType.Consumable)]
    public void ScoreAbility_HandlesAllAbilityTypes_WithoutCrashing(AbilityType abilityType)
    {
        KeyAction action = CreateTestableKeyAction(abilityType);
        GameStateSnapshot state = CreateDefaultSnapshot();

        float score = strategy.ScoreAbility(action, in state, 0);

        Assert.True(score > float.MinValue, $"AbilityType {abilityType} returned float.MinValue unexpectedly");
    }

    // === Helper methods ===

    private static GameStateSnapshot CreateDefaultSnapshot()
    {
        return CreateSnapshot();
    }

    private static GameStateSnapshot CreateSnapshot(
        int healthPercent = 80,
        int resourcePercent = 50,
        int mobCount = 1,
        bool isTargetCasting = false,
        bool targetAlive = true,
        bool inCombat = true)
    {
        return new GameStateSnapshot(
            HealthPercent: healthPercent,
            ResourcePercent: resourcePercent,
            ResourceCurrent: resourcePercent,
            ResourceMax: 100,
            ComboPoints: 0,
            TargetHealthPercent: 60,
            GcdRemainingMs: 0,
            NetworkLatencyMs: 50,
            SpellQueueMs: 400,
            MainHandSwingElapsedMs: 0,
            MainHandSpeedMs: 2600,
            MobCount: mobCount,
            InCombat: inCombat,
            TargetAlive: targetAlive,
            IsTargetCasting: isTargetCasting,
            TickTimestamp: Environment.TickCount64);
    }

    /// <summary>
    /// Creates a KeyAction that passes both CanRun() and OnCooldown() gates
    /// by injecting a RecordInt via reflection to satisfy the globalTime dependency.
    /// RequirementsRuntime defaults to empty array, so CanRun() returns true.
    /// LastClicked defaults to DateTime.MinValue, so OnCooldown() returns false.
    /// </summary>
    private static KeyAction CreateTestableKeyAction(AbilityType abilityType, float weight = 1.0f)
    {
        KeyAction action = new()
        {
            AbilityType = abilityType,
            Weight = weight
        };

        // Set globalTime via reflection so CanRun() does not throw NRE.
        // Use ForceUpdate(1) so globalTime.Value != canRunTime (default 0),
        // forcing CanRun() to re-evaluate rather than returning cached false.
        RecordInt globalTime = new(0);
        globalTime.ForceUpdate(1);
        FieldInfo? field = typeof(KeyAction).GetField("globalTime",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(action, globalTime);

        return action;
    }
}

/// <summary>
/// Tests for HealerRoleStrategy scoring behavior.
/// </summary>
public sealed class HealerRoleStrategyTests
{
    private readonly HealerRoleStrategy strategy = new(NullLogger<HealerRoleStrategy>.Instance);

    [Fact]
    public void RoleName_ReturnsHealer()
    {
        Assert.Equal("Healer", strategy.RoleName);
    }

    [Fact]
    public void Strategy_CanBeConstructed()
    {
        Assert.NotNull(strategy);
    }

    [Fact]
    public void Strategy_ImplementsIRoleStrategy()
    {
        Assert.IsAssignableFrom<IRoleStrategy>(strategy);
    }

    [Fact]
    public void ScoreAbility_UninitializedKeyAction_ReturnsMinValue()
    {
        KeyAction action = new();
        GameStateSnapshot state = CreateDefaultSnapshot();

        float score = strategy.ScoreAbility(action, in state, 0);

        Assert.Equal(float.MinValue, score);
    }

    // === Triage scoring tests ===

    [Fact]
    public void DirectHeal_CriticalHealth_GetsHighBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.DirectHeal);
        GameStateSnapshot critical = CreateSnapshot(healthPercent: 20);

        float score = strategy.ScoreAbility(action, in critical, 0);

        // DirectHeal at <25% health gets +15.0 critical bonus
        Assert.True(score > 15.0f, $"Expected > 15.0 at critical health but got {score}");
    }

    [Fact]
    public void DirectHeal_LowHealth_GetsModerateBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.DirectHeal);
        GameStateSnapshot low = CreateSnapshot(healthPercent: 40);

        float score = strategy.ScoreAbility(action, in low, 0);

        // DirectHeal at <50% health gets +8.0 low health bonus
        Assert.True(score > 8.0f, $"Expected > 8.0 at low health but got {score}");
    }

    [Fact]
    public void DirectHeal_MediumHealth_GetsSmallBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.DirectHeal);
        GameStateSnapshot medium = CreateSnapshot(healthPercent: 60);

        float score = strategy.ScoreAbility(action, in medium, 0);

        // DirectHeal at <70% health gets +3.0 medium bonus
        Assert.True(score > 3.0f, $"Expected > 3.0 at medium health but got {score}");
    }

    [Fact]
    public void DirectHeal_FullHealth_GetsPenalty()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.DirectHeal);
        GameStateSnapshot full = CreateSnapshot(healthPercent: 100);

        float score = strategy.ScoreAbility(action, in full, 0);

        // DirectHeal at 100% health gets -5.0 penalty
        // Score: 1.0 (weight) + (-5.0) + 1.0 (tiebreaker) = -3.0
        Assert.True(score < 0f, $"Expected negative score at full health but got {score}");
    }

    [Fact]
    public void DirectHeal_CriticalHealth_HigherThanLowHealth()
    {
        KeyAction actionCritical = CreateTestableKeyAction(AbilityType.DirectHeal);
        KeyAction actionLow = CreateTestableKeyAction(AbilityType.DirectHeal);
        GameStateSnapshot critical = CreateSnapshot(healthPercent: 20);
        GameStateSnapshot low = CreateSnapshot(healthPercent: 40);

        float criticalScore = strategy.ScoreAbility(actionCritical, in critical, 0);
        float lowScore = strategy.ScoreAbility(actionLow, in low, 0);

        Assert.True(criticalScore > lowScore,
            $"Critical score ({criticalScore}) should be > low score ({lowScore})");
    }

    // === Mana efficiency tests ===

    [Fact]
    public void ManaRegeneration_CriticalMana_GetsUrgentBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.ManaRegeneration);
        GameStateSnapshot criticalMana = CreateSnapshot(resourcePercent: 15);

        float score = strategy.ScoreAbility(action, in criticalMana, 0);

        // ManaRegeneration at <20% gets 5.0 * 2 = 10.0 urgent bonus
        Assert.True(score > 10.0f, $"Expected > 10.0 at critical mana but got {score}");
    }

    [Fact]
    public void ManaRegeneration_LowMana_GetsModerateBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.ManaRegeneration);
        GameStateSnapshot lowMana = CreateSnapshot(resourcePercent: 35);

        float score = strategy.ScoreAbility(action, in lowMana, 0);

        // ManaRegeneration at <40% gets +5.0 bonus
        Assert.True(score > 5.0f, $"Expected > 5.0 at low mana but got {score}");
    }

    [Fact]
    public void ManaRegeneration_HighMana_NoBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.ManaRegeneration);
        GameStateSnapshot highMana = CreateSnapshot(resourcePercent: 80);

        float score = strategy.ScoreAbility(action, in highMana, 0);

        // No mana regen bonus at high mana; base weight 1.0 + tiebreaker 1.0
        Assert.True(score < 3.0f, $"Expected < 3.0 at high mana but got {score}");
    }

    // === Emergency heal tests ===

    [Fact]
    public void EmergencyHeal_GetsFlat_Bonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.EmergencyHeal);
        GameStateSnapshot state = CreateSnapshot(healthPercent: 80);

        float score = strategy.ScoreAbility(action, in state, 0);

        // EmergencyHeal gets +10.0 flat bonus (plus triage bonus based on health)
        Assert.True(score > 10.0f, $"Expected > 10.0 for emergency heal but got {score}");
    }

    [Fact]
    public void EmergencyHeal_CriticalHealth_GetsTriageAndEmergencyBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.EmergencyHeal);
        GameStateSnapshot critical = CreateSnapshot(healthPercent: 20);

        float score = strategy.ScoreAbility(action, in critical, 0);

        // EmergencyHeal gets +10.0 flat + 15.0 critical triage bonus
        Assert.True(score > 25.0f, $"Expected > 25.0 for emergency heal at critical health but got {score}");
    }

    // === Combat resurrection tests ===

    [Fact]
    public void CombatResurrection_TargetDead_GetsBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.CombatResurrection);
        GameStateSnapshot targetDead = CreateSnapshot(targetAlive: false);

        float score = strategy.ScoreAbility(action, in targetDead, 0);

        // CombatResurrection gets +12.0 when target is not alive
        Assert.True(score > 12.0f, $"Expected > 12.0 when target dead but got {score}");
    }

    [Fact]
    public void CombatResurrection_TargetAlive_NoBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.CombatResurrection);
        GameStateSnapshot targetAlive = CreateSnapshot(targetAlive: true);

        float score = strategy.ScoreAbility(action, in targetAlive, 0);

        // No combat res bonus when target is alive
        Assert.True(score < 3.0f, $"Expected < 3.0 when target alive but got {score}");
    }

    // === Dispel tests ===

    [Fact]
    public void Dispel_GetsBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.Dispel);
        GameStateSnapshot state = CreateDefaultSnapshot();

        float score = strategy.ScoreAbility(action, in state, 0);

        // Dispel gets +5.0 bonus
        Assert.True(score > 5.0f, $"Expected > 5.0 for dispel but got {score}");
    }

    // === Damage prevention tests ===

    [Fact]
    public void DamagePrevention_GetsPreventiveBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.DamagePrevention);
        GameStateSnapshot state = CreateDefaultSnapshot();

        float score = strategy.ScoreAbility(action, in state, 0);

        // DamagePrevention gets preventive shield bonus
        Assert.True(score > 2.0f, $"Expected > 2.0 for damage prevention but got {score}");
    }

    [Fact]
    public void DamagePrevention_LowHealth_GetsMultipliedBonus()
    {
        KeyAction actionLow = CreateTestableKeyAction(AbilityType.DamagePrevention);
        KeyAction actionFull = CreateTestableKeyAction(AbilityType.DamagePrevention);
        GameStateSnapshot lowHealth = CreateSnapshot(healthPercent: 60);
        GameStateSnapshot fullHealth = CreateSnapshot(healthPercent: 100);

        float scoreLow = strategy.ScoreAbility(actionLow, in lowHealth, 0);
        float scoreFull = strategy.ScoreAbility(actionFull, in fullHealth, 0);

        // At <80% health, prevention bonus is multiplied by 1.5
        Assert.True(scoreLow > scoreFull,
            $"Low health score ({scoreLow}) should be > full health score ({scoreFull})");
    }

    // === HoT tests ===

    [Fact]
    public void HoT_GetsBaseBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.HoT);
        GameStateSnapshot state = CreateDefaultSnapshot();

        float score = strategy.ScoreAbility(action, in state, 0);

        // HoT gets +1.0 base HoT bonus
        Assert.True(score > 2.0f, $"Expected > 2.0 for HoT but got {score}");
    }

    [Fact]
    public void HoT_LowMana_GetsManaEfficiencyBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.HoT);
        GameStateSnapshot lowMana = CreateSnapshot(resourcePercent: 15);

        float score = strategy.ScoreAbility(action, in lowMana, 0);

        // HoT at <20% mana also gets mana efficiency bonus (3.0 * 1.5 = 4.5)
        Assert.True(score > 5.0f, $"Expected > 5.0 for HoT at critical mana but got {score}");
    }

    // === Healing buff tests ===

    [Fact]
    public void HealingBuff_GetsBuffAndCooldownBonus()
    {
        KeyAction action = CreateTestableKeyAction(AbilityType.HealingBuff);
        GameStateSnapshot state = CreateDefaultSnapshot();

        float score = strategy.ScoreAbility(action, in state, 0);

        // HealingBuff gets +1.5 buff bonus + 1.0 powerful cooldown bonus
        Assert.True(score > 3.0f, $"Expected > 3.0 for healing buff but got {score}");
    }

    // === Tiebreaker tests ===

    [Fact]
    public void SequenceIndex_AffectsTiebreaker()
    {
        KeyAction actionFirst = CreateTestableKeyAction(AbilityType.Other);
        KeyAction actionLater = CreateTestableKeyAction(AbilityType.Other);
        GameStateSnapshot state = CreateDefaultSnapshot();

        float scoreFirst = strategy.ScoreAbility(actionFirst, in state, 0);
        float scoreLater = strategy.ScoreAbility(actionLater, in state, 10);

        Assert.True(scoreFirst > scoreLater,
            $"Index 0 score ({scoreFirst}) should be > index 10 score ({scoreLater})");
    }

    // === All ability types handled ===

    [Theory]
    [InlineData(AbilityType.Other)]
    [InlineData(AbilityType.Damage)]
    [InlineData(AbilityType.DirectHeal)]
    [InlineData(AbilityType.EmergencyHeal)]
    [InlineData(AbilityType.CombatResurrection)]
    [InlineData(AbilityType.Dispel)]
    [InlineData(AbilityType.DamagePrevention)]
    [InlineData(AbilityType.HoT)]
    [InlineData(AbilityType.HealingBuff)]
    [InlineData(AbilityType.ManaRegeneration)]
    [InlineData(AbilityType.Consumable)]
    [InlineData(AbilityType.AutoAttack)]
    [InlineData(AbilityType.Approach)]
    public void ScoreAbility_HandlesAllAbilityTypes_WithoutCrashing(AbilityType abilityType)
    {
        KeyAction action = CreateTestableKeyAction(abilityType);
        GameStateSnapshot state = CreateDefaultSnapshot();

        float score = strategy.ScoreAbility(action, in state, 0);

        Assert.True(score > float.MinValue,
            $"AbilityType {abilityType} returned float.MinValue unexpectedly");
    }

    // === Helper methods ===

    private static GameStateSnapshot CreateDefaultSnapshot()
    {
        return CreateSnapshot();
    }

    private static GameStateSnapshot CreateSnapshot(
        int healthPercent = 80,
        int resourcePercent = 50,
        int mobCount = 1,
        bool isTargetCasting = false,
        bool targetAlive = true,
        bool inCombat = true)
    {
        return new GameStateSnapshot(
            HealthPercent: healthPercent,
            ResourcePercent: resourcePercent,
            ResourceCurrent: resourcePercent,
            ResourceMax: 100,
            ComboPoints: 0,
            TargetHealthPercent: 60,
            GcdRemainingMs: 0,
            NetworkLatencyMs: 50,
            SpellQueueMs: 400,
            MainHandSwingElapsedMs: 0,
            MainHandSpeedMs: 2600,
            MobCount: mobCount,
            InCombat: inCombat,
            TargetAlive: targetAlive,
            IsTargetCasting: isTargetCasting,
            TickTimestamp: Environment.TickCount64);
    }

    /// <summary>
    /// Creates a KeyAction that passes both CanRun() and OnCooldown() gates
    /// by injecting a RecordInt via reflection to satisfy the globalTime dependency.
    /// </summary>
    private static KeyAction CreateTestableKeyAction(AbilityType abilityType, float weight = 1.0f)
    {
        KeyAction action = new()
        {
            AbilityType = abilityType,
            Weight = weight
        };

        // Set globalTime via reflection so CanRun() does not throw NRE.
        // Use ForceUpdate(1) so globalTime.Value != canRunTime (default 0),
        // forcing CanRun() to re-evaluate rather than returning cached false.
        RecordInt globalTime = new(0);
        globalTime.ForceUpdate(1);
        FieldInfo? field = typeof(KeyAction).GetField("globalTime",
            BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(action, globalTime);

        return action;
    }
}
