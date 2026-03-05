using Core.GOAP;
using Core.Goals;

using FluentAssertions;

using System;

using Xunit;

namespace CoreUnitTests.GOAP;

/// <summary>
/// Unit tests for GOAP anti-oscillation guardrails:
/// goal-switch hysteresis and pull-range hysteresis latch behavior.
/// </summary>
public sealed class GoapAgentHysteresisTests
{
    private sealed class TestGoal : GoapGoal
    {
        public TestGoal(string name) : base(name) { }

        public override float Cost => 1.0f;

        public override bool CanRun() => true;
    }

    private sealed class HysteresisTestHarness
    {
        private const int GoalSwitchHysteresisThreshold = 3;

        private GoapGoal? pendingGoal;
        private int pendingGoalTicks;
        private DateTime pullRangeHysteresisUntilUtc = DateTime.MinValue;

        public GoapGoal? CurrentGoal { get; set; }

        public bool TryAdvanceHysteresis(GoapGoal? newGoal)
        {
            if (newGoal == CurrentGoal)
            {
                pendingGoal = null;
                pendingGoalTicks = 0;
                return true;
            }

            if (newGoal == pendingGoal)
            {
                pendingGoalTicks++;
            }
            else
            {
                pendingGoal = newGoal;
                pendingGoalTicks = 1;
            }

            return pendingGoalTicks >= GoalSwitchHysteresisThreshold;
        }

        public bool WithInPullRangeHysteresis(bool nowInRange, DateTime nowUtc)
        {
            if (nowInRange)
            {
                pullRangeHysteresisUntilUtc = nowUtc.AddMilliseconds(500);
                return true;
            }

            return nowUtc < pullRangeHysteresisUntilUtc;
        }
    }

    [Fact]
    public void SameGoalFor3Ticks_TransitionCommitted()
    {
        TestGoal currentGoal = new("CurrentGoal");
        TestGoal newGoal = new("NewGoal");
        HysteresisTestHarness harness = new() { CurrentGoal = currentGoal };

        harness.TryAdvanceHysteresis(newGoal).Should().BeFalse();
        harness.TryAdvanceHysteresis(newGoal).Should().BeFalse();
        harness.TryAdvanceHysteresis(newGoal).Should().BeTrue();
    }

    [Fact]
    public void GoalOscillation_NeverReachesThreshold()
    {
        TestGoal currentGoal = new("CurrentGoal");
        TestGoal goalA = new("GoalA");
        TestGoal goalB = new("GoalB");
        HysteresisTestHarness harness = new() { CurrentGoal = currentGoal };

        for (int i = 0; i < 5; i++)
        {
            harness.TryAdvanceHysteresis(goalA).Should().BeFalse();
            harness.TryAdvanceHysteresis(goalB).Should().BeFalse();
        }
    }

    [Fact]
    public void NewGoalResetsCounter()
    {
        TestGoal currentGoal = new("CurrentGoal");
        TestGoal goalA = new("GoalA");
        TestGoal goalB = new("GoalB");
        HysteresisTestHarness harness = new() { CurrentGoal = currentGoal };

        harness.TryAdvanceHysteresis(goalA).Should().BeFalse();
        harness.TryAdvanceHysteresis(goalA).Should().BeFalse();

        harness.TryAdvanceHysteresis(goalB).Should().BeFalse();
        harness.TryAdvanceHysteresis(goalB).Should().BeFalse();
        harness.TryAdvanceHysteresis(goalB).Should().BeTrue();
    }

    [Fact]
    public void SameGoalAsCurrent_ClearsPending()
    {
        TestGoal goalA = new("GoalA");
        TestGoal goalB = new("GoalB");
        HysteresisTestHarness harness = new() { CurrentGoal = goalA };

        harness.TryAdvanceHysteresis(goalB).Should().BeFalse();
        harness.TryAdvanceHysteresis(goalB).Should().BeFalse();

        harness.TryAdvanceHysteresis(goalA).Should().BeTrue();
        harness.TryAdvanceHysteresis(goalB).Should().BeFalse();
    }

    [Fact]
    public void PullRangeHysteresis_HoldsTrueWithinWindow()
    {
        HysteresisTestHarness harness = new();
        DateTime now = DateTime.UtcNow;

        harness.WithInPullRangeHysteresis(true, now).Should().BeTrue();
        harness.WithInPullRangeHysteresis(false, now.AddMilliseconds(250)).Should().BeTrue();
    }

    [Fact]
    public void PullRangeHysteresis_ExpiresAfterWindow()
    {
        HysteresisTestHarness harness = new();
        DateTime now = DateTime.UtcNow;

        harness.WithInPullRangeHysteresis(true, now).Should().BeTrue();
        harness.WithInPullRangeHysteresis(false, now.AddMilliseconds(600)).Should().BeFalse();
    }

    [Fact]
    public void PullRangeHysteresis_RefreshesOnInRangeSample()
    {
        HysteresisTestHarness harness = new();
        DateTime now = DateTime.UtcNow;

        harness.WithInPullRangeHysteresis(true, now).Should().BeTrue();
        harness.WithInPullRangeHysteresis(false, now.AddMilliseconds(450)).Should().BeTrue();
        harness.WithInPullRangeHysteresis(true, now.AddMilliseconds(480)).Should().BeTrue();
        harness.WithInPullRangeHysteresis(false, now.AddMilliseconds(900)).Should().BeTrue();
        harness.WithInPullRangeHysteresis(false, now.AddMilliseconds(1100)).Should().BeFalse();
    }
}
