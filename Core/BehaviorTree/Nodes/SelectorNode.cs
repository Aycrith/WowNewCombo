namespace Core.BehaviorTree.Nodes;

using System.Collections.Generic;

/// <summary>
/// Selector node that executes children in order until one succeeds.
/// Returns Success if any child succeeds, Failure if all fail.
/// Similar to an "OR" operation.
/// </summary>
public sealed class SelectorNode : IBehaviorNode
{
    /// <summary>
    /// Name of the node for debugging.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Child nodes to execute.
    /// </summary>
    public List<IBehaviorNode> Children { get; } = new();

    /// <summary>
    /// Creates a new selector node.
    /// </summary>
    /// <param name="name">Node name for debugging.</param>
    public SelectorNode(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Execute children until one succeeds.
    /// </summary>
    public NodeStatus Execute(BehaviorContext context)
    {
        foreach (IBehaviorNode child in Children)
        {
            NodeStatus status = child.Execute(context);
            if (status != NodeStatus.Failure)
            {
                return status;
            }
        }
        return NodeStatus.Failure;
    }

    /// <summary>
    /// Reset all children.
    /// </summary>
    public void Reset()
    {
        foreach (IBehaviorNode child in Children)
        {
            child.Reset();
        }
    }
}
