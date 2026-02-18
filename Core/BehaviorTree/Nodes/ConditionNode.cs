namespace Core.BehaviorTree.Nodes;

using System;

/// <summary>
/// Condition node that evaluates a predicate.
/// Returns Success if condition is true, Failure otherwise.
/// </summary>
public sealed class ConditionNode : IBehaviorNode
{
    /// <summary>
    /// Name of the node for debugging.
    /// </summary>
    public string Name { get; }

    private readonly Func<BehaviorContext, bool> condition;

    /// <summary>
    /// Creates a new condition node.
    /// </summary>
    /// <param name="name">Node name for debugging.</param>
    /// <param name="condition">Predicate to evaluate.</param>
    public ConditionNode(string name, Func<BehaviorContext, bool> condition)
    {
        Name = name;
        this.condition = condition;
    }

    /// <summary>
    /// Evaluate the condition.
    /// </summary>
    public NodeStatus Execute(BehaviorContext context)
    {
        bool result = condition(context);
        return result ? NodeStatus.Success : NodeStatus.Failure;
    }

    /// <summary>
    /// No state to reset.
    /// </summary>
    public void Reset()
    {
        // Stateless
    }
}

/// <summary>
/// Inverted condition node that returns Success when condition is false.
/// </summary>
public sealed class InverterNode : IBehaviorNode
{
    /// <summary>
    /// Name of the node for debugging.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Child node to invert.
    /// </summary>
    public IBehaviorNode Child { get; }

    /// <summary>
    /// Creates a new inverter node.
    /// </summary>
    /// <param name="name">Node name for debugging.</param>
    /// <param name="child">Child node to invert.</param>
    public InverterNode(string name, IBehaviorNode child)
    {
        Name = name;
        Child = child;
    }

    /// <summary>
    /// Invert the child's result.
    /// </summary>
    public NodeStatus Execute(BehaviorContext context)
    {
        NodeStatus status = Child.Execute(context);
        return status switch
        {
            NodeStatus.Success => NodeStatus.Failure,
            NodeStatus.Failure => NodeStatus.Success,
            _ => status
        };
    }

    /// <summary>
    /// Reset the child.
    /// </summary>
    public void Reset()
    {
        Child.Reset();
    }
}
