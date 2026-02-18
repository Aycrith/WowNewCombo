using System.Numerics;

using Core.Database;

using FluentAssertions;

using SharedLib;

using Xunit;

namespace CoreUnitTests.Database;

public class NpcSearchResultTests
{
    [Fact]
    public void NpcSearchResult_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        Creature creature = new()
        {
            Name = "TestNPC",
            Entry = 12345,
            MinLevel = 25,
            MaxLevel = 25
        };
        Vector3 worldPosition = new(100.5f, 200.3f, 50.0f);
        const float distance = 15.5f;

        // Act
        var result = new NpcSearchResult(creature, worldPosition, distance);

        // Assert
        result.Creature.Should().Be(creature);
        result.WorldPosition.Should().Be(worldPosition);
        result.Distance.Should().Be(distance);
    }

    [Fact]
    public void NpcSearchResult_CreateWithZeroDistance_StoresCorrectly()
    {
        // Arrange
        Creature creature = new() { Name = "NPC" };
        Vector3 position = Vector3.Zero;

        // Act
        var result = new NpcSearchResult(creature, position, 0f);

        // Assert
        result.Distance.Should().Be(0f);
    }

    [Fact]
    public void NpcSearchResult_With_ModifiesSingleProperty()
    {
        // Arrange
        Creature creature = new() { Name = "NPC" };
        var original = new NpcSearchResult(creature, new Vector3(100, 200, 50), 10f);

        // Act
        var modified = original with { Distance = 20f };

        // Assert
        modified.Distance.Should().Be(20f);
        modified.Creature.Should().Be(original.Creature);
        modified.WorldPosition.Should().Be(original.WorldPosition);
    }

    [Fact]
    public void NpcSearchResult_Equality_SameValues_AreEqual()
    {
        // Arrange
        Creature creature = new() { Name = "NPC", Entry = 123 };
        Vector3 position = new(100, 200, 50);

        var result1 = new NpcSearchResult(creature, position, 10f);
        var result2 = new NpcSearchResult(creature, position, 10f);

        // Assert
        result1.Should().Be(result2);
        (result1 == result2).Should().BeTrue();
    }

    [Fact]
    public void NpcSearchResult_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        Creature creature = new() { Name = "NPC" };
        Vector3 position = new(100, 200, 50);

        var result1 = new NpcSearchResult(creature, position, 10f);
        var result2 = new NpcSearchResult(creature, position, 20f);

        // Assert
        result1.Should().NotBe(result2);
        (result1 != result2).Should().BeTrue();
    }

    [Fact]
    public void NpcSearchResult_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        Creature creature = new() { Name = "Test" };
        Vector3 position = new(50, 100, 25);
        const float distance = 30f;
        var result = new NpcSearchResult(creature, position, distance);

        // Act
        var (resultCreature, resultPosition, resultDistance) = result;

        // Assert
        resultCreature.Should().Be(creature);
        resultPosition.Should().Be(position);
        resultDistance.Should().Be(distance);
    }

    [Fact]
    public void NpcSearchResult_NegativeDistance_StoresCorrectly()
    {
        // Arrange
        Creature creature = new() { Name = "NPC" };
        Vector3 position = new(100, 200, 50);

        // Act
        var result = new NpcSearchResult(creature, position, -5f);

        // Assert
        result.Distance.Should().Be(-5f);
    }

    [Fact]
    public void NpcSearchResult_LargeDistance_StoresCorrectly()
    {
        // Arrange
        Creature creature = new() { Name = "NPC" };
        Vector3 position = new(100, 200, 50);

        // Act
        var result = new NpcSearchResult(creature, position, float.MaxValue);

        // Assert
        result.Distance.Should().Be(float.MaxValue);
    }

    [Fact]
    public void NpcSearchResult_DifferentCreatures_AreNotEqual()
    {
        // Arrange
        Creature creature1 = new() { Name = "NPC1", Entry = 1 };
        Creature creature2 = new() { Name = "NPC2", Entry = 2 };
        Vector3 position = new(100, 200, 50);

        var result1 = new NpcSearchResult(creature1, position, 10f);
        var result2 = new NpcSearchResult(creature2, position, 10f);

        // Assert
        result1.Should().NotBe(result2);
    }

    [Fact]
    public void NpcSearchResult_ToString_ContainsCreatureName()
    {
        // Arrange
        Creature creature = new() { Name = "TestCreature" };
        var result = new NpcSearchResult(creature, Vector3.Zero, 0f);

        // Act
        string str = result.ToString();

        // Assert
        str.Should().Contain("TestCreature");
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(5.5f)]
    [InlineData(100f)]
    public void NpcSearchResult_VariousDistances_StoredCorrectly(float distance)
    {
        // Arrange
        Creature creature = new() { Name = "NPC" };
        Vector3 position = new(100, 200, 50);

        // Act
        var result = new NpcSearchResult(creature, position, distance);

        // Assert
        result.Distance.Should().Be(distance);
    }
}
