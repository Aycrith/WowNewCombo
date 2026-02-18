using Core;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Actionbar;

public class SlotValidationResultTests
{
    [Fact]
    public void SlotValidationResult_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        const int slot = 5;
        const string expectedSpell = "Frostbolt";
        const int actualTextureId = 12345;
        string[] possibleSpells = ["Frostbolt", "Frostbolt Rank 2"];
        const SlotValidationStatus status = SlotValidationStatus.Valid;

        // Act
        var result = new SlotValidationResult(slot, expectedSpell, actualTextureId, possibleSpells, status);

        // Assert
        result.Slot.Should().Be(slot);
        result.ExpectedSpell.Should().Be(expectedSpell);
        result.ActualTextureId.Should().Be(actualTextureId);
        result.PossibleSpells.Should().BeEquivalentTo(possibleSpells);
        result.Status.Should().Be(status);
    }

    [Fact]
    public void SlotValidationResult_CreateWithEmptyPossibleSpells_StoresCorrectly()
    {
        // Arrange
        string[] possibleSpells = [];

        // Act
        var result = new SlotValidationResult(1, "Test", 0, possibleSpells, SlotValidationStatus.EmptySlot);

        // Assert
        result.PossibleSpells.Should().BeEmpty();
    }

    [Fact]
    public void SlotValidationResult_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new SlotValidationResult(1, "Spell", 123, ["Spell"], SlotValidationStatus.Valid);

        // Act
        var modified = original with { Slot = 5 };

        // Assert
        modified.Slot.Should().Be(5);
        modified.ExpectedSpell.Should().Be(original.ExpectedSpell);
    }

    [Fact]
    public void SlotValidationResult_Equality_DifferentTextureIds_AreNotEqual()
    {
        // Arrange
        var result1 = new SlotValidationResult(3, "Heal", 456, ["Heal"], SlotValidationStatus.Valid);
        var result2 = new SlotValidationResult(3, "Heal", 457, ["Heal"], SlotValidationStatus.Valid);

        // Assert
        result1.Should().NotBe(result2);
        (result1 != result2).Should().BeTrue();
    }

    [Fact]
    public void SlotValidationResult_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        var result = new SlotValidationResult(10, "Fireball", 789, ["Fireball", "Fireball Rank 2"], SlotValidationStatus.Mismatch);

        // Act
        var (slot, expectedSpell, actualTextureId, possibleSpells, status) = result;

        // Assert
        slot.Should().Be(10);
        expectedSpell.Should().Be("Fireball");
        actualTextureId.Should().Be(789);
        possibleSpells.Should().HaveCount(2);
        status.Should().Be(SlotValidationStatus.Mismatch);
    }

    [Theory]
    [InlineData(SlotValidationStatus.Valid)]
    [InlineData(SlotValidationStatus.Mismatch)]
    [InlineData(SlotValidationStatus.EmptySlot)]
    [InlineData(SlotValidationStatus.UnknownTexture)]
    [InlineData(SlotValidationStatus.NotOnActionBar)]
    public void SlotValidationResult_AllStatuses_Accepted(SlotValidationStatus status)
    {
        // Arrange & Act
        var result = new SlotValidationResult(1, "Test", 0, [], status);

        // Assert
        result.Status.Should().Be(status);
    }

    [Fact]
    public void SlotValidationResult_ToString_ContainsExpectedSpell()
    {
        // Arrange
        var result = new SlotValidationResult(5, "Shadow Bolt", 123, ["Shadow Bolt"], SlotValidationStatus.Valid);

        // Act
        string str = result.ToString();

        // Assert
        str.Should().Contain("Shadow Bolt");
    }
}
