using Core.Goals;
using System.Collections.Generic;
using System.Linq;

namespace Core.GOAP
{
    /**
	 * Plans what actions can be completed in order to fulfill a goal state.
	 */

    public static class GoapPlanner
    {
        public static readonly Dictionary<GoapKey, bool> EmptyGoalState = new();
        public static readonly Stack<GoapGoal> EmptyGoal = new();

        /**
		 * Plan what sequence of actions can fulfill the goal.
		 * Returns null if a plan could not be found, or a list of the actions
		 * that must be performed, in order, to fulfill the goal.
		 */

        public static Stack<GoapGoal> Plan(IEnumerable<GoapGoal> availableActions,
            Dictionary<GoapKey, bool> worldState,
            Dictionary<GoapKey, bool> goal)
        {
            Node root = new(null, 0, worldState, null);

            // check what actions can run using their checkProceduralPrecondition
            HashSet<GoapGoal> usableActions = new();
            foreach (GoapGoal a in availableActions)
            {
                if (a.CanRun())
                {
                    usableActions.Add(a);
                }
                else
                {
                    a.SetState(InState(a.Preconditions, root.state));
                }
            }

            // build up the tree and record the leaf nodes that provide a solution to the goal.
            List<Node> leaves = new();
            if (!BuildGraph(root, leaves, usableActions, goal))
            {
                return EmptyGoal;
            }

            // get the cheapest leaf
            Stack<GoapGoal> result = new();
            Node? node = leaves.MinBy(a => a.runningCost);
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

        /**
		 * Returns true if at least one solution was found.
		 * The possible paths are stored in the leaves list. Each leaf has a
		 * 'runningCost' value where the lowest cost will be the best action
		 * sequence.
		 */

        private static bool BuildGraph(Node parent, List<Node> leaves, HashSet<GoapGoal> usableActions, Dictionary<GoapKey, bool> goal)
        {
            bool foundOne = false;

            // go through each action available at this node and see if we can use it here
            foreach (GoapGoal action in usableActions)
            {
                // if the parent state has the conditions for this action's preconditions, we can use it here
                var result = InState(action.Preconditions, parent.state);
                action.SetState(result);

                if (!result.ContainsValue(false))
                {
                    // apply the action's effects to the parent state
                    var currentState = PopulateState(parent.state, action.Effects);
                    //Debug.Log(GoapAgent.prettyPrint(currentState));
                    Node node = new(parent, parent.runningCost + action.Cost, currentState, action);

                    result = InState(goal, currentState);
                    if (!result.ContainsValue(false))
                    {
                        // we found a solution!
                        leaves.Add(node);
                        foundOne = true;
                    }
                    else
                    {
                        // not at a solution yet, so test all the remaining actions and branch out the tree
                        HashSet<GoapGoal> subset = ActionSubset(usableActions, action);
                        bool found = BuildGraph(node, leaves, subset, goal);
                        if (found)
                        {
                            foundOne = true;
                        }
                    }
                }
            }

            return foundOne;
        }

        /**
		 * Create a subset of the actions excluding the removeMe one. Creates a new set.
		 */

        private static HashSet<GoapGoal> ActionSubset(HashSet<GoapGoal> actions, GoapGoal removeMe)
        {
            HashSet<GoapGoal> subset = new(actions);
            subset.Remove(removeMe);
            return subset;
        }

        /**
		 * Check that all items in 'test' are in 'state'. If just one does not match or is not there
		 * then this returns false.
		 */

        private static Dictionary<GoapKey, bool> InState(Dictionary<GoapKey, bool> test, Dictionary<GoapKey, bool> state)
        {
            Dictionary<GoapKey, bool> resultState = new();
            foreach (var t in test)
            {
                bool exists = state.TryGetValue(t.Key, out bool current);
                resultState[t.Key] = exists && test[t.Key].Equals(current);
            }
            return resultState;
        }

        /**
		 * Apply the stateChange to the currentState
		 */

        private static Dictionary<GoapKey, bool> PopulateState(Dictionary<GoapKey, bool> currentState, Dictionary<GoapKey, bool> futureState)
        {
            Dictionary<GoapKey, bool> state = new(currentState);
            foreach (var kv in futureState)
            {
                state[kv.Key] = kv.Value;
            }
            return state;
        }

        /**
		 * Used for building up the graph and holding the running costs of actions.
		 */

        private class Node
        {
            public readonly Node? parent;
            public readonly float runningCost;
            public readonly Dictionary<GoapKey, bool> state;
            public readonly GoapGoal? action;

            public Node(Node? parent, float runningCost, Dictionary<GoapKey, bool> state, GoapGoal? action)
            {
                this.parent = parent;
                this.runningCost = runningCost;
                this.state = state;
                this.action = action;
            }
        }
    }
}