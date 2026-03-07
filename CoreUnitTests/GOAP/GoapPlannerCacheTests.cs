using Core.GOAP;
using Core.Goals;
using FluentAssertions;

using System.Collections.Generic;
using System.Collections.Specialized;

using Xunit;

namespace CoreUnitTests.GOAP;

public sealed class GoapPlannerCacheTests
{
    private static BitVector32 CreateWorldState(params (GoapKey key, bool value)[] states)
    {
        BitVector32 state = new();
        foreach ((GoapKey key, bool value) in states)
        {
            state[1 << (int)key] = value;
        }

        return state;
    }

    private static bool[] CreateGoalState(params (GoapKey key, bool value)[] states)
    {
        bool[] goal = new bool[(int)GoapKey.LENGTH];
        foreach ((GoapKey key, bool value) in states)
        {
            goal[(int)key] = value;
        }

        return goal;
    }

    [Fact]
    public void Plan_TwiceWithSameWorldState_ReevaluatesCanRunForDynamicGoals()
    {
        CountingGoal g1 = new("Goal1", effectKey: GoapKey.incombat, canRun: true);
        CountingGoal g2 = new("Goal2", effectKey: GoapKey.hastarget, canRun: true);
        GoapGoal[] available = [g1, g2];
        BitVector32 worldState = CreateWorldState();
        bool[] goalState = CreateGoalState((GoapKey.incombat, true));

        _ = GoapPlanner.Plan(available, worldState, goalState);
        _ = GoapPlanner.Plan(available, worldState, goalState);

        // Planner correctness must not rely on WorldState-only cache keys because many goals
        // evaluate dynamic state outside WorldState (e.g. cooldowns, auras/forms, global time).
        g1.CanRunCallCount.Should().Be(2);
        g2.CanRunCallCount.Should().Be(2);
    }

    [Fact]
    public void Plan_WhenWorldStateChanges_ReevaluatesCanRun()
    {
        CountingGoal goal = new("Goal1", effectKey: GoapKey.incombat, canRun: true);
        GoapGoal[] available = [goal];
        bool[] goalState = CreateGoalState((GoapKey.incombat, true));

        BitVector32 worldState1 = CreateWorldState();
        BitVector32 worldState2 = CreateWorldState((GoapKey.hastarget, true));

        _ = GoapPlanner.Plan(available, worldState1, goalState);
        _ = GoapPlanner.Plan(available, worldState2, goalState);

        goal.CanRunCallCount.Should().Be(2);
    }

    [Fact]
    public void Plan_WithFortyOneAvailableGoals_AllowsRepeatedPlanning()
    {
        GoapPlanner.InvalidateCache();

        List<GoapGoal> goals = new(41);
        for (int i = 0; i < 40; i++)
        {
            goals.Add(new CountingGoal($"Disabled{i}", effectKey: GoapKey.hastarget, canRun: false));
        }

        CountingGoal directGoal = new("DirectCombat", effectKey: GoapKey.incombat, canRun: true);
        goals.Add(directGoal);

        GoapGoal[] available = goals.ToArray();
        BitVector32 worldState = CreateWorldState();
        bool[] goalState = CreateGoalState((GoapKey.incombat, true));

        Stack<GoapGoal> firstPlan = GoapPlanner.Plan(available, worldState, goalState);
        Stack<GoapGoal> secondPlan = GoapPlanner.Plan(available, worldState, goalState);

        firstPlan.Should().HaveCount(1);
        firstPlan.Peek().Should().Be(directGoal);
        secondPlan.Should().HaveCount(1);
        secondPlan.Peek().Should().Be(directGoal);
    }

    private sealed class CountingGoal : GoapGoal
    {
        private readonly bool canRun;

        public int CanRunCallCount { get; private set; }

        public CountingGoal(string name, GoapKey effectKey, bool canRun) : base(name)
        {
            this.canRun = canRun;
            AddEffect(effectKey, true);
        }

        public override float Cost => 1f;

        public override bool CanRun()
        {
            CanRunCallCount++;
            return canRun;
        }
    }
}
