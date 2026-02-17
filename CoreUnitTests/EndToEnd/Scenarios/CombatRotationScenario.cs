using FluentAssertions;
using MockWoWClient.InputHandling;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.EndToEnd.Scenarios;

/// <summary>
/// Scenario 3: Combat Rotation
/// Validates the full combat cycle including rotation optimizer.
/// </summary>
[EndToEndScenario("CombatRotation")]
public class CombatRotationScenario : TestScenarioBase
{
    public CombatRotationScenario(ITestOutputHelper output) : base(output) { }

    public override string ScenarioName => "Combat Rotation";

    public override string ScenarioDescription =>
        "Validates the full combat cycle: targeting, approach, combat, and looting. " +
        "Tests rotation optimizer, GCD handling, and ability execution.";

    [Fact]
    public async Task Combat_ShouldStart_WhenTargetingAndAttacking()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 50, new Vector3(5, 0, 0));
        await Task.Delay(100);

        // Target the NPC
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100);

        AssertHasTarget("Wolf");

        // Act - Start combat (key 1 = Sinister Strike)
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await Task.Delay(200);

        // Assert
        AssertInCombat();
        GameState.CombatLog.Should().NotBeEmpty("combat events should be recorded");
    }

    [Fact]
    public async Task AutoAttack_ShouldDealDamage()
    {
        // Arrange
        SpawnNpc("Target Dummy", 1, 100, new Vector3(3, 0, 0));
        await Task.Delay(100);

        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100);

        var initialHealth = GameState.CurrentTarget!.Health;
        _output.WriteLine($"  Initial target health: {initialHealth}");

        // Act - Use action bar slot 1
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);

        // Wait for combat to process
        await WaitForConditionAsync(
            () => GameState.CombatLog.Count > 0,
            "combat log should have entries",
            TimeSpan.FromSeconds(2));

        // Assert
        GameState.CurrentTarget.Health.Should().BeLessThan(initialHealth, "target should take damage");
        _output.WriteLine($"  After damage: {GameState.CurrentTarget.Health}");
    }

    [Fact]
    public async Task TargetDeath_ShouldEndCombat()
    {
        // Arrange
        SpawnNpc("Weak Mob", 1, 10, new Vector3(5, 0, 0));
        await Task.Delay(100);

        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100);

        // Act - Deal damage until death
        for (int i = 0; i < 5; i++)
        {
            MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
            await Task.Delay(200);
        }

        // Assert
        GameState.CurrentTarget!.IsDead.Should().BeTrue("target should be dead");

        // Should have a corpse
        GameState.Corpses.Should().Contain(c => c.NpcName == "Weak Mob", "corpse should exist");
    }

    [Fact]
    public async Task GCD_ShouldPreventAbilitySpam()
    {
        // Arrange
        SpawnNpc("GCD Test", 2, 100, new Vector3(5, 0, 0));
        await Task.Delay(100);

        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100);

        // Act - Spam keys rapidly
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);

        await Task.Delay(100);

        // Count combat events - should be limited by GCD
        var eventCount = GameState.CombatLog.Count;
        _output.WriteLine($"  Combat events: {eventCount}");

        // Should have at most 1-2 events due to GCD
        eventCount.Should().BeLessThan(3, "GCD should prevent spam");
    }

    [Fact]
    public async Task Loot_ShouldBeAvailable_AfterTargetDeath()
    {
        // Arrange - Kill a mob
        var npc = SpawnNpc("Loot Mob", 1, 5, new Vector3(5, 0, 0));
        await Task.Delay(100);

        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100);

        // Kill it (damage both target and original NPC)
        GameState.CurrentTarget!.TakeDamage(10);
        npc.TakeDamage(10); // Also damage the original NPC
        await Task.Delay(100);

        // Act - Find nearest corpse
        var corpse = GameState.GetNearestLootableCorpse(10f);

        // Assert
        corpse.Should().NotBeNull("corpse should be available for looting");
        corpse!.NpcName.Should().Be("Loot Mob");
    }
}
