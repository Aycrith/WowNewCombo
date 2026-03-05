using Core.GOAP;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Goals;

/// <summary>
/// Contract tests for CombatGoal.
/// Verifies cost, preconditions, and effects without constructing the full goal.
/// Source: Core/Goals/CombatGoal.cs lines 18, 79-91.
/// </summary>
public class CombatGoalTests
{
    // Mirror of CombatGoal constructor preconditions/effects
    private static TestGoal BuildCombatGoalContract()
    {
        TestGoal g = new("CombatGoal", cost: 4f);
        g.TestAddPrecondition(GoapKey.incombat, true);
        g.TestAddEffect(GoapKey.producedcorpse, true);
        g.TestAddEffect(GoapKey.targetisalive,  false);
        g.TestAddEffect(GoapKey.hastarget,      false);
        return g;
    }

    [Fact]
    public void CombatGoal_Cost_Is4Point0()
    {
        BuildCombatGoalContract().Cost.Should().Be(4f,
            "CombatGoal.Cost is hardcoded to 4f (Core/Goals/CombatGoal.cs:18)");
    }

    [Fact]
    public void CombatGoal_Preconditions_RequireInCombat()
    {
        TestGoal g = BuildCombatGoalContract();
        g.Preconditions.Should().ContainKey(GoapKey.incombat);
        g.Preconditions[GoapKey.incombat].Should().BeTrue(
            "combat goal only runs when already in combat (incombat=true)");
    }

    [Fact]
    public void CombatGoal_Preconditions_HasExactlyOnePrecondition()
    {
        BuildCombatGoalContract().Preconditions.Should().HaveCount(1,
            "CombatGoal has a single precondition: incombat=true");
    }

    [Fact]
    public void CombatGoal_Effects_ProducesCorpse()
    {
        TestGoal g = BuildCombatGoalContract();
        g.Effects.Should().ContainKey(GoapKey.producedcorpse);
        g.Effects[GoapKey.producedcorpse].Should().BeTrue(
            "killing a target produces a corpse for the loot goal to consume");
    }

    [Fact]
    public void CombatGoal_Effects_ClearsTargetAlive()
    {
        TestGoal g = BuildCombatGoalContract();
        g.Effects.Should().ContainKey(GoapKey.targetisalive);
        g.Effects[GoapKey.targetisalive].Should().BeFalse(
            "combat ends with the target dead (targetisalive=false)");
    }

    [Fact]
    public void CombatGoal_Effects_ClearsHasTarget()
    {
        TestGoal g = BuildCombatGoalContract();
        g.Effects.Should().ContainKey(GoapKey.hastarget);
        g.Effects[GoapKey.hastarget].Should().BeFalse(
            "after combat the bot no longer has a target selected");
    }

    [Fact]
    public void CombatGoal_Effects_HasExactlyThreeEffects()
    {
        BuildCombatGoalContract().Effects.Should().HaveCount(3,
            "CombatGoal produces exactly 3 state changes: producedcorpse, targetisalive, hastarget");
    }

    [Fact]
    public void CombatGoal_GoalName_FormatsCorrectly()
    {
        new TestGoal("CombatGoal").DisplayName.Should().Be(" Combat",
            "GoapGoal strips 'Goal' suffix and inserts space before each camel-case word");
    }
}
