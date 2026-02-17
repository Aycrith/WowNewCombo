namespace Core.BehaviorTree.Nodes;

using System.Collections.Generic;

/// <summary>
/// Sequence node that executes children in order until one fails.
/// Returns Success if all children succeed, Failure if any fail.
/// Similar to an "AND" operation.
/// </summary>
public sealed class SequenceNode : IBehaviorNode
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
    /// Creates a new sequence node.
    /// </summary>
    /// <param name="name">Node name for debugging.</param>
    public SequenceNode(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Execute children until one fails.
    /// </summary>
    public NodeStatus Execute(BehaviorContext context)
    {
        foreach (IBehaviorNode child in Children)
        {
            NodeStatus status = child.Execute(context);
            if (status != NodeStatus.Success)
            {
                return status;
            }
        }
        return NodeStatus.Success;
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
