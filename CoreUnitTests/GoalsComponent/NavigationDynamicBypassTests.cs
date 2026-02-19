using Core.Goals;

using FluentAssertions;

using System.Numerics;

using Xunit;

namespace CoreUnitTests.GoalsComponent;

public sealed class NavigationDynamicBypassTests
{
    [Fact]
    public void StitchDetourWithRemainingPath_ReconnectsToNextWaypoint_AndPreservesTail()
    {
        Vector3[] remainingPath =
        [
            new Vector3(10, 0, 0),
            new Vector3(20, 2, 0),
            new Vector3(30, 4, 0)
        ];

        Vector3[] localDetour =
        [
            new Vector3(0, 0, 0),
            new Vector3(5, 4, 0),
            new Vector3(10, 0, 0)
        ];

        Vector3[] stitched = Navigation.StitchDetourWithRemainingPath(localDetour, remainingPath);

        stitched.Should().HaveCount(4);
        stitched[0].Should().Be(new Vector3(5, 4, 0));
        stitched[1].Should().Be(new Vector3(10, 0, 0));
        stitched[2].Should().Be(new Vector3(20, 2, 0));
        stitched[3].Should().Be(new Vector3(30, 4, 0));
    }

    [Fact]
    public void StitchDetourWithRemainingPath_DeduplicatesReconnectWaypoint()
    {
        Vector3[] remainingPath =
        [
            new Vector3(10, 0, 0),
            new Vector3(20, 0, 0)
        ];

        Vector3[] localDetour =
        [
            new Vector3(0, 0, 0),
            new Vector3(8, 2, 0),
            new Vector3(10, 0, 0),
            new Vector3(10.1f, 0, 0)
        ];

        Vector3[] stitched = Navigation.StitchDetourWithRemainingPath(localDetour, remainingPath, duplicateDistance: 0.5f);

        stitched.Should().HaveCount(3);
        stitched[0].Should().Be(new Vector3(8, 2, 0));
        stitched[1].Should().Be(new Vector3(10, 0, 0));
        stitched[2].Should().Be(new Vector3(20, 0, 0));
    }

    [Fact]
    public void BuildFrontObstacleBypassPath_CreatesLateralBypass_ThenReconnectsToTarget()
    {
        Vector3 start = new(0, 0, 0);
        Vector3 next = new(12, 0, 0);

        Vector3[]? bypass = Navigation.BuildFrontObstacleBypassPath(start, next, attemptIndex: 0);

        Assert.NotNull(bypass);
        Vector3[] path = bypass!;
        path.Should().HaveCount(3);
        path[0].Should().Be(start);
        path[2].Should().Be(next);
        path[1].Y.Should().BeGreaterThan(0f);
        path[1].X.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void BuildFrontObstacleBypassPath_AlternatesSidesAcrossAttempts()
    {
        Vector3 start = new(0, 0, 0);
        Vector3 next = new(12, 0, 0);

        Vector3[]? first = Navigation.BuildFrontObstacleBypassPath(start, next, attemptIndex: 0);
        Vector3[]? second = Navigation.BuildFrontObstacleBypassPath(start, next, attemptIndex: 1);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Vector3[] firstPath = first!;
        Vector3[] secondPath = second!;
        firstPath[1].Y.Should().BeGreaterThan(0f);
        secondPath[1].Y.Should().BeLessThan(0f);
    }
}
