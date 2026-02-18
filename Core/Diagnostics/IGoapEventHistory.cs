using System;
using System.Collections.Generic;

namespace Core.Diagnostics;

/// <summary>
/// Interface for tracking GOAP event history including goal transitions,
/// NO PLAN events, and precondition failures for troubleshooting.
/// </summary>
public interface IGoapEventHistory
{
    /// <summary>
    /// Records a goal transition from one goal to another.
    /// </summary>
    void RecordTransition(string? fromGoal, string toGoal, string reason, int planDepth);

    /// <summary>
    /// Records a NO PLAN event when the planner fails to find a valid plan.
    /// </summary>
    void RecordNoPlanEvent(string worldState, List<string> availableGoals, string context);

    /// <summary>
    /// Records a precondition failure for a goal.
    /// </summary>
    void RecordPreconditionFailure(string goalName, string failedPrecondition, string expected, string actual);

    /// <summary>
    /// Gets the most recent goal transitions.
    /// </summary>
    List<GoalTransitionEntry> GetRecentTransitions(int count);

    /// <summary>
    /// Gets recent NO PLAN events.
    /// </summary>
    List<NoPlanEventEntry> GetNoPlanEvents(int count);

    /// <summary>
    /// Gets recent precondition failures.
    /// </summary>
    List<PreconditionFailureEntry> GetPreconditionFailures(int count);

    /// <summary>
    /// Total number of transitions recorded.
    /// </summary>
    int TotalTransitions { get; }

    /// <summary>
    /// Total number of NO PLAN events recorded.
    /// </summary>
    int TotalNoPlanEvents { get; }
}

/// <summary>
/// Represents a recorded goal transition.
/// </summary>
public sealed class GoalTransitionEntry
{
    public DateTime Timestamp { get; set; }
    public string? FromGoal { get; set; }
    public string ToGoal { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int PlanDepth { get; set; }
}

/// <summary>
/// Represents a recorded NO PLAN event.
/// </summary>
public sealed class NoPlanEventEntry
{
    public DateTime Timestamp { get; set; }
    public string WorldState { get; set; } = string.Empty;
    public List<string> AvailableGoals { get; set; } = new();
    public string Context { get; set; } = string.Empty;
}

/// <summary>
/// Represents a recorded precondition failure.
/// </summary>
public sealed class PreconditionFailureEntry
{
    public DateTime Timestamp { get; set; }
    public string GoalName { get; set; } = string.Empty;
    public string FailedPrecondition { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
}
