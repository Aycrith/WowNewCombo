using Core.Goals;

using FluentAssertions;

using System.Numerics;

using Xunit;

namespace CoreUnitTests.GoalsComponent;

public sealed class NavigationDynamicBypassTests
{
    [Fact]
    public void MergeRouteSegments_CombinesSegments_WithoutDuplicateReconnectNode()
    {
        Vector3[] local =
        [
            new Vector3(4, 2, 0),
            new Vector3(10, 0, 0)
        ];

        Vector3[] recalculatedTail =
        [
            new Vector3(10, 0, 0),
            new Vector3(15, 3, 0),
            new Vector3(24, 6, 0)
        ];

        Vector3[] merged = Navigation.MergeRouteSegments(local, recalculatedTail, duplicateDistance: 0.5f);

        merged.Should().HaveCount(4);
        merged[0].Should().Be(new Vector3(4, 2, 0));
        merged[1].Should().Be(new Vector3(10, 0, 0));
        merged[2].Should().Be(new Vector3(15, 3, 0));
        merged[3].Should().Be(new Vector3(24, 6, 0));
    }

    [Fact]
    public void MergeRouteSegments_DeduplicatesNearAdjacentNodes()
    {
        Vector3[] local =
        [
            new Vector3(4, 2, 0),
            new Vector3(10, 0, 0)
        ];

        Vector3[] recalculatedTail =
        [
            new Vector3(10.2f, 0.1f, 0),
            new Vector3(14, 2, 0)
        ];

        Vector3[] merged = Navigation.MergeRouteSegments(local, recalculatedTail, duplicateDistance: 0.5f);

        merged.Should().HaveCount(3);
        merged[0].Should().Be(new Vector3(4, 2, 0));
        merged[1].Should().Be(new Vector3(10, 0, 0));
        merged[2].Should().Be(new Vector3(14, 2, 0));
    }

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

    // Task 1: IsMeaningfulDynamicDetour unit tests

    [Fact]
    public void IsMeaningfulDynamicDetour_NullDetour_ReturnsFalse()
    {
        Vector3[] original = [new(0, 0, 0), new(5, 0, 0)];
        Navigation.IsMeaningfulDynamicDetour(original, null).Should().BeFalse();
    }

    [Fact]
    public void IsMeaningfulDynamicDetour_SinglePointDetour_ReturnsFalse()
    {
        Vector3[] original = [new(0, 0, 0), new(5, 0, 0)];
        Vector3[] detour = [new(0, 0, 0)];
        Navigation.IsMeaningfulDynamicDetour(original, detour).Should().BeFalse();
    }

    [Fact]
    public void IsMeaningfulDynamicDetour_IdenticalPaths_ReturnsFalse()
    {
        Vector3[] p = [new(0, 0, 0), new(5, 0, 0), new(10, 0, 0)];
        Navigation.IsMeaningfulDynamicDetour(p, p).Should().BeFalse();
    }

    [Fact]
    public void IsMeaningfulDynamicDetour_SlightDivergence_BelowThreshold_ReturnsFalse()
    {
        // 1.0f per node × 2 nodes = 2.0f < 2.5f threshold
        Vector3[] original = [new(0, 0, 0), new(5, 0, 0), new(10, 0, 0)];
        Vector3[] detour   = [new(0, 0, 0), new(5, 1, 0), new(10, 1, 0)];
        Navigation.IsMeaningfulDynamicDetour(original, detour).Should().BeFalse();
    }

    [Fact]
    public void IsMeaningfulDynamicDetour_MeaningfulDivergence_ReturnsTrue()
    {
        // 2.0f per node × 2 nodes = 4.0f >= 2.5f threshold
        Vector3[] original = [new(0, 0, 0), new(5, 0, 0), new(10, 0, 0)];
        Vector3[] detour   = [new(0, 0, 0), new(5, 2, 0), new(10, 2, 0)];
        Navigation.IsMeaningfulDynamicDetour(original, detour).Should().BeTrue();
    }

    [Fact]
    public void IsMeaningfulDynamicDetour_LongerDetour_AlwaysTrue()
    {
        Vector3[] original = [new(0, 0, 0), new(5, 0, 0)];
        Vector3[] detour   = [new(0, 0, 0), new(2, 0, 0), new(5, 0, 0)];
        Navigation.IsMeaningfulDynamicDetour(original, detour).Should().BeTrue();
    }

    // Task 2: TailRecalcFailures counter test

    [Fact]
    public void Navigation_TailRecalcFailures_PropertyIsPublicInt()
    {
        System.Reflection.PropertyInfo? prop = typeof(Navigation)
            .GetProperty("TailRecalcFailures",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        prop.Should().NotBeNull("TailRecalcFailures must be public for soak telemetry");
        prop!.PropertyType.Should().Be(typeof(int));
    }
}
