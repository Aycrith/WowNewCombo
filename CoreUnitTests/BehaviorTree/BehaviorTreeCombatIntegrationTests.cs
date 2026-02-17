namespace CoreUnitTests.BehaviorTree;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

using Core;
using Core.BehaviorTree;
using Core.BehaviorTree.Nodes;

using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

/// <summary>
/// Integration tests for BehaviorTreeCombatEngine with realistic combat scenarios.
/// Tests engine tick mechanics, tree reset, state management, and combat decision trees.
/// </summary>
public sealed class BehaviorTreeCombatIntegrationTests
{
    private static BehaviorContext CreateMockContext(
        int nearbyEnemies = 1,
        float playerHealth = 100f,
        float targetHealth = 100f,
        bool targetDead = false)
    {
        return new BehaviorContext
        {
            Player = null!,
            Casting = null!,
            StopMoving = null!,
            Input = null!,
            Logger = NullLogger.Instance,
            NearbyEnemies = nearbyEnemies,
            CurrentTarget = targetDead ? null : new TargetInfo
            {
                HealthPercent = targetHealth,
                Level = 60,
                IsDead = false,
                IsElite = false,
                Distance = 10f,
                Position = new Vector3(100f, 200f, 300f)
            },
            CombatSequence = Array.Empty<KeyAction>()
        };
    }

    #region Engine Basic Tests

    [Fact]
    public void Engine_CreatedWithoutTree_TickReturnsFailure()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        var context = CreateMockContext();

        NodeStatus result = engine.Tick(context);

        result.Should().Be(NodeStatus.Failure);
    }

    [Fact]
    public void Engine_WithSimpleSuccessTree_TickReturnsSuccess()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        var tree = new ActionNode("Success", _ => NodeStatus.Success);
        engine.SetBehaviorTree(tree);
        var context = CreateMockContext();

        NodeStatus result = engine.Tick(context);

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void Engine_TickWithException_ReturnsFailureAndLogs()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        var tree = new ActionNode("Throws", _ => throw new InvalidOperationException("test error"));
        engine.SetBehaviorTree(tree);
        var context = CreateMockContext();

        NodeStatus result = engine.Tick(context);

        result.Should().Be(NodeStatus.Failure);
    }

    [Fact]
    public void Engine_Reset_ClearsTreeState()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        bool wasReset = false;

        var testNode = new CustomResettableNode("Test", () => wasReset = true);
        engine.SetBehaviorTree(testNode);

        engine.Reset();

        wasReset.Should().BeTrue();
    }

    [Fact]
    public void Engine_WithSimpleFailureTree_TickReturnsFailure()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        var tree = new ActionNode("AlwaysFail", _ => NodeStatus.Failure);
        engine.SetBehaviorTree(tree);
        var context = CreateMockContext();

        NodeStatus result = engine.Tick(context);

        result.Should().Be(NodeStatus.Failure);
    }

    #endregion

    #region Combat Scenario Tests

    [Fact]
    public void CombatScenario_SimplePullSequence_ExecutesInOrder()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        List<int> executionOrder = [];

        SequenceNode pullSeq = new("PullSequence");
        pullSeq.Children.Add(new ActionNode("CheckTarget", ctx =>
        {
            executionOrder.Add(1);
            return ctx.CurrentTarget != null ? NodeStatus.Success : NodeStatus.Failure;
        }));
        pullSeq.Children.Add(new ActionNode("Cast", ctx =>
        {
            executionOrder.Add(2);
            return NodeStatus.Success;
        }));
        pullSeq.Children.Add(new ActionNode("Wait", ctx =>
        {
            executionOrder.Add(3);
            return NodeStatus.Success;
        }));

        engine.SetBehaviorTree(pullSeq);
        var context = CreateMockContext(targetHealth: 100f);

        NodeStatus result = engine.Tick(context);

        result.Should().Be(NodeStatus.Success);
        executionOrder.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void CombatScenario_MultipleTicksWithState_PersistsState()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());

        SequenceNode tree = new("Combat");
        tree.Children.Add(new ActionNode("Tick1", ctx =>
        {
            int currentCount = (int?)ctx.State.GetValueOrDefault("roundCount", 0) ?? 0;
            ctx.State["roundCount"] = currentCount + 1;
            return NodeStatus.Success;
        }));

        engine.SetBehaviorTree(tree);
        var context = CreateMockContext();

        engine.Tick(context);
        engine.Tick(context);
        engine.Tick(context);

        context.State["roundCount"].Should().Be(3);
    }

    [Fact]
    public void CombatScenario_RotationWithConditions_SkipsOnFailedCondition()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        int castCount = 0;

        // Rotation: Check enemies -> Spell1 -> Fallback
        SelectorNode rotation = new("Rotation");

        SequenceNode spell1Seq = new("Spell1Sequence");
        spell1Seq.Children.Add(new ConditionNode("HasEnemies", ctx => ctx.NearbyEnemies > 0));
        spell1Seq.Children.Add(new ActionNode("CastSpell1", ctx =>
        {
            castCount++;
            return NodeStatus.Success;
        }));

        rotation.Children.Add(spell1Seq);
        rotation.Children.Add(new ActionNode("Fallback", _ => NodeStatus.Success));

        engine.SetBehaviorTree(rotation);
        var context = CreateMockContext(nearbyEnemies: 0);

        engine.Tick(context);

        castCount.Should().Be(0, "spell should not cast with no enemies");
    }

    [Fact]
    public void CombatScenario_LowHealthRetreat_ExecutesWhenHealthLow()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        bool retreatCalled = false;

        SelectorNode root = new("CombatRoot");

        SequenceNode emergencySeq = new("Emergency");
        emergencySeq.Children.Add(new ConditionNode("IsLowHealth", ctx => ctx.Player != null ? ctx.Player.HealthPercent() < 30 : false));
        emergencySeq.Children.Add(new ActionNode("Retreat", ctx =>
        {
            retreatCalled = true;
            return NodeStatus.Success;
        }));

        root.Children.Add(emergencySeq);
        root.Children.Add(new ActionNode("NormalCombat", _ => NodeStatus.Success));

        engine.SetBehaviorTree(root);
        var context = CreateMockContext(playerHealth: 25f);

        NodeStatus result = engine.Tick(context);

        result.Should().Be(NodeStatus.Success);
        retreatCalled.Should().BeTrue();
    }

    [Fact]
    public void CombatScenario_MultiTargetDetection_RetreatWhenMultiple()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        bool retreatTriggered = false;

        SelectorNode root = new("Root");

        SequenceNode multiTargetSeq = new("MultiTargetCheck");
        multiTargetSeq.Children.Add(new ConditionNode("TooManyEnemies",
            ctx => ctx.NearbyEnemies > 2));
        multiTargetSeq.Children.Add(new ActionNode("Retreat", _ =>
        {
            retreatTriggered = true;
            return NodeStatus.Success;
        }));

        root.Children.Add(multiTargetSeq);
        root.Children.Add(new ActionNode("AttackNormal", _ => NodeStatus.Success));

        engine.SetBehaviorTree(root);
        var context = CreateMockContext(nearbyEnemies: 3);

        engine.Tick(context);

        retreatTriggered.Should().BeTrue();
    }

    [Fact]
    public void CombatScenario_TargetSwitching_ExecutesNewSequence()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        List<string> targetSequence = [];

        SelectorNode root = new("Root");
        root.Children.Add(new ActionNode("CheckTarget", ctx =>
        {
            if (ctx.CurrentTarget != null)
            {
                targetSequence.Add($"Target@{ctx.CurrentTarget.HealthPercent}hp");
                return NodeStatus.Success;
            }
            return NodeStatus.Failure;
        }));

        engine.SetBehaviorTree(root);

        var context1 = CreateMockContext(targetHealth: 100f);
        engine.Tick(context1);

        var context2 = CreateMockContext(targetHealth: 50f);
        engine.Tick(context2);

        targetSequence.Should().Contain("Target@100hp");
        targetSequence.Should().Contain("Target@50hp");
    }

    [Fact]
    public void CombatScenario_CombatWithCooldowns_WaitsForCooldown()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        int castAttempts = 0;

        SequenceNode tree = new("Combat");
        tree.Children.Add(new ActionNode("CheckCooldown", ctx =>
        {
            double elapsed = ctx.ElapsedMs;
            return elapsed >= 5000 ? NodeStatus.Success : NodeStatus.Running;
        }));
        tree.Children.Add(new ActionNode("Cast", ctx =>
        {
            castAttempts++;
            return NodeStatus.Success;
        }));

        engine.SetBehaviorTree(tree);

        var context = CreateMockContext();
        context.ElapsedMs = 2000;
        engine.Tick(context);

        castAttempts.Should().Be(0, "should not cast while cooldown active");

        context.ElapsedMs = 6000;
        engine.Tick(context);

        castAttempts.Should().Be(1, "should cast after cooldown");
    }

    [Fact]
    public void CombatScenario_NestedSelectors_FallbackBehavior()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        List<string> executed = [];

        SelectorNode root = new("Root");

        SelectorNode firstChoice = new("FirstChoice");
        firstChoice.Children.Add(new ActionNode("Option1", _ =>
        {
            executed.Add("Option1");
            return NodeStatus.Failure;
        }));
        firstChoice.Children.Add(new ActionNode("Option2", _ =>
        {
            executed.Add("Option2");
            return NodeStatus.Success;
        }));

        root.Children.Add(firstChoice);
        root.Children.Add(new ActionNode("Fallback", _ =>
        {
            executed.Add("Fallback");
            return NodeStatus.Success;
        }));

        engine.SetBehaviorTree(root);
        engine.Tick(CreateMockContext());

        executed.Should().Equal("Option1", "Option2");
        executed.Should().NotContain("Fallback");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void ErrorHandling_NodeThrowsException_EngineHandlesGracefully()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());

        SelectorNode root = new("Root");
        root.Children.Add(new ActionNode("ThrowingNode", _ =>
            throw new InvalidOperationException("Intentional error")));
        root.Children.Add(new ActionNode("Fallback", _ => NodeStatus.Success));

        engine.SetBehaviorTree(root);

        var context = CreateMockContext();
        NodeStatus result = engine.Tick(context);

        result.Should().Be(NodeStatus.Failure, "engine handles exception and returns failure");
    }

    [Fact]
    public void ErrorHandling_NestedNodeThrowsException_EngineHandlesGracefully()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());

        SequenceNode tree = new("Root");
        tree.Children.Add(new ActionNode("Step1", _ => NodeStatus.Success));
        tree.Children.Add(new ActionNode("ThrowingStep", _ =>
            throw new ArgumentNullException("Intentional nested error")));
        tree.Children.Add(new ActionNode("Step3", _ => NodeStatus.Success));

        engine.SetBehaviorTree(tree);

        var context = CreateMockContext();
        NodeStatus result = engine.Tick(context);

        result.Should().Be(NodeStatus.Failure, "nested exception should propagate to engine");
    }

    #endregion

    #region Performance Baseline Tests

    [Fact]
    public void Performance_SimplePull_Executes_LessThan100ms()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());

        SequenceNode tree = new("Pull");
        for (int i = 0; i < 10; i++)
        {
            tree.Children.Add(new ActionNode($"Action{i}", _ => NodeStatus.Success));
        }

        engine.SetBehaviorTree(tree);
        var context = CreateMockContext();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            engine.Tick(context);
        }
        watch.Stop();

        watch.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    [Fact]
    public void Performance_DeepTree_Handles_ComplexNesting()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());

        // Create deep tree: 3 levels deep, 5 branches each
        SelectorNode root = new("Level1");
        for (int i = 0; i < 5; i++)
        {
            SequenceNode level2 = new($"Level2_{i}");
            for (int j = 0; j < 5; j++)
            {
                SelectorNode level3 = new($"Level3_{i}_{j}");
                level3.Children.Add(new ActionNode("LeafFail", _ => NodeStatus.Failure));
                level3.Children.Add(new ActionNode("LeafSuccess", _ => NodeStatus.Success));
                level2.Children.Add(level3);
            }
            root.Children.Add(level2);
        }

        engine.SetBehaviorTree(root);
        var context = CreateMockContext();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        NodeStatus result = engine.Tick(context);
        watch.Stop();

        result.Should().Be(NodeStatus.Success);
        watch.ElapsedMilliseconds.Should().BeLessThan(50, "deep tree should still execute quickly");
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void StateManagement_TreePreservesStateAcrossTicks()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());

        SequenceNode tree = new("Counter");
        tree.Children.Add(new ActionNode("Increment", ctx =>
        {
            int count = (int?)ctx.State.GetValueOrDefault("count", 0) ?? 0;
            ctx.State["count"] = count + 1;
            return NodeStatus.Success;
        }));

        engine.SetBehaviorTree(tree);
        var context = CreateMockContext();

        engine.Tick(context);
        engine.Tick(context);
        engine.Tick(context);
        engine.Tick(context);
        engine.Tick(context);

        context.State["count"].Should().Be(5);
    }

    [Fact]
    public void StateManagement_ResetClearsTreeState()
    {
        var engine = new BehaviorTreeCombatEngine(new NullLogger<BehaviorTreeCombatEngine>());
        int resetCount = 0;

        SequenceNode tree = new("Counter");
        tree.Children.Add(new CustomResettableNode("Tracker", () => resetCount++));

        engine.SetBehaviorTree(tree);
        var context = CreateMockContext();

        engine.Tick(context);
        engine.Reset();

        resetCount.Should().Be(1, "reset should call Reset on nodes");
    }

    #endregion

    #region Factory Tests

    [Fact]
    public void Factory_CreatesNewEngineInstances()
    {
        var factory = new BehaviorTreeCombatEngineFactory(new NullLoggerFactory());

        var engine1 = factory.CreateEngine();
        var engine2 = factory.CreateEngine();

        engine1.Should().NotBeSameAs(engine2);
    }

    #endregion

    #region Helper Node Types

    /// <summary>
    /// Custom node that supports reset callback for testing state cleanup.
    /// </summary>
    private sealed class CustomResettableNode(string name, Action onReset) : IBehaviorNode
    {
        public string Name { get; } = name;

        public NodeStatus Execute(BehaviorContext context) => NodeStatus.Success;

        public void Reset() => onReset();
    }

    #endregion
}
