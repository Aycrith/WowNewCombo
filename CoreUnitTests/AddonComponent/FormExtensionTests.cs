using Core;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.AddonComponent;

public class FormExtensionTests
{
    [Theory]
    [InlineData(Form.Druid_Bear, "Druid_Bear")]
    [InlineData(Form.Druid_Cat, "Druid_Cat")]
    [InlineData(Form.Druid_Aquatic, "Druid_Aquatic")]
    [InlineData(Form.Druid_Travel, "Druid_Travel")]
    [InlineData(Form.Druid_Moonkin, "Druid_Moonkin")]
    [InlineData(Form.Druid_Flight, "Druid_Flight")]
    [InlineData(Form.Druid_Cat_Prowl, "Druid_Cat_Prowl")]
    public void ToStringF_DruidForms_ReturnCorrectNames(Form form, string expected)
    {
        // Act
        string result = form.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(Form.Rogue_Stealth, "Rogue_Stealth")]
    [InlineData(Form.Rogue_Vanish, "Rogue_Vanish")]
    public void ToStringF_RogueForms_ReturnCorrectNames(Form form, string expected)
    {
        // Act
        string result = form.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(Form.Warrior_BattleStance, "Warrior_BattleStance")]
    [InlineData(Form.Warrior_DefensiveStance, "Warrior_DefensiveStance")]
    [InlineData(Form.Warrior_BerserkerStance, "Warrior_BerserkerStance")]
    public void ToStringF_WarriorStances_ReturnCorrectNames(Form form, string expected)
    {
        // Act
        string result = form.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(Form.Priest_Shadowform, "Priest_Shadowform")]
    [InlineData(Form.Shaman_GhostWolf, "Shaman_GhostWolf")]
    public void ToStringF_OtherClassForms_ReturnCorrectNames(Form form, string expected)
    {
        // Act
        string result = form.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(Form.Paladin_Devotion_Aura, "Paladin_Devotion_Aura")]
    [InlineData(Form.Paladin_Retribution_Aura, "Paladin_Retribution_Aura")]
    [InlineData(Form.Paladin_Concentration_Aura, "Paladin_Concentration_Aura")]
    [InlineData(Form.Paladin_Crusader_Aura, "Paladin_Crusader_Aura")]
    public void ToStringF_PaladinAuras_ReturnCorrectNames(Form form, string expected)
    {
        // Act
        string result = form.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(Form.DeathKnight_Blood_Presence, "DeathKnight_Blood_Presence")]
    [InlineData(Form.DeathKnight_Frost_Presence, "DeathKnight_Frost_Presence")]
    [InlineData(Form.DeathKnight_Unholy_Presence, "DeathKnight_Unholy_Presence")]
    public void ToStringF_DeathKnightPresences_ReturnCorrectNames(Form form, string expected)
    {
        // Act
        string result = form.ToStringF();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToStringF_None_ReturnsNone()
    {
        // Act
        string result = Form.None.ToStringF();

        // Assert
        result.Should().Be("None");
    }

    [Fact]
    public void ToStringF_AllForms_HaveCorrectNames()
    {
        // Arrange & Act
        var allValues = System.Enum.GetValues<Form>();

        // Assert
        foreach (Form form in allValues)
        {
            string result = form.ToStringF();
            result.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData(Form.Paladin_Shadow_Resistance_Aura, "Paladin_Shadow_Resistance_Aura")]
    [InlineData(Form.Paladin_Frost_Resistance_Aura, "Paladin_Frost_Resistance_Aura")]
    [InlineData(Form.Paladin_Fire_Resistance_Aura, "Paladin_Fire_Resistance_Aura")]
    [InlineData(Form.Paladin_Sanctity_Aura, "Paladin_Sanctity_Aura")]
    public void ToStringF_PaladinResistanceAuras_ReturnCorrectNames(Form form, string expected)
    {
        // Act
        string result = form.ToStringF();

        // Assert
        result.Should().Be(expected);
    }
}
