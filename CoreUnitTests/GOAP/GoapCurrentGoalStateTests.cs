using Core.GOAP;
using Core.Goals;

using FluentAssertions;

using System;
using System.Threading;

using Xunit;

namespace CoreUnitTests.GOAP;

public sealed class GoapCurrentGoalStateTests
{
    [Fact]
    public void NewState_HasNoneGoalAndZeroCounters()
    {
        GoapCurrentGoalState state = new();

        state.CurrentGoalName.Should().Be("None");
        state.CurrentGoalAge.Should().Be(TimeSpan.Zero);
        state.TransitionCount.Should().Be(0);
    }

    [Fact]
    public void SetCurrentGoalName_FirstTransition_SetsAgeAndIncrementsCounter()
    {
        GoapCurrentGoalState state = new();

        state.SetCurrentGoalName("FollowRouteGoal");

        state.CurrentGoalName.Should().Be("FollowRouteGoal");
        state.TransitionCount.Should().Be(1);
        state.CurrentGoalAge.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void SetCurrentGoalName_SameGoal_DoesNotIncrementTransitionCount()
    {
        GoapCurrentGoalState state = new();
        state.SetCurrentGoalName("FollowRouteGoal");
        int transitionsAfterFirstSet = state.TransitionCount;

        Thread.Sleep(20);
        state.SetCurrentGoalName("FollowRouteGoal");

        state.TransitionCount.Should().Be(transitionsAfterFirstSet);
        state.CurrentGoalAge.Should().BeGreaterThan(TimeSpan.FromMilliseconds(15));
    }

    [Fact]
    public void SetCurrentGoalName_NewGoal_ResetsAgeAndIncrementsTransitionCount()
    {
        GoapCurrentGoalState state = new();
        state.SetCurrentGoalName("FollowRouteGoal");

        Thread.Sleep(30);
        TimeSpan firstGoalAge = state.CurrentGoalAge;

        state.SetCurrentGoalName("CombatGoal");

        state.TransitionCount.Should().Be(2);
        firstGoalAge.Should().BeGreaterThan(TimeSpan.FromMilliseconds(20));
        state.CurrentGoalName.Should().Be("CombatGoal");
        state.CurrentGoalAge.Should().BeLessThan(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void Clear_FromActiveGoal_SetsNoneAndResetsAge()
    {
        GoapCurrentGoalState state = new();
        state.SetCurrentGoalName("FollowRouteGoal");

        state.Clear();

        state.CurrentGoalName.Should().Be("None");
        state.CurrentGoalAge.Should().Be(TimeSpan.Zero);
        state.TransitionCount.Should().Be(2);
    }

    [Fact]
    public void SetCurrentGoal_UsesGoalTypeName()
    {
        GoapCurrentGoalState state = new();
        DummyGoal goal = new();

        state.SetCurrentGoal(goal);

        state.CurrentGoalName.Should().Be(nameof(DummyGoal));
        state.TransitionCount.Should().Be(1);
    }

    private sealed class DummyGoal : GoapGoal
    {
        public DummyGoal() : base(nameof(DummyGoal))
        {
        }

        public override float Cost => 1f;

        public override bool CanRun() => true;
    }
}
