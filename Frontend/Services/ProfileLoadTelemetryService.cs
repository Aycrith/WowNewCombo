using System;

namespace Frontend.Services;

public sealed record ProfileLoadTelemetrySnapshot
{
    public string Status { get; init; } = "Idle";
    public string? RequestedProfile { get; init; }
    public string? AppliedProfile { get; init; }
    public string? FailureKind { get; init; }
    public string? FailureReason { get; init; }
    public string? CorrelationId { get; init; }
    public string Source { get; init; } = "api/bot/profile/load";
    public DateTimeOffset? AttemptedUtc { get; init; }
    public DateTimeOffset? UpdatedUtc { get; init; }
}

public sealed class ProfileLoadTelemetryService
{
    private readonly object syncRoot = new();
    private ProfileLoadTelemetrySnapshot snapshot = new();

    public ProfileLoadTelemetrySnapshot GetSnapshot()
    {
        lock (syncRoot)
        {
            return snapshot with { };
        }
    }

    public void RecordAttempt(string requestedProfile, string? correlationId, string source = "api/bot/profile/load")
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        lock (syncRoot)
        {
            snapshot = snapshot with
            {
                Status = "InProgress",
                RequestedProfile = requestedProfile,
                FailureKind = null,
                FailureReason = null,
                CorrelationId = correlationId,
                Source = source,
                AttemptedUtc = now,
                UpdatedUtc = now
            };
        }
    }

    public void RecordSuccess(string requestedProfile, string? appliedProfile, string? correlationId, string source = "api/bot/profile/load")
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        lock (syncRoot)
        {
            snapshot = new ProfileLoadTelemetrySnapshot
            {
                Status = "Succeeded",
                RequestedProfile = requestedProfile,
                AppliedProfile = appliedProfile,
                CorrelationId = correlationId,
                Source = source,
                AttemptedUtc = snapshot.AttemptedUtc ?? now,
                UpdatedUtc = now
            };
        }
    }

    public void RecordFailure(
        string requestedProfile,
        string? appliedProfile,
        string failureKind,
        string failureReason,
        string? correlationId,
        string source = "api/bot/profile/load")
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        lock (syncRoot)
        {
            snapshot = new ProfileLoadTelemetrySnapshot
            {
                Status = "Failed",
                RequestedProfile = requestedProfile,
                AppliedProfile = appliedProfile,
                FailureKind = failureKind,
                FailureReason = failureReason,
                CorrelationId = correlationId,
                Source = source,
                AttemptedUtc = snapshot.AttemptedUtc ?? now,
                UpdatedUtc = now
            };
        }
    }
}
