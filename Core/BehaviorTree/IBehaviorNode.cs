namespace Core.BehaviorTree;

using System.Threading.Tasks;

/// <summary>
/// Status returned by behavior tree node execution.
/// </summary>
public enum NodeStatus
{
    /// <summary>
    /// Node completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Node failed to complete.
    /// </summary>
    Failure,

    /// <summary>
    /// Node is still running (async operation).
    /// </summary>
    Running
}

/// <summary>
/// Interface for all behavior tree nodes.
/// </summary>
public interface IBehaviorNode
{
    /// <summary>
    /// Name of the node for debugging/logging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Execute the node with the given context.
    /// </summary>
    /// <param name="context">Shared behavior context containing game state and services.</param>
    /// <returns>Status of execution.</returns>
    NodeStatus Execute(BehaviorContext context);

    /// <summary>
    /// Reset the node to initial state.
    /// </summary>
    void Reset();
}

/// <summary>
/// Interface for nodes that support async operations.
/// </summary>
public interface IAsyncBehaviorNode : IBehaviorNode
{
    /// <summary>
    /// Execute the node asynchronously.
    /// </summary>
    /// <param name="context">Shared behavior context.</param>
    /// <returns>Task returning execution status.</returns>
    Task<NodeStatus> ExecuteAsync(BehaviorContext context);
}
