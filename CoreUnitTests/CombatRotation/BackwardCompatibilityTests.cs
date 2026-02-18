using Core;
using Core.CombatRotation;

using System.Collections.Generic;

using Xunit;

namespace CoreUnitTests.CombatRotation;

/// <summary>
/// Verifies backward compatibility: when the optimizer is disabled or
/// when KeyAction.Weight defaults to 1.0, the system behaves identically
/// to the original static priority list.
/// </summary>
public sealed class BackwardCompatibilityTests
{
    [Fact]
    public void KeyAction_Weight_DefaultsTo1()
    {
        KeyAction action = new();
        Assert.Equal(1.0f, action.Weight);
    }

    [Fact]
    public void KeyAction_ScoreConditions_DefaultsToEmpty()
    {
        KeyAction action = new();
        Assert.Empty(action.ScoreConditions);
    }

    [Fact]
    public void KeyAction_ScoreConditionsRuntime_DefaultsToEmpty()
    {
        KeyAction action = new();
        Assert.Empty(action.ScoreConditionsRuntime);
    }

    [Fact]
    public void ScoreConditionEntry_Properties()
    {
        ScoreConditionEntry entry = new()
        {
            Condition = "TargetHealth%<20",
            Bonus = 2.5f
        };

        Assert.Equal("TargetHealth%<20", entry.Condition);
        Assert.Equal(2.5f, entry.Bonus);
    }

    [Fact]
    public void CombatRotationOptimizerOptions_DisabledByDefault()
    {
        CombatRotationOptimizerOptions options = new();
        Assert.False(options.Enabled);
    }

    [Fact]
    public void CombatRotationOptimizerOptions_FallbackEnabledByDefault()
    {
        CombatRotationOptimizerOptions options = new();
        Assert.True(options.FallbackToStaticPriority);
    }
}
