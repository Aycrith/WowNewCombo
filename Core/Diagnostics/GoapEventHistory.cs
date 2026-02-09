using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Core.Diagnostics;

/// <summary>
/// Thread-safe in-memory implementation of GOAP event history tracking.
/// Records goal transitions, NO PLAN events, and precondition failures.
/// </summary>
public sealed class GoapEventHistory : IGoapEventHistory
{
    private readonly ConcurrentQueue<GoalTransitionEntry> transitions = new();
    private readonly ConcurrentQueue<NoPlanEventEntry> noPlanEvents = new();
    private readonly ConcurrentQueue<PreconditionFailureEntry> preconditionFailures = new();

    private int totalTransitions;
    private int totalNoPlanEvents;

    /// <inheritdoc />
    public int TotalTransitions => totalTransitions;

    /// <inheritdoc />
    public int TotalNoPlanEvents => totalNoPlanEvents;

    /// <inheritdoc />
    public void RecordTransition(string? fromGoal, string toGoal, string reason, int planDepth)
    {
        var entry = new GoalTransitionEntry
        {
            Timestamp = DateTime.UtcNow,
            FromGoal = fromGoal,
            ToGoal = toGoal,
            Reason = reason,
            PlanDepth = planDepth
        };

        transitions.Enqueue(entry);
        Interlocked.Increment(ref totalTransitions);

        // Keep only last 1000 entries to prevent unbounded growth
        while (transitions.Count > 1000 && transitions.TryDequeue(out _)) { }
    }

    /// <inheritdoc />
    public void RecordNoPlanEvent(string worldState, List<string> availableGoals, string context)
    {
        var entry = new NoPlanEventEntry
        {
            Timestamp = DateTime.UtcNow,
            WorldState = worldState,
            AvailableGoals = new List<string>(availableGoals),
            Context = context
        };

        noPlanEvents.Enqueue(entry);
        Interlocked.Increment(ref totalNoPlanEvents);

        // Keep only last 100 entries
        while (noPlanEvents.Count > 100 && noPlanEvents.TryDequeue(out _)) { }
    }

    /// <inheritdoc />
    public void RecordPreconditionFailure(string goalName, string failedPrecondition, string expected, string actual)
    {
        var entry = new PreconditionFailureEntry
        {
            Timestamp = DateTime.UtcNow,
            GoalName = goalName,
            FailedPrecondition = failedPrecondition,
            Expected = expected,
            Actual = actual
        };

        preconditionFailures.Enqueue(entry);

        // Keep only last 500 entries
        while (preconditionFailures.Count > 500 && preconditionFailures.TryDequeue(out _)) { }
    }

    /// <inheritdoc />
    public List<GoalTransitionEntry> GetRecentTransitions(int count)
    {
        return transitions.TakeLast(count).ToList();
    }

    /// <inheritdoc />
    public List<NoPlanEventEntry> GetNoPlanEvents(int count)
    {
        return noPlanEvents.TakeLast(count).ToList();
    }

    /// <inheritdoc />
    public List<PreconditionFailureEntry> GetPreconditionFailures(int count)
    {
        return preconditionFailures.TakeLast(count).ToList();
    }

    /// <summary>
    /// Clears all history. Useful for testing or reset scenarios.
    /// </summary>
    public void Clear()
    {
        while (transitions.TryDequeue(out _)) { }
        while (noPlanEvents.TryDequeue(out _)) { }
        while (preconditionFailures.TryDequeue(out _)) { }
        Interlocked.Exchange(ref totalTransitions, 0);
        Interlocked.Exchange(ref totalNoPlanEvents, 0);
    }
}
