using Core.Goals;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Core.GOAP;

/**
* Plans what actions can be completed in order to fulfill a goal state.
*/

public static class GoapPlanner
{
    public static readonly bool[] EmptyGoalState = Array.Empty<bool>();
    public static readonly Stack<GoapGoal> EmptyGoal = new();

    private static readonly object cacheSync = new();
    private static int cacheGeneration;
    private static readonly Dictionary<UsableCacheKey, GoapGoal[]> usableGoalCache = [];
    private static readonly Dictionary<PlanCacheKey, GoapGoal[]> planCache = [];

    /// <summary>
    /// Explicitly invalidates the usable-goals cache so the next
    /// <see cref="Plan"/> call re-evaluates every goal's CanRun().
    /// Call this when external state (e.g. player form/stance) changes
    /// in a way not captured by the WorldState bit vector.
    /// </summary>
    public static void InvalidateCache()
    {
        lock (cacheSync)
        {
            cacheGeneration++;
            usableGoalCache.Clear();
            planCache.Clear();
        }
    }

    /**
    * Plan what sequence of actions can fulfill the goal.
    * Returns null if a plan could not be found, or a list of the actions
    * that must be performed, in order, to fulfill the goal.
    */

    public static Stack<GoapGoal> Plan(
        GoapGoal[] available,
        BitVector32 worldState,
        bool[] goal)
    {
        return Plan(available, worldState, goal, new GoapPlannerExecutionOptions());
    }

    public static Stack<GoapGoal> Plan(
        GoapGoal[] available,
        BitVector32 worldState,
        bool[] goal,
        GoapPlannerExecutionOptions executionOptions)
    {
        if (available.Length > 31)
            throw new InvalidOperationException(
                $"GoapPlanner bitmask supports at most 31 goals; got {available.Length}. " +
                "Increase bitmask type to ulong to support up to 63 goals.");

        int generation = Volatile.Read(ref cacheGeneration);
        int availableSignature = ComputeAvailableSignature(available);
        int goalSignature = ComputeGoalSignature(goal);

        if (TryGetCachedPlan(
                worldState,
                goal,
                generation,
                availableSignature,
                goalSignature,
                executionOptions,
                out Stack<GoapGoal>? cachedPlan))
        {
            return cachedPlan!;
        }

        Node root = new(null, 0, worldState, null);
        PriorityQueue<Node, float> leaves = new();

        GoapGoal[] usable = GetUsableGoals(
            available,
            worldState,
            generation,
            availableSignature,
            executionOptions);
        if (usable.Length == 0)
            return EmptyGoal;

        // Build initial mask with all usable-goal bits set
        uint allMask = (1u << usable.Length) - 1u;

        BuildGraph(root, leaves, usable, allMask, goal);

        // get the cheapest leaf
        if (leaves.TryDequeue(out Node? node, out _))
        {
            Stack<GoapGoal> result = new();
            while (node != null)
            {
                if (node.action != null)
                {
                    result.Push(node.action);
                }
                node = node.parent;
            }

            StorePlanCache(
                result,
                worldState,
                generation,
                availableSignature,
                goalSignature,
                executionOptions);
            return result;
        }

        return EmptyGoal;
    }

    private static GoapGoal[] EvaluateUsableGoals(GoapGoal[] available)
    {
        int usableCount = 0;
        GoapGoal[] usableBuffer = new GoapGoal[available.Length];
        for (int i = 0; i < available.Length; i++)
        {
            GoapGoal goal = available[i];
            if (goal.CanRun())
            {
                usableBuffer[usableCount++] = goal;
            }
        }

        return usableCount == 0 ? [] : usableBuffer[..usableCount];
    }

    private static GoapGoal[] GetUsableGoals(
        GoapGoal[] available,
        BitVector32 worldState,
        int generation,
        int availableSignature,
        GoapPlannerExecutionOptions executionOptions)
    {
        if (!executionOptions.EnableUsableGoalCache)
        {
            return EvaluateUsableGoals(available);
        }

        UsableCacheKey key = new(generation, worldState.Data, availableSignature);

        lock (cacheSync)
        {
            if (usableGoalCache.TryGetValue(key, out GoapGoal[]? cached))
            {
                return cached;
            }
        }

        GoapGoal[] evaluated = EvaluateUsableGoals(available);
        lock (cacheSync)
        {
            TrimCache(usableGoalCache, executionOptions.MaxUsableCacheEntries);
            usableGoalCache[key] = evaluated;
        }

        return evaluated;
    }

    private static bool TryGetCachedPlan(
        BitVector32 worldState,
        bool[] goal,
        int generation,
        int availableSignature,
        int goalSignature,
        GoapPlannerExecutionOptions executionOptions,
        out Stack<GoapGoal>? cachedPlan)
    {
        cachedPlan = null;
        if (!executionOptions.EnablePlanCache)
        {
            return false;
        }

        PlanCacheKey key = new(generation, worldState.Data, goalSignature, availableSignature);
        GoapGoal[]? cachedTopFirst;
        lock (cacheSync)
        {
            if (!planCache.TryGetValue(key, out cachedTopFirst))
            {
                return false;
            }
        }

        if (!ValidateCachedPlan(cachedTopFirst, worldState, goal))
        {
            lock (cacheSync)
            {
                planCache.Remove(key);
            }

            return false;
        }

        cachedPlan = BuildStackFromTopFirst(cachedTopFirst);
        return true;
    }

    private static bool ValidateCachedPlan(GoapGoal[] cachedTopFirst, BitVector32 worldState, bool[] goal)
    {
        if (cachedTopFirst.Length == 0)
        {
            return false;
        }

        BitVector32 currentState = worldState;
        for (int i = 0; i < cachedTopFirst.Length; i++)
        {
            GoapGoal action = cachedTopFirst[i];
            if (!action.CanRun())
            {
                return false;
            }

            if (!InState(action.Preconditions, currentState))
            {
                return false;
            }

            currentState = PopulateState(currentState, action.Effects);
        }

        return InState(goal, currentState);
    }

    private static void StorePlanCache(
        Stack<GoapGoal> plan,
        BitVector32 worldState,
        int generation,
        int availableSignature,
        int goalSignature,
        GoapPlannerExecutionOptions executionOptions)
    {
        if (!executionOptions.EnablePlanCache || plan.Count == 0)
        {
            return;
        }

        PlanCacheKey key = new(generation, worldState.Data, goalSignature, availableSignature);
        GoapGoal[] topFirst = plan.ToArray();

        lock (cacheSync)
        {
            TrimCache(planCache, executionOptions.MaxPlanCacheEntries);
            planCache[key] = topFirst;
        }
    }

    private static Stack<GoapGoal> BuildStackFromTopFirst(GoapGoal[] topFirst)
    {
        Stack<GoapGoal> plan = new(topFirst.Length);
        for (int i = topFirst.Length - 1; i >= 0; i--)
        {
            plan.Push(topFirst[i]);
        }

        return plan;
    }

    private static int ComputeAvailableSignature(GoapGoal[] available)
    {
        return HashCode.Combine(RuntimeHelpers.GetHashCode(available), available.Length);
    }

    private static int ComputeGoalSignature(bool[] goal)
    {
        HashCode hash = new();
        hash.Add(goal.Length);
        for (int i = 0; i < goal.Length; i++)
        {
            if (goal[i])
            {
                hash.Add(i);
            }
        }

        return hash.ToHashCode();
    }

    private static void TrimCache<TKey, TValue>(Dictionary<TKey, TValue> cache, int maxEntries)
        where TKey : notnull
    {
        int boundedMaxEntries = Math.Clamp(maxEntries, 1, 1024);
        if (cache.Count < boundedMaxEntries)
        {
            return;
        }

        cache.Clear();
    }

    private readonly record struct UsableCacheKey(
        int Generation,
        int WorldStateData,
        int AvailableSignature);

    private readonly record struct PlanCacheKey(
        int Generation,
        int WorldStateData,
        int GoalSignature,
        int AvailableSignature);


    /**
	* Returns true if at least one solution was found.
	* The possible paths are stored in the leaves list. Each leaf has a
	* 'runningCost' value where the lowest cost will be the best action
	* sequence.
	*/

    private static void BuildGraph(
        Node parent,
        PriorityQueue<Node, float> leaves,
        GoapGoal[] usable,
        uint includeMask,
        bool[] goal)
    {
        for (int i = 0; i < usable.Length; i++)
        {
            if ((includeMask & (1u << i)) == 0)
                continue;

            GoapGoal action = usable[i];

            if (!InState(action.Preconditions, parent.state))
                continue;

            BitVector32 effectedState = PopulateState(parent.state, action.Effects);
            Node node = new(parent, parent.runningCost + action.Cost, effectedState, action);

            if (InState(goal, effectedState))
            {
                leaves.Enqueue(node, node.runningCost);
            }
            else
            {
                // Exclude this goal from deeper branches — prevents using the same goal twice
                uint nextMask = includeMask & ~(1u << i);
                BuildGraph(node, leaves, usable, nextMask, goal);
            }
        }
    }

    /**
	* Check that all items in 'test' are in 'state'. If just one does not match or is not there
	* then this returns false.
	*/

    private static bool InState(Dictionary<GoapKey, bool> test, BitVector32 state)
    {
        foreach ((GoapKey key, bool value) in test)
        {
            if (state[1 << (int)key] != value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool InState(bool[] test, BitVector32 state)
    {
        // Only check indices that are explicitly set (not default/false)
        // This allows partial goal matching for chain planning
        for (int i = 0; i < test.Length; i++)
        {
            if (test[i] && !state[1 << i])
            {
                // Goal requires true but state is false
                return false;
            }
        }
        return true;
    }

    /**
	* Apply the stateChange to the currentState
	*/

    private static BitVector32 PopulateState(BitVector32 state, Dictionary<GoapKey, bool> effects)
    {
        BitVector32 future = new(state);
        foreach ((GoapKey key, bool value) in effects)
        {
            future[1 << (int)key] = value;
        }
        return future;
    }

    /**
	* Used for building up the graph and holding the running costs of actions.
	*/

    private sealed class Node
    {
        public readonly Node? parent;
        public readonly float runningCost;
        public readonly BitVector32 state;
        public readonly GoapGoal? action;

        public Node(Node? parent, float runningCost, BitVector32 state, GoapGoal? action)
        {
            this.parent = parent;
            this.runningCost = runningCost;
            this.state = state;
            this.action = action;
        }
    }
}
