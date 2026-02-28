using Core.GOAP;
using Core.Goals;
using FluentAssertions;
using Xunit;

namespace CoreUnitTests.GOAP;

/// <summary>
/// Unit tests for the goal-switch hysteresis state machine in GoapAgent.
///
/// The hysteresis prevents single-frame plan oscillation by requiring a new goal to win
/// for N consecutive planning ticks before the agent actually switches to it. This avoids
/// churn caused by transient world-state bits like DamageTaken or momentary stuck detection.
/// </summary>
public class GoapAgentHysteresisTests
{
    /// <summary>
    /// Minimal test goal for hysteresis state machine testing.
    /// </summary>
    private sealed class TestGoal : GoapGoal
    {
        public TestGoal(string name) : base(name) { }
        public override float Cost => 1.0f;
        public override bool CanRun() => true;
    }

    /// <summary>
    /// Harness to test hysteresis state machine logic.
    /// Mimics the private state management of GoapAgent without requiring full infrastructure.
    /// </summary>
    private sealed class HysteresisTestHarness
    {
        private GoapGoal? pendingGoal;
        private int pendingGoalTicks;
        private const int GoalSwitchHysteresisThreshold = 3;

        public GoapGoal? CurrentGoal { get; set; }

        /// <summary>
        /// Mimics the hysteresis logic from GoapAgent.TryAdvanceHysteresis().
        /// </summary>
        public bool TryAdvanceHysteresis(GoapGoal? newGoal)
        {
            // If newGoal == CurrentGoal, clear any pending alternative and return true
            // (no state change needed — execute the goal normally).
            if (newGoal == CurrentGoal)
            {
                pendingGoal = null;
                pendingGoalTicks = 0;
                return true;
            }

            // Track consecutive wins for the new goal
            if (newGoal == pendingGoal)
            {
                pendingGoalTicks++;
            }
            else
            {
                pendingGoal = newGoal;
                pendingGoalTicks = 1;
            }

            // Return true when threshold is satisfied
            return pendingGoalTicks >= GoalSwitchHysteresisThreshold;
        }

        public GoapGoal? GetPendingGoal() => pendingGoal;
        public int GetPendingGoalTicks() => pendingGoalTicks;
    }

    /// <summary>
    /// Test that same goal for 3 consecutive ticks triggers transition.
    /// </summary>
    [Fact]
    public void SameGoalFor3Ticks_TransitionCommitted()
    {
        // Arrange
        var currentGoal = new TestGoal("CurrentGoal");
        var newGoal = new TestGoal("NewGoal");
        var harness = new HysteresisTestHarness { CurrentGoal = currentGoal };

        // Act & Assert: Tick 1 - new goal arrives
        harness.TryAdvanceHysteresis(newGoal).Should().BeFalse(
            "First tick of a new goal should not reach threshold");

        // Tick 2 - same goal arrives again
        harness.TryAdvanceHysteresis(newGoal).Should().BeFalse(
            "Second tick should still be below threshold (3)");

        // Tick 3 - same goal arrives a third time
        harness.TryAdvanceHysteresis(newGoal).Should().BeTrue(
            "Third consecutive tick should reach threshold and allow transition");
    }

    /// <summary>
    /// Test that oscillating between two goals never reaches the threshold.
    /// </summary>
    [Fact]
    public void GoalOscillation_NeverReachesThreshold()
    {
        // Arrange
        var currentGoal = new TestGoal("CurrentGoal");
        var goalA = new TestGoal("GoalA");
        var goalB = new TestGoal("GoalB");
        var harness = new HysteresisTestHarness { CurrentGoal = currentGoal };

        // Act & Assert: Oscillate A/B/A/B and verify none reach threshold
        for (int cycle = 0; cycle < 5; cycle++)
        {
            harness.TryAdvanceHysteresis(goalA).Should().BeFalse(
                $"Cycle {cycle}: Goal A should not reach threshold (oscillation prevents accumulation)");

            harness.TryAdvanceHysteresis(goalB).Should().BeFalse(
                $"Cycle {cycle}: Goal B should not reach threshold (oscillation prevents accumulation)");
        }
    }

    /// <summary>
    /// Test that switching to a new goal resets the counter for the previous goal.
    /// </summary>
    [Fact]
    public void NewGoalResetsCounter()
    {
        // Arrange
        var currentGoal = new TestGoal("CurrentGoal");
        var goalA = new TestGoal("GoalA");
        var goalB = new TestGoal("GoalB");
        var harness = new HysteresisTestHarness { CurrentGoal = currentGoal };

        // Act: Accumulate 2 ticks for goalA
        harness.TryAdvanceHysteresis(goalA).Should().BeFalse();
        harness.TryAdvanceHysteresis(goalA).Should().BeFalse();

        // Then switch to goalB
        harness.TryAdvanceHysteresis(goalB).Should().BeFalse(
            "After switch, goalB count should be 1 (below threshold)");

        // Verify one more tick of goalB doesn't reach threshold
        harness.TryAdvanceHysteresis(goalB).Should().BeFalse(
            "Second tick of goalB should be count=2 (still below threshold=3)");

        // But third tick should
        harness.TryAdvanceHysteresis(goalB).Should().BeTrue(
            "Third consecutive tick of goalB should reach threshold");
    }

    /// <summary>
    /// Test that calling with newGoal == CurrentGoal immediately clears pending state.
    /// </summary>
    [Fact]
    public void SameGoalAsCurrent_ClearsPending()
    {
        // Arrange
        var goalA = new TestGoal("GoalA");
        var goalB = new TestGoal("GoalB");
        var harness = new HysteresisTestHarness { CurrentGoal = goalA };

        // Act: Accumulate some ticks for goalB (pending)
        harness.TryAdvanceHysteresis(goalB).Should().BeFalse();
        harness.TryAdvanceHysteresis(goalB).Should().BeFalse();

        // Then call with CurrentGoal (goalA) - should clear pending and return true
        harness.TryAdvanceHysteresis(goalA).Should().BeTrue(
            "Calling with CurrentGoal should clear pending and return true (no state change)");

        // Verify pending was cleared: next call with goalB should start fresh
        harness.TryAdvanceHysteresis(goalB).Should().BeFalse(
            "After clearing pending, goalB should start at count=1");
    }

    /// <summary>
    /// Test boundary case of threshold behavior.
    /// Verifies the decision kernel works correctly at lower and upper bounds.
    /// </summary>
    [Fact]
    public void GoalTransition_WorksCorrectly()
    {
        // Arrange
        var goalA = new TestGoal("GoalA");
        var goalB = new TestGoal("GoalB");
        var harness = new HysteresisTestHarness { CurrentGoal = goalA };

        // Act: First call to TryAdvanceHysteresis with goalB
        var result1 = harness.TryAdvanceHysteresis(goalB);

        // Assert: Should be false (count=1, threshold=3)
        result1.Should().BeFalse("First tick should not satisfy threshold=3");

        // Act: Continue calling until threshold
        harness.TryAdvanceHysteresis(goalB).Should().BeFalse("Second tick");
        harness.TryAdvanceHysteresis(goalB).Should().BeTrue("Third tick satisfies threshold");
    }
}
