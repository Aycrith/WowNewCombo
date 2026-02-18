namespace Core.BehaviorTree.Nodes;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

/// <summary>
/// Action node that executes an action.
/// </summary>
public sealed class ActionNode : IBehaviorNode
{
    /// <summary>
    /// Name of the node for debugging.
    /// </summary>
    public string Name { get; }

    private readonly Func<BehaviorContext, NodeStatus> action;

    /// <summary>
    /// Creates a new action node.
    /// </summary>
    /// <param name="name">Node name for debugging.</param>
    /// <param name="action">Action to execute.</param>
    public ActionNode(string name, Func<BehaviorContext, NodeStatus> action)
    {
        Name = name;
        this.action = action;
    }

    /// <summary>
    /// Execute the action.
    /// </summary>
    public NodeStatus Execute(BehaviorContext context)
    {
        try
        {
            return action(context);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "[BehaviorTree] Action '{ActionName}' threw exception", Name);
            return NodeStatus.Failure;
        }
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
/// Async action node that supports async operations.
/// </summary>
public sealed class AsyncActionNode : IAsyncBehaviorNode
{
    /// <summary>
    /// Name of the node for debugging.
    /// </summary>
    public string Name { get; }

    private readonly Func<BehaviorContext, CancellationToken, Task<NodeStatus>> asyncAction;

    /// <summary>
    /// Creates a new async action node.
    /// </summary>
    /// <param name="name">Node name for debugging.</param>
    /// <param name="asyncAction">Async action to execute.</param>
    public AsyncActionNode(string name, Func<BehaviorContext, CancellationToken, Task<NodeStatus>> asyncAction)
    {
        Name = name;
        this.asyncAction = asyncAction;
    }

    /// <summary>
    /// Execute synchronously (not recommended for async actions).
    /// </summary>
    public NodeStatus Execute(BehaviorContext context)
    {
        // Run async action synchronously - not ideal but needed for interface
        return ExecuteAsync(context).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Execute the async action.
    /// </summary>
    public async Task<NodeStatus> ExecuteAsync(BehaviorContext context)
    {
        try
        {
            return await asyncAction(context, context.CancellationToken);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "[BehaviorTree] Async action '{ActionName}' threw exception", Name);
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// No state to reset.
    /// </summary>
    public void Reset()
    {
        // Stateless
    }
}
