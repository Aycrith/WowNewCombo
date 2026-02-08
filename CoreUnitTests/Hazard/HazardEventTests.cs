using System;
using System.Numerics;

using Core.Hazard;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Hazard;

public class HazardEventTests
{
    [Fact]
    public void HazardEvent_CreateWithRequiredValues_StoresCorrectly()
    {
        // Arrange
        Vector3 worldPos = new(100.5f, 200.3f, 50.0f);
        int mapId = 0;
        int uiMapId = 1426;
        HazardEventType type = HazardEventType.Stuck;

        // Act
        var hazardEvent = new HazardEvent
        {
            WorldPosition = worldPos,
            MapId = mapId,
            UIMapId = uiMapId,
            Type = type
        };

        // Assert
        hazardEvent.WorldPosition.Should().Be(worldPos);
        hazardEvent.MapId.Should().Be(mapId);
        hazardEvent.UIMapId.Should().Be(uiMapId);
        hazardEvent.Type.Should().Be(type);
    }

    [Fact]
    public void HazardEvent_CreateWithOptionalValues_StoresCorrectly()
    {
        // Arrange
        DateTime timestamp = DateTime.UtcNow.AddMinutes(-5);

        // Act
        var hazardEvent = new HazardEvent
        {
            WorldPosition = new Vector3(100, 200, 50),
            MapId = 1,
            UIMapId = 141,
            Type = HazardEventType.Death,
            MapX = 45.5f,
            MapY = 67.8f,
            Timestamp = timestamp,
            Zone = "Elwynn Forest",
            DurationMs = 5000,
            AttemptCount = 3,
            PlayerClass = "Warrior",
            PlayerLevel = 25
        };

        // Assert
        hazardEvent.MapX.Should().Be(45.5f);
        hazardEvent.MapY.Should().Be(67.8f);
        hazardEvent.Timestamp.Should().Be(timestamp);
        hazardEvent.Zone.Should().Be("Elwynn Forest");
        hazardEvent.DurationMs.Should().Be(5000);
        hazardEvent.AttemptCount.Should().Be(3);
        hazardEvent.PlayerClass.Should().Be("Warrior");
        hazardEvent.PlayerLevel.Should().Be(25);
    }

    [Fact]
    public void HazardEvent_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var hazardEvent = new HazardEvent
        {
            WorldPosition = new Vector3(0, 0, 0),
            MapId = 0,
            UIMapId = 0,
            Type = HazardEventType.Stuck
        };

        // Assert
        hazardEvent.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        hazardEvent.Zone.Should().BeEmpty();
        hazardEvent.MapX.Should().Be(0f);
        hazardEvent.MapY.Should().Be(0f);
        hazardEvent.DurationMs.Should().Be(0);
        hazardEvent.AttemptCount.Should().Be(0);
        hazardEvent.PlayerLevel.Should().Be(0);
        hazardEvent.PlayerClass.Should().BeNull();
    }

    [Fact]
    public void HazardEvent_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new HazardEvent
        {
            WorldPosition = new Vector3(100, 200, 50),
            MapId = 0,
            UIMapId = 1426,
            Type = HazardEventType.Stuck,
            Zone = "Original Zone"
        };

        // Act
        var modified = original with { Zone = "New Zone" };

        // Assert
        modified.Zone.Should().Be("New Zone");
        modified.WorldPosition.Should().Be(original.WorldPosition);
        modified.MapId.Should().Be(original.MapId);
        modified.UIMapId.Should().Be(original.UIMapId);
        modified.Type.Should().Be(original.Type);
    }

    [Fact]
    public void HazardEvent_With_ModifiesProperties()
    {
        // Arrange
        DateTime timestamp = DateTime.UtcNow;
        var original = new HazardEvent
        {
            WorldPosition = new Vector3(100, 200, 50),
            MapId = 0,
            UIMapId = 1426,
            Type = HazardEventType.Stuck,
            Zone = "Original Zone",
            Timestamp = timestamp
        };

        // Act
        var modified = original with { Zone = "New Zone" };

        // Assert
        modified.Zone.Should().Be("New Zone");
        modified.WorldPosition.Should().Be(original.WorldPosition);
        modified.MapId.Should().Be(original.MapId);
        modified.UIMapId.Should().Be(original.UIMapId);
        modified.Type.Should().Be(original.Type);
        modified.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void HazardEvent_With_DifferentWorldPosition_CreatesNewInstance()
    {
        // Arrange
        var original = new HazardEvent
        {
            WorldPosition = new Vector3(100, 200, 50),
            MapId = 0,
            UIMapId = 1426,
            Type = HazardEventType.Stuck
        };

        // Act
        var modified = original with { WorldPosition = new Vector3(101, 200, 50) };

        // Assert
        modified.WorldPosition.X.Should().Be(101);
        modified.MapId.Should().Be(original.MapId);
    }

    [Theory]
    [InlineData(HazardEventType.Stuck)]
    [InlineData(HazardEventType.Death)]
    [InlineData(HazardEventType.TargetEvade)]
    [InlineData(HazardEventType.PathfindingFailure)]
    [InlineData(HazardEventType.UnexpectedAggro)]
    [InlineData(HazardEventType.ManualMarker)]
    public void HazardEvent_AllHazardEventTypes_Accepted(HazardEventType type)
    {
        // Arrange & Act
        var hazardEvent = new HazardEvent
        {
            WorldPosition = Vector3.Zero,
            MapId = 0,
            UIMapId = 0,
            Type = type
        };

        // Assert
        hazardEvent.Type.Should().Be(type);
    }
}
