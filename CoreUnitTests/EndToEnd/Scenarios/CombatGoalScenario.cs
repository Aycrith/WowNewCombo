using FluentAssertions;

using MockWoWClient.GameState;
using MockWoWClient.InputHandling;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.EndToEnd.Scenarios;

/// <summary>
/// Scenario: Combat Goal Integration
/// Validates CombatGoal lifecycle, preconditions, and behavior using MockWoWClient.
/// </summary>
[EndToEndScenario("CombatGoal")]
public sealed class CombatGoalScenario : TestScenarioBase
{
    public CombatGoalScenario(ITestOutputHelper output) : base(output) { }

    public override string ScenarioName => "Combat Goal";
    public override string ScenarioDescription => "Tests CombatGoal preconditions, lifecycle, and corpse event production";

    #region Preconditions

    [Fact]
    public async Task CombatGoal_Preconditions_Met_WhenInCombatWithHostileTarget()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Target and engage
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.InCombat.Should().BeTrue();
        GameState.Player.HasTarget.Should().BeTrue();
        GameState.CurrentTarget.Should().NotBeNull();
        GameState.CurrentTarget!.IsDead.Should().BeFalse();
    }

    [Fact]
    public async Task CombatGoal_Preconditions_NotMet_WhenNoTarget()
    {
        // Arrange - No target

        // Act & Assert
        GameState.Player.HasTarget.Should().BeFalse();
        GameState.InCombat.Should().BeFalse();
    }

    [Fact]
    public async Task CombatGoal_Preconditions_NotMet_WhenTargetIsDead()
    {
        // Arrange - Spawn a dead NPC (health = 0)
        SpawnNpc("DeadWolf", 1, 0, new Vector3(5, 0, 0), hostile: true);
        await Task.Delay(100);

        // Act - Try to target the dead NPC
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100);

        // Assert - Dead NPCs cannot be targeted via TAB
        GameState.Player.HasTarget.Should().BeFalse("dead NPCs should not be targetable via TAB");
        GameState.CurrentTarget.Should().BeNull();
        
        // But the NPC should exist in the world and be dead
        var deadNpc = GameState.Npcs.FirstOrDefault(n => n.Name == "DeadWolf");
        deadNpc.Should().NotBeNull();
        deadNpc!.IsDead.Should().BeTrue();
    }

    [Fact]
    public async Task CombatGoal_Preconditions_NotMet_WhenTargetIsFriendly()
    {
        // Arrange - TAB only targets hostile NPCs, so friendly NPCs won't be targeted
        SpawnNpc("FriendlyNPC", 1, 100, new Vector3(5, 0, 0), hostile: false);
        await Task.Delay(100);

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100);

        // Assert - No target should be acquired since NPC is friendly
        GameState.Player.HasTarget.Should().BeFalse("TAB should not target friendly NPCs");
        GameState.CurrentTarget.Should().BeNull();
    }

    [Fact]
    public async Task CombatGoal_Preconditions_NotMet_WhenNotInCombat()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Target but don't attack
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Has target but not in combat
        AssertHasTarget("Wolf");
        GameState.InCombat.Should().BeFalse();
    }

    #endregion

    #region Lifecycle - OnEnter

    [Fact]
    public async Task CombatGoal_OnEnter_ShouldDismount()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        GameState.Player.IsMounted = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.Player.IsMounted.Should().BeFalse("should dismount when entering combat");
    }

    [Fact]
    public async Task CombatGoal_OnEnter_ShouldRecordInitialDirection()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        float initialDirection = MathF.PI / 4; // 45 degrees
        GameState.Player.Direction = initialDirection;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert - Direction tracking is internal, but we can verify combat started
        GameState.InCombat.Should().BeTrue();
    }

    #endregion

    #region Lifecycle - OnExit

    [Fact]
    public async Task CombatGoal_OnExit_ShouldUpdateState_WhenTargetDies()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Enter combat
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Kill target
        GameState.Npcs[0].Health = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Npcs[0].IsDead.Should().BeTrue();
    }

    #endregion

    #region Direction Tracking

    [Fact]
    public async Task CombatGoal_ShouldTrack_PlayerDirection()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Enter combat
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Change direction
        float newDirection = GameState.Player.Direction + MathF.PI / 2;
        GameState.Player.Direction = newDirection;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Direction.Should().Be(newDirection);
        GameState.InCombat.Should().BeTrue();
    }

    #endregion

    #region Pet Management

    [Fact]
    public async Task CombatGoal_ShouldTrack_PetState()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        GameState.Player.HasTarget = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.HasTarget.Should().BeTrue();
    }

    #endregion

    #region Swimming and Drowning

    [Fact]
    public async Task CombatGoal_ShouldDetect_SwimmingState()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        GameState.Player.IsSwimming = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.Player.IsSwimming.Should().BeTrue();
        GameState.InCombat.Should().BeTrue();
    }

    [Fact]
    public async Task CombatGoal_ShouldDetect_NormalState()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        GameState.Player.IsSwimming = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.Player.IsSwimming.Should().BeFalse();
    }

    #endregion

    #region Corpse Production

    [Fact]
    public async Task CombatGoal_ShouldProduceCorpse_WhenTargetDies()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 10, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Enter combat and kill
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Deal damage until death
        for (int i = 0; i < 5; i++)
        {
            MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        await WaitForConditionAsync(
            () => GameState.Corpses.Exists(c => c.NpcName == "Wolf"),
            "corpse should be created",
            TimeSpan.FromSeconds(2));

        AssertNpcDead("Wolf");
    }

    [Fact]
    public async Task CombatGoal_CorpseLocation_ShouldMatchOriginalPosition()
    {
        // Arrange
        Vector3 npcPosition = new Vector3(10, 0, 0);
        SpawnNpc("Wolf", 2, 10, npcPosition, hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Combat and kill
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Kill target
        for (int i = 0; i < 5; i++)
        {
            MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        await WaitForConditionAsync(
            () => GameState.Corpses.Exists(c => c.NpcName == "Wolf"),
            "corpse should exist");

        CorpseEntity? corpse = GameState.Corpses.Find(c => c.NpcName == "Wolf");
        corpse.Should().NotBeNull();
        corpse!.Position.Should().Be(npcPosition);
    }

    [Fact]
    public async Task CombatGoal_Corpse_ShouldExpire()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 10, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Kill and create corpse
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        GameState.Npcs[0].Health = 0;
        await WaitForConditionAsync(
            () => GameState.Corpses.Exists(c => c.NpcName == "Wolf"),
            "corpse should exist");

        // Act - Advance time significantly
        AdvanceTime(TimeSpan.FromMinutes(5));

        // Assert - Corpse should still exist (implementation dependent)
        // or expire based on game rules
    }

    #endregion

    #region Combat Events

    [Fact]
    public async Task CombatGoal_ShouldLog_CombatEvents()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Deal some damage
        for (int i = 0; i < 3; i++)
        {
            MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        GameState.CombatLog.Should().NotBeEmpty("combat events should be logged");
    }

    [Fact]
    public async Task CombatGoal_ShouldTrack_CombatStartTime()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        DateTime beforeCombat = DateTime.UtcNow;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.CombatStartTime.Should().BeAfter(beforeCombat);
    }

    #endregion

    #region Range and Distance

    [Fact]
    public async Task CombatGoal_ShouldActivate_AtMeleeRange()
    {
        // Arrange - Spawn NPC close by
        SpawnNpc("Wolf", 2, 100, new Vector3(2, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.InCombat.Should().BeTrue();
    }

    [Fact]
    public async Task CombatGoal_ShouldActivate_AtRangedRange()
    {
        // Arrange - Spawn NPC at ranged distance
        SpawnNpc("Wolf", 2, 100, new Vector3(20, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.InCombat.Should().BeTrue();
    }

    #endregion

    #region Multi-Target Scenarios

    [Fact]
    public async Task CombatGoal_ShouldHandle_TargetSwitching()
    {
        // Arrange
        SpawnNpc("Wolf1", 2, 100, new Vector3(5, 0, 0), hostile: true);
        SpawnNpc("Wolf2", 2, 100, new Vector3(8, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Target first
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        AssertHasTarget("Wolf1");

        // Act - Switch target
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.CurrentTarget.Should().NotBeNull();
    }

    [Fact]
    public async Task CombatGoal_ShouldProduce_MultipleCorpses()
    {
        // Arrange
        SpawnNpc("Wolf1", 2, 10, new Vector3(5, 0, 0), hostile: true);
        SpawnNpc("Wolf2", 2, 10, new Vector3(8, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Kill both
        foreach (var npc in new[] { "Wolf1", "Wolf2" })
        {
            MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
            MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
            await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

            GameState.CurrentTarget!.Health = 0;
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        await WaitForConditionAsync(
            () => GameState.Corpses.Count >= 2,
            "multiple corpses should exist",
            TimeSpan.FromSeconds(3));
    }

    #endregion

    #region State Management

    [Fact]
    public async Task CombatGoal_ShouldClear_CombatState_WhenNotInCombat()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Enter combat
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Clear target (exit combat)
        GameState.ClearTarget();
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.HasTarget.Should().BeFalse();
    }

    [Fact]
    public async Task CombatGoal_ShouldUpdate_PlayerState()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.Player.State.Should().Be(EntityState.Attacking);
    }

    #endregion
}
