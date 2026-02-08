using Core;
using Core.CombatRotation;

using FluentAssertions;
using Requirement = Core.Requirement;

using Xunit;

namespace CoreUnitTests.CombatRotation;

public class RoleStrategyHelpersTests
{
    [Fact]
    public void EvaluateScoreConditions_EmptyConditions_ReturnsZero()
    {
        // Arrange
        var action = new KeyAction { ScoreConditionsRuntime = [] };

        // Act
        float result = RoleStrategyHelpers.EvaluateScoreConditions(action);

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void EvaluateScoreConditions_NullConditions_ReturnsZero()
    {
        // Arrange
        var action = new KeyAction { ScoreConditionsRuntime = null };

        // Act
        float result = RoleStrategyHelpers.EvaluateScoreConditions(action);

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void EvaluateScoreConditions_SingleMetCondition_ReturnsBonus()
    {
        // Arrange
        var requirement = new Requirement { HasRequirement = () => true };
        var conditions = new[]
        {
            new ScoreConditionRuntime(requirement, 1.5f)
        };
        var action = new KeyAction { ScoreConditionsRuntime = conditions };

        // Act
        float result = RoleStrategyHelpers.EvaluateScoreConditions(action);

        // Assert
        result.Should().Be(1.5f);
    }

    [Fact]
    public void EvaluateScoreConditions_SingleUnmetCondition_ReturnsZero()
    {
        // Arrange
        var requirement = new Requirement { HasRequirement = () => false };
        var conditions = new[]
        {
            new ScoreConditionRuntime(requirement, 2.0f)
        };
        var action = new KeyAction { ScoreConditionsRuntime = conditions };

        // Act
        float result = RoleStrategyHelpers.EvaluateScoreConditions(action);

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void EvaluateScoreConditions_MultipleConditions_SumsMetOnes()
    {
        // Arrange
        var metRequirement = new Requirement { HasRequirement = () => true };
        var unmetRequirement = new Requirement { HasRequirement = () => false };
        var conditions = new[]
        {
            new ScoreConditionRuntime(metRequirement, 1.0f),
            new ScoreConditionRuntime(unmetRequirement, 2.0f),
            new ScoreConditionRuntime(metRequirement, 3.0f)
        };
        var action = new KeyAction { ScoreConditionsRuntime = conditions };

        // Act
        float result = RoleStrategyHelpers.EvaluateScoreConditions(action);

        // Assert - Only met conditions (1.0f + 3.0f) = 4.0f
        result.Should().Be(4.0f);
    }

    [Fact]
    public void EvaluateScoreConditions_AllConditionsMet_SumsAllBonuses()
    {
        // Arrange
        var requirement = new Requirement { HasRequirement = () => true };
        var conditions = new[]
        {
            new ScoreConditionRuntime(requirement, 0.5f),
            new ScoreConditionRuntime(requirement, 1.0f),
            new ScoreConditionRuntime(requirement, 1.5f)
        };
        var action = new KeyAction { ScoreConditionsRuntime = conditions };

        // Act
        float result = RoleStrategyHelpers.EvaluateScoreConditions(action);

        // Assert
        result.Should().Be(3.0f);
    }

    [Fact]
    public void EvaluateScoreConditions_AllConditionsUnmet_ReturnsZero()
    {
        // Arrange
        var requirement = new Requirement { HasRequirement = () => false };
        var conditions = new[]
        {
            new ScoreConditionRuntime(requirement, 5.0f),
            new ScoreConditionRuntime(requirement, 10.0f),
            new ScoreConditionRuntime(requirement, 15.0f)
        };
        var action = new KeyAction { ScoreConditionsRuntime = conditions };

        // Act
        float result = RoleStrategyHelpers.EvaluateScoreConditions(action);

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void EvaluateScoreConditions_NegativeBonuses_SumsCorrectly()
    {
        // Arrange
        var requirement = new Requirement { HasRequirement = () => true };
        var conditions = new[]
        {
            new ScoreConditionRuntime(requirement, -1.0f),
            new ScoreConditionRuntime(requirement, 2.0f),
            new ScoreConditionRuntime(requirement, -0.5f)
        };
        var action = new KeyAction { ScoreConditionsRuntime = conditions };

        // Act
        float result = RoleStrategyHelpers.EvaluateScoreConditions(action);

        // Assert
        result.Should().Be(0.5f);
    }

    [Fact]
    public void EvaluateScoreConditions_LargeNumberOfConditions_WorksCorrectly()
    {
        // Arrange
        var requirement = new Requirement { HasRequirement = () => true };
        var conditions = new ScoreConditionRuntime[100];
        for (int i = 0; i < 100; i++)
        {
            conditions[i] = new ScoreConditionRuntime(requirement, 0.1f);
        }
        var action = new KeyAction { ScoreConditionsRuntime = conditions };

        // Act
        float result = RoleStrategyHelpers.EvaluateScoreConditions(action);

        // Assert - Use approximate comparison for floating point
        result.Should().BeApproximately(10.0f, 0.001f);
    }

    [Fact]
    public void EvaluateScoreConditions_ZeroBonusCondition_Met_ReturnsZero()
    {
        // Arrange
        var requirement = new Requirement { HasRequirement = () => true };
        var conditions = new[]
        {
            new ScoreConditionRuntime(requirement, 0f)
        };
        var action = new KeyAction { ScoreConditionsRuntime = conditions };

        // Act
        float result = RoleStrategyHelpers.EvaluateScoreConditions(action);

        // Assert
        result.Should().Be(0f);
    }
}
