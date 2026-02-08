using Core;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.AddonComponent;

public class PowerTypeExtensionTests
{
    [Theory]
    [InlineData(PowerType.Mana, "Mana")]
    [InlineData(PowerType.Rage, "Rage")]
    [InlineData(PowerType.Energy, "Energy")]
    [InlineData(PowerType.Focus, "Focus")]
    [InlineData(PowerType.RunicPower, "RunicPower")]
    [InlineData(PowerType.ComboPoints, "ComboPoints")]
    [InlineData(PowerType.SoulShards, "SoulShards")]
    [InlineData(PowerType.HolyPower, "HolyPower")]
    [InlineData(PowerType.LunarPower, "LunarPower")]
    [InlineData(PowerType.Maelstrom, "Maelstrom")]
    [InlineData(PowerType.Chi, "Chi")]
    [InlineData(PowerType.Insanity, "Insanity")]
    [InlineData(PowerType.ArcaneCharges, "ArcaneCharges")]
    [InlineData(PowerType.Fury, "Fury")]
    [InlineData(PowerType.Pain, "Pain")]
    [InlineData(PowerType.Essence, "Essence")]
    public void ToStringF_ReturnsCorrectName(PowerType powerType, string expected)
    {
        // Act
        string result = powerType.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(PowerType.HealthCost, "HealthCost")]
    [InlineData(PowerType.None, "None")]
    [InlineData(PowerType.Happiness, "Happiness")]
    [InlineData(PowerType.Runes, "Runes")]
    [InlineData(PowerType.Alternate, "Alternate")]
    [InlineData(PowerType.Obsolete2, "Obsolete2")]
    public void ToStringF_LesserUsedTypes_ReturnsCorrectName(PowerType powerType, string expected)
    {
        // Act
        string result = powerType.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(PowerType.RuneBlood, "RuneBlood")]
    [InlineData(PowerType.RuneFrost, "RuneFrost")]
    [InlineData(PowerType.RuneUnholy, "RuneUnholy")]
    public void ToStringF_RuneTypes_ReturnsCorrectName(PowerType powerType, string expected)
    {
        // Act
        string result = powerType.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToStringF_NumPowerTypes_ReturnsNumPowerTypes()
    {
        // Act
        string result = PowerType.NumPowerTypes.ToStringF();

        // Assert
        result.Should().Be("NumPowerTypes");
    }

    [Fact]
    public void ToStringF_AllPowerTypes_HaveCorrectNames()
    {
        // Arrange & Act
        var allValues = System.Enum.GetValues<PowerType>();

        // Assert - All should return non-null non-empty strings
        foreach (PowerType powerType in allValues)
        {
            string result = powerType.ToStringF();
            result.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData(PowerType.Mana, true)]
    [InlineData(PowerType.Rage, true)]
    [InlineData(PowerType.Energy, true)]
    [InlineData(PowerType.ComboPoints, true)]
    [InlineData(PowerType.RunicPower, true)]
    public void CommonPowerTypes_AreRecognized(PowerType powerType, bool shouldBeKnown)
    {
        // Act
        string result = powerType.ToStringF();

        // Assert
        result.Should().NotBe("None");
    }
}
