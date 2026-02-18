using System;

using Core;
using Core.CombatRotation;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.CombatRotation;

public class ScoreConditionRuntimeTests
{
    private static Core.Requirement CreateTestRequirement(bool result)
    {
        return new Core.Requirement
        {
            HasRequirement = () => result,
            LogMessage = () => result ? "Requirement met" : "Requirement not met"
        };
    }

    [Fact]
    public void ScoreConditionRuntime_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        Core.Requirement requirement = CreateTestRequirement(true);
        const float bonus = 1.5f;

        // Act
        var runtime = new ScoreConditionRuntime(requirement, bonus);

        // Assert
        runtime.Requirement.Should().Be(requirement);
        runtime.Bonus.Should().Be(bonus);
    }

    [Fact]
    public void ScoreConditionRuntime_CreateWithZeroBonus_StoresCorrectly()
    {
        // Arrange
        Core.Requirement requirement = CreateTestRequirement(true);

        // Act
        var runtime = new ScoreConditionRuntime(requirement, 0f);

        // Assert
        runtime.Bonus.Should().Be(0f);
    }

    [Fact]
    public void ScoreConditionRuntime_CreateWithNegativeBonus_StoresCorrectly()
    {
        // Arrange
        Core.Requirement requirement = CreateTestRequirement(true);

        // Act
        var runtime = new ScoreConditionRuntime(requirement, -1.0f);

        // Assert
        runtime.Bonus.Should().Be(-1.0f);
    }

    [Fact]
    public void ScoreConditionRuntime_With_ModifiesSingleProperty()
    {
        // Arrange
        Core.Requirement requirement = CreateTestRequirement(true);
        var original = new ScoreConditionRuntime(requirement, 1.0f);

        // Act
        var modified = original with { Bonus = 2.0f };

        // Assert
        modified.Bonus.Should().Be(2.0f);
        modified.Requirement.Should().Be(original.Requirement);
    }

    [Fact]
    public void ScoreConditionRuntime_Equality_SameValues_AreEqual()
    {
        // Arrange
        Core.Requirement requirement = CreateTestRequirement(true);
        var runtime1 = new ScoreConditionRuntime(requirement, 1.5f);
        var runtime2 = new ScoreConditionRuntime(requirement, 1.5f);

        // Assert
        runtime1.Should().Be(runtime2);
        (runtime1 == runtime2).Should().BeTrue();
    }

    [Fact]
    public void ScoreConditionRuntime_Equality_DifferentBonus_AreNotEqual()
    {
        // Arrange
        Core.Requirement requirement = CreateTestRequirement(true);
        var runtime1 = new ScoreConditionRuntime(requirement, 1.0f);
        var runtime2 = new ScoreConditionRuntime(requirement, 2.0f);

        // Assert
        runtime1.Should().NotBe(runtime2);
        (runtime1 != runtime2).Should().BeTrue();
    }

    [Fact]
    public void ScoreConditionRuntime_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        Core.Requirement requirement = CreateTestRequirement(false);
        const float bonus = 2.5f;
        var runtime = new ScoreConditionRuntime(requirement, bonus);

        // Act
        var (resultRequirement, resultBonus) = runtime;

        // Assert
        resultRequirement.Should().Be(requirement);
        resultBonus.Should().Be(bonus);
    }

    [Fact]
    public void ScoreConditionRuntime_ToString_ContainsBonus()
    {
        // Arrange
        Core.Requirement requirement = CreateTestRequirement(true);
        var runtime = new ScoreConditionRuntime(requirement, 3.0f);

        // Act
        string result = runtime.ToString();

        // Assert
        result.Should().Contain("3");
    }

    [Fact]
    public void ScoreConditionRuntime_LargeBonus_StoresCorrectly()
    {
        // Arrange
        Core.Requirement requirement = CreateTestRequirement(true);

        // Act
        var runtime = new ScoreConditionRuntime(requirement, 1000f);

        // Assert
        runtime.Bonus.Should().Be(1000f);
    }

    [Fact]
    public void ScoreConditionRuntime_RequirementCanBeEvaluated()
    {
        // Arrange
        Core.Requirement requirement = CreateTestRequirement(true);
        var runtime = new ScoreConditionRuntime(requirement, 1.0f);

        // Act & Assert
        runtime.Requirement.HasRequirement().Should().BeTrue();
    }

    [Fact]
    public void ScoreConditionRuntime_RequirementReturnsFalse_WhenConfigured()
    {
        // Arrange
        Core.Requirement requirement = CreateTestRequirement(false);
        var runtime = new ScoreConditionRuntime(requirement, 1.0f);

        // Act & Assert
        runtime.Requirement.HasRequirement().Should().BeFalse();
    }

    [Fact]
    public void ScoreConditionRuntime_TwoDifferentRequirements_AreNotEqual()
    {
        // Arrange
        Core.Requirement req1 = CreateTestRequirement(true);
        Core.Requirement req2 = CreateTestRequirement(false);
        var runtime1 = new ScoreConditionRuntime(req1, 1.0f);
        var runtime2 = new ScoreConditionRuntime(req2, 1.0f);

        // Assert
        runtime1.Should().NotBe(runtime2);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    [InlineData(2.0f)]
    [InlineData(10.0f)]
    [InlineData(-1.0f)]
    [InlineData(-5.0f)]
    public void ScoreConditionRuntime_VariousBonusValues_StoredCorrectly(float bonus)
    {
        // Arrange
        Core.Requirement requirement = CreateTestRequirement(true);

        // Act
        var runtime = new ScoreConditionRuntime(requirement, bonus);

        // Assert
        runtime.Bonus.Should().Be(bonus);
    }
}
