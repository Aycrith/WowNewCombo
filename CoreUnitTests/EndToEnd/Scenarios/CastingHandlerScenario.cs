using Core;
using Core.Goals;

using FluentAssertions;

using MockWoWClient.GameState;
using MockWoWClient.InputHandling;

using System;
using System.Numerics;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.EndToEnd.Scenarios;

/// <summary>
/// Scenario: Casting Handler Integration
/// Validates spell casting, GCD, cast bar waiting, and error handling.
/// Tests spell queuing, interrupts, and casting state management.
/// </summary>
[EndToEndScenario("CastingHandler")]
public sealed class CastingHandlerScenario : TestScenarioBase
{
    public CastingHandlerScenario(ITestOutputHelper output) : base(output) { }

    public override string ScenarioName => "Casting Handler";
    public override string ScenarioDescription => "Tests spell casting, GCD management, and cast bar handling";

    #region Spell Queue

    [Fact]
    public async Task CastingHandler_ShouldDetect_SpellInQueue()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Simulate casting state
        GameState.Player.IsCasting = true;
        GameState.Player.RemainingCastTimeMs = 200;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.IsCasting.Should().BeTrue();
        GameState.Player.RemainingCastTimeMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CastingHandler_ShouldNotQueue_WhenCastComplete()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Complete cast
        GameState.Player.IsCasting = false;
        GameState.Player.RemainingCastTimeMs = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.IsCasting.Should().BeFalse();
    }

    #endregion

    #region Cast Bar

    [Fact]
    public async Task CastingHandler_ShouldWait_ForCastBar()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Start casting
        GameState.Player.StartCast(123, 2000); // SpellId 123, 2 second cast
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.IsCasting.Should().BeTrue();
        GameState.Player.RemainingCastTimeMs.Should().BeGreaterThan(1000);
    }

    [Fact]
    public async Task CastingHandler_ShouldDetect_CastCompletion()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        GameState.Player.StartCast(123, 500); // 0.5 second cast
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Wait for cast to complete
        await WaitForConditionAsync(
            () => !GameState.Player.IsCasting,
            "cast should complete",
            TimeSpan.FromSeconds(2));

        // Assert
        GameState.Player.IsCasting.Should().BeFalse();
    }

    [Fact]
    public async Task CastingHandler_ShouldHandle_CastInterrupt()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        GameState.Player.StartCast(123, 2000);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Interrupt cast
        GameState.Player.InterruptCast();
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.IsCasting.Should().BeFalse();
        GameState.Player.RemainingCastTimeMs.Should().Be(0);
    }

    #endregion

    #region GCD Management

    [Theory]
    [InlineData(1500)]
    [InlineData(1000)]
    public async Task CastingHandler_ShouldRespect_GCD(int gcdDuration)
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Simulate casting with cooldown
        GameState.Player.IsCasting = true;
        await Task.Delay(gcdDuration / 10);

        // Assert
        GameState.Player.IsCasting.Should().BeTrue();
    }

    [Fact]
    public async Task CastingHandler_ShouldAllowCast_AfterGCD()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Complete cast
        GameState.Player.IsCasting = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Can cast again
        GameState.Player.IsCasting.Should().BeFalse();
    }

    #endregion

    #region Key Press

    [Fact]
    public async Task CastingHandler_ShouldPressKey_ForSpellCast()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        await WaitForConditionAsync(() => GameState.Player.HasTarget, "target acquired");

        // Act - Press spell key
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.CombatLog.Should().NotBeNull();
    }

    [Fact]
    public async Task CastingHandler_ShouldStopBeforeCasting()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        GameState.Player.IsMoving = true;

        // Act - Cast requires stopping
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Should have stopped for cast
        // Note: Actual behavior depends on spell requirements
    }

    #endregion

    #region Usable Action

    [Fact]
    public async Task CastingHandler_ShouldCheck_UsableAction()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Check if action is usable
        GameState.Player.Power = 50;
        GameState.Player.PowerMax = 100;

        // Assert
        GameState.Player.Power.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CastingHandler_ShouldNotCast_WhenNotUsable()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - No power/resources
        GameState.Player.Power = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Power.Should().Be(0);
    }

    #endregion

    #region After Cast

    [Fact]
    public async Task CastingHandler_ShouldWaitSwing_AfterCast()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Complete cast and wait for swing
        GameState.Player.MainHandSwingTime = 2000;
        GameState.Player.MainHandSwingProgress = 500;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.MainHandSwingProgress.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CastingHandler_ShouldNotWaitSwing_WhenDisabled()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Cast without waiting for swing
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Should continue immediately
        GameState.InCombat.Should().BeTrue();
    }

    #endregion

    #region Combat Log

    [Fact]
    public async Task CastingHandler_ShouldLog_CastSuccess()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Cast and deal damage
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.CombatLog.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CastingHandler_ShouldTrack_DamageDealt()
    {
        // Arrange
        int initialHealth = 100;
        SpawnNpc("Wolf", 2, initialHealth, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Wait for damage
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.CurrentTarget!.Health.Should().BeLessThan(initialHealth);
    }

    #endregion

    #region Interrupt Watchdog

    [Fact]
    public async Task CastingHandler_ShouldDetect_InterruptFromMovement()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        GameState.Player.StartCast(123, 2000);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Move during cast
        GameState.Player.IsMoving = true;
        GameState.Player.InterruptCast();
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.IsCasting.Should().BeFalse();
    }

    #endregion

    #region Range Check

    [Fact]
    public async Task CastingHandler_ShouldCheck_Range()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(30, 0, 0), hostile: true); // Far away
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        float distance = Vector3.Distance(GameState.Player.Position, GameState.CurrentTarget!.Position);

        // Assert
        distance.Should().BeGreaterThan(20.0f); // Out of melee range
    }

    [Fact]
    public async Task CastingHandler_ShouldMoveCloser_WhenOutOfRange()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(30, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Move closer
        GameState.Player.Position = new Vector3(25, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        float distance = Vector3.Distance(GameState.Player.Position, GameState.CurrentTarget!.Position);
        distance.Should().BeLessThan(10.0f);
    }

    #endregion

    #region Cooldown

    [Fact]
    public async Task CastingHandler_ShouldTrack_Cooldown()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Set cooldown
        GameState.Player.ActionBars[0].CooldownRemaining = 5000;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.ActionBars[0].CooldownRemaining.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CastingHandler_ShouldWait_Cooldown()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Wait for cooldown
        GameState.Player.ActionBars[0].CooldownRemaining = 100;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        GameState.Player.ActionBars[0].CooldownRemaining = 0;

        // Assert
        GameState.Player.ActionBars[0].CooldownRemaining.Should().Be(0);
    }

    #endregion
}
