using Core;
using Core.FeatureFlags;
using Core.Goals;
using NavigationGoal = Core.Goals.Navigation;

using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace CoreUnitTests.GoalsComponent;

/// <summary>
/// Tests for Navigation static helper logic.
/// All methods under test are reproduced here as they appear in Navigation.cs.
/// If the production code is ever refactored to expose these as internal/public,
/// these tests should switch to calling the real implementation.
/// </summary>
public class NavigationHelperTests
{
    // ---- IsSharpTurn mirror ----
    private static bool IsSharpTurn(Vector3 from, Vector3 via, Vector3 to, float minTurnRadians)
    {
        var incoming = new Vector2(via.X - from.X, via.Y - from.Y);
        var outgoing = new Vector2(to.X - via.X, to.Y - via.Y);
        if (incoming.LengthSquared() < 0.01f || outgoing.LengthSquared() < 0.01f)
        {
            return false;
        }

        incoming = Vector2.Normalize(incoming);
        outgoing = Vector2.Normalize(outgoing);
        float dot = Math.Clamp(Vector2.Dot(incoming, outgoing), -1f, 1f);
        float angle = MathF.Acos(dot);
        return angle >= minTurnRadians;
    }

    private const float SimplifyPreserveTurnRadians = MathF.PI / 6f; // 30 deg
    private const float TightTurnPrecisionRadians = MathF.PI / 5f;   // 36 deg
    private const float MinDistanceMount = 10f;

    private static float GetMountedReachedDistance(Vector3 playerW, params Vector3[] routeTopFirst)
    {
        const float MountedSharpTurnScale = 1.5f;
        const float MountedStraightScale = 0.6f;

        if (routeTopFirst.Length < 2)
        {
            return MinDistanceMount;
        }

        Vector3 current = routeTopFirst[0];
        Vector3 next = routeTopFirst[1];
        if (!IsSharpTurn(playerW, current, next, SimplifyPreserveTurnRadians))
        {
            return MinDistanceMount * MountedStraightScale;
        }

        if (IsSharpTurn(playerW, current, next, TightTurnPrecisionRadians))
        {
            return MinDistanceMount * MountedSharpTurnScale;
        }

        return MinDistanceMount;
    }

    // ---- IsSharpTurn Tests ----

    [Fact]
    public void IsSharpTurn_StraightLine_ReturnsFalse()
    {
        // A->B->C all in a straight line east
        var from = new Vector3(0, 0, 0);
        var via = new Vector3(10, 0, 0);
        var to = new Vector3(20, 0, 0);
        IsSharpTurn(from, via, to, SimplifyPreserveTurnRadians).Should().BeFalse();
    }

    [Fact]
    public void IsSharpTurn_90DegreeTurn_ReturnsTrue()
    {
        // A(0,0)->B(10,0)->C(10,10): 90 deg left turn at B
        var from = new Vector3(0, 0, 0);
        var via = new Vector3(10, 0, 0);
        var to = new Vector3(10, 10, 0);
        IsSharpTurn(from, via, to, SimplifyPreserveTurnRadians).Should().BeTrue("90 deg > 30 deg threshold");
    }

    [Fact]
    public void IsSharpTurn_20DegreeTurn_ReturnsFalse()
    {
        // A gentle 20 deg bend - should not trigger SimplifyPreserveTurnRadians (30 deg)
        float angle20 = 20f * MathF.PI / 180f;
        var from = new Vector3(0, 0, 0);
        var via = new Vector3(10, 0, 0);
        // to is 20 deg off the incoming direction
        var to = new Vector3(via.X + MathF.Cos(angle20) * 10f,
                             via.Y + MathF.Sin(angle20) * 10f, 0);
        IsSharpTurn(from, via, to, SimplifyPreserveTurnRadians).Should().BeFalse("20 deg < 30 deg threshold");
    }

    [Fact]
    public void IsSharpTurn_ZeroLengthIncoming_ReturnsFalse()
    {
        // from == via (zero-length incoming vector) - must not crash, must return false
        var from = new Vector3(10, 0, 0);
        var via = new Vector3(10, 0, 0); // same as from
        var to = new Vector3(20, 10, 0);
        IsSharpTurn(from, via, to, SimplifyPreserveTurnRadians).Should().BeFalse("zero-length guard");
    }

    [Fact]
    public void IsSharpTurn_PlayerPastVia_DoesNotFalselyTrigger()
    {
        // This is the bug that was fixed: playerW (from) is slightly past via.
        // The incoming vector playerW->via is near-zero/backward but LengthSquared > 0.01.
        // With OutDoorMinDistance=3 guard in ReduceByDistance, from is always >3 from via.
        // Simulate: from is 2 units past via -> incoming vector points backward
        var via = new Vector3(10, 0, 0);
        var from = new Vector3(12, 0, 0); // 2 units past via in +X direction
        var to = new Vector3(20, 0, 0); // continuing in same direction

        // incoming = via - from = (-2, 0)  (points left/backward)
        // outgoing = to - via  = (10, 0)   (points right/forward)
        // These are anti-parallel -> angle ~= PI -> IsSharpTurn returns true (wrong for this path)
        // This proves why the OutDoorMinDistance guard in ReduceByDistance is necessary.
        bool result = IsSharpTurn(from, via, to, SimplifyPreserveTurnRadians);
        result.Should().BeTrue("confirms the geometry inversion when player overshoots via");
        // The production code guards against this by requiring playerW > OutDoorMinDistance from curr
    }

    [Fact]
    public void GetMountedReachedDistance_WithoutUpcomingPoints_ReturnsBaseline()
    {
        float reached = GetMountedReachedDistance(new Vector3(0, 0, 0));
        reached.Should().Be(MinDistanceMount);
    }

    [Fact]
    public void GetMountedReachedDistance_StraightSegment_TightensThreshold()
    {
        Vector3 player = new(0, 0, 0);
        Vector3 current = new(10, 0, 0);
        Vector3 next = new(20, 0, 0);

        float reached = GetMountedReachedDistance(player, current, next);
        reached.Should().BeApproximately(6f, 0.001f);
    }

    [Fact]
    public void GetMountedReachedDistance_ModerateTurn_UsesBaselineThreshold()
    {
        float angle32 = 32f * MathF.PI / 180f;
        Vector3 player = new(0, 0, 0);
        Vector3 current = new(10, 0, 0);
        Vector3 next = new(
            current.X + MathF.Cos(angle32) * 10f,
            current.Y + MathF.Sin(angle32) * 10f,
            0);

        float reached = GetMountedReachedDistance(player, current, next);
        reached.Should().BeApproximately(MinDistanceMount, 0.001f);
    }

    [Fact]
    public void GetMountedReachedDistance_SharpTurn_ExpandsThreshold()
    {
        float angle45 = 45f * MathF.PI / 180f;
        Vector3 player = new(0, 0, 0);
        Vector3 current = new(10, 0, 0);
        Vector3 next = new(
            current.X + MathF.Cos(angle45) * 10f,
            current.Y + MathF.Sin(angle45) * 10f,
            0);

        float reached = GetMountedReachedDistance(player, current, next);
        reached.Should().BeApproximately(15f, 0.001f);
    }

    [Fact]
    public void EstimateSpeedMps_UsesHorizontalDistanceOverTime()
    {
        Vector3 previous = new(0, 0, 0);
        Vector3 current = new(6, 8, 0); // 10 units XY distance

        float speed = NavigationGoal.EstimateSpeedMps(previous, current, deltaSeconds: 2.0);
        speed.Should().BeApproximately(5f, 0.001f);
    }

    [Fact]
    public void EstimateSpeedMps_NonPositiveDelta_ReturnsZero()
    {
        float speed = NavigationGoal.EstimateSpeedMps(new Vector3(0, 0, 0), new Vector3(5, 0, 0), 0);
        speed.Should().Be(0f);
    }

    [Fact]
    public void ComputeHeadingAdjustCooldownMs_NoExperimentsEnabled_ReturnsZero()
    {
        NavigationExperimentsOptions options = new()
        {
            EnableOscillationConfidenceThrottle = false,
            EnableAdaptiveHeadingCooldown = false
        };

        int cooldownMs = NavigationGoal.ComputeHeadingAdjustCooldownMs(options, oscillationConfidence: 1f, speedMps: 0.5f);
        cooldownMs.Should().Be(0);
    }

    [Fact]
    public void ComputeHeadingAdjustCooldownMs_OscillationAndAdaptive_AppliesBothScales()
    {
        NavigationExperimentsOptions options = new()
        {
            EnableOscillationConfidenceThrottle = true,
            EnableAdaptiveHeadingCooldown = true,
            HeadingAdjustCooldownMs = 140,
            MaxOscillationCooldownMultiplier = 4f,
            BaselineAdaptiveSpeedMps = 2f,
            MinAdaptiveCooldownScale = 0.5f,
            MaxAdaptiveCooldownScale = 2f
        };

        // Base 140 * oscillation factor 4 * adaptive factor 2 = 1120
        int cooldownMs = NavigationGoal.ComputeHeadingAdjustCooldownMs(options, oscillationConfidence: 1f, speedMps: 1f);
        cooldownMs.Should().Be(1120);
    }

    // ---- IsExcessiveHazardDetour mirror ----
    private static float TotalDistance(Vector3[] path)
    {
        float d = 0;
        for (int i = 0; i < path.Length - 1; i++)
        {
            d += Vector2.Distance(new(path[i].X, path[i].Y), new(path[i + 1].X, path[i + 1].Y));
        }

        return d;
    }

    private const float MaxAdditional = 80f;
    private const float MaxRatio = 1.75f;
    private const float HardMax = 120f;

    private static bool IsExcessiveHazardDetour(Vector3[] orig, Vector3[] detour)
    {
        if (orig.Length < 2 || detour.Length < 2)
        {
            return false;
        }

        float od = TotalDistance(orig);
        float dd = TotalDistance(detour);
        if (od <= 0.01f || dd <= od)
        {
            return false;
        }

        float additional = dd - od;
        float ratio = dd / od;
        if (additional >= HardMax)
        {
            return true;
        }

        return additional >= MaxAdditional && ratio >= MaxRatio;
    }

    [Fact]
    public void IsExcessiveHazardDetour_ShorterDetour_ReturnsFalse()
    {
        Vector3[] orig = [new(0, 0, 0), new(100, 0, 0)];  // 100 units
        Vector3[] detour = [new(0, 0, 0), new(50, 0, 0), new(100, 0, 0)]; // still 100 units
        IsExcessiveHazardDetour(orig, detour).Should().BeFalse();
    }

    [Fact]
    public void IsExcessiveHazardDetour_SmallExcess_ReturnsFalse()
    {
        // 100 unit original, ~280 unit detour path geometry would exceed threshold.
        // Use a detour with +50 and ratio 1.5 to stay below both thresholds.
        Vector3[] orig = [new(0, 0, 0), new(100, 0, 0)];
        Vector3[] detour = [new(0, 0, 0), new(50, 50, 0), new(100, 0, 0)]; // ~141.42 total
        IsExcessiveHazardDetour(orig, detour).Should().BeFalse();
    }

    [Fact]
    public void IsExcessiveHazardDetour_ExceedsBothThresholds_ReturnsTrue()
    {
        // 50 unit original, ~250 unit detour (+200, ratio 5.0) - exceeds both thresholds
        Vector3[] orig = [new(0, 0, 0), new(50, 0, 0)];
        Vector3[] detour = [new(0, 0, 0), new(0, 200, 0), new(50, 200, 0)]; // 250 total
        IsExcessiveHazardDetour(orig, detour).Should().BeTrue();
    }

    [Fact]
    public void IsExcessiveHazardDetour_HardMaxAlone_ReturnsTrue()
    {
        // 10 unit original, 150 unit detour - exceeds HardMax (120) alone
        Vector3[] orig = [new(0, 0, 0), new(10, 0, 0)];
        Vector3[] detour = [new(0, 0, 0), new(0, 150, 0), new(10, 150, 0)];
        IsExcessiveHazardDetour(orig, detour).Should().BeTrue("hard max 120 exceeded");
    }

    // ---- Tests for Navigation.IsSharpTurn (now internal) ----
    // These tests verify the mounted sharp-turn preservation fix.
    // After the fix, Navigation.ShouldPreserveDetailedRoute checks for sharp turns
    // even when mounted, no longer skipping them.

    [Fact]
    public void IsSharpTurn_NinetyDegrees_IsSharp_Internal()
    {
        // Test the internal IsSharpTurn method by calling the helper version
        // (The real internal method will be tested through integration tests)
        var from = new Vector3(0, 0, 0);
        var via = new Vector3(10, 0, 0);
        var to = new Vector3(10, 10, 0);
        float minTurnRadians = MathF.PI / 6f; // 30 degrees

        bool result = IsSharpTurn(from, via, to, minTurnRadians);
        result.Should().BeTrue("90 degree turn should be detected as sharp (> 30 degrees)");
    }

    [Fact]
    public void IsSharpTurn_FifteenDegrees_IsNotSharp_Internal()
    {
        // Test via the helper version to verify threshold behavior
        float angle15 = 15f * MathF.PI / 180f;
        var from = new Vector3(0, 0, 0);
        var via = new Vector3(10, 0, 0);
        var to = new Vector3(via.X + MathF.Cos(angle15) * 10f,
                             via.Y + MathF.Sin(angle15) * 10f, 0);

        float minTurnRadians = MathF.PI / 6f; // 30 degrees
        bool result = IsSharpTurn(from, via, to, minTurnRadians);
        result.Should().BeFalse("15 degree turn should not be detected as sharp (< 30 degrees)");
    }

    // ---- Detour stitching ----

    /// <summary>
    /// When detour ends at the same destination as the preserved route, the replayed
    /// original prefix must be dropped to avoid backtracking loops.
    /// </summary>
    [Fact]
    public void BuildInlineDetourRoute_ReconnectsAtDestination_DropsReplayedPrefix()
    {
        Vector3 player = new(0, 0, 0);
        List<Vector3> preservedTopFirst =
        [
            new(10, 0, 0),
            new(20, 0, 0),
            new(30, 0, 0),
            new(40, 0, 0) // destination already on preserved route
        ];
        Vector3[] detour =
        [
            player,            // current position anchor; should be skipped
            new(8, 6, 0),
            new(22, 7, 0),
            new(40, 0, 0)      // reconnect at destination
        ];

        Vector3[] merged = NavigationGoal.BuildInlineDetourRoute(
            detour,
            preservedTopFirst,
            player,
            reconnectDistance: 1f,
            duplicateDistance: 1f);

        merged.Should().Equal(
            new Vector3(8, 6, 0),
            new Vector3(22, 7, 0),
            new Vector3(40, 0, 0));
    }

    [Fact]
    public void BuildInlineDetourRoute_ReconnectsMidRoute_PreservesTailOnly()
    {
        Vector3 player = new(0, 0, 0);
        List<Vector3> preservedTopFirst =
        [
            new(10, 0, 0),
            new(20, 0, 0),
            new(30, 0, 0), // reconnect point
            new(40, 0, 0),
            new(50, 0, 0)
        ];
        Vector3[] detour =
        [
            player,
            new(12, 8, 0),
            new(24, 8, 0),
            new(30, 0, 0)
        ];

        Vector3[] merged = NavigationGoal.BuildInlineDetourRoute(
            detour,
            preservedTopFirst,
            player,
            reconnectDistance: 1f,
            duplicateDistance: 1f);

        merged.Should().Equal(
            new Vector3(12, 8, 0),
            new Vector3(24, 8, 0),
            new Vector3(30, 0, 0),
            new Vector3(40, 0, 0),
            new Vector3(50, 0, 0));
    }

    [Fact]
    public void BuildInlineDetourRoute_WithoutReconnect_KeepsPreservedRouteAfterDetour()
    {
        Vector3 player = new(0, 0, 0);
        List<Vector3> preservedTopFirst =
        [
            new(10, 0, 0),
            new(20, 0, 0),
            new(30, 0, 0)
        ];
        Vector3[] detour =
        [
            player,
            new(5, 6, 0),
            new(6, 7, 0)
        ];

        Vector3[] merged = NavigationGoal.BuildInlineDetourRoute(
            detour,
            preservedTopFirst,
            player,
            reconnectDistance: 1f,
            duplicateDistance: 1f);

        merged.Should().Equal(
            new Vector3(5, 6, 0),
            new Vector3(6, 7, 0),
            new Vector3(10, 0, 0),
            new Vector3(20, 0, 0),
            new Vector3(30, 0, 0));
    }

    [Fact]
    public void BuildInlineDetourRoute_MidRouteReconnect_KeepsRemainingTail()
    {
        Vector3 player = new(0, 0, 0);
        List<Vector3> preservedTopFirst =
        [
            new(10, 0, 0),
            new(20, 0, 0), // reconnect point
            new(30, 0, 0),
            new(40, 0, 0),
            new(50, 0, 0)
        ];
        Vector3[] detour =
        [
            player,
            new(9, 5, 0),
            new(18, 4, 0),
            new(20, 0, 0)
        ];

        Vector3[] merged = NavigationGoal.BuildInlineDetourRoute(
            detour,
            preservedTopFirst,
            player,
            reconnectDistance: 1f,
            duplicateDistance: 1f);

        merged.Should().Equal(
            new Vector3(9, 5, 0),
            new Vector3(18, 4, 0),
            new Vector3(20, 0, 0),
            new Vector3(30, 0, 0),
            new Vector3(40, 0, 0),
            new Vector3(50, 0, 0));
    }

    // ---- Route-aware detour anchor selection ----

    [Fact]
    public void TrySelectDetourAnchor_LongRoute_SelectsFirstPointBeyondMinDistance()
    {
        Vector3 player = new(0, 0, 0);
        Vector3[] routeTopFirst =
        [
            new(3, 0, 0),
            new(7, 0, 0),
            new(12, 0, 0), // first >= 8f within lookahead
            new(20, 0, 0),
            new(40, 0, 0)
        ];

        bool selected = NavigationGoal.TrySelectDetourAnchor(
            routeTopFirst,
            player,
            out Vector3 anchor,
            lookaheadPoints: 4,
            minAnchorDistance: 8f);

        selected.Should().BeTrue();
        anchor.Should().Be(new Vector3(12, 0, 0));
    }

    [Fact]
    public void TrySelectDetourAnchor_AllTooClose_FallsBackToFurthestInLookahead()
    {
        Vector3 player = new(0, 0, 0);
        Vector3[] routeTopFirst =
        [
            new(1, 0, 0),
            new(2, 0, 0),
            new(3, 0, 0),
            new(4, 0, 0),
            new(20, 0, 0)
        ];

        bool selected = NavigationGoal.TrySelectDetourAnchor(
            routeTopFirst,
            player,
            out Vector3 anchor,
            lookaheadPoints: 4,
            minAnchorDistance: 8f);

        selected.Should().BeTrue();
        anchor.Should().Be(new Vector3(4, 0, 0), "fallback should pick furthest point in the inspected lookahead window");
    }

    [Fact]
    public void TrySelectDetourAnchor_NoViableDistance_ReturnsFalse()
    {
        Vector3 player = new(0, 0, 0);
        Vector3[] routeTopFirst =
        [
            new(0, 0, 0),
            new(0, 0, 0),
            new(0, 0, 0)
        ];

        bool selected = NavigationGoal.TrySelectDetourAnchor(
            routeTopFirst,
            player,
            out _,
            lookaheadPoints: 4,
            minAnchorDistance: 8f);

        selected.Should().BeFalse();
    }

    // ---- Pending reroute guards ----

    [Fact]
    public void ShouldApplyPendingReroute_TargetMissingFromRoute_ReturnsFalse()
    {
        RerouteInfo pending = new()
        {
            StartedAt = DateTime.UtcNow,
            OriginalTarget = new Vector3(200, 200, 0),
            DetourWaypoints = [new Vector3(0, 0, 0), new Vector3(10, 10, 0)]
        };
        Vector3[] routeTopFirst =
        [
            new(10, 0, 0),
            new(20, 0, 0),
            new(30, 0, 0)
        ];

        bool shouldApply = NavigationGoal.ShouldApplyPendingReroute(
            pending,
            routeTopFirst,
            DateTime.UtcNow,
            maxAgeSeconds: 15,
            targetMatchDistance: 4f);

        shouldApply.Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyPendingReroute_StaleReroute_ReturnsFalse()
    {
        DateTime now = DateTime.UtcNow;
        RerouteInfo pending = new()
        {
            StartedAt = now.AddSeconds(-20),
            OriginalTarget = new Vector3(20, 0, 0),
            DetourWaypoints = [new Vector3(0, 0, 0), new Vector3(10, 10, 0)]
        };
        Vector3[] routeTopFirst =
        [
            new(10, 0, 0),
            new(20, 0, 0),
            new(30, 0, 0)
        ];

        bool shouldApply = NavigationGoal.ShouldApplyPendingReroute(
            pending,
            routeTopFirst,
            now,
            maxAgeSeconds: 15,
            targetMatchDistance: 4f);

        shouldApply.Should().BeFalse();
    }

    [Fact]
    public void ShouldApplyPendingReroute_FreshAndOnRoute_ReturnsTrue()
    {
        DateTime now = DateTime.UtcNow;
        RerouteInfo pending = new()
        {
            StartedAt = now.AddSeconds(-5),
            OriginalTarget = new Vector3(20, 1, 0), // within 4f of route point (20,0,0)
            DetourWaypoints = [new Vector3(0, 0, 0), new Vector3(10, 10, 0)]
        };
        Vector3[] routeTopFirst =
        [
            new(10, 0, 0),
            new(20, 0, 0),
            new(30, 0, 0)
        ];

        bool shouldApply = NavigationGoal.ShouldApplyPendingReroute(
            pending,
            routeTopFirst,
            now,
            maxAgeSeconds: 15,
            targetMatchDistance: 4f);

        shouldApply.Should().BeTrue();
    }

    [Fact]
    public void IsSharpTurn_IndoorThreshold_DetectsBendMissedByOutdoorThreshold()
    {
        // 20° bend is below the outdoor 30° preserve threshold but meets the indoor 20° threshold.
        // This is the key scenario: a corridor angle that RDP would cut outdoors but must preserve indoors.
        float angle20 = 20f * MathF.PI / 180f;
        Vector3 from = new(0, 0, 0);
        Vector3 via  = new(10, 0, 0);
        Vector3 to   = new(via.X + MathF.Cos(angle20) * 10f,
                           via.Y + MathF.Sin(angle20) * 10f, 0);

        float indoorThreshold  = MathF.PI / 9f;  // 20°
        float outdoorThreshold = MathF.PI / 6f;  // 30°

        IsSharpTurn(from, via, to, indoorThreshold).Should()
            .BeTrue("20° meets the indoor 20° preservation threshold");
        IsSharpTurn(from, via, to, outdoorThreshold).Should()
            .BeFalse("20° is below the outdoor 30° preservation threshold");
    }

    [Fact]
    public void IsSharpTurn_FortyFiveDegrees_IsSharp_Internal()
    {
        // Test mounted sharp-turn preservation: 45 degree turn must be preserved
        float angle45 = 45f * MathF.PI / 180f;
        var from = new Vector3(0, 0, 0);
        var via = new Vector3(10, 0, 0);
        var to = new Vector3(via.X + MathF.Cos(angle45) * 10f,
                             via.Y + MathF.Sin(angle45) * 10f, 0);

        float minTurnRadians = MathF.PI / 6f; // 30 degrees
        bool result = IsSharpTurn(from, via, to, minTurnRadians);
        result.Should().BeTrue("45 degree turn should be detected as sharp (> 30 degrees) - needed for mounted preservation");
    }

    // ---- Navigation.IsSharpTurn (real internal static) boundary tests ----
    // These call the production implementation directly via InternalsVisibleTo.
    // The mirror tests above validate the algorithm; these tests lock in the >= boundary semantics.

    [Fact]
    public void Navigation_IsSharpTurn_ExactThresholdAngle_ReturnsTrue()
    {
        // Exactly 30° == threshold (>= means true at the boundary)
        float threshold = MathF.PI / 6f; // 30°
        var from = new Vector3(0, 0, 0);
        var via = new Vector3(10, 0, 0);
        var to = new Vector3(via.X + MathF.Cos(threshold) * 10f,
                             via.Y + MathF.Sin(threshold) * 10f, 0);

        NavigationGoal.IsSharpTurn(from, via, to, threshold)
            .Should().BeTrue("angle == threshold satisfies >=, so IsSharpTurn must return true");
    }

    [Fact]
    public void Navigation_IsSharpTurn_BelowThreshold_ReturnsFalse()
    {
        // 15° < 30° threshold
        float threshold = MathF.PI / 6f; // 30°
        float angle15 = MathF.PI / 12f;  // 15°
        var from = new Vector3(0, 0, 0);
        var via = new Vector3(10, 0, 0);
        var to = new Vector3(via.X + MathF.Cos(angle15) * 10f,
                             via.Y + MathF.Sin(angle15) * 10f, 0);

        NavigationGoal.IsSharpTurn(from, via, to, threshold)
            .Should().BeFalse("15° is below the 30° threshold");
    }

    [Fact]
    public void Navigation_IsSharpTurn_StraightPath_ReturnsFalse()
    {
        // 0° — perfectly straight, angle = 0 < threshold
        var from = new Vector3(0, 0, 0);
        var via = new Vector3(5, 0, 0);
        var to = new Vector3(10, 0, 0);

        NavigationGoal.IsSharpTurn(from, via, to, MathF.PI / 6f)
            .Should().BeFalse("straight path has 0° angle, always below threshold");
    }

    [Fact]
    public void Navigation_IsSharpTurn_NinetyDegrees_ReturnsTrue()
    {
        // 90° well above any practical threshold
        var from = new Vector3(0, 0, 0);
        var via = new Vector3(10, 0, 0);
        var to = new Vector3(10, 10, 0); // 90° left turn at via

        NavigationGoal.IsSharpTurn(from, via, to, MathF.PI / 6f)
            .Should().BeTrue("90° >> 30° threshold");
    }
}
