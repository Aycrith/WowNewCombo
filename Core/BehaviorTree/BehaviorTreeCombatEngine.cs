namespace Core.BehaviorTree;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Core;
using Core.BehaviorTree.Nodes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Combat engine that uses behavior trees for decision making.
/// Alternative to GOAP-based combat system.
/// </summary>
public sealed class BehaviorTreeCombatEngine
{
    private readonly ILogger<BehaviorTreeCombatEngine> logger;
    private IBehaviorNode? rootNode;

    /// <summary>
    /// Creates a new behavior tree combat engine.
    /// </summary>
    public BehaviorTreeCombatEngine(ILogger<BehaviorTreeCombatEngine> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Sets the root behavior tree node.
    /// </summary>
    public void SetBehaviorTree(IBehaviorNode root)
    {
        rootNode = root;
        logger.LogInformation("[BehaviorTreeCombat] Behavior tree set: {RootName}", root.Name);
    }

    /// <summary>
    /// Execute a single tick of the behavior tree.
    /// </summary>
    /// <param name="context">Shared behavior context.</param>
    /// <returns>Status of execution.</returns>
    public NodeStatus Tick(BehaviorContext context)
    {
        if (rootNode == null)
        {
            logger.LogWarning("[BehaviorTreeCombat] No behavior tree set");
            return NodeStatus.Failure;
        }

        try
        {
            NodeStatus status = rootNode.Execute(context);
            return status;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BehaviorTreeCombat] Exception during tick");
            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// Reset the behavior tree to initial state.
    /// </summary>
    public void Reset()
    {
        rootNode?.Reset();
        logger.LogDebug("[BehaviorTreeCombat] Behavior tree reset");
    }

    /// <summary>
    /// Build a standard combat tree from class configuration.
    /// </summary>
    public IBehaviorNode BuildCombatTree(ClassConfiguration config)
    {
        var converter = new JsonToBehaviorTreeConverter(logger);
        return converter.ConvertCombatSequence(config);
    }
}

/// <summary>
/// Factory for creating behavior tree engines.
/// </summary>
public interface IBehaviorTreeCombatEngineFactory
{
    /// <summary>
    /// Create a new behavior tree combat engine.
    /// </summary>
    BehaviorTreeCombatEngine CreateEngine();
}

/// <summary>
/// Default implementation of behavior tree engine factory.
/// </summary>
public sealed class BehaviorTreeCombatEngineFactory : IBehaviorTreeCombatEngineFactory
{
    private readonly ILoggerFactory loggerFactory;

    /// <summary>
    /// Creates a new factory.
    /// </summary>
    public BehaviorTreeCombatEngineFactory(ILoggerFactory loggerFactory)
    {
        this.loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Create a new behavior tree combat engine.
    /// </summary>
    public BehaviorTreeCombatEngine CreateEngine()
    {
        ILogger<BehaviorTreeCombatEngine> logger = loggerFactory.CreateLogger<BehaviorTreeCombatEngine>();
        return new BehaviorTreeCombatEngine(logger);
    }
}
