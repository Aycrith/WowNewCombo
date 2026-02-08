using Core;
using Core.CombatRotation;
using Core.Goals;
using FluentAssertions;
using System;
using System.Collections.Generic;
using Requirement = Core.Requirement;
using Xunit;

namespace CoreUnitTests.RequirementTests;

/// <summary>
/// Test suite for the Requirement class and related functionality.
/// Tests the core requirement logic including logical operations and extension methods.
/// </summary>
public class RequirementTests
{
    #region Requirement Construction

    [Fact]
    public void Requirement_DefaultValues_ReturnsFalse()
    {
        // Arrange & Act
        var requirement = new Requirement();

        // Assert
        requirement.HasRequirement().Should().BeFalse();
        requirement.LogMessage().Should().Be("Unknown requirement");
        requirement.VisibleIfHasRequirement.Should().BeFalse();
    }

    [Fact]
    public void Requirement_SetHasRequirement_ReturnsSetValue()
    {
        // Arrange
        var requirement = new Requirement();

        // Act
        requirement.HasRequirement = () => true;

        // Assert
        requirement.HasRequirement().Should().BeTrue();
    }

    [Fact]
    public void Requirement_SetLogMessage_ReturnsSetValue()
    {
        // Arrange
        var requirement = new Requirement();

        // Act
        requirement.LogMessage = () => "Test message";

        // Assert
        requirement.LogMessage().Should().Be("Test message");
    }

    [Fact]
    public void Requirement_SetVisibleIfHasRequirement_ReturnsSetValue()
    {
        // Arrange
        var requirement = new Requirement
        {
            VisibleIfHasRequirement = true
        };

        // Assert
        requirement.VisibleIfHasRequirement.Should().BeTrue();
    }

    #endregion

    #region Requirement Constants

    [Fact]
    public void Requirement_Constants_AreCorrect()
    {
        // Assert
        Requirement.And.Should().Be(" and ");
        Requirement.Or.Should().Be(" or ");
        Requirement.SymbolNegate.Should().Be("!");
        Requirement.SymbolAnd.Should().Be("&&");
        Requirement.SymbolOr.Should().Be("||");
        Requirement.SymbolAndChar.Should().Be('&');
        Requirement.SymbolOrChar.Should().Be('|');
    }

    #endregion

    #region RequirementExt - And

    [Fact]
    public void RequirementExt_And_BothTrue_ReturnsTrue()
    {
        // Arrange
        var req1 = new Requirement { HasRequirement = () => true, LogMessage = () => "A" };
        var req2 = new Requirement { HasRequirement = () => true, LogMessage = () => "B" };

        // Act
        RequirementExt.And(req1, req2);

        // Assert
        req1.HasRequirement().Should().BeTrue();
        req1.LogMessage().Should().Contain("A");
        req1.LogMessage().Should().Contain("B");
        req1.LogMessage().Should().Contain(Requirement.And);
    }

    [Fact]
    public void RequirementExt_And_FirstFalse_ReturnsFalse()
    {
        // Arrange
        var req1 = new Requirement { HasRequirement = () => false, LogMessage = () => "A" };
        var req2 = new Requirement { HasRequirement = () => true, LogMessage = () => "B" };

        // Act
        RequirementExt.And(req1, req2);

        // Assert
        req1.HasRequirement().Should().BeFalse();
    }

    [Fact]
    public void RequirementExt_And_SecondFalse_ReturnsFalse()
    {
        // Arrange
        var req1 = new Requirement { HasRequirement = () => true, LogMessage = () => "A" };
        var req2 = new Requirement { HasRequirement = () => false, LogMessage = () => "B" };

        // Act
        RequirementExt.And(req1, req2);

        // Assert
        req1.HasRequirement().Should().BeFalse();
    }

    [Fact]
    public void RequirementExt_And_BothFalse_ReturnsFalse()
    {
        // Arrange
        var req1 = new Requirement { HasRequirement = () => false, LogMessage = () => "A" };
        var req2 = new Requirement { HasRequirement = () => false, LogMessage = () => "B" };

        // Act
        RequirementExt.And(req1, req2);

        // Assert
        req1.HasRequirement().Should().BeFalse();
    }

    #endregion

    #region RequirementExt - Or

    [Fact]
    public void RequirementExt_Or_BothTrue_ReturnsTrue()
    {
        // Arrange
        var req1 = new Requirement { HasRequirement = () => true, LogMessage = () => "A" };
        var req2 = new Requirement { HasRequirement = () => true, LogMessage = () => "B" };

        // Act
        RequirementExt.Or(req1, req2);

        // Assert
        req1.HasRequirement().Should().BeTrue();
    }

    [Fact]
    public void RequirementExt_Or_FirstTrue_ReturnsTrue()
    {
        // Arrange
        var req1 = new Requirement { HasRequirement = () => true, LogMessage = () => "A" };
        var req2 = new Requirement { HasRequirement = () => false, LogMessage = () => "B" };

        // Act
        RequirementExt.Or(req1, req2);

        // Assert
        req1.HasRequirement().Should().BeTrue();
        req1.LogMessage().Should().Contain("A");
        req1.LogMessage().Should().Contain("B");
        req1.LogMessage().Should().Contain(Requirement.Or);
    }

    [Fact]
    public void RequirementExt_Or_SecondTrue_ReturnsTrue()
    {
        // Arrange
        var req1 = new Requirement { HasRequirement = () => false, LogMessage = () => "A" };
        var req2 = new Requirement { HasRequirement = () => true, LogMessage = () => "B" };

        // Act
        RequirementExt.Or(req1, req2);

        // Assert
        req1.HasRequirement().Should().BeTrue();
    }

    [Fact]
    public void RequirementExt_Or_BothFalse_ReturnsFalse()
    {
        // Arrange
        var req1 = new Requirement { HasRequirement = () => false, LogMessage = () => "A" };
        var req2 = new Requirement { HasRequirement = () => false, LogMessage = () => "B" };

        // Act
        RequirementExt.Or(req1, req2);

        // Assert
        req1.HasRequirement().Should().BeFalse();
    }

    #endregion

    #region RequirementExt - Negate

    [Fact]
    public void RequirementExt_Negate_True_BecomesFalse()
    {
        // Arrange
        var req = new Requirement { HasRequirement = () => true, LogMessage = () => "Test" };
        var keyword = "!".AsSpan();

        // Act
        RequirementExt.Negate(req, keyword);

        // Assert
        req.HasRequirement().Should().BeFalse();
        req.LogMessage().Should().StartWith("!");
    }

    [Fact]
    public void RequirementExt_Negate_False_BecomesTrue()
    {
        // Arrange
        var req = new Requirement { HasRequirement = () => false, LogMessage = () => "Test" };
        var keyword = "!".AsSpan();

        // Act
        RequirementExt.Negate(req, keyword);

        // Assert
        req.HasRequirement().Should().BeTrue();
    }

    [Fact]
    public void RequirementExt_Negate_NotKeyword_AddsNotPrefix()
    {
        // Arrange
        var req = new Requirement { HasRequirement = () => true, LogMessage = () => "HasTarget" };
        var keyword = "not ".AsSpan();

        // Act
        RequirementExt.Negate(req, keyword);

        // Assert
        req.HasRequirement().Should().BeFalse();
        req.LogMessage().Should().StartWith("not ");
    }

    #endregion

    #region Complex Logical Combinations

    [Fact]
    public void Requirement_AndThenOr_CombinesCorrectly()
    {
        // Arrange
        var req1 = new Requirement { HasRequirement = () => true, LogMessage = () => "A" };
        var req2 = new Requirement { HasRequirement = () => false, LogMessage = () => "B" };
        var req3 = new Requirement { HasRequirement = () => true, LogMessage = () => "C" };

        // Act - (A && B) || C
        RequirementExt.And(req1, req2); // req1 = A && B (false)
        RequirementExt.Or(req1, req3);  // req1 = (A && B) || C (true)

        // Assert
        req1.HasRequirement().Should().BeTrue();
    }

    [Fact]
    public void Requirement_NegateThenAnd_ComposesCorrectly()
    {
        // Arrange
        var req1 = new Requirement { HasRequirement = () => true, LogMessage = () => "HasTarget" };
        var req2 = new Requirement { HasRequirement = () => true, LogMessage = () => "InCombat" };
        var keyword = "!".AsSpan();

        // Act - !HasTarget && InCombat
        RequirementExt.Negate(req1, keyword); // req1 = !HasTarget (false)
        RequirementExt.And(req1, req2);       // req1 = !HasTarget && InCombat (false)

        // Assert
        req1.HasRequirement().Should().BeFalse();
    }

    [Fact]
    public void Requirement_MultipleAnds_ChainsCorrectly()
    {
        // Arrange
        var req1 = new Requirement { HasRequirement = () => true, LogMessage = () => "A" };
        var req2 = new Requirement { HasRequirement = () => true, LogMessage = () => "B" };
        var req3 = new Requirement { HasRequirement = () => true, LogMessage = () => "C" };

        // Act - A && B && C
        RequirementExt.And(req1, req2); // req1 = A && B
        RequirementExt.And(req1, req3); // req1 = (A && B) && C

        // Assert
        req1.HasRequirement().Should().BeTrue();
        req1.LogMessage().Should().Contain("A");
        req1.LogMessage().Should().Contain("B");
        req1.LogMessage().Should().Contain("C");
    }

    #endregion

    #region Closure Behavior

    [Fact]
    public void Requirement_And_ModifiesOriginalRequirement()
    {
        // Arrange
        var originalResult = true;
        var req1 = new Requirement { HasRequirement = () => originalResult, LogMessage = () => "A" };
        var req2 = new Requirement { HasRequirement = () => true, LogMessage = () => "B" };

        // Act
        RequirementExt.And(req1, req2);

        // Modify original
        originalResult = false;

        // Assert - Should use original closure
        req1.HasRequirement().Should().BeFalse();
    }

    [Fact]
    public void Requirement_Or_ModifiesOriginalRequirement()
    {
        // Arrange
        var originalResult = false;
        var req1 = new Requirement { HasRequirement = () => originalResult, LogMessage = () => "A" };
        var req2 = new Requirement { HasRequirement = () => false, LogMessage = () => "B" };

        // Act
        RequirementExt.Or(req1, req2);

        // Modify original
        originalResult = true;

        // Assert - Should use original closure
        req1.HasRequirement().Should().BeTrue();
    }

    [Fact]
    public void Requirement_Negate_ModifiesOriginalRequirement()
    {
        // Arrange
        var originalResult = false;
        var req = new Requirement { HasRequirement = () => originalResult, LogMessage = () => "Test" };
        var keyword = "!".AsSpan();

        // Act
        RequirementExt.Negate(req, keyword);

        // Modify original
        originalResult = true;

        // Assert - Should use original closure
        req.HasRequirement().Should().BeFalse();
    }

    #endregion
}

/// <summary>
/// Tests for ScoreConditionEntry and ScoreConditionRuntime
/// </summary>
public class ScoreConditionTests
{
    #region ScoreConditionEntry

    [Fact]
    public void ScoreConditionEntry_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var scoreCondition = new ScoreConditionEntry();

        // Assert
        scoreCondition.Condition.Should().BeEmpty();
        scoreCondition.Bonus.Should().Be(0);
    }

    [Fact]
    public void ScoreConditionEntry_SetValues_StoresCorrectly()
    {
        // Arrange
        var scoreCondition = new ScoreConditionEntry
        {
            Condition = "HasTarget",
            Bonus = 15
        };

        // Assert
        scoreCondition.Condition.Should().Be("HasTarget");
        scoreCondition.Bonus.Should().Be(15);
    }

    [Fact]
    public void ScoreConditionEntry_NegativeBonus_StoresCorrectly()
    {
        // Arrange
        var scoreCondition = new ScoreConditionEntry
        {
            Condition = "LowHealth",
            Bonus = -5
        };

        // Assert
        scoreCondition.Bonus.Should().Be(-5);
    }

    [Fact]
    public void ScoreConditionEntry_EmptyCondition_StoresCorrectly()
    {
        // Arrange
        var scoreCondition = new ScoreConditionEntry
        {
            Condition = "",
            Bonus = 10
        };

        // Assert
        scoreCondition.Condition.Should().BeEmpty();
        scoreCondition.Bonus.Should().Be(10);
    }

    #endregion

    #region ScoreConditionRuntime

    [Fact]
    public void ScoreConditionRuntime_DefaultValues_AreCorrect()
    {
        // Arrange
        var req = new Requirement { HasRequirement = () => true, LogMessage = () => "Test" };

        // Act
        var runtime = new ScoreConditionRuntime(req, 10);

        // Assert
        runtime.Requirement.Should().Be(req);
        runtime.Bonus.Should().Be(10);
    }

    [Fact]
    public void ScoreConditionRuntime_SetValues_StoresCorrectly()
    {
        // Arrange
        var req = new Requirement { HasRequirement = () => false, LogMessage = () => "Test" };

        // Act
        var runtime = new ScoreConditionRuntime(req, -5);

        // Assert
        runtime.Requirement.Should().Be(req);
        runtime.Bonus.Should().Be(-5);
    }

    [Fact]
    public void ScoreConditionRuntime_RequirementEvaluation_WorksCorrectly()
    {
        // Arrange
        var req = new Requirement { HasRequirement = () => true, LogMessage = () => "Test" };
        var runtime = new ScoreConditionRuntime(req, 10);

        // Act & Assert
        runtime.Requirement.HasRequirement().Should().BeTrue();
    }

    [Fact]
    public void ScoreConditionRuntime_RequirementEvaluation_False_ReturnsFalse()
    {
        // Arrange
        var req = new Requirement { HasRequirement = () => false, LogMessage = () => "Test" };
        var runtime = new ScoreConditionRuntime(req, 10);

        // Act & Assert
        runtime.Requirement.HasRequirement().Should().BeFalse();
    }

    [Fact]
    public void ScoreConditionRuntime_ZeroBonus_StoresCorrectly()
    {
        // Arrange
        var req = new Requirement { HasRequirement = () => true, LogMessage = () => "Test" };

        // Act
        var runtime = new ScoreConditionRuntime(req, 0);

        // Assert
        runtime.Bonus.Should().Be(0);
    }

    [Fact]
    public void ScoreConditionRuntime_LargeBonus_StoresCorrectly()
    {
        // Arrange
        var req = new Requirement { HasRequirement = () => true, LogMessage = () => "Test" };

        // Act
        var runtime = new ScoreConditionRuntime(req, 1000);

        // Assert
        runtime.Bonus.Should().Be(1000);
    }

    [Fact]
    public void ScoreConditionRuntime_NegativeBonus_StoresCorrectly()
    {
        // Arrange
        var req = new Requirement { HasRequirement = () => true, LogMessage = () => "Test" };

        // Act
        var runtime = new ScoreConditionRuntime(req, -100);

        // Assert
        runtime.Bonus.Should().Be(-100);
    }

    #endregion
}
