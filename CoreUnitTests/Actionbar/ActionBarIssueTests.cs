using Core;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Actionbar;

public class ActionBarIssueTests
{
    private static KeyAction CreateTestKeyAction(string name, int slot = 1)
    {
        return new KeyAction { Name = name, Slot = slot };
    }

    [Fact]
    public void ActionBarIssue_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        var keyAction = CreateTestKeyAction("Frostbolt", 5);
        const SlotValidationStatus status = SlotValidationStatus.Mismatch;
        const bool canResolve = true;

        // Act
        var issue = new ActionBarIssue(keyAction, status, canResolve);

        // Assert
        issue.KeyAction.Should().Be(keyAction);
        issue.Status.Should().Be(status);
        issue.CanResolve.Should().Be(canResolve);
        issue.SpellName.Should().Be("Frostbolt");
        issue.Slot.Should().Be(5);
    }

    [Fact]
    public void ActionBarIssue_CreateCannotResolve_StoresCorrectly()
    {
        // Arrange
        var keyAction = CreateTestKeyAction("Unknown Spell", 10);

        // Act
        var issue = new ActionBarIssue(keyAction, SlotValidationStatus.NotOnActionBar, false);

        // Assert
        issue.CanResolve.Should().BeFalse();
    }

    [Fact]
    public void ActionBarIssue_SpellNameProperty_ReturnsKeyActionName()
    {
        // Arrange
        var keyAction = CreateTestKeyAction("Test Spell");
        var issue = new ActionBarIssue(keyAction, SlotValidationStatus.Valid, true);

        // Assert
        issue.SpellName.Should().Be("Test Spell");
    }

    [Fact]
    public void ActionBarIssue_SlotProperty_ReturnsKeyActionSlot()
    {
        // Arrange
        var keyAction = CreateTestKeyAction("Test", 42);
        var issue = new ActionBarIssue(keyAction, SlotValidationStatus.Valid, true);

        // Assert
        issue.Slot.Should().Be(42);
    }

    [Fact]
    public void ActionBarIssue_With_ModifiesSingleProperty()
    {
        // Arrange
        var keyAction = CreateTestKeyAction("Test");
        var original = new ActionBarIssue(keyAction, SlotValidationStatus.Valid, true);

        // Act
        var modified = original with { CanResolve = false };

        // Assert
        modified.CanResolve.Should().BeFalse();
        modified.KeyAction.Should().Be(original.KeyAction);
        modified.Status.Should().Be(original.Status);
    }

    [Fact]
    public void ActionBarIssue_Equality_SameValues_AreEqual()
    {
        // Arrange
        var keyAction = CreateTestKeyAction("Heal", 3);
        var issue1 = new ActionBarIssue(keyAction, SlotValidationStatus.Mismatch, true);
        var issue2 = new ActionBarIssue(keyAction, SlotValidationStatus.Mismatch, true);

        // Assert
        issue1.Should().Be(issue2);
        (issue1 == issue2).Should().BeTrue();
    }

    [Fact]
    public void ActionBarIssue_Equality_DifferentStatus_AreNotEqual()
    {
        // Arrange
        var keyAction = CreateTestKeyAction("Test");
        var issue1 = new ActionBarIssue(keyAction, SlotValidationStatus.Valid, true);
        var issue2 = new ActionBarIssue(keyAction, SlotValidationStatus.Mismatch, true);

        // Assert
        issue1.Should().NotBe(issue2);
    }

    [Fact]
    public void ActionBarIssue_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        var keyAction = CreateTestKeyAction("Fireball", 7);
        var issue = new ActionBarIssue(keyAction, SlotValidationStatus.EmptySlot, false);

        // Act
        var (resultKeyAction, resultStatus, resultCanResolve) = issue;

        // Assert
        resultKeyAction.Should().Be(keyAction);
        resultStatus.Should().Be(SlotValidationStatus.EmptySlot);
        resultCanResolve.Should().BeFalse();
    }

    [Fact]
    public void ActionBarIssue_ToString_ContainsSpellName()
    {
        // Arrange
        var keyAction = CreateTestKeyAction("Test Spell");
        var issue = new ActionBarIssue(keyAction, SlotValidationStatus.Valid, true);

        // Act
        string result = issue.ToString();

        // Assert
        result.Should().Contain("Test Spell");
    }

    [Theory]
    [InlineData(SlotValidationStatus.Valid, true)]
    [InlineData(SlotValidationStatus.Mismatch, true)]
    [InlineData(SlotValidationStatus.EmptySlot, false)]
    [InlineData(SlotValidationStatus.UnknownTexture, false)]
    [InlineData(SlotValidationStatus.NotOnActionBar, false)]
    public void ActionBarIssue_VariousCombinations_Accepted(SlotValidationStatus status, bool canResolve)
    {
        // Arrange & Act
        var keyAction = CreateTestKeyAction("Test");
        var issue = new ActionBarIssue(keyAction, status, canResolve);

        // Assert
        issue.Status.Should().Be(status);
        issue.CanResolve.Should().Be(canResolve);
    }
}
