using Core;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Actionbar;

public class ActionBarCostTests
{
    [Theory]
    [InlineData(PowerType.Mana, 50)]
    [InlineData(PowerType.Rage, 30)]
    [InlineData(PowerType.Energy, 40)]
    [InlineData(PowerType.Focus, 25)]
    [InlineData(PowerType.RunicPower, 60)]
    public void ActionBarCost_CreateWithValues_StoresCorrectly(PowerType powerType, int cost)
    {
        // Act
        var actionBarCost = new ActionBarCost(powerType, cost);

        // Assert
        actionBarCost.PowerType.Should().Be(powerType);
        actionBarCost.Cost.Should().Be(cost);
    }

    [Fact]
    public void ActionBarCost_ZeroCost_StoresCorrectly()
    {
        // Act
        var actionBarCost = new ActionBarCost(PowerType.Mana, 0);

        // Assert
        actionBarCost.Cost.Should().Be(0);
        actionBarCost.PowerType.Should().Be(PowerType.Mana);
    }

    [Fact]
    public void ActionBarCost_NegativeCost_StoresCorrectly()
    {
        // Act
        var actionBarCost = new ActionBarCost(PowerType.Rage, -10);

        // Assert
        actionBarCost.Cost.Should().Be(-10);
    }

    [Fact]
    public void ActionBarCost_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new ActionBarCost(PowerType.Mana, 50);

        // Act
        var modified = original with { Cost = 75 };

        // Assert
        modified.Cost.Should().Be(75);
        modified.PowerType.Should().Be(original.PowerType);
    }

    [Fact]
    public void ActionBarCost_Equality_SameValues_AreEqual()
    {
        // Arrange
        var cost1 = new ActionBarCost(PowerType.Energy, 40);
        var cost2 = new ActionBarCost(PowerType.Energy, 40);

        // Assert
        cost1.Should().Be(cost2);
        (cost1 == cost2).Should().BeTrue();
        cost1.GetHashCode().Should().Be(cost2.GetHashCode());
    }

    [Fact]
    public void ActionBarCost_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var cost1 = new ActionBarCost(PowerType.Mana, 50);
        var cost2 = new ActionBarCost(PowerType.Mana, 60);

        // Assert
        cost1.Should().NotBe(cost2);
        (cost1 != cost2).Should().BeTrue();
    }

    [Fact]
    public void ActionBarCost_Equality_DifferentPowerTypes_AreNotEqual()
    {
        // Arrange
        var cost1 = new ActionBarCost(PowerType.Mana, 50);
        var cost2 = new ActionBarCost(PowerType.Rage, 50);

        // Assert
        cost1.Should().NotBe(cost2);
    }

    [Fact]
    public void ActionBarCost_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        var actionBarCost = new ActionBarCost(PowerType.Focus, 35);

        // Act
        var (powerType, cost) = actionBarCost;

        // Assert
        powerType.Should().Be(PowerType.Focus);
        cost.Should().Be(35);
    }

    [Fact]
    public void ActionBarCost_ToString_ContainsValues()
    {
        // Arrange
        var actionBarCost = new ActionBarCost(PowerType.RunicPower, 60);

        // Act
        string result = actionBarCost.ToString();

        // Assert
        result.Should().Contain("RunicPower");
        result.Should().Contain("60");
    }

    [Fact]
    public void ActionBarCost_LargeCost_StoresCorrectly()
    {
        // Act
        var actionBarCost = new ActionBarCost(PowerType.Mana, int.MaxValue);

        // Assert
        actionBarCost.Cost.Should().Be(int.MaxValue);
    }

    [Theory]
    [InlineData(PowerType.Mana)]
    [InlineData(PowerType.Rage)]
    [InlineData(PowerType.Energy)]
    [InlineData(PowerType.Focus)]
    [InlineData(PowerType.RunicPower)]
    public void ActionBarCost_AllPowerTypes_Accepted(PowerType powerType)
    {
        // Arrange & Act
        var actionBarCost = new ActionBarCost(powerType, 50);

        // Assert
        actionBarCost.PowerType.Should().Be(powerType);
    }
}
