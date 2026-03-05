using System;
using System.Collections.Generic;
using System.Numerics;

namespace Core;

public enum StuckTriggerReason
{
    TimeoutNoProgress,
    ShortNoProgress,
    SpinDetected,
    PredictiveRisk,
    ExternalOscillationNotify
}

public sealed record StuckTriggerSample(
    DateTime TimestampUtc,
    StuckTriggerReason Reason,
    double ActionDurationMs,
    UnstuckState State,
    int? PredictiveRisk);

public sealed record StuckDetectorRuntimeSnapshot(
    UnstuckState CurrentState,
    bool IsCurrentlyStuck,
    double ActionDurationMs,
    double UnstuckMs,
    bool IsSpinningDetected,
    float RangeDiffThreshold,
    float MinDistanceThreshold,
    double UnstuckAfterMsThreshold,
    double ActionStuckTimeThreshold,
    StuckTriggerReason? LastTriggerReason,
    DateTime? LastTriggerUtc,
    int? LastPredictiveRisk,
    IReadOnlyList<StuckTriggerSample> RecentTriggers);

public sealed record NavigationRuntimeSnapshot(
    int RouteToNextWaypointCount,
    int WayPointCount,
    int TailRecalcFailures,
    float? DistanceToNextWaypoint,
    float LastHeadingDiffRadians,
    bool OscillationDetected,
    int OscillationCount,
    bool TurnStuckGraceActive,
    int TurnStuckGraceRemainingMs,
    int SuppressedTurnStuckRecoveryCount,
    DateTime? LastSuppressedTurnStuckRecoveryUtc,
    bool FrontBypassBreakerActive,
    int FrontBypassBreakerRemainingMs,
    bool HazardDetourBreakerActive,
    int HazardDetourBreakerRemainingMs,
    int FrontBypassAttemptCount,
    int RepeatedFrontBypassCount,
    int RepeatedFrontBypassNoProgressCount,
    int RepeatedHazardDetourCount,
    int RerouteTriggerCount,
    int RerouteApplyCount,
    int RerouteDropCount,
    string? LastRerouteDropReason,
    float? LastRerouteAnchorDistance);

public sealed record NavigationRerouteRuntimeSnapshot(
    int RerouteTriggerCount,
    int RerouteApplyCount,
    int RerouteDropCount,
    int DetourOnlyCollapseCount,
    string? LastRerouteDropReason,
    float? LastRerouteAnchorDistance,
    bool HasActiveReroute,
    Guid? ActiveRerouteId,
    DateTime? ActiveRerouteStartedAt,
    Vector3? ActiveRerouteOriginalTarget,
    int ActiveRerouteWaypointCount);

public sealed record CastingRuntimeSnapshot(
    int CurrentActionNotDetectedCountWindow,
    int UIFeedbackNotDetectedCountWindow,
    int AmbiguousCastResolvedCountWindow,
    int InterruptedRetrySuppressedCountWindow,
    int WindowSeconds);

public sealed record CombatRuntimeSnapshot(
    int LostTargetCountWindow,
    int LostTargetBurstCountWindow,
    int LostTargetReacquireAttemptCountWindow,
    int LostTargetReacquireSuccessCountWindow,
    int TargetlessCombatGraceUsedCountWindow,
    int ReacquireFallbackAttemptCountWindow,
    int ReacquireFallbackSuccessCountWindow,
    int WindowSeconds);

public sealed record PullRuntimeSnapshot(
    int PullFailureSoftRetryCountWindow,
    int WindowSeconds);
