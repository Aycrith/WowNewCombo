using Core.Diagnostics;
using FluentAssertions;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace CoreUnitTests.Diagnostics;

/// <summary>
/// Tests for GoapEventHistory and ExecutionTracer diagnostic systems.
/// </summary>
public class DiagnosticsTests
{
    #region GoapEventHistory - Empty State

    [Fact]
    public void GoapEventHistory_NewInstance_HasZeroTransitions()
    {
        // Arrange
        GoapEventHistory history = new();

        // Assert
        history.TotalTransitions.Should().Be(0);
        history.TotalNoPlanEvents.Should().Be(0);
    }

    [Fact]
    public void GoapEventHistory_GetRecentTransitions_EmptyReturnsEmptyList()
    {
        // Arrange
        GoapEventHistory history = new();

        // Act
        List<GoalTransitionEntry> result = history.GetRecentTransitions(10);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GoapEventHistory_GetNoPlanEvents_EmptyReturnsEmptyList()
    {
        // Arrange
        GoapEventHistory history = new();

        // Act
        List<NoPlanEventEntry> result = history.GetNoPlanEvents(5);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GoapEventHistory_GetPreconditionFailures_EmptyReturnsEmptyList()
    {
        // Arrange
        GoapEventHistory history = new();

        // Act
        List<PreconditionFailureEntry> result = history.GetPreconditionFailures(5);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GoapEventHistory - Adding and Retrieving Events

    [Fact]
    public void GoapEventHistory_RecordTransition_IncrementsTotalAndIsRetrievable()
    {
        // Arrange
        GoapEventHistory history = new();

        // Act
        history.RecordTransition("GoalA", "GoalB", "higher priority", 2);

        // Assert
        history.TotalTransitions.Should().Be(1);
        List<GoalTransitionEntry> transitions = history.GetRecentTransitions(10);
        transitions.Should().HaveCount(1);
        transitions[0].FromGoal.Should().Be("GoalA");
        transitions[0].ToGoal.Should().Be("GoalB");
        transitions[0].Reason.Should().Be("higher priority");
        transitions[0].PlanDepth.Should().Be(2);
    }

    [Fact]
    public void GoapEventHistory_RecordTransition_NullFromGoalIsAllowed()
    {
        // Arrange
        GoapEventHistory history = new();

        // Act
        history.RecordTransition(null, "GoalA", "initial", 1);

        // Assert
        List<GoalTransitionEntry> transitions = history.GetRecentTransitions(1);
        transitions.Should().HaveCount(1);
        transitions[0].FromGoal.Should().BeNull();
    }

    [Fact]
    public void GoapEventHistory_RecordNoPlanEvent_IncrementsTotalAndIsRetrievable()
    {
        // Arrange
        GoapEventHistory history = new();
        List<string> goals = ["CombatGoal", "LootGoal", "FollowRoute"];

        // Act
        history.RecordNoPlanEvent("HP=50,Mana=30", goals, "no valid plan");

        // Assert
        history.TotalNoPlanEvents.Should().Be(1);
        List<NoPlanEventEntry> events = history.GetNoPlanEvents(10);
        events.Should().HaveCount(1);
        events[0].WorldState.Should().Be("HP=50,Mana=30");
        events[0].AvailableGoals.Should().BeEquivalentTo(goals);
        events[0].Context.Should().Be("no valid plan");
    }

    [Fact]
    public void GoapEventHistory_RecordPreconditionFailure_IsRetrievable()
    {
        // Arrange
        GoapEventHistory history = new();

        // Act
        history.RecordPreconditionFailure("CombatGoal", "HasTarget", "true", "false");

        // Assert
        List<PreconditionFailureEntry> failures = history.GetPreconditionFailures(10);
        failures.Should().HaveCount(1);
        failures[0].GoalName.Should().Be("CombatGoal");
        failures[0].FailedPrecondition.Should().Be("HasTarget");
        failures[0].Expected.Should().Be("true");
        failures[0].Actual.Should().Be("false");
    }

    [Fact]
    public void GoapEventHistory_GetRecentTransitions_ReturnsOnlyRequestedCount()
    {
        // Arrange
        GoapEventHistory history = new();
        for (int i = 0; i < 10; i++)
        {
            history.RecordTransition($"Goal{i}", $"Goal{i + 1}", "reason", 1);
        }

        // Act
        List<GoalTransitionEntry> result = history.GetRecentTransitions(3);

        // Assert
        result.Should().HaveCount(3);
        // Should return the last 3 (most recent)
        result[2].ToGoal.Should().Be("Goal10");
    }

    #endregion

    #region GoapEventHistory - Ring Buffer / FIFO Eviction

    [Fact]
    public void GoapEventHistory_TransitionsExceedCapacity_EvictsOldest()
    {
        // Arrange - transition capacity is 1000
        GoapEventHistory history = new();

        // Act - add more than capacity
        for (int i = 0; i < 1050; i++)
        {
            history.RecordTransition(null, $"Goal{i}", "reason", 1);
        }

        // Assert - total count tracks all, but stored should be around 1000
        history.TotalTransitions.Should().Be(1050);
        List<GoalTransitionEntry> transitions = history.GetRecentTransitions(2000);
        transitions.Count.Should().BeLessOrEqualTo(1000);
    }

    [Fact]
    public void GoapEventHistory_NoPlanEventsExceedCapacity_EvictsOldest()
    {
        // Arrange - noPlanEvents capacity is 100
        GoapEventHistory history = new();

        // Act
        for (int i = 0; i < 120; i++)
        {
            history.RecordNoPlanEvent($"state{i}", ["goal"], "ctx");
        }

        // Assert
        history.TotalNoPlanEvents.Should().Be(120);
        List<NoPlanEventEntry> events = history.GetNoPlanEvents(200);
        events.Count.Should().BeLessOrEqualTo(100);
    }

    [Fact]
    public void GoapEventHistory_PreconditionFailuresExceedCapacity_EvictsOldest()
    {
        // Arrange - preconditionFailures capacity is 500
        GoapEventHistory history = new();

        // Act
        for (int i = 0; i < 550; i++)
        {
            history.RecordPreconditionFailure($"Goal{i}", "precond", "expected", "actual");
        }

        // Assert
        List<PreconditionFailureEntry> failures = history.GetPreconditionFailures(600);
        failures.Count.Should().BeLessOrEqualTo(500);
    }

    #endregion

    #region GoapEventHistory - Clear

    [Fact]
    public void GoapEventHistory_Clear_RemovesAllEntriesAndResetsCounters()
    {
        // Arrange
        GoapEventHistory history = new();
        history.RecordTransition("A", "B", "reason", 1);
        history.RecordNoPlanEvent("state", ["goal"], "ctx");
        history.RecordPreconditionFailure("goal", "precond", "exp", "act");

        // Act
        history.Clear();

        // Assert
        history.TotalTransitions.Should().Be(0);
        history.TotalNoPlanEvents.Should().Be(0);
        history.GetRecentTransitions(10).Should().BeEmpty();
        history.GetNoPlanEvents(10).Should().BeEmpty();
        history.GetPreconditionFailures(10).Should().BeEmpty();
    }

    #endregion

    #region GoapEventHistory - Thread Safety

    [Fact]
    public async Task GoapEventHistory_ConcurrentWrites_DoNotThrow()
    {
        // Arrange
        GoapEventHistory history = new();
        int itemsPerThread = 100;
        int threadCount = 4;

        // Act - concurrent writes from multiple threads
        Task[] tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    history.RecordTransition($"From{threadId}", $"To{threadId}_{i}", "concurrent", 1);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert - all items recorded
        history.TotalTransitions.Should().Be(threadCount * itemsPerThread);
    }

    [Fact]
    public async Task GoapEventHistory_ConcurrentReadsAndWrites_DoNotThrow()
    {
        // Arrange
        GoapEventHistory history = new();

        // Act - concurrent reads and writes
        Task writeTask = Task.Run(() =>
        {
            for (int i = 0; i < 200; i++)
            {
                history.RecordTransition(null, $"Goal{i}", "reason", 1);
            }
        });

        Task readTask = Task.Run(() =>
        {
            for (int i = 0; i < 200; i++)
            {
                _ = history.GetRecentTransitions(10);
            }
        });

        // Should not throw
        await Task.WhenAll(writeTask, readTask);

        history.TotalTransitions.Should().BeGreaterThan(0);
    }

    #endregion

    #region ExecutionTracer - Empty State

    [Fact]
    public void ExecutionTracer_NewInstance_HasZeroTraces()
    {
        // Arrange
        ExecutionTracer tracer = new();

        // Assert
        tracer.TotalTraces.Should().Be(0);
        tracer.GetRecentTraces(10).Should().BeEmpty();
    }

    #endregion

    #region ExecutionTracer - RecordTrace

    [Fact]
    public void ExecutionTracer_RecordTrace_IncrementsTotalAndIsRetrievable()
    {
        // Arrange
        ExecutionTracer tracer = new();
        TraceEntry entry = new()
        {
            Category = TraceCategory.Goal,
            Operation = "FollowRoute",
            Success = true,
            Details = "reached waypoint",
            DurationMs = 42
        };

        // Act
        tracer.RecordTrace(entry);

        // Assert
        tracer.TotalTraces.Should().Be(1);
        List<TraceEntry> traces = tracer.GetRecentTraces(10);
        traces.Should().HaveCount(1);
        traces[0].Operation.Should().Be("FollowRoute");
        traces[0].Category.Should().Be(TraceCategory.Goal);
        traces[0].Success.Should().BeTrue();
        traces[0].DurationMs.Should().Be(42);
    }

    #endregion

    #region ExecutionTracer - BeginTrace / TraceScope

    [Fact]
    public void ExecutionTracer_BeginTrace_DisposingRecordsEntry()
    {
        // Arrange
        ExecutionTracer tracer = new();

        // Act
        using (tracer.BeginTrace(TraceCategory.Navigation, "FindPath", "A to B"))
        {
            // Simulate work
        }

        // Assert
        tracer.TotalTraces.Should().Be(1);
        List<TraceEntry> traces = tracer.GetRecentTraces(1);
        traces[0].Operation.Should().Be("FindPath");
        traces[0].Category.Should().Be(TraceCategory.Navigation);
        traces[0].Details.Should().Be("A to B");
        traces[0].Success.Should().BeTrue();
        traces[0].DurationMs.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void ExecutionTracer_TraceScope_MarkFailed_RecordsFailure()
    {
        // Arrange
        ExecutionTracer tracer = new();

        // Act
        TraceScope scope = new(tracer, TraceCategory.Combat, "CastSpell", null);
        scope.MarkFailed("out of mana");
        scope.Dispose();

        // Assert
        List<TraceEntry> traces = tracer.GetRecentTraces(1);
        traces[0].Success.Should().BeFalse();
        traces[0].Details.Should().Be("out of mana");
    }

    [Fact]
    public void ExecutionTracer_TraceScope_DoubleDispose_RecordsOnlyOnce()
    {
        // Arrange
        ExecutionTracer tracer = new();

        // Act
        TraceScope scope = new(tracer, TraceCategory.System, "Init", null);
        scope.Dispose();
        scope.Dispose(); // double dispose

        // Assert
        tracer.TotalTraces.Should().Be(1);
    }

    #endregion

    #region ExecutionTracer - GetTracesByCategory

    [Fact]
    public void ExecutionTracer_GetTracesByCategory_FiltersCorrectly()
    {
        // Arrange
        ExecutionTracer tracer = new();
        tracer.RecordTrace(new TraceEntry { Category = TraceCategory.Goal, Operation = "GoalA" });
        tracer.RecordTrace(new TraceEntry { Category = TraceCategory.Navigation, Operation = "Nav1" });
        tracer.RecordTrace(new TraceEntry { Category = TraceCategory.Goal, Operation = "GoalB" });
        tracer.RecordTrace(new TraceEntry { Category = TraceCategory.Combat, Operation = "Attack" });

        // Act
        List<TraceEntry> goalTraces = tracer.GetTracesByCategory(TraceCategory.Goal, 10);

        // Assert
        goalTraces.Should().HaveCount(2);
        goalTraces[0].Operation.Should().Be("GoalA");
        goalTraces[1].Operation.Should().Be("GoalB");
    }

    [Fact]
    public void ExecutionTracer_GetTracesByCategory_NoMatches_ReturnsEmpty()
    {
        // Arrange
        ExecutionTracer tracer = new();
        tracer.RecordTrace(new TraceEntry { Category = TraceCategory.Goal, Operation = "GoalA" });

        // Act
        List<TraceEntry> result = tracer.GetTracesByCategory(TraceCategory.Combat, 10);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ExecutionTracer - Ring Buffer / Eviction

    [Fact]
    public void ExecutionTracer_ExceedsCapacity_EvictsOldest()
    {
        // Arrange - capacity is 5000
        ExecutionTracer tracer = new();

        // Act
        for (int i = 0; i < 5100; i++)
        {
            tracer.RecordTrace(new TraceEntry { Operation = $"Op{i}" });
        }

        // Assert
        tracer.TotalTraces.Should().Be(5100);
        List<TraceEntry> traces = tracer.GetRecentTraces(6000);
        traces.Count.Should().BeLessOrEqualTo(5000);
    }

    #endregion

    #region ExecutionTracer - ClearTraces

    [Fact]
    public void ExecutionTracer_ClearTraces_RemovesAllAndResetsCounter()
    {
        // Arrange
        ExecutionTracer tracer = new();
        tracer.RecordTrace(new TraceEntry { Operation = "Op1" });
        tracer.RecordTrace(new TraceEntry { Operation = "Op2" });

        // Act
        tracer.ClearTraces();

        // Assert
        tracer.TotalTraces.Should().Be(0);
        tracer.GetRecentTraces(10).Should().BeEmpty();
    }

    #endregion
}
