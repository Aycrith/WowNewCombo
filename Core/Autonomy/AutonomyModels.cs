using System;
using System.Collections.Generic;

namespace Core.Autonomy;

public sealed class ArtifactRef
{
    public string Kind { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public sealed class ScreenshotRef
{
    public string RequestId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTimeOffset RequestedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public double? CaptureLatencyMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public sealed class FailoverDecision
{
    public string PrimaryAction { get; set; } = string.Empty;
    public string SecondaryAction { get; set; } = string.Empty;
    public string TertiaryAction { get; set; } = string.Empty;
    public string DecisionReason { get; set; } = string.Empty;
    public bool DemoteLiveMode { get; set; }
    public string TargetSurface { get; set; } = "SyntheticOnly";
}

public sealed class RemediationTask
{
    public string Id { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string Summary { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string WorktreePath { get; set; } = string.Empty;
    public bool ProtectedWorktreeRequired { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public sealed class AutonomyIncident
{
    public string Id { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Subsystem { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Gate { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string Outcome { get; set; } = "Open";
    public int OccurrenceCount { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public List<ArtifactRef> Artifacts { get; set; } = [];
    public ScreenshotRef? Screenshot { get; set; }
    public FailoverDecision? Failover { get; set; }
    public RemediationTask? RemediationTask { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public sealed class AutonomyBudget
{
    public int MaxCycleRuntimeMinutes { get; set; } = 120;
    public int MaxRetriesPerIncident { get; set; } = 2;
    public int SameReasonFailureLimit { get; set; } = 2;
    public int MutationCooldownMinutes { get; set; } = 60;
    public int LiveDemotionMinutes { get; set; } = 60;
}

public sealed class RetryLedgerEntry
{
    public string Fingerprint { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public int SameReasonFailures { get; set; }
    public string LastAction { get; set; } = string.Empty;
    public string LastOutcome { get; set; } = string.Empty;
    public DateTimeOffset? LastAttemptUtc { get; set; }
}

public sealed class PromotionState
{
    public string RequestedSurface { get; set; } = "Hybrid";
    public string EffectiveSurface { get; set; } = "Hybrid";
    public string LiveMode { get; set; } = "Guarded";
    public string LastDecisionReason { get; set; } = string.Empty;
    public DateTimeOffset? LastUpdatedUtc { get; set; }
    public DateTimeOffset? LiveDemotedUntilUtc { get; set; }
}

public sealed class KillSwitchState
{
    public bool Enabled { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public sealed class LiveWindowState
{
    public bool Enabled { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public sealed class AutonomyRunSummary
{
    public string CycleId { get; set; } = string.Empty;
    public string CurrentPhase { get; set; } = string.Empty;
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public bool SyntheticPassed { get; set; }
    public bool LiveAttempted { get; set; }
    public bool LiveValid { get; set; }
    public string InvalidReason { get; set; } = string.Empty;
    public List<string> IncidentIds { get; set; } = [];
    public List<string> AppliedActions { get; set; } = [];
}

public sealed class AutonomyRunState
{
    public string SupervisorId { get; set; } = "default";
    public string CurrentPhase { get; set; } = "Idle";
    public string? LastCycleId { get; set; }
    public int CycleCount { get; set; }
    public DateTimeOffset? LastUpdatedUtc { get; set; }
    public AutonomyBudget Budget { get; set; } = new();
    public List<AutonomyIncident> IncidentQueue { get; set; } = [];
    public Dictionary<string, RetryLedgerEntry> RetryLedger { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public PromotionState PromotionState { get; set; } = new();
    public KillSwitchState KillSwitchState { get; set; } = new();
    public LiveWindowState LiveWindowState { get; set; } = new();
    public string WorkspaceFingerprint { get; set; } = string.Empty;
    public bool? WorkspaceDirty { get; set; }
    public string StartupBlockerFingerprint { get; set; } = string.Empty;
    public int SameReasonRetryCount { get; set; }
    public string LiveDemotionReason { get; set; } = string.Empty;
    public List<AutonomyRunSummary> RecentRuns { get; set; } = [];
}
