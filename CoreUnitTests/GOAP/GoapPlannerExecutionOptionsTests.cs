using Core.GOAP;
using Core.Goals;

using FluentAssertions;

using System.Collections.Generic;
using System.Collections.Specialized;

using Xunit;

namespace CoreUnitTests.GOAP;

public sealed class GoapPlannerExecutionOptionsTests
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
    public void Plan_WithUsableGoalCacheEnabled_SameWorldState_UsesCachedUsableSet()
    {
        GoapPlanner.InvalidateCache();

        CountingGoal acquireTarget = new(
            "AcquireTarget",
            true,
            effects: [(GoapKey.hastarget, true)]);

        CountingGoal enterCombat = new(
            "EnterCombat",
            true,
            preconditions: [(GoapKey.hastarget, true)],
            effects: [(GoapKey.incombat, true)]);

        GoapGoal[] available = [acquireTarget, enterCombat];
        BitVector32 worldState = CreateWorldState();
        bool[] goalState = CreateGoalState((GoapKey.incombat, true));

        GoapPlannerExecutionOptions options = new(
            EnableUsableGoalCache: true,
            EnablePlanCache: false);

        _ = GoapPlanner.Plan(available, worldState, goalState, options);
        _ = GoapPlanner.Plan(available, worldState, goalState, options);

        acquireTarget.CanRunCallCount.Should().Be(1);
        enterCombat.CanRunCallCount.Should().Be(1);
    }

    [Fact]
    public void Plan_WithUsableGoalCacheEnabled_WorldStateChange_ReevaluatesCanRun()
    {
        GoapPlanner.InvalidateCache();

        CountingGoal goal = new("CombatGoal", true, effects: [(GoapKey.incombat, true)]);

        GoapGoal[] available = [goal];
        bool[] goalState = CreateGoalState((GoapKey.incombat, true));
        GoapPlannerExecutionOptions options = new(
            EnableUsableGoalCache: true,
            EnablePlanCache: false);

        _ = GoapPlanner.Plan(available, CreateWorldState(), goalState, options);
        _ = GoapPlanner.Plan(available, CreateWorldState((GoapKey.hastarget, true)), goalState, options);

        goal.CanRunCallCount.Should().Be(2);
    }

    [Fact]
    public void Plan_WithPlanCacheEnabled_ReusesPlanWithoutReevaluatingUnusedGoals()
    {
        GoapPlanner.InvalidateCache();

        CountingGoal acquireTarget = new(
            "AcquireTarget",
            true,
            effects: [(GoapKey.hastarget, true)]);

        CountingGoal enterCombat = new(
            "EnterCombat",
            true,
            preconditions: [(GoapKey.hastarget, true)],
            effects: [(GoapKey.incombat, true)]);

        CountingGoal unrelated = new("Unrelated", true, effects: [(GoapKey.shouldloot, true)]);

        GoapGoal[] available = [acquireTarget, enterCombat, unrelated];
        BitVector32 worldState = CreateWorldState();
        bool[] goalState = CreateGoalState((GoapKey.incombat, true));
        GoapPlannerExecutionOptions options = new(
            EnableUsableGoalCache: false,
            EnablePlanCache: true);

        _ = GoapPlanner.Plan(available, worldState, goalState, options);
        _ = GoapPlanner.Plan(available, worldState, goalState, options);

        acquireTarget.CanRunCallCount.Should().Be(2);
        enterCombat.CanRunCallCount.Should().Be(2);
        unrelated.CanRunCallCount.Should().Be(1);
    }

    [Fact]
    public void Plan_WithPlanCacheEnabled_InvalidatesWhenCanRunFlips()
    {
        GoapPlanner.InvalidateCache();

        MutableGoal goal = new("CombatGoal", true, effects: [(GoapKey.incombat, true)]);

        GoapGoal[] available = [goal];
        BitVector32 worldState = CreateWorldState();
        bool[] goalState = CreateGoalState((GoapKey.incombat, true));
        GoapPlannerExecutionOptions options = new(
            EnableUsableGoalCache: false,
            EnablePlanCache: true);

        Stack<GoapGoal> firstPlan = GoapPlanner.Plan(available, worldState, goalState, options);
        firstPlan.Should().HaveCount(1);

        goal.CanRunValue = false;
        Stack<GoapGoal> secondPlan = GoapPlanner.Plan(available, worldState, goalState, options);
        secondPlan.Should().BeEmpty();
        goal.CanRunCallCount.Should().Be(3);
    }

    [Fact]
    public void InvalidateCache_ClearsUsableAndPlanCaches()
    {
        GoapPlanner.InvalidateCache();

        CountingGoal goal = new("CombatGoal", true, effects: [(GoapKey.incombat, true)]);
        GoapGoal[] available = [goal];
        bool[] goalState = CreateGoalState((GoapKey.incombat, true));
        GoapPlannerExecutionOptions options = new(
            EnableUsableGoalCache: true,
            EnablePlanCache: true);

        _ = GoapPlanner.Plan(available, CreateWorldState(), goalState, options);
        _ = GoapPlanner.Plan(available, CreateWorldState(), goalState, options);
        goal.CanRunCallCount.Should().Be(2);

        GoapPlanner.InvalidateCache();
        _ = GoapPlanner.Plan(available, CreateWorldState(), goalState, options);
        goal.CanRunCallCount.Should().Be(3);
    }

    private sealed class CountingGoal : GoapGoal
    {
        private readonly bool canRun;
        public int CanRunCallCount { get; private set; }

        public CountingGoal(
            string name,
            bool canRun,
            (GoapKey key, bool value)[]? preconditions = null,
            (GoapKey key, bool value)[]? effects = null)
            : base(name)
        {
            this.canRun = canRun;

            if (preconditions != null)
            {
                foreach ((GoapKey key, bool value) in preconditions)
                {
                    AddPrecondition(key, value);
                }
            }

            if (effects != null)
            {
                foreach ((GoapKey key, bool value) in effects)
                {
                    AddEffect(key, value);
                }
            }
        }

        public override float Cost => 1f;

        public override bool CanRun()
        {
            CanRunCallCount++;
            return canRun;
        }
    }

    private sealed class MutableGoal : GoapGoal
    {
        public MutableGoal(
            string name,
            bool canRunValue,
            (GoapKey key, bool value)[]? preconditions = null,
            (GoapKey key, bool value)[]? effects = null)
            : base(name)
        {
            CanRunValue = canRunValue;

            if (preconditions != null)
            {
                foreach ((GoapKey key, bool value) in preconditions)
                {
                    AddPrecondition(key, value);
                }
            }

            if (effects != null)
            {
                foreach ((GoapKey key, bool value) in effects)
                {
                    AddEffect(key, value);
                }
            }
        }

        public bool CanRunValue { get; set; }
        public int CanRunCallCount { get; private set; }

        public override float Cost => 1f;

        public override bool CanRun()
        {
            CanRunCallCount++;
            return CanRunValue;
        }
    }
}
