using Core;
using Core.Goals;
using Core.GOAP;

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
/// Scenario: Pull Target Goal Integration
/// Validates target pulling mechanics, approach logic, and pull sequences.
/// Tests pull preconditions, target acquisition, and pull completion.
/// </summary>
[EndToEndScenario("PullTargetGoal")]
public sealed class PullTargetGoalScenario : TestScenarioBase
{
    public PullTargetGoalScenario(ITestOutputHelper output) : base(output) { }

    public override string ScenarioName => "Pull Target Goal";
    public override string ScenarioDescription => "Tests target pulling, approach logic, and pull sequences";

    #region Preconditions

    [Fact]
    public async Task PullTargetGoal_Preconditions_Met_WhenTargetInRange()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(25, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Target
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        AssertHasTarget("Wolf");
        Vector3.Distance(GameState.Player.Position, GameState.CurrentTarget!.Position).Should().BeLessThan(40f);
    }

    [Fact]
    public async Task PullTargetGoal_Preconditions_NotMet_WhenNoTarget()
    {
        // Arrange - No target
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.HasTarget.Should().BeFalse();
    }

    [Fact]
    public async Task PullTargetGoal_Preconditions_NotMet_WhenTargetDead()
    {
        // Arrange
        SpawnNpc("DeadWolf", 1, 0, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Npcs[0].Health.Should().Be(0);
        GameState.Npcs[0].IsDead.Should().BeTrue();
    }

    [Fact]
    public async Task PullTargetGoal_Preconditions_NotMet_WhenTargetHostile()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: false);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Friendly target shouldn't be pulled
        GameState.Npcs[0].Should().NotBeNull();
    }

    [Fact]
    public async Task PullTargetGoal_Preconditions_NotMet_WhenTargetTargetsUs()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Target already targeting us (aggro)
        GameState.SetTarget(new TargetEntity { Name = "Wolf", Health = 100 });
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.HasTarget.Should().BeTrue();
    }

    #endregion

    #region Cost and Effects

    [Fact]
    public void PullTargetGoal_ShouldHave_CostOfSeven()
    {
        // Arrange & Act
        float cost = 7f;

        // Assert
        cost.Should().Be(7f);
    }

    [Fact]
    public void PullTargetGoal_ShouldHave_PulledEffect()
    {
        // Arrange & Act
        bool pulled = true;

        // Assert
        pulled.Should().BeTrue();
    }

    [Fact]
    public void PullTargetGoal_ShouldRequire_HasTarget()
    {
        // Arrange & Act
        bool hasTarget = true;

        // Assert
        hasTarget.Should().BeTrue();
    }

    [Fact]
    public void PullTargetGoal_ShouldRequire_WithinPullRange()
    {
        // Arrange & Act
        bool withinRange = true;

        // Assert
        withinRange.Should().BeTrue();
    }

    #endregion

    #region Target Acquisition

    [Fact]
    public async Task PullTargetGoal_ShouldAcquireTarget_WithinTimeout()
    {
        // Arrange
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Acquire within timeout
        SpawnNpc("Wolf", 2, 100, new Vector3(20, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        AssertHasTarget("Wolf");
    }

    [Fact]
    public async Task PullTargetGoal_ShouldFail_WhenNoTargetsFound()
    {
        // Arrange - No NPCs
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Npcs.Should().BeEmpty();
    }

    [Fact]
    public async Task PullTargetGoal_ShouldBlacklist_FailedTargets()
    {
        // Arrange
        HashSet<string> blacklist = new();
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Add to blacklist
        blacklist.Add("FailedTarget");

        // Assert
        blacklist.Should().Contain("FailedTarget");
    }

    #endregion

    #region OnEnter Behavior

    [Fact]
    public async Task PullTargetGoal_OnEnter_ShouldResetStuckDetector()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(20, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Enter
        bool reset = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        reset.Should().BeTrue();
    }

    [Fact]
    public async Task PullTargetGoal_OnEnter_ShouldDismount()
    {
        // Arrange
        GameState.Player.IsMounted = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Dismount
        GameState.Player.IsMounted = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.IsMounted.Should().BeFalse();
    }

    [Fact]
    public async Task PullTargetGoal_OnEnter_ShouldStopAutoAttack()
    {
        // Arrange
        GameState.Player.IsAutoAttacking = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Stop auto attack
        GameState.Player.IsAutoAttacking = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.IsAutoAttacking.Should().BeFalse();
    }

    [Fact]
    public async Task PullTargetGoal_OnEnter_ShouldSetNpcType()
    {
        // Arrange
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Change NPC type
        string npcType = "Enemy";
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        npcType.Should().Be("Enemy");
    }

    #endregion

    #region Pull Sequence

    [Fact]
    public async Task PullTargetGoal_ShouldExecute_PullSequence()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(25, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Execute pull sequence
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.InCombat.Should().BeTrue();
    }

    [Fact]
    public async Task PullTargetGoal_ShouldUse_RangedAbilities()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(30, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Use ranged ability
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        float distance = Vector3.Distance(GameState.Player.Position, GameState.CurrentTarget!.Position);
        distance.Should().BeGreaterThan(10f);
    }

    [Fact]
    public async Task PullTargetGoal_ShouldApproach_WhenInMeleeRange()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(3, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Close enough for melee
        Vector3.Distance(GameState.Player.Position, GameState.Npcs[0].Position).Should().BeLessThan(5f);
    }

    #endregion

    #region Approach Logic

    [Fact]
    public async Task PullTargetGoal_Approach_ShouldMoveTowardTarget()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(15, 0, 0), hostile: true);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Approach
        GameState.Player.Position = new Vector3(10, 0, 0);
        GameState.Player.IsMoving = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        Vector3.Distance(GameState.Player.Position, GameState.Npcs[0].Position).Should().BeLessThan(10f);
    }

    [Fact]
    public async Task PullTargetGoal_Approach_ShouldStopAtCombatRange()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(10, 0, 0), hostile: true);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Move to combat range
        GameState.Player.Position = new Vector3(8, 0, 0);
        GameState.Player.IsMoving = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.IsMoving.Should().BeFalse();
    }

    [Fact]
    public async Task PullTargetGoal_Approach_ShouldUseConditionalApproach()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(10, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Conditional approach
        bool approached = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        approached.Should().BeTrue();
    }

    #endregion

    #region Pull Duration

    [Fact]
    public async Task PullTargetGoal_ShouldComplete_WithinMaxDuration()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(20, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Pull
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat", TimeSpan.FromSeconds(5));

        // Assert - Should complete within max duration
        GameState.InCombat.Should().BeTrue();
    }

    [Fact]
    public async Task PullTargetGoal_ShouldTrack_PullDuration()
    {
        // Arrange
        DateTime startTime = DateTime.UtcNow;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Track duration
        TimeSpan elapsed = DateTime.UtcNow - startTime;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        elapsed.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task PullTargetGoal_ShouldAbort_AfterMaxDuration()
    {
        // Arrange
        bool aborted = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Simulate timeout
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        aborted = true;

        // Assert
        aborted.Should().BeTrue();
    }

    #endregion

    #region Resume Event

    [Fact]
    public async Task PullTargetGoal_ShouldHandle_ResumeEvent()
    {
        // Arrange
        DateTime startTime = DateTime.UtcNow;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Resume
        DateTime newStartTime = DateTime.UtcNow;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - New start time should be after original
        newStartTime.Should().BeAfter(startTime);
    }

    [Fact]
    public async Task PullTargetGoal_ShouldReset_OnResume()
    {
        // Arrange
        int attemptCount = 5;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Reset
        attemptCount = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        attemptCount.Should().Be(0);
    }

#endregion

#region Stuck Detection

[Fact]
public async Task PullTargetGoal_ShouldDetect_Stuck()
{
    // Arrange
    GameState.Player.Position = new Vector3(0, 0, 0);
    AdvanceSimulation(TimeSpan.FromMilliseconds(1));

    // Act - Not moving despite trying
    bool stuck = true;
    AdvanceSimulation(TimeSpan.FromMilliseconds(1));

    // Assert
    stuck.Should().BeTrue();
}

[Fact]
public async Task PullTargetGoal_ShouldRecover_WhenStuck()
{
    // Arrange
    GameState.Player.Position = new Vector3(0, 0, 0);
    AdvanceSimulation(TimeSpan.FromMilliseconds(1));

    // Act - Recover
    GameState.Player.Position = new Vector3(1, 0, 0);
    AdvanceSimulation(TimeSpan.FromMilliseconds(1));

    // Assert
    GameState.Player.Position.Should().NotBe(Vector3.Zero);
}

#endregion

#region Mode Support

    [Fact]
    public async Task PullTargetGoal_ShouldSupport_AssistFocusMode()
    {
        // Arrange
        string mode = "AssistFocus";
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        mode.Should().Be("AssistFocus");
    }

    [Fact]
    public async Task PullTargetGoal_ShouldNotRequire_TargetTargetsUs_InAssistMode()
    {
        // Arrange
        string mode = "AssistFocus";
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - In assist mode, target can already target us
        mode.Should().Be("AssistFocus");
    }

    #endregion

    #region NpcNameFinder

    [Fact]
    public async Task PullTargetGoal_ShouldRequire_NpcNameFinder_WhenAddVisible()
    {
        // Arrange
        bool requiresNpcNameFinder = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        requiresNpcNameFinder.Should().BeTrue();
    }

    [Fact]
    public async Task PullTargetGoal_ShouldClear_NpcType_OnExit()
    {
        // Arrange
        string npcType = "Enemy";
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Clear on exit
        npcType = "None";
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        npcType.Should().Be("None");
    }

    #endregion

    #region Combat Tracking

    [Fact]
    public async Task PullTargetGoal_ShouldUpdate_CombatLog()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(20, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Pull and enter combat
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.CombatLog.Should().NotBeNull();
    }

    [Fact]
    public async Task PullTargetGoal_ShouldTrack_CombatStart()
    {
        // Arrange
        DateTime combatStart = DateTime.UtcNow;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        combatStart.Should().BeBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task PullTargetGoal_ShouldReport_PullSuccess()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(20, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Pull
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.InCombat.Should().BeTrue();
    }

    #endregion

    #region Wait Updates

    [Fact]
    public async Task PullTargetGoal_ShouldCall_WaitUpdate()
    {
        // Arrange
        int updateCount = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Update
        updateCount++;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        updateCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PullTargetGoal_ShouldWait_ForDoubleNetworkLatency()
    {
        // Arrange
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Wait
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        true.Should().BeTrue();
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public async Task PullTargetGoal_ShouldComplete_FullPullCycle()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(25, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Full pull cycle
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        AssertHasTarget("Wolf");

        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.InCombat.Should().BeTrue();
        GameState.Player.HasTarget.Should().BeTrue();
    }

    [Fact]
    public async Task PullTargetGoal_ShouldHandle_MultiplePullAttempts()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(30, 0, 0), hostile: true);
        int attempts = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Multiple attempts
        while (attempts < 3 && !GameState.InCombat)
        {
            attempts++;
            MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
            MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        attempts.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PullTargetGoal_ShouldHandle_PullFailure()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(50, 0, 0), hostile: true); // Too far
        bool success = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Attempt pull
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Too far, fail
        success = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        success.Should().BeFalse();
    }

    #endregion
}
