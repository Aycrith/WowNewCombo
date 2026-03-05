using Core.Goals;

using FluentAssertions;

using System.Numerics;

using Xunit;

namespace CoreUnitTests.GoalsComponent;

public sealed class RouteSegmentTrackerTests
{
    [Fact]
    public void ObserveRouteRebuild_NoConsumedPoint_DoesNotFlagRegression()
    {
        RouteSegmentTracker tracker = new();
        Vector3[] route = [new Vector3(10, 10, 0), new Vector3(20, 10, 0)];

        bool flagged = tracker.ObserveRouteRebuild(route, "path-apply", out string? warning);

        flagged.Should().BeFalse();
        warning.Should().BeNull();
        tracker.RegressionCount.Should().Be(0);
        tracker.RouteRebuildCount.Should().Be(1);
    }

    [Fact]
    public void ObserveRouteRebuild_HeadNearConsumedPoint_FlagsRegression()
    {
        RouteSegmentTracker tracker = new();
        tracker.ObserveSubSegmentConsumed(new Vector3(100, 100, 0));

        Vector3[] route = [new Vector3(106, 103, 0), new Vector3(120, 120, 0)];
        bool flagged = tracker.ObserveRouteRebuild(route, "detour-insert", out string? warning);

        flagged.Should().BeTrue();
        warning.Should().NotBeNull();
        tracker.RegressionCount.Should().Be(1);
        tracker.LastRegressionUtc.Should().NotBeNull();
        tracker.LastRegressionReason.Should().Contain("detour-insert");
    }

    [Fact]
    public void ObserveRouteRebuild_HeadFarFromConsumedPoint_DoesNotFlagRegression()
    {
        RouteSegmentTracker tracker = new();
        tracker.ObserveSubSegmentConsumed(new Vector3(0, 0, 0));

        Vector3[] route = [new Vector3(50, 50, 0), new Vector3(60, 50, 0)];
        bool flagged = tracker.ObserveRouteRebuild(route, "path-apply", out _);

        flagged.Should().BeFalse();
        tracker.RegressionCount.Should().Be(0);
    }

    [Fact]
    public void Reset_ClearsCountersAndRegressionState()
    {
        RouteSegmentTracker tracker = new();
        tracker.ObserveSubSegmentConsumed(new Vector3(10, 10, 0));
        tracker.ObserveWaypointConsumed();
        _ = tracker.ObserveRouteRebuild([new Vector3(12, 11, 0)], "resume", out _);

        tracker.Reset();

        tracker.RouteRebuildCount.Should().Be(0);
        tracker.WaypointTransitionCount.Should().Be(0);
        tracker.SubSegmentTransitionCount.Should().Be(0);
        tracker.RegressionCount.Should().Be(0);
        tracker.LastRegressionReason.Should().BeNull();
        tracker.LastRegressionUtc.Should().BeNull();
    }
}
