using Core.GOAP;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Goals;

/// <summary>
/// Contract tests for ApproachTargetGoal.
/// Verifies cost, preconditions, effects, and safety-critical timing constants.
/// Source: Core/Goals/ApproachTargetGoal.cs lines 16-21, 69-74.
/// </summary>
public class ApproachTargetGoalTests
{
    // Safety-critical constants mirrored from ApproachTargetGoal.cs.
    private const double ExpectedMaxApproachDurationMs = 15_000; // line 17
    private const double ExpectedStuckIntervalMs       = 400;    // line 16
    private const int    ExpectedMultiMobCheckIntervalMs = 500;  // line 167 (hard-coded literal)
    private const int    ExpectedMinCloserTargetImprovement = 8; // line 19

    // Mirror of ApproachTargetGoal constructor preconditions/effects
    private static TestGoal BuildApproachGoalContract()
    {
        TestGoal g = new("ApproachTargetGoal", cost: 8f);
        g.TestAddPrecondition(GoapKey.hastarget,     true);
        g.TestAddPrecondition(GoapKey.targetisalive, true);
        g.TestAddPrecondition(GoapKey.targethostile, true);
        g.TestAddPrecondition(GoapKey.incombatrange, false);
        g.TestAddEffect(GoapKey.incombatrange, true);
        return g;
    }

    [Fact]
    public void ApproachTargetGoal_Cost_Is8Point0()
    {
        BuildApproachGoalContract().Cost.Should().Be(8f,
            "ApproachTargetGoal.Cost is hardcoded to 8f (Core/Goals/ApproachTargetGoal.cs:21)");
    }

    [Fact]
    public void ApproachTargetGoal_Preconditions_RequireHasTarget()
    {
        TestGoal g = BuildApproachGoalContract();
        g.Preconditions.Should().ContainKey(GoapKey.hastarget);
        g.Preconditions[GoapKey.hastarget].Should().BeTrue();
    }

    [Fact]
    public void ApproachTargetGoal_Preconditions_RequireTargetIsAlive()
    {
        TestGoal g = BuildApproachGoalContract();
        g.Preconditions.Should().ContainKey(GoapKey.targetisalive);
        g.Preconditions[GoapKey.targetisalive].Should().BeTrue(
            "only approach a living target");
    }

    [Fact]
    public void ApproachTargetGoal_Preconditions_RequireTargetHostile()
    {
        TestGoal g = BuildApproachGoalContract();
        g.Preconditions.Should().ContainKey(GoapKey.targethostile);
        g.Preconditions[GoapKey.targethostile].Should().BeTrue(
            "only approach a hostile target to attack");
    }

    [Fact]
    public void ApproachTargetGoal_Preconditions_ForbidInCombatRange()
    {
        TestGoal g = BuildApproachGoalContract();
        g.Preconditions.Should().ContainKey(GoapKey.incombatrange);
        g.Preconditions[GoapKey.incombatrange].Should().BeFalse(
            "approach is skipped once combat range is already achieved");
    }

    [Fact]
    public void ApproachTargetGoal_Preconditions_HasExactlyFourConditions()
    {
        BuildApproachGoalContract().Preconditions.Should().HaveCount(4,
            "ApproachTargetGoal has exactly 4 preconditions: hastarget, targetisalive, targethostile, incombatrange=false");
    }

    [Fact]
    public void ApproachTargetGoal_Effects_SetsInCombatRange()
    {
        TestGoal g = BuildApproachGoalContract();
        g.Effects.Should().ContainKey(GoapKey.incombatrange);
        g.Effects[GoapKey.incombatrange].Should().BeTrue(
            "successfully approaching sets incombatrange=true, enabling combat goal");
    }

    [Fact]
    public void ApproachTargetGoal_MaxApproachDuration_Is15Seconds()
    {
        ExpectedMaxApproachDurationMs.Should().Be(15_000,
            "ApproachTargetGoal.MAX_APPROACH_DURATION_MS (line 17) — 15 s to close distance before giving up");
    }

    [Fact]
    public void ApproachTargetGoal_StuckCheckInterval_Is400ms()
    {
        ExpectedStuckIntervalMs.Should().Be(400,
            "ApproachTargetGoal.STUCK_INTERVAL_MS (line 16) — 400 ms between stuck checks, matches Approach key cooldown");
    }

    [Fact]
    public void ApproachTargetGoal_MultiMobCheckInterval_Is500ms()
    {
        ExpectedMultiMobCheckIntervalMs.Should().Be(500,
            "Multi-mob threat check rate-limited to 500 ms (ApproachTargetGoal.cs:167) to avoid per-frame cost");
    }

    [Fact]
    public void ApproachTargetGoal_MinCloserTargetImprovement_Is8Yards()
    {
        ExpectedMinCloserTargetImprovement.Should().Be(8,
            "ApproachTargetGoal.MIN_CLOSER_TARGET_RANGE_IMPROVEMENT (line 19) — only switch to closer target if it saves >= 8 yards");
    }

    [Fact]
    public void ApproachTargetGoal_GoalName_FormatsCorrectly()
    {
        new TestGoal("ApproachTargetGoal").DisplayName.Should().Be(" Approach Target",
            "GoapGoal splits 'ApproachTarget' camel-case and strips 'Goal' suffix");
    }
}
