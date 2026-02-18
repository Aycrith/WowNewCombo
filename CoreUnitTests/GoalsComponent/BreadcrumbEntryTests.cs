using System;
using System.Numerics;

using Core.GoalsComponent;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.GoalsComponent;

public class BreadcrumbEntryTests
{
    [Fact]
    public void BreadcrumbEntry_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        Vector3 position = new(100.5f, 200.3f, 50.0f);
        const float mapId = 0;
        DateTime timestamp = DateTime.UtcNow;

        // Act
        var entry = new BreadcrumbEntry(position, mapId, timestamp);

        // Assert
        entry.Position.Should().Be(position);
        entry.MapId.Should().Be(mapId);
        entry.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void BreadcrumbEntry_CreateWithZeroValues_StoresCorrectly()
    {
        // Arrange
        Vector3 position = Vector3.Zero;
        const float mapId = 0f;
        DateTime timestamp = DateTime.MinValue;

        // Act
        var entry = new BreadcrumbEntry(position, mapId, timestamp);

        // Assert
        entry.Position.Should().Be(Vector3.Zero);
        entry.MapId.Should().Be(0f);
    }

    [Fact]
    public void BreadcrumbEntry_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new BreadcrumbEntry(new Vector3(100, 200, 50), 0, DateTime.UtcNow);

        // Act
        var modified = original with { MapId = 1 };

        // Assert
        modified.MapId.Should().Be(1f);
        modified.Position.Should().Be(original.Position);
        modified.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void BreadcrumbEntry_Equality_SameValues_AreEqual()
    {
        // Arrange
        Vector3 position = new(150, 250, 75);
        const float mapId = 1;
        DateTime timestamp = new(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        var entry1 = new BreadcrumbEntry(position, mapId, timestamp);
        var entry2 = new BreadcrumbEntry(position, mapId, timestamp);

        // Assert
        entry1.Should().Be(entry2);
        (entry1 == entry2).Should().BeTrue();
        entry1.GetHashCode().Should().Be(entry2.GetHashCode());
    }

    [Fact]
    public void BreadcrumbEntry_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        DateTime timestamp = DateTime.UtcNow;
        var entry1 = new BreadcrumbEntry(new Vector3(100, 200, 50), 0, timestamp);
        var entry2 = new BreadcrumbEntry(new Vector3(101, 200, 50), 0, timestamp);

        // Assert
        entry1.Should().NotBe(entry2);
        (entry1 != entry2).Should().BeTrue();
    }

    [Fact]
    public void BreadcrumbEntry_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        Vector3 position = new(300, 400, 100);
        const float mapId = 1;
        DateTime timestamp = DateTime.UtcNow;
        var entry = new BreadcrumbEntry(position, mapId, timestamp);

        // Act
        var (resultPosition, resultMapId, resultTimestamp) = entry;

        // Assert
        resultPosition.Should().Be(position);
        resultMapId.Should().Be(mapId);
        resultTimestamp.Should().Be(timestamp);
    }

    [Fact]
    public void BreadcrumbEntry_ToString_ContainsPositionInfo()
    {
        // Arrange
        var entry = new BreadcrumbEntry(new Vector3(100, 200, 50), 0, DateTime.UtcNow);

        // Act
        string result = entry.ToString();

        // Assert
        result.Should().Contain("100");
        result.Should().Contain("200");
    }

    [Fact]
    public void BreadcrumbEntry_HistoricalTimestamp_StoresCorrectly()
    {
        // Arrange
        DateTime past = new(2020, 5, 20, 15, 30, 0, DateTimeKind.Utc);

        // Act
        var entry = new BreadcrumbEntry(Vector3.Zero, 0, past);

        // Assert
        entry.Timestamp.Should().Be(past);
    }

    [Fact]
    public void BreadcrumbEntry_NegativeCoordinates_StoresCorrectly()
    {
        // Arrange
        Vector3 position = new(-500, -1000, -50);

        // Act
        var entry = new BreadcrumbEntry(position, 0, DateTime.UtcNow);

        // Assert
        entry.Position.X.Should().Be(-500);
        entry.Position.Y.Should().Be(-1000);
        entry.Position.Z.Should().Be(-50);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(999)]
    public void BreadcrumbEntry_VariousMapIds_Accepted(float mapId)
    {
        // Act
        var entry = new BreadcrumbEntry(Vector3.Zero, mapId, DateTime.UtcNow);

        // Assert
        entry.MapId.Should().Be(mapId);
    }
}
