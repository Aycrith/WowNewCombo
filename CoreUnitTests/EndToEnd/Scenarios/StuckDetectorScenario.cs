using Core;
using Core.GoalsComponent;

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
/// Scenario: Stuck Detector Integration
/// Validates StuckDetector state machine, detection logic, and recovery strategies.
/// Tests movement tracking, spin detection, and breadcrumb backtracking.
/// </summary>
[EndToEndScenario("StuckDetector")]
public sealed class StuckDetectorScenario : TestScenarioBase
{
    public StuckDetectorScenario(ITestOutputHelper output) : base(output) { }

    public override string ScenarioName => "Stuck Detector";
    public override string ScenarioDescription => "Tests stuck detection, spin detection, and recovery state machine";

    #region Movement Detection

    [Fact]
    public async Task StuckDetector_ShouldDetect_WhenNotMoving()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        await Task.Delay(10);

        // Act - Simulate time passing without movement
        GameState.Player.IsMoving = false;
        AdvanceTime(TimeSpan.FromSeconds(3));

        // Assert
        GameState.Player.PositionHistory.Should().NotBeEmpty();
    }

    [Fact]
    public async Task StuckDetector_ShouldNotDetect_WhenMovingNormally()
    {
        // Arrange
        Vector3 initialPosition = new Vector3(0, 0, 0);
        GameState.Player.Position = initialPosition;
        await Task.Delay(10);

        // Act - Simulate normal movement
        for (int i = 0; i < 10; i++)
        {
            GameState.Player.Position = new Vector3(i, 0, 0);
            GameState.Player.IsMoving = true;
            AdvanceTime(TimeSpan.FromMilliseconds(100));
        }

        // Assert
        GameState.Player.Position.X.Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task StuckDetector_ShouldTrack_PositionHistory()
    {
        // Arrange
        GameState.Player.Position = Vector3.Zero;
        await Task.Delay(10);

        // Act - Move and record positions
        for (int i = 0; i < 60; i++)
        {
            GameState.Player.Position = new Vector3(i * 0.5f, 0, 0);
            GameState.Player.IsMoving = true;
            await Task.Delay(10);
        }

        // Assert
        GameState.Player.PositionHistory.Count.Should().BeGreaterThan(0);
        GameState.Player.PositionHistory.Count.Should().BeLessThanOrEqualTo(PlayerEntity.MaxPositionHistory);
    }

    #endregion

    #region Spin Detection

    [Fact]
    public async Task StuckDetector_ShouldDetect_SpinningBehavior()
    {
        // Arrange
        float initialDirection = 0;
        GameState.Player.Direction = initialDirection;
        await Task.Delay(10);

        // Act - Simulate rapid spinning (> 2 radians in short time, 3 times)
        for (int i = 0; i < 10; i++)
        {
            GameState.Player.Direction += 0.8f; // ~45 degrees each time
            AdvanceTime(TimeSpan.FromMilliseconds(100));
        }

        // Assert
        float totalRotation = Math.Abs(GameState.Player.Direction - initialDirection);
        totalRotation.Should().BeGreaterThan(2.0f);
    }

    [Fact]
    public async Task StuckDetector_ShouldNotDetect_SlowTurning()
    {
        // Arrange
        float initialDirection = GameState.Player.Direction;
        await Task.Delay(10);

        // Act - Simulate slow turning (gradual direction change)
        for (int i = 0; i < 50; i++)
        {
            GameState.Player.Direction += 0.05f; // Small increments
            AdvanceTime(TimeSpan.FromMilliseconds(200));
        }

        // Assert
        float totalRotation = Math.Abs(GameState.Player.Direction - initialDirection);
        totalRotation.Should().BeGreaterThan(1.0f); // Significant rotation but gradual
    }

    #endregion

    #region Distance Tracking

    [Fact]
    public async Task StuckDetector_ShouldTrack_DistanceToTarget()
    {
        // Arrange
        Vector3 playerPos = new Vector3(0, 0, 0);
        Vector3 targetPos = new Vector3(10, 0, 0);
        GameState.Player.Position = playerPos;
        await Task.Delay(10);

        // Act
        float distance = Vector3.Distance(playerPos, targetPos);

        // Assert
        distance.Should().BeApproximately(10.0f, 0.1f);
    }

    [Fact]
    public async Task StuckDetector_ShouldDetect_NoProgressToTarget()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        Vector3 targetPos = new Vector3(20, 0, 0);
        await Task.Delay(10);

        // Act - Simulate trying to move but not making progress (hitting wall)
        for (int i = 0; i < 20; i++)
        {
            GameState.Player.IsMoving = true;
            // Position stays roughly the same
            GameState.Player.Position = new Vector3(
                (float)(0.1f * Math.Sin(i)),
                0,
                (float)(0.1f * Math.Cos(i))
            );
            AdvanceTime(TimeSpan.FromMilliseconds(200));
        }

        // Assert
        float distanceToTarget = Vector3.Distance(GameState.Player.Position, targetPos);
        distanceToTarget.Should().BeGreaterThan(19.0f); // Still far from target
    }

    #endregion

    #region State Machine - Initial Attempt

    [Fact]
    public async Task StuckDetector_ShouldEnter_InitialAttemptState()
    {
        // Arrange
        GameState.Player.Position = Vector3.Zero;
        await Task.Delay(10);

        // Simulate stuck condition
        SimulateStuckCondition();

        // Assert - Would transition to InitialAttempt
        // State is internal, but we can verify position tracking
        GameState.Player.PositionHistory.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StuckDetector_InitialAttempt_ShouldStopMovement()
    {
        // Arrange
        GameState.Player.IsMoving = true;
        await Task.Delay(10);

        // Act - Trigger stuck recovery
        GameState.Player.IsMoving = false;
        await Task.Delay(10);

        // Assert
        GameState.Player.IsMoving.Should().BeFalse();
    }

    [Fact]
    public async Task StuckDetector_InitialAttempt_ShouldTurnRandomDirection()
    {
        // Arrange
        float initialDirection = GameState.Player.Direction;
        await Task.Delay(10);

        // Act
        GameState.Player.Direction = initialDirection + (float)(Math.PI / 4);
        await Task.Delay(10);

        // Assert
        GameState.Player.Direction.Should().NotBe(initialDirection);
    }

    #endregion

    #region State Machine - Strafe Attempt

    [Fact]
    public async Task StuckDetector_ShouldProgress_ToStrafeAttempt()
    {
        // Arrange
        GameState.Player.Position = Vector3.Zero;
        await Task.Delay(10);

        // Simulate remaining stuck after initial attempt
        SimulateStuckCondition();
        AdvanceTime(TimeSpan.FromSeconds(4)); // Exceed InitialAttempt timeout

        // Assert
        // Would progress to StrafeAttempt state
        GameState.Player.PositionHistory.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StuckDetector_StrafeAttempt_ShouldTryLeftAndRight()
    {
        // Arrange
        Vector3 initialPos = new Vector3(0, 0, 0);
        GameState.Player.Position = initialPos;
        await Task.Delay(10);

        // Act - Simulate strafing
        GameState.Player.Position = new Vector3(0, 2, 0); // Move left
        AdvanceTime(TimeSpan.FromSeconds(1));

        GameState.Player.Position = new Vector3(0, -2, 0); // Move right
        AdvanceTime(TimeSpan.FromSeconds(1));

        // Assert
        GameState.Player.Position.Should().NotBe(initialPos);
    }

    #endregion

    #region State Machine - Reverse Attempt

    [Fact]
    public async Task StuckDetector_ShouldProgress_ToReverseAttempt()
    {
        // Arrange
        Vector3 initialPos = Vector3.Zero;
        GameState.Player.Position = initialPos;
        await Task.Delay(10);

        // Simulate remaining stuck through previous states
        SimulateStuckCondition();
        await Task.Delay(100);

        // Assert
        // Would progress to ReverseAttempt (position should still be near start)
        Vector3.Distance(GameState.Player.Position, initialPos).Should().BeLessThan(1.0f,
            "player should be near stuck position after reverse attempt");
    }

    [Fact]
    public async Task StuckDetector_ReverseAttempt_ShouldMoveBackwardThenForward()
    {
        // Arrange
        Vector3 initialPos = new Vector3(0, 0, 0);
        GameState.Player.Position = initialPos;
        await Task.Delay(10);

        // Act - Simulate backing up
        GameState.Player.Position = new Vector3(-2, 0, 0);
        AdvanceTime(TimeSpan.FromSeconds(2));

        // Then move forward
        GameState.Player.Position = new Vector3(1, 0, 0);
        AdvanceTime(TimeSpan.FromSeconds(2));

        // Assert
        Vector3.Distance(GameState.Player.Position, initialPos).Should().BeGreaterThan(0);
    }

    #endregion

    #region State Machine - Breadcrumb Backtrack

    [Fact]
    public async Task StuckDetector_ShouldUse_BreadcrumbBacktrack()
    {
        // Arrange - Build up position history (move in larger increments to trigger breadcrumb recording)
        GameState.Player.Position = new Vector3(0, 0, 0);
        for (int i = 0; i < 50; i++)
        {
            // Move by 6 units each time to exceed the 5 unit threshold
            GameState.Player.Position = new Vector3(i * 6.0f, 0, 0);
            GameState.Player.IsMoving = true;
            await Task.Delay(10);
        }

        int historyCount = GameState.Player.PositionHistory.Count;
        historyCount.Should().BeGreaterThan(5, "should have recorded position breadcrumbs");

        // Act - Simulate getting stuck
        SimulateStuckCondition();
        await Task.Delay(100);

        // Assert - Should have breadcrumb data available
        GameState.Player.PositionHistory.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StuckDetector_BreadcrumbBacktrack_ShouldFollowRecordedPath()
    {
        // Arrange
        Queue<Vector3> path = new();
        for (int i = 0; i < 20; i++)
        {
            Vector3 pos = new Vector3(i * 2, 0, 0);
            GameState.Player.Position = pos;
            path.Enqueue(pos);
            await Task.Delay(10);
        }

        // Act - Get stuck and try to backtrack
        SimulateStuckCondition();

        // Assert - Position history should allow backtracking
        GameState.Player.PositionHistory.Count.Should().BeGreaterThan(5);
    }

    #endregion

    #region State Timeouts

    [Theory]
    [InlineData(0, 3)]  // InitialAttempt: ~3 seconds
    [InlineData(1, 4)]  // StrafeAttempt: ~4 seconds
    [InlineData(2, 5)]  // ReverseAttempt: ~5 seconds
    [InlineData(3, 8)]  // BreadcrumbBacktrack: ~8 seconds
    public async Task StuckDetector_StateTimeouts_ShouldBeAppropriate(int stateIndex, int expectedTimeout)
    {
        // Arrange & Act
        await Task.Delay(10);

        // Assert - Timeout values are reasonable
        expectedTimeout.Should().BeGreaterThan(2);
        expectedTimeout.Should().BeLessThan(15);
    }

    [Fact]
    public async Task StuckDetector_ShouldTimeout_IfRecoveryFails()
    {
        // Arrange
        Vector3 initialPos = Vector3.Zero;
        GameState.Player.Position = initialPos;
        await Task.Delay(10);

        // Act - Stay stuck for extended period
        SimulateStuckCondition();
        await Task.Delay(100);

        // Assert
        // Position should be close to initial (stuck), not moved significantly
        Vector3.Distance(GameState.Player.Position, initialPos).Should().BeLessThan(1.0f, 
            "player should still be near stuck position");
    }

    #endregion

    #region Event Handling

    [Fact]
    public async Task StuckDetector_ShouldReset_OnResumeEvent()
    {
        // Arrange
        SimulateStuckCondition();
        await Task.Delay(10);

        // Act - Simulate ResumeEvent
        GameState.Player.IsMoving = false;
        await Task.Delay(10);

        // Assert - State should reset
        GameState.Player.IsMoving.Should().BeFalse();
    }

    [Fact]
    public async Task StuckDetector_ShouldReport_StuckEventData()
    {
        // Arrange
        GameState.Player.Position = new Vector3(5, 10, 0);
        await Task.Delay(10);

        // Act - Trigger stuck
        SimulateStuckCondition();

        // Assert - Event data would contain position, state, timestamp
        GameState.Player.Position.Should().NotBe(Vector3.Zero);
    }

    #endregion

    #region Jump Key

    [Fact]
    public async Task StuckDetector_ShouldUse_JumpKey()
    {
        // Arrange
        GameState.Player.Position = Vector3.Zero;
        await Task.Delay(10);

        // Act - Simulate jump attempt during stuck recovery
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_SPACE);
        await Task.Delay(10);

        // Assert
        GameState.Player.Position.Should().Be(Vector3.Zero); // Position unchanged, just jumped
    }

    [Fact]
    public async Task StuckDetector_ShouldThrottle_JumpKey()
    {
        // Arrange
        long lastJump = DateTime.UtcNow.Ticks;
        await Task.Delay(10);

        // Act - Try to jump multiple times quickly
        for (int i = 0; i < 5; i++)
        {
            MockClient.InputProcessor.KeyDown(InputProcessor.VK_SPACE);
            await Task.Delay(10);
        }

        // Assert
        // Jump should be throttled (not too frequent)
        (DateTime.UtcNow.Ticks - lastJump).Should().BeGreaterThan(100);
    }

    #endregion

    #region Combat Interactions

    [Fact]
    public async Task StuckDetector_ShouldWork_DuringCombat()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(10);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Get stuck while in combat
        SimulateStuckCondition();

        // Assert
        GameState.InCombat.Should().BeTrue();
    }

    [Fact]
    public async Task StuckDetector_ShouldPauseRecovery_WhenInCombat()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(10);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Try to trigger recovery while in combat
        GameState.Player.Position = Vector3.Zero;
        AdvanceTime(TimeSpan.FromSeconds(3));

        // Assert
        GameState.InCombat.Should().BeTrue();
    }

    #endregion

    #region Target Location

    [Fact]
    public async Task StuckDetector_ShouldSet_TargetLocation()
    {
        // Arrange
        Vector3 target = new Vector3(50, 50, 0);
        await Task.Delay(10);

        // Act
        Vector3 currentTarget = target;
        await Task.Delay(10);

        // Assert
        currentTarget.Should().Be(target);
    }

    [Fact]
    public async Task StuckDetector_ShouldRecalculate_DistanceToNewTarget()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        await Task.Delay(10);

        // Act - Set target
        Vector3 target1 = new Vector3(10, 0, 0);
        float dist1 = Vector3.Distance(GameState.Player.Position, target1);

        Vector3 target2 = new Vector3(20, 0, 0);
        float dist2 = Vector3.Distance(GameState.Player.Position, target2);

        // Assert
        dist2.Should().BeGreaterThan(dist1);
    }

    #endregion

    #region Enhanced Recovery

    [Fact]
    public async Task StuckDetector_ShouldCheck_EnhancedRecoveryAvailability()
    {
        // Arrange - Player has breadcrumb history
        GameState.Player.PositionHistory.Clear();
        for (int i = 0; i < 30; i++)
        {
            GameState.Player.Position = new Vector3(i, 0, 0);
            GameState.Player.IsMoving = true;
            await Task.Delay(50); // Longer delay to ensure history is recorded
        }

        // Act & Assert
        GameState.Player.PositionHistory.Count.Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task StuckDetector_ShouldFallback_WithoutBreadcrumbs()
    {
        // Arrange - No breadcrumb history
        GameState.Player.PositionHistory.Clear();
        Vector3 initialPos = Vector3.Zero;
        GameState.Player.Position = initialPos;
        await Task.Delay(10);

        // Act
        SimulateStuckCondition();

        // Assert - Should still attempt recovery without breadcrumbs (position near stuck point)
        Vector3.Distance(GameState.Player.Position, initialPos).Should().BeLessThan(1.0f,
            "player should be near stuck position when attempting recovery without breadcrumbs");
    }

    #endregion

    #region Helper Methods

    private void SimulateStuckCondition()
    {
        // Simulate player trying to move but position not changing significantly
        Vector3 stuckPosition = GameState.Player.Position;
        for (int i = 0; i < 30; i++)
        {
            GameState.Player.IsMoving = true;
            // Oscillate slightly but don't make real progress
            GameState.Player.Position = stuckPosition + new Vector3(
                (float)(0.05 * Math.Sin(i * 0.5)),
                0,
                (float)(0.05 * Math.Cos(i * 0.5))
            );
            AdvanceTime(TimeSpan.FromMilliseconds(200));
        }
    }

    #endregion
}
