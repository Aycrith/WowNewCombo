using System;
using System.Collections.Generic;

namespace Core;

public enum StuckTriggerReason
{
    TimeoutNoProgress,
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
    int RepeatedHazardDetourCount);
