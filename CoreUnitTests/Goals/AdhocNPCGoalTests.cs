using Core;
using Core.Goals;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Goals;

public sealed class AdhocNPCGoalTests
{
    [Fact]
    public void ShouldAbortForZeroWaypointServiceDeadlock_WhenBreadcrumbBacktrackRepeatsWithNoRoute_ReturnsTrue()
    {
        bool shouldAbort = AdhocNPCGoal.ShouldAbortForZeroWaypointServiceDeadlock(
            UnstuckState.BreadcrumbBacktrack,
            routeToNextWaypointCount: 0,
            wayPointCount: 0,
            consecutiveDetections: 2);

        shouldAbort.Should().BeTrue();
    }

    [Fact]
    public void ShouldAbortForZeroWaypointServiceDeadlock_WhenRouteStillExists_ReturnsFalse()
    {
        bool shouldAbort = AdhocNPCGoal.ShouldAbortForZeroWaypointServiceDeadlock(
            UnstuckState.BreadcrumbBacktrack,
            routeToNextWaypointCount: 1,
            wayPointCount: 0,
            consecutiveDetections: 3);

        shouldAbort.Should().BeFalse();
    }

    [Fact]
    public void ShouldAbortForZeroWaypointServiceDeadlock_WhenStateIsNotBreadcrumbBacktrack_ReturnsFalse()
    {
        bool shouldAbort = AdhocNPCGoal.ShouldAbortForZeroWaypointServiceDeadlock(
            UnstuckState.PathClearAttempt,
            routeToNextWaypointCount: 0,
            wayPointCount: 0,
            consecutiveDetections: 3);

        shouldAbort.Should().BeFalse();
    }
}
