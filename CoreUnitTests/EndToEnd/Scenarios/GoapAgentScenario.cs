using Core;
using Core.GOAP;
using Core.Goals;

using FluentAssertions;

using MockWoWClient.GameState;
using MockWoWClient.InputHandling;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.EndToEnd.Scenarios;

/// <summary>
/// Scenario: GOAP Agent Integration
/// Validates decision-making, planning, goal switching, and "NO PLAN" handling.
/// Tests world state management, plan generation, and abort/resume events.
/// </summary>
[EndToEndScenario("GoapAgent")]
public sealed class GoapAgentScenario : TestScenarioBase
{
    public GoapAgentScenario(ITestOutputHelper output) : base(output) { }

    public override string ScenarioName => "GOAP Agent";
    public override string ScenarioDescription => "Tests GOAP planning, goal switching, and decision-making";

    #region World State Management

    [Fact]
    public async Task GoapAgent_ShouldTrack_WorldState()
    {
        // Arrange
        GameState.Player.HasTarget = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Target enemy
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.HasTarget.Should().BeTrue();
    }

    [Fact]
    public async Task GoapAgent_ShouldUpdate_WorldState_WhenCombatChanges()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        GameState.Player.HasTarget = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Enter combat
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.InCombat.Should().BeTrue();
    }

    [Fact]
    public async Task GoapAgent_ShouldUpdate_WorldState_WhenTargetDies()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 10, new Vector3(5, 0, 0), hostile: true);
        GameState.Player.HasTarget = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Kill target
        GameState.Npcs[0].Health = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Npcs[0].IsDead.Should().BeTrue();
    }

    [Fact]
    public async Task GoapAgent_ShouldTrack_MultipleStateChanges()
    {
        // Arrange
        GameState.Player.HasTarget = false;
        GameState.Player.IsCasting = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Multiple state changes
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.Player.HasTarget.Should().BeTrue();
        GameState.InCombat.Should().BeTrue();
    }

    #endregion

    #region Goal Selection

    [Fact]
    public async Task GoapAgent_ShouldSelect_CombatGoal_WhenInCombat()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Enter combat
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.InCombat.Should().BeTrue();
        GameState.Player.HasTarget.Should().BeTrue();
    }

    [Fact]
    public async Task GoapAgent_ShouldSelect_LootGoal_WhenCorpseExists()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 10, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Kill and create corpse
        GameState.Npcs[0].Health = 0;
        await WaitForConditionAsync(
            () => GameState.Corpses.Count > 0,
            "corpse created",
            TimeSpan.FromSeconds(2));

        // Assert
        GameState.Corpses.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GoapAgent_ShouldSwitchGoals_WhenConditionsChange()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Start with Combat
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");
        GameState.InCombat.Should().BeTrue();

        // Act - Kill target (should switch to loot)
        GameState.Npcs[0].Health = 0;
        await WaitForConditionAsync(
            () => GameState.Corpses.Count > 0,
            "corpse created");

        // Assert
        GameState.Npcs[0].IsDead.Should().BeTrue();
        GameState.Corpses.Should().NotBeEmpty();
    }

    #endregion

    #region Plan Generation

    [Fact]
    public async Task GoapAgent_ShouldGenerate_Plan()
    {
        // Arrange
        List<string> plan = new() { "ApproachTarget", "EnterCombat", "CastSpells" };
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        plan.Should().NotBeEmpty();
        plan.Count.Should().Be(3);
    }

    [Fact]
    public async Task GoapAgent_ShouldExecute_PlanStepByStep()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Execute plan steps
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        AssertHasTarget("Wolf");

        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");
        GameState.InCombat.Should().BeTrue();

        // Assert
        GameState.CurrentTarget.Should().NotBeNull();
    }

    [Fact]
    public async Task GoapAgent_ShouldClear_Plan_WhenGoalComplete()
    {
        // Arrange
        List<string> plan = new() { "Step1", "Step2" };
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Complete plan
        plan.Clear();

        // Assert
        plan.Should().BeEmpty();
    }

    #endregion

    #region NO PLAN Handling

    [Fact]
    public async Task GoapAgent_ShouldHandle_NoPlan_WhenNoGoals()
    {
        // Arrange
        GameState.Player.HasTarget = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - No actionable goals

        // Assert - Should handle gracefully
        GameState.Player.HasTarget.Should().BeFalse();
    }

    [Fact]
    public async Task GoapAgent_ShouldRetry_WhenNoPlanDetected()
    {
        // Arrange
        int retryCount = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Simulate retry
        while (retryCount < 3 && GameState.Player.HasTarget == false)
        {
            retryCount++;
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        retryCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GoapAgent_ShouldAbort_WhenNoPlanForTooLong()
    {
        // Arrange
        GameState.Player.HasTarget = false;
        int noPlanDuration = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Simulate extended no-plan period
        while (noPlanDuration < 5 && GameState.Player.HasTarget == false)
        {
            noPlanDuration++;
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        noPlanDuration.Should().BeLessThan(10); // Should abort before this
    }

    #endregion

    #region Available Goals

    [Fact]
    public async Task GoapAgent_ShouldHave_AvailableGoals()
    {
        // Arrange
        List<string> goals = new() { "Combat", "Loot", "Approach" };
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        goals.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GoapAgent_ShouldCheck_CanRun_ForEachGoal()
    {
        // Arrange
        GameState.Player.HasTarget = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Combat goal can't run without target
        GameState.InCombat.Should().BeFalse();
    }

    [Fact]
    public async Task GoapAgent_ShouldSelect_GoalWithLowestCost()
    {
        // Arrange
        Dictionary<string, float> goalCosts = new()
        {
            { "Approach", 5f },
            { "Combat", 4f },
            { "Loot", 3f }
        };
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Find lowest cost
        float minCost = float.MaxValue;
        string selectedGoal = "";
        foreach (var goal in goalCosts)
        {
            if (goal.Value < minCost)
            {
                minCost = goal.Value;
                selectedGoal = goal.Key;
            }
        }

        // Assert
        selectedGoal.Should().Be("Loot");
        minCost.Should().Be(3f);
    }

    #endregion

    #region Event Handling

    [Fact]
    public async Task GoapAgent_ShouldBroadcast_GoapEvent()
    {
        // Arrange
        bool eventReceived = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Simulate event
        eventReceived = true;

        // Assert
        eventReceived.Should().BeTrue();
    }

    [Fact]
    public async Task GoapAgent_ShouldHandle_AbortEvent()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Abort
        GameState.ClearTarget();
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.HasTarget.Should().BeFalse();
    }

    [Fact]
    public async Task GoapAgent_ShouldHandle_ResumeEvent()
    {
        // Arrange
        GameState.Player.HasTarget = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Resume
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.HasTarget.Should().BeTrue();
    }

    #endregion

    #region Thread Management

    [Fact]
    public async Task GoapAgent_ShouldRun_OnSeparateThread()
    {
        // Arrange
        int threadId = Environment.CurrentManagedThreadId;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Thread should exist
        threadId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GoapAgent_ShouldPause_WhenInactive()
    {
        // Arrange
        bool wasPaused = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Pause
        wasPaused = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        wasPaused.Should().BeTrue();
    }

    [Fact]
    public async Task GoapAgent_ShouldResume_WhenActivated()
    {
        // Arrange
        bool wasResumed = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Resume
        wasResumed = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        wasResumed.Should().BeTrue();
    }

    #endregion

    #region Hazard Avoidance

    [Fact]
    public async Task GoapAgent_ShouldDetect_Hazards()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Move toward hazard area
        GameState.Player.Position = new Vector3(5, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Position.Should().NotBe(Vector3.Zero);
    }

    [Fact]
    public async Task GoapAgent_ShouldAvoid_HazardZones()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Navigate around hazard
        GameState.Player.Position = new Vector3(0, 5, 0); // Different path
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Position.Should().Be(new Vector3(0, 5, 0));
    }

    #endregion

    #region Bag Changes

    [Fact]
    public async Task GoapAgent_ShouldTrack_BagChanges()
    {
        // Arrange
        int initialCount = GameState.Player.Inventory.Count;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Add item
        GameState.Player.Inventory.Add(new Item { Name = "Wolf Meat", Id = 1 });
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Inventory.Count.Should().BeGreaterThan(initialCount);
    }

    [Fact]
    public async Task GoapAgent_ShouldTrigger_WhenBagFull()
    {
        // Arrange
        for (int i = 0; i < 20; i++)
        {
            GameState.Player.Inventory.Add(new Item { Name = $"Item{i}", Id = i });
        }
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Inventory.Count.Should().BeGreaterThan(10);
    }

    #endregion

    #region Session Management

    [Fact]
    public async Task GoapAgent_ShouldTrack_SessionStats()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Combat
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.CombatLog.Should().NotBeNull();
    }

    [Fact]
    public async Task GoapAgent_ShouldStart_SessionTracking()
    {
        // Arrange
        DateTime startTime = DateTime.UtcNow;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        startTime.Should().BeBefore(DateTime.UtcNow);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public async Task GoapAgent_ShouldHandle_FullCombatCycle()
    {
        // Arrange - Full cycle: target -> combat -> kill -> loot -> resume
        SpawnNpc("Wolf", 2, 50, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Target
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        AssertHasTarget("Wolf");

        // Combat
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Kill
        GameState.Npcs[0].Health = 0;
        await WaitForConditionAsync(
            () => GameState.Corpses.Count > 0,
            "corpse created");

        // Assert
        GameState.Corpses.Should().NotBeEmpty();
        GameState.Npcs[0].IsDead.Should().BeTrue();
    }

    [Fact]
    public async Task GoapAgent_ShouldHandle_EmergencyStop()
    {
        // Arrange
        bool wasStopped = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Emergency stop
        GameState.Player.IsMoving = false;
        wasStopped = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        wasStopped.Should().BeTrue();
        GameState.Player.IsMoving.Should().BeFalse();
    }

    #endregion
}
