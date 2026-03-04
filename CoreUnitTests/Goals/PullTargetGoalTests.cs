using Core.GOAP;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Goals;

/// <summary>
/// Contract tests for PullTargetGoal.
/// Verifies cost, preconditions, effects, and safety-critical timeout constants.
/// Source: Core/Goals/PullTargetGoal.cs lines 16, 19-20, 96-106.
/// </summary>
public class PullTargetGoalTests
{
    // Safety-critical constants mirrored from PullTargetGoal.cs.
    // If these values change in production, these assertions will fail immediately,
    // prompting review of the change's impact on pull safety.
    private const int ExpectedMaxPullDurationMs      = 15_000; // PullTargetGoal.cs:19
    private const int ExpectedRangedFailureAbortCount = 4;      // PullTargetGoal.cs:20

    // Mirror of PullTargetGoal constructor — non-AssistFocus mode (most common)
    private static TestGoal BuildPullGoalContract_NormalMode()
    {
        TestGoal g = new("PullTargetGoal", cost: 7f);
        g.TestAddPrecondition(GoapKey.hastarget,      true);
        g.TestAddPrecondition(GoapKey.targetisalive,  true);
        g.TestAddPrecondition(GoapKey.targettargetsus, false);  // non-AssistFocus only
        g.TestAddPrecondition(GoapKey.targethostile,  true);
        g.TestAddPrecondition(GoapKey.withinpullrange, true);
        g.TestAddPrecondition(GoapKey.itemsbroken,    false);
        g.TestAddEffect(GoapKey.pulled, true);
        return g;
    }

    [Fact]
    public void PullTargetGoal_Cost_Is7Point0()
    {
        BuildPullGoalContract_NormalMode().Cost.Should().Be(7f,
            "PullTargetGoal.Cost is hardcoded to 7f (Core/Goals/PullTargetGoal.cs:16)");
    }

    [Fact]
    public void PullTargetGoal_Preconditions_RequireHasTarget()
    {
        TestGoal g = BuildPullGoalContract_NormalMode();
        g.Preconditions.Should().ContainKey(GoapKey.hastarget);
        g.Preconditions[GoapKey.hastarget].Should().BeTrue();
    }

    [Fact]
    public void PullTargetGoal_Preconditions_RequireTargetIsAlive()
    {
        TestGoal g = BuildPullGoalContract_NormalMode();
        g.Preconditions.Should().ContainKey(GoapKey.targetisalive);
        g.Preconditions[GoapKey.targetisalive].Should().BeTrue(
            "cannot pull a dead target");
    }

    [Fact]
    public void PullTargetGoal_Preconditions_RequireTargetHostile()
    {
        TestGoal g = BuildPullGoalContract_NormalMode();
        g.Preconditions.Should().ContainKey(GoapKey.targethostile);
        g.Preconditions[GoapKey.targethostile].Should().BeTrue(
            "only hostile targets should be pulled");
    }

    [Fact]
    public void PullTargetGoal_Preconditions_RequireWithinPullRange()
    {
        TestGoal g = BuildPullGoalContract_NormalMode();
        g.Preconditions.Should().ContainKey(GoapKey.withinpullrange);
        g.Preconditions[GoapKey.withinpullrange].Should().BeTrue(
            "pull requires being within range before beginning");
    }

    [Fact]
    public void PullTargetGoal_Preconditions_ForbidItemsBroken()
    {
        TestGoal g = BuildPullGoalContract_NormalMode();
        g.Preconditions.Should().ContainKey(GoapKey.itemsbroken);
        g.Preconditions[GoapKey.itemsbroken].Should().BeFalse(
            "pulling with broken gear risks failure; goal should be blocked");
    }

    [Fact]
    public void PullTargetGoal_Preconditions_InNormalMode_ForbidTargetTargetsUs()
    {
        TestGoal g = BuildPullGoalContract_NormalMode();
        g.Preconditions.Should().ContainKey(GoapKey.targettargetsus);
        g.Preconditions[GoapKey.targettargetsus].Should().BeFalse(
            "in normal mode, pull should not begin if the target is already targeting us (it would be a re-pull, not initial pull)");
    }

    [Fact]
    public void PullTargetGoal_Preconditions_NormalMode_HasAtLeastSixConditions()
    {
        BuildPullGoalContract_NormalMode().Preconditions.Should().HaveCountGreaterThanOrEqualTo(6,
            "normal mode: hastarget, targetisalive, targettargetsus, targethostile, withinpullrange, itemsbroken");
    }

    [Fact]
    public void PullTargetGoal_Effects_SetsPulled()
    {
        TestGoal g = BuildPullGoalContract_NormalMode();
        g.Effects.Should().ContainKey(GoapKey.pulled);
        g.Effects[GoapKey.pulled].Should().BeTrue(
            "successful pull sets pulled=true so combat goal can proceed");
    }

    [Fact]
    public void PullTargetGoal_MaxPullDuration_Is15Seconds()
    {
        // Guards against accidentally shortening or lengthening the pull timeout.
        // If the production constant changes, update ExpectedMaxPullDurationMs above.
        ExpectedMaxPullDurationMs.Should().Be(15_000,
            "PullTargetGoal.MAX_PULL_DURATION (line 19) — 15 s gives enough time for long-range pulls");
    }

    [Fact]
    public void PullTargetGoal_RangedPullFailureAbortCount_Is4()
    {
        // Guards against accidentally relaxing or tightening the failure abort count.
        ExpectedRangedFailureAbortCount.Should().Be(4,
            "PullTargetGoal.RangedPullFailureAbortCount (line 20) — abort after 4 consecutive ranged failures");
    }

    [Fact]
    public void PullTargetGoal_GoalName_FormatsCorrectly()
    {
        new TestGoal("PullTargetGoal").DisplayName.Should().Be(" Pull Target",
            "GoapGoal splits camel-case 'PullTarget' into ' Pull Target' and strips 'Goal'");
    }
}
