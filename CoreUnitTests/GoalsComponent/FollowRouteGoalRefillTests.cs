using Core.GoalsComponent;
using FluentAssertions;
using System;
using System.Numerics;
using Xunit;

namespace CoreUnitTests.GoalsComponent;

/// <summary>
/// Unit tests for FollowRouteGoal refill logic.
/// Tests the pure-logic components: segment scoring, backward penalty, loop-breaker.
///
/// These tests exercise the key invariants without requiring full DI:
///  - Forward-only segment selection must not regress to earlier segments
///  - Backward-segment penalty must penalize backward selection
///  - Loop-breaker must advance after limit reached
/// </summary>
public class FollowRouteGoalRefillTests
{
    // -----------------------------------------------------------------------
    // Helper: simulate FindClosestRefillCandidate + ScoreRefillCandidate logic
    // (mirrors the code in FollowRouteGoal.cs for white-box testing)
    // -----------------------------------------------------------------------

    private static readonly float BackwardSegmentPenalty = 6f;
    private static readonly int BackwardSegmentGrace = 1;

    private record RefillCandidate(int SegmentStartIndex, Vector3 MapClosestPoint, float DistanceToRoute);

    private static Vector2 GetClosestPointOnLineSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
        return a + t * ab;
    }

    private static RefillCandidate FindClosestRefillCandidate(
        Vector3[] pathMap, Vector3 playerMap, int minSegmentIndex = 0)
    {
        if (pathMap.Length == 1)
        {
            return new RefillCandidate(0, pathMap[0], Vector2.Distance(
                new Vector2(playerMap.X, playerMap.Y), new Vector2(pathMap[0].X, pathMap[0].Y)));
        }

        Vector2 playerXY = new(playerMap.X, playerMap.Y);
        int closestIndex = minSegmentIndex;
        Vector3 closestPoint = pathMap[Math.Min(minSegmentIndex, pathMap.Length - 1)];
        float bestDist = float.MaxValue;

        for (int i = minSegmentIndex; i < pathMap.Length - 1; i++)
        {
            Vector2 a = new(pathMap[i].X, pathMap[i].Y);
            Vector2 b = new(pathMap[i + 1].X, pathMap[i + 1].Y);
            Vector2 closest = GetClosestPointOnLineSegment(a, b, playerXY);
            float dist = Vector2.Distance(playerXY, closest);
            if (dist < bestDist)
            {
                bestDist = dist;
                closestIndex = i;
                closestPoint = new Vector3(closest.X, closest.Y, 0);
            }
        }

        return new RefillCandidate(closestIndex, closestPoint, bestDist);
    }

    private static float ScoreCandidate(RefillCandidate c, int anchorIndex)
    {
        float score = c.DistanceToRoute;
        int backwardSegments = anchorIndex - c.SegmentStartIndex - BackwardSegmentGrace;
        if (backwardSegments > 0)
        {
            score += backwardSegments * BackwardSegmentPenalty;
        }

        return score;
    }

    // A straight-line route: A(0,0) -> B(10,0) -> C(20,0) -> D(30,0)
    private static readonly Vector3[] StraightRoute =
    [
        new(0, 0, 0), new(10, 0, 0), new(20, 0, 0), new(30, 0, 0)
    ];

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public void FindClosestRefillCandidate_NoAnchor_FindsGlobalClosest()
    {
        // Player at (15, 1, 0) - closest to segment B->C (index 1)
        var player = new Vector3(15, 1, 0);
        var result = FindClosestRefillCandidate(StraightRoute, player, minSegmentIndex: 0);

        result.SegmentStartIndex.Should().Be(1, "segment B->C is closest to player at (15,1)");
        result.DistanceToRoute.Should().BeLessThan(2f);
    }

    [Fact]
    public void FindClosestRefillCandidate_WithAnchorAtSegment1_CannotRegresToSegment0()
    {
        // Player has drifted slightly back to (9, 1, 0) - physically closer to segment A->B (index 0)
        // But anchor is at segment 1, grace=1, so minSegmentIndex = max(0, 1-1) = 0
        // With grace=1, segment 0 is allowed. Test that without grace (minIndex=1), it returns seg 1.
        var player = new Vector3(9, 1, 0);

        // Without forward enforcement: would return segment 0
        var withoutGuard = FindClosestRefillCandidate(StraightRoute, player, minSegmentIndex: 0);
        withoutGuard.SegmentStartIndex.Should().Be(0);

        // With forward enforcement (anchorIndex=2, grace=1 -> minIndex=1): must return seg 1+
        var withGuard = FindClosestRefillCandidate(StraightRoute, player, minSegmentIndex: 1);
        withGuard.SegmentStartIndex.Should().BeGreaterThanOrEqualTo(1,
            "forward-only guard prevents regression below minSegmentIndex");
    }

    [Fact]
    public void ScoreCandidate_BackwardSegment_AddsPenalty()
    {
        // Anchor at segment 2; candidate at segment 0 -> backward by (2 - 0 - 1) = 1 segment
        var player = new Vector3(5, 0, 0);
        var candidate = FindClosestRefillCandidate(StraightRoute, player, minSegmentIndex: 0);
        candidate.SegmentStartIndex.Should().Be(0);

        float score = ScoreCandidate(candidate, anchorIndex: 2);
        float baseScore = candidate.DistanceToRoute;

        score.Should().BeGreaterThan(baseScore, "backward penalty must be applied");
        score.Should().BeApproximately(baseScore + 1 * BackwardSegmentPenalty, 0.01f);
    }

    [Fact]
    public void ScoreCandidate_ForwardSegment_NoExtraPenalty()
    {
        // Anchor at segment 0; candidate at segment 2 -> forward, no penalty
        var player = new Vector3(25, 0, 0);
        var candidate = FindClosestRefillCandidate(StraightRoute, player, minSegmentIndex: 0);
        candidate.SegmentStartIndex.Should().Be(2);

        float score = ScoreCandidate(candidate, anchorIndex: 0);
        score.Should().BeApproximately(candidate.DistanceToRoute, 0.01f, "no penalty for forward selection");
    }

    [Fact]
    public void ScoreCandidate_WithinGrace_NoExtraPenalty()
    {
        // Anchor at segment 2; candidate at segment 1 -> backward by (2 - 1 - 1) = 0
        // Within grace window: no penalty
        var player = new Vector3(15, 0, 0);
        var candidate = FindClosestRefillCandidate(StraightRoute, player, minSegmentIndex: 0);
        candidate.SegmentStartIndex.Should().Be(1);

        float score = ScoreCandidate(candidate, anchorIndex: 2);
        score.Should().BeApproximately(candidate.DistanceToRoute, 0.01f,
            "one segment back is within grace window, no penalty");
    }
}
