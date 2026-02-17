using FluentAssertions;
using MockWoWClient.InputHandling;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.EndToEnd.Scenarios;

/// <summary>
/// Scenario 2: Target Acquisition
/// Validates that the bot can target enemies using Tab key.
/// </summary>
[EndToEndScenario("TargetAcquisition")]
public class TargetAcquisitionScenario : TestScenarioBase
{
    public TargetAcquisitionScenario(ITestOutputHelper output) : base(output) { }

    public override string ScenarioName => "Target Acquisition";

    public override string ScenarioDescription =>
        "Validates that the bot can acquire targets using Tab key targeting. " +
        "Tests target detection, health reading, and distance calculation.";

    [Fact]
    public async Task TabKey_ShouldTargetNearestHostileNpc()
    {
        // Arrange
        _output.WriteLine("  Spawning test NPC...");
        var npc = SpawnNpc("Test Wolf", 2, 50, new Vector3(10, 0, 0));

        // Give time for NPC to spawn
        await Task.Delay(100);

        // Act - Simulate Tab key press
        _output.WriteLine("  Pressing Tab key...");
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100);

        // Assert
        AssertHasTarget("Test Wolf");
        GameState.CurrentTarget!.Level.Should().Be(2);
        GameState.CurrentTarget.Health.Should().Be(50);
    }

    [Fact]
    public async Task Target_ShouldHaveCorrectPosition()
    {
        // Arrange
        var position = new Vector3(25, 10, 0);
        SpawnNpc("Distant Mob", 3, 80, position);
        await Task.Delay(100);

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100);

        // Assert
        AssertHasTarget("Distant Mob");
        GameState.CurrentTarget!.Position.Should().Be(position);
    }

    [Fact]
    public async Task MultipleNpcs_ShouldTargetNearest()
    {
        // Arrange
        SpawnNpc("Far Mob", 1, 30, new Vector3(50, 0, 0));
        SpawnNpc("Near Mob", 2, 40, new Vector3(5, 0, 0));
        SpawnNpc("Medium Mob", 3, 60, new Vector3(20, 0, 0));
        await Task.Delay(100);

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100);

        // Assert - Should target the nearest one (Near Mob at distance 5)
        AssertHasTarget("Near Mob");
    }

    [Fact]
    public async Task NoHostileNpcs_ShouldNotHaveTarget()
    {
        // Arrange - No NPCs spawned
        GameState.CurrentTarget.Should().BeNull();

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100);

        // Assert - Should still have no target
        GameState.CurrentTarget.Should().BeNull("no hostile NPCs should result in no target");
    }

    [Fact]
    public async Task ClearTarget_ShouldRemoveCurrentTarget()
    {
        // Arrange
        SpawnNpc("Target", 1, 20, new Vector3(5, 0, 0));
        await Task.Delay(100);

        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100);

        AssertHasTarget("Target");

        // Act - Press Escape to clear target
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_ESCAPE);
        await Task.Delay(100);

        // Assert
        GameState.CurrentTarget.Should().BeNull("target should be cleared");
    }
}
