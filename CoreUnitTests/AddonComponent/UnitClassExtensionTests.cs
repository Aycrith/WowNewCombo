using Core;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.AddonComponent;

public class UnitClassExtensionTests
{
    [Theory]
    [InlineData(UnitClass.Warrior, "Warrior")]
    [InlineData(UnitClass.Paladin, "Paladin")]
    [InlineData(UnitClass.Hunter, "Hunter")]
    [InlineData(UnitClass.Rogue, "Rogue")]
    [InlineData(UnitClass.Priest, "Priest")]
    [InlineData(UnitClass.DeathKnight, "DeathKnight")]
    [InlineData(UnitClass.Shaman, "Shaman")]
    [InlineData(UnitClass.Mage, "Mage")]
    [InlineData(UnitClass.Warlock, "Warlock")]
    [InlineData(UnitClass.Monk, "Monk")]
    [InlineData(UnitClass.Druid, "Druid")]
    [InlineData(UnitClass.DemonHunter, "DemonHunter")]
    public void ToStringF_AllClasses_ReturnCorrectNames(UnitClass unitClass, string expected)
    {
        // Act
        string result = unitClass.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToStringF_None_ReturnsNone()
    {
        // Act
        string result = UnitClass.None.ToStringF();

        // Assert
        result.Should().Be("None");
    }

    [Fact]
    public void ToStringF_AllClasses_HaveCorrectNames()
    {
        // Arrange & Act
        var allValues = System.Enum.GetValues<UnitClass>();

        // Assert
        foreach (UnitClass unitClass in allValues)
        {
            string result = unitClass.ToStringF();
            result.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData(UnitClass.Warrior, "Warrior")]
    [InlineData(UnitClass.Paladin, "Paladin")]
    [InlineData(UnitClass.Hunter, "Hunter")]
    [InlineData(UnitClass.Rogue, "Rogue")]
    public void ClassicClasses_AreRecognized(UnitClass unitClass, string expected)
    {
        // Act
        string result = unitClass.ToStringF();

        // Assert
        result.Should().Be(expected);
        result.Should().NotBe("None");
    }

    [Theory]
    [InlineData(UnitClass.DeathKnight, "DeathKnight")]
    [InlineData(UnitClass.Monk, "Monk")]
    [InlineData(UnitClass.DemonHunter, "DemonHunter")]
    public void ExpansionClasses_AreRecognized(UnitClass unitClass, string expected)
    {
        // Act
        string result = unitClass.ToStringF();

        // Assert
        result.Should().Be(expected);
        result.Should().NotBe("None");
    }
}
