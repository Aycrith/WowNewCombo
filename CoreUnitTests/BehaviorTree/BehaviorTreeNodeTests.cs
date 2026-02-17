namespace CoreUnitTests.BehaviorTree;

using System;
using System.Collections.Generic;
using System.Numerics;

using Core.BehaviorTree;
using Core.BehaviorTree.Nodes;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

public sealed class BehaviorTreeNodeTests
{
    private static BehaviorContext CreateTestContext() => new()
    {
        Player = null!,
        Casting = null!,
        StopMoving = null!,
        Input = null!,
        Logger = NullLogger.Instance
    };

    #region SelectorNode Tests

    [Fact]
    public void Selector_ReturnsSuccess_WhenFirstChildSucceeds()
    {
        SelectorNode selector = new("TestSelector");
        selector.Children.Add(new ActionNode("AlwaysSuccess", _ => NodeStatus.Success));
        selector.Children.Add(new ActionNode("NeverReached", _ => NodeStatus.Failure));

        NodeStatus result = selector.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void Selector_ReturnsSuccess_WhenSecondChildSucceedsAfterFirstFails()
    {
        SelectorNode selector = new("TestSelector");
        selector.Children.Add(new ActionNode("Fail", _ => NodeStatus.Failure));
        selector.Children.Add(new ActionNode("Succeed", _ => NodeStatus.Success));

        NodeStatus result = selector.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void Selector_ReturnsFailure_WhenAllChildrenFail()
    {
        SelectorNode selector = new("TestSelector");
        selector.Children.Add(new ActionNode("Fail1", _ => NodeStatus.Failure));
        selector.Children.Add(new ActionNode("Fail2", _ => NodeStatus.Failure));
        selector.Children.Add(new ActionNode("Fail3", _ => NodeStatus.Failure));

        NodeStatus result = selector.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Failure);
    }

    [Fact]
    public void Selector_ReturnsRunning_WhenChildReturnsRunning()
    {
        SelectorNode selector = new("TestSelector");
        selector.Children.Add(new ActionNode("Fail", _ => NodeStatus.Failure));
        selector.Children.Add(new ActionNode("Running", _ => NodeStatus.Running));
        selector.Children.Add(new ActionNode("NeverReached", _ => NodeStatus.Success));

        NodeStatus result = selector.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Running);
    }

    [Fact]
    public void Selector_SkipsRemainingChildren_AfterSuccess()
    {
        int executionCount = 0;
        SelectorNode selector = new("TestSelector");
        selector.Children.Add(new ActionNode("Succeed", _ =>
        {
            executionCount++;
            return NodeStatus.Success;
        }));
        selector.Children.Add(new ActionNode("ShouldNotRun", _ =>
        {
            executionCount++;
            return NodeStatus.Success;
        }));

        selector.Execute(CreateTestContext());

        executionCount.Should().Be(1);
    }

    [Fact]
    public void Selector_ReturnsFailure_WhenEmpty()
    {
        SelectorNode selector = new("EmptySelector");

        NodeStatus result = selector.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Failure);
    }

    [Fact]
    public void Selector_Reset_PropagatesToAllChildren()
    {
        int resetCount = 0;
        ResettableNode child1 = new("Child1", () => resetCount++);
        ResettableNode child2 = new("Child2", () => resetCount++);
        SelectorNode selector = new("TestSelector");
        selector.Children.Add(child1);
        selector.Children.Add(child2);

        selector.Reset();

        resetCount.Should().Be(2);
    }

    [Fact]
    public void Selector_Name_IsSetCorrectly()
    {
        SelectorNode selector = new("MySelectorName");

        selector.Name.Should().Be("MySelectorName");
    }

    #endregion

    #region SequenceNode Tests

    [Fact]
    public void Sequence_ReturnsSuccess_WhenAllChildrenSucceed()
    {
        SequenceNode sequence = new("TestSequence");
        sequence.Children.Add(new ActionNode("S1", _ => NodeStatus.Success));
        sequence.Children.Add(new ActionNode("S2", _ => NodeStatus.Success));
        sequence.Children.Add(new ActionNode("S3", _ => NodeStatus.Success));

        NodeStatus result = sequence.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void Sequence_ReturnsFailure_WhenAnyChildFails()
    {
        SequenceNode sequence = new("TestSequence");
        sequence.Children.Add(new ActionNode("S1", _ => NodeStatus.Success));
        sequence.Children.Add(new ActionNode("Fail", _ => NodeStatus.Failure));
        sequence.Children.Add(new ActionNode("S3", _ => NodeStatus.Success));

        NodeStatus result = sequence.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Failure);
    }

    [Fact]
    public void Sequence_ReturnsRunning_WhenChildReturnsRunning()
    {
        SequenceNode sequence = new("TestSequence");
        sequence.Children.Add(new ActionNode("S1", _ => NodeStatus.Success));
        sequence.Children.Add(new ActionNode("Running", _ => NodeStatus.Running));

        NodeStatus result = sequence.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Running);
    }

    [Fact]
    public void Sequence_StopsExecution_AfterFirstFailure()
    {
        int executionCount = 0;
        SequenceNode sequence = new("TestSequence");
        sequence.Children.Add(new ActionNode("Fail", _ =>
        {
            executionCount++;
            return NodeStatus.Failure;
        }));
        sequence.Children.Add(new ActionNode("ShouldNotRun", _ =>
        {
            executionCount++;
            return NodeStatus.Success;
        }));

        sequence.Execute(CreateTestContext());

        executionCount.Should().Be(1);
    }

    [Fact]
    public void Sequence_ReturnsSuccess_WhenEmpty()
    {
        SequenceNode sequence = new("EmptySequence");

        NodeStatus result = sequence.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void Sequence_Reset_PropagatesToAllChildren()
    {
        int resetCount = 0;
        ResettableNode child1 = new("Child1", () => resetCount++);
        ResettableNode child2 = new("Child2", () => resetCount++);
        ResettableNode child3 = new("Child3", () => resetCount++);
        SequenceNode sequence = new("TestSequence");
        sequence.Children.Add(child1);
        sequence.Children.Add(child2);
        sequence.Children.Add(child3);

        sequence.Reset();

        resetCount.Should().Be(3);
    }

    [Fact]
    public void Sequence_ExecutesAllChildren_InOrder()
    {
        List<int> executionOrder = [];
        SequenceNode sequence = new("TestSequence");
        sequence.Children.Add(new ActionNode("First", _ =>
        {
            executionOrder.Add(1);
            return NodeStatus.Success;
        }));
        sequence.Children.Add(new ActionNode("Second", _ =>
        {
            executionOrder.Add(2);
            return NodeStatus.Success;
        }));
        sequence.Children.Add(new ActionNode("Third", _ =>
        {
            executionOrder.Add(3);
            return NodeStatus.Success;
        }));

        sequence.Execute(CreateTestContext());

        executionOrder.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Sequence_Name_IsSetCorrectly()
    {
        SequenceNode sequence = new("MySequenceName");

        sequence.Name.Should().Be("MySequenceName");
    }

    #endregion

    #region ConditionNode Tests

    [Fact]
    public void Condition_ReturnsSuccess_WhenConditionIsTrue()
    {
        ConditionNode condition = new("TrueCondition", _ => true);

        NodeStatus result = condition.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void Condition_ReturnsFailure_WhenConditionIsFalse()
    {
        ConditionNode condition = new("FalseCondition", _ => false);

        NodeStatus result = condition.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Failure);
    }

    [Fact]
    public void Condition_ReceivesContext()
    {
        BehaviorContext context = CreateTestContext();
        context.NearbyEnemies = 5;
        ConditionNode condition = new("CheckEnemies", ctx => ctx.NearbyEnemies > 3);

        NodeStatus result = condition.Execute(context);

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void Condition_Reset_DoesNotThrow()
    {
        ConditionNode condition = new("StatelessCondition", _ => true);

        Action act = () => condition.Reset();

        act.Should().NotThrow();
    }

    [Fact]
    public void Condition_Name_IsSetCorrectly()
    {
        ConditionNode condition = new("MyCondition", _ => true);

        condition.Name.Should().Be("MyCondition");
    }

    #endregion

    #region InverterNode Tests

    [Fact]
    public void Inverter_ReturnsFailure_WhenChildReturnsSuccess()
    {
        ActionNode child = new("AlwaysSuccess", _ => NodeStatus.Success);
        InverterNode inverter = new("TestInverter", child);

        NodeStatus result = inverter.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Failure);
    }

    [Fact]
    public void Inverter_ReturnsSuccess_WhenChildReturnsFailure()
    {
        ActionNode child = new("AlwaysFail", _ => NodeStatus.Failure);
        InverterNode inverter = new("TestInverter", child);

        NodeStatus result = inverter.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void Inverter_ReturnsRunning_WhenChildReturnsRunning()
    {
        ActionNode child = new("AlwaysRunning", _ => NodeStatus.Running);
        InverterNode inverter = new("TestInverter", child);

        NodeStatus result = inverter.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Running);
    }

    [Fact]
    public void Inverter_Reset_PropagatesToChild()
    {
        bool wasReset = false;
        ResettableNode child = new("Child", () => wasReset = true);
        InverterNode inverter = new("TestInverter", child);

        inverter.Reset();

        wasReset.Should().BeTrue();
    }

    [Fact]
    public void Inverter_Name_IsSetCorrectly()
    {
        ActionNode child = new("Child", _ => NodeStatus.Success);
        InverterNode inverter = new("MyInverter", child);

        inverter.Name.Should().Be("MyInverter");
    }

    [Fact]
    public void Inverter_Child_IsExposed()
    {
        ActionNode child = new("Child", _ => NodeStatus.Success);
        InverterNode inverter = new("TestInverter", child);

        inverter.Child.Should().BeSameAs(child);
    }

    #endregion

    #region ActionNode Tests

    [Fact]
    public void Action_ReturnsSuccess_WhenActionSucceeds()
    {
        ActionNode action = new("SuccessAction", _ => NodeStatus.Success);

        NodeStatus result = action.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void Action_ReturnsFailure_WhenActionFails()
    {
        ActionNode action = new("FailAction", _ => NodeStatus.Failure);

        NodeStatus result = action.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Failure);
    }

    [Fact]
    public void Action_ReturnsRunning_WhenActionReturnsRunning()
    {
        ActionNode action = new("RunningAction", _ => NodeStatus.Running);

        NodeStatus result = action.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Running);
    }

    [Fact]
    public void Action_ReturnsFailure_WhenActionThrows()
    {
        ActionNode action = new("ThrowingAction", _ => throw new InvalidOperationException("test error"));

        NodeStatus result = action.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Failure);
    }

    [Fact]
    public void Action_Reset_DoesNotThrow()
    {
        ActionNode action = new("StatelessAction", _ => NodeStatus.Success);

        Action act = () => action.Reset();

        act.Should().NotThrow();
    }

    [Fact]
    public void Action_ReceivesContext_AndCanModifyState()
    {
        BehaviorContext context = CreateTestContext();
        ActionNode action = new("ModifyState", ctx =>
        {
            ctx.State["executed"] = true;
            return NodeStatus.Success;
        });

        action.Execute(context);

        context.State.Should().ContainKey("executed");
        context.State["executed"].Should().Be(true);
    }

    #endregion

    #region BehaviorContext Tests

    [Fact]
    public void Context_StateDictionary_IsMutable()
    {
        BehaviorContext context = CreateTestContext();

        context.State["key1"] = "value1";
        context.State["key2"] = 42;

        context.State.Should().HaveCount(2);
        context.State["key1"].Should().Be("value1");
        context.State["key2"].Should().Be(42);
    }

    [Fact]
    public void Context_NearbyEnemies_CanBeSet()
    {
        BehaviorContext context = CreateTestContext();

        context.NearbyEnemies = 7;

        context.NearbyEnemies.Should().Be(7);
    }

    [Fact]
    public void Context_ElapsedMs_CanBeSet()
    {
        BehaviorContext context = CreateTestContext();

        context.ElapsedMs = 123.456;

        context.ElapsedMs.Should().Be(123.456);
    }

    [Fact]
    public void Context_CurrentTarget_CanBeSetAndCleared()
    {
        BehaviorContext context = CreateTestContext();

        context.CurrentTarget = new TargetInfo { HealthPercent = 50f, Level = 10 };
        context.CurrentTarget.Should().NotBeNull();
        context.CurrentTarget!.HealthPercent.Should().Be(50f);

        context.CurrentTarget = null;
        context.CurrentTarget.Should().BeNull();
    }

    #endregion

    #region TargetInfo Tests

    [Fact]
    public void TargetInfo_PropertiesInitializeCorrectly()
    {
        TargetInfo target = new()
        {
            HealthPercent = 75.5f,
            Level = 60,
            IsDead = false,
            IsElite = true,
            Distance = 30.0f,
            Position = new Vector3(100f, 200f, 300f)
        };

        target.HealthPercent.Should().Be(75.5f);
        target.Level.Should().Be(60);
        target.IsDead.Should().BeFalse();
        target.IsElite.Should().BeTrue();
        target.Distance.Should().Be(30.0f);
        target.Position.Should().Be(new Vector3(100f, 200f, 300f));
    }

    [Fact]
    public void TargetInfo_DefaultValues()
    {
        TargetInfo target = new();

        target.HealthPercent.Should().Be(0f);
        target.Level.Should().Be(0);
        target.IsDead.Should().BeFalse();
        target.IsElite.Should().BeFalse();
        target.Distance.Should().Be(0f);
        target.Position.Should().Be(Vector3.Zero);
    }

    #endregion

    #region Nested Tree Tests

    [Fact]
    public void NestedTree_SelectorContainingSequences_FirstSequenceSucceeds()
    {
        // Selector tries sequences in order; first sequence succeeds fully
        SequenceNode seq1 = new("Sequence1");
        seq1.Children.Add(new ActionNode("S1A", _ => NodeStatus.Success));
        seq1.Children.Add(new ActionNode("S1B", _ => NodeStatus.Success));

        SequenceNode seq2 = new("Sequence2");
        seq2.Children.Add(new ActionNode("S2A", _ => NodeStatus.Failure));

        SelectorNode root = new("Root");
        root.Children.Add(seq1);
        root.Children.Add(seq2);

        NodeStatus result = root.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void NestedTree_SelectorContainingSequences_FallsToSecondSequence()
    {
        // First sequence fails, selector tries second which succeeds
        SequenceNode seq1 = new("Sequence1");
        seq1.Children.Add(new ActionNode("S1A", _ => NodeStatus.Success));
        seq1.Children.Add(new ActionNode("S1B", _ => NodeStatus.Failure));

        SequenceNode seq2 = new("Sequence2");
        seq2.Children.Add(new ActionNode("S2A", _ => NodeStatus.Success));

        SelectorNode root = new("Root");
        root.Children.Add(seq1);
        root.Children.Add(seq2);

        NodeStatus result = root.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void NestedTree_SequenceContainingSelectors_AllSelectorsSucceed()
    {
        SelectorNode sel1 = new("Selector1");
        sel1.Children.Add(new ActionNode("Fail", _ => NodeStatus.Failure));
        sel1.Children.Add(new ActionNode("Succeed", _ => NodeStatus.Success));

        SelectorNode sel2 = new("Selector2");
        sel2.Children.Add(new ActionNode("Succeed", _ => NodeStatus.Success));

        SequenceNode root = new("Root");
        root.Children.Add(sel1);
        root.Children.Add(sel2);

        NodeStatus result = root.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void NestedTree_SequenceWithConditionAndAction()
    {
        BehaviorContext context = CreateTestContext();
        context.NearbyEnemies = 3;

        SequenceNode sequence = new("AttackSequence");
        sequence.Children.Add(new ConditionNode("HasEnemies", ctx => ctx.NearbyEnemies > 0));
        sequence.Children.Add(new ActionNode("Attack", ctx =>
        {
            ctx.State["attacked"] = true;
            return NodeStatus.Success;
        }));

        NodeStatus result = sequence.Execute(context);

        result.Should().Be(NodeStatus.Success);
        context.State["attacked"].Should().Be(true);
    }

    [Fact]
    public void NestedTree_SequenceWithFailingCondition_SkipsAction()
    {
        BehaviorContext context = CreateTestContext();
        context.NearbyEnemies = 0;

        SequenceNode sequence = new("AttackSequence");
        sequence.Children.Add(new ConditionNode("HasEnemies", ctx => ctx.NearbyEnemies > 0));
        sequence.Children.Add(new ActionNode("Attack", ctx =>
        {
            ctx.State["attacked"] = true;
            return NodeStatus.Success;
        }));

        NodeStatus result = sequence.Execute(context);

        result.Should().Be(NodeStatus.Failure);
        context.State.Should().NotContainKey("attacked");
    }

    [Fact]
    public void NestedTree_InverterInSelector()
    {
        // Inverter makes a succeeding child appear as failure,
        // so selector falls through to second child
        InverterNode invertedSuccess = new("InvertSuccess",
            new ActionNode("Succeed", _ => NodeStatus.Success));

        SelectorNode selector = new("Root");
        selector.Children.Add(invertedSuccess);
        selector.Children.Add(new ActionNode("Fallback", _ => NodeStatus.Success));

        List<int> executionOrder = [];
        // Rebuild with tracking
        SelectorNode tracked = new("Root");
        tracked.Children.Add(new InverterNode("InvertSuccess",
            new ActionNode("Succeed", _ =>
            {
                executionOrder.Add(1);
                return NodeStatus.Success;
            })));
        tracked.Children.Add(new ActionNode("Fallback", _ =>
        {
            executionOrder.Add(2);
            return NodeStatus.Success;
        }));

        NodeStatus result = tracked.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Success);
        executionOrder.Should().Equal(1, 2);
    }

    [Fact]
    public void NestedTree_DeepNesting_ThreeLevels()
    {
        // Root selector -> sequence -> selector -> condition
        ConditionNode deepCondition = new("DeepCheck", _ => true);

        SelectorNode innerSelector = new("InnerSelector");
        innerSelector.Children.Add(new ActionNode("Fail", _ => NodeStatus.Failure));
        innerSelector.Children.Add(deepCondition);

        SequenceNode middleSequence = new("MiddleSequence");
        middleSequence.Children.Add(innerSelector);
        middleSequence.Children.Add(new ActionNode("FinalAction", _ => NodeStatus.Success));

        SelectorNode root = new("Root");
        root.Children.Add(middleSequence);

        NodeStatus result = root.Execute(CreateTestContext());

        result.Should().Be(NodeStatus.Success);
    }

    [Fact]
    public void NestedTree_Reset_PropagatesToEntireTree()
    {
        int resetCount = 0;
        ResettableNode leaf1 = new("Leaf1", () => resetCount++);
        ResettableNode leaf2 = new("Leaf2", () => resetCount++);
        ResettableNode leaf3 = new("Leaf3", () => resetCount++);

        SequenceNode sequence = new("Seq");
        sequence.Children.Add(leaf1);
        sequence.Children.Add(leaf2);

        SelectorNode root = new("Root");
        root.Children.Add(sequence);
        root.Children.Add(leaf3);

        root.Reset();

        resetCount.Should().Be(3);
    }

    #endregion

    #region Helper Types

    /// <summary>
    /// Test helper node that tracks reset calls.
    /// </summary>
    private sealed class ResettableNode(string name, Action onReset) : IBehaviorNode
    {
        public string Name { get; } = name;

        public NodeStatus Execute(BehaviorContext context) => NodeStatus.Success;

        public void Reset() => onReset();
    }

    #endregion
}
