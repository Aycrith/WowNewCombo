using Core.Goals;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Core.GOAP;

/**
* Plans what actions can be completed in order to fulfill a goal state.
*/

public static class GoapPlanner
{
    [ThreadStatic]
    private static GoapGoal[]? cachedUsableGoals;

    [ThreadStatic]
    private static GoapGoal[]? cachedAvailableGoals;

    [ThreadStatic]
    private static int cachedWorldStateBits;

    [ThreadStatic]
    private static bool hasUsableGoalsCache;

    public static readonly bool[] EmptyGoalState = Array.Empty<bool>();
    public static readonly Stack<GoapGoal> EmptyGoal = new();

    /// <summary>
    /// Explicitly invalidates the usable-goals cache so the next
    /// <see cref="Plan"/> call re-evaluates every goal's CanRun().
    /// Call this when external state (e.g. player form/stance) changes
    /// in a way not captured by the WorldState bit vector.
    /// </summary>
    public static void InvalidateCache()
    {
        hasUsableGoalsCache = false;
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
        Node root = new(null, 0, worldState, null);

        // Use local collections instead of static fields to avoid thread-safety issues
        HashSet<GoapGoal> usable = new();
        PriorityQueue<Node, float> leaves = new();

        int worldStateBits = worldState.Data;

        if (hasUsableGoalsCache &&
            cachedUsableGoals != null &&
            cachedWorldStateBits == worldStateBits &&
            ReferenceEquals(cachedAvailableGoals, available))
        {
            for (int i = 0; i < cachedUsableGoals.Length; i++)
            {
                usable.Add(cachedUsableGoals[i]);
            }
        }
        else
        {
            List<GoapGoal> usableList = new(available.Length);

            // check what actions can run using their checkProceduralPrecondition
            for (int i = 0; i < available.Length; i++)
            {
                GoapGoal a = available[i];
                if (a.CanRun())
                {
                    usable.Add(a);
                    usableList.Add(a);
                }
            }

            cachedUsableGoals = usableList.Count == 0 ? Array.Empty<GoapGoal>() : usableList.ToArray();
            cachedAvailableGoals = available;
            cachedWorldStateBits = worldStateBits;
            hasUsableGoalsCache = true;
        }

        // build up the tree and record the leaf nodes that provide a solution to the goal.
        if (BuildGraph(root, leaves, usable, goal) == 0)
        {
            return EmptyGoal;
        }

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
            return result;
        }

        return EmptyGoal;
    }


    /**
	* Returns true if at least one solution was found.
	* The possible paths are stored in the leaves list. Each leaf has a
	* 'runningCost' value where the lowest cost will be the best action
	* sequence.
	*/

    private static int BuildGraph(Node parent, PriorityQueue<Node, float> leaves, HashSet<GoapGoal> usable, bool[] goal)
    {
        // go through each action available at this node and see if we can use it here
        foreach (GoapGoal action in usable)
        {
            // if the parent state has the conditions for this action's preconditions, we can use it here
            if (InState(action.Preconditions, parent.state))
            {
                // apply the action's effects to the parent state
                BitVector32 effectedState = PopulateState(parent.state, action.Effects);
                Node node = new(parent, parent.runningCost + action.Cost, effectedState, action);

                if (InState(goal, effectedState))
                {
                    // we found a solution!
                    leaves.Enqueue(node, node.runningCost);
                }
                else
                {
                    // not at a solution yet, so test all the remaining actions and branch out the tree
                    HashSet<GoapGoal> subset = new(usable);
                    subset.Remove(action);

                    BuildGraph(node, leaves, subset, goal);
                }
            }
        }

        return leaves.Count;
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
