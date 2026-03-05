using System;

namespace Core.Navigation;

/// <summary>Metrics for a single soak observation window (default 10 minutes).</summary>
public sealed record NavSoakWindow
{
    public DateTime WindowStartUtc { get; init; }
    public DateTime WindowEndUtc { get; init; }
    public int FrontBypassActivations { get; init; }
    public int SuccessfulReconnects { get; init; }
    public int StuckEvents { get; init; }
    public int RepeatStuckCount { get; init; }
    public int TailRecalcFailures { get; init; }
    public float MaxRouteDeviation { get; init; }
    public float AvgRouteDeviation { get; init; }
    public int RerouteTriggerCount { get; init; }
    public int RerouteApplyCount { get; init; }
    public int RerouteDropCount { get; init; }
    public int DetourOnlyCollapseCount { get; init; }

    public double RepeatStuckRate =>
        StuckEvents == 0 ? 0.0 : Math.Round((double)RepeatStuckCount / StuckEvents, 4);
}
