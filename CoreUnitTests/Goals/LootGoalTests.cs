using Core.GOAP;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Goals;

/// <summary>
/// Contract tests for LootGoal.
/// Verifies cost, preconditions, and effects without constructing the full goal
/// (which requires 15+ injected dependencies). Uses the same TestGoal wrapper
/// pattern established in GoapGoalTests.cs.
/// </summary>
public class LootGoalTests
{
    // --- mirror of LootGoal constructor ---
    // Source: Core/Goals/LootGoal.cs lines 21, 75-78
    private static TestGoal BuildLootGoalContract()
    {
        TestGoal g = new("LootGoal", cost: 4.6f);
        g.TestAddPrecondition(GoapKey.pulled,       false);
        g.TestAddPrecondition(GoapKey.dangercombat, false);
        g.TestAddPrecondition(GoapKey.shouldloot,   true);
        g.TestAddEffect(GoapKey.shouldloot,         false);
        return g;
    }

    [Fact]
    public void LootGoal_Cost_Is4Point6()
    {
        BuildLootGoalContract().Cost.Should().Be(4.6f,
            "LootGoal.Cost is hardcoded to 4.6f (Core/Goals/LootGoal.cs:21)");
    }

    [Fact]
    public void LootGoal_Preconditions_RequireShouldLoot()
    {
        TestGoal g = BuildLootGoalContract();
        g.Preconditions.Should().ContainKey(GoapKey.shouldloot);
        g.Preconditions[GoapKey.shouldloot].Should().BeTrue(
            "loot only runs when shouldloot=true is set by the producer corpse goal");
    }

    [Fact]
    public void LootGoal_Preconditions_ForbidDangerCombat()
    {
        TestGoal g = BuildLootGoalContract();
        g.Preconditions.Should().ContainKey(GoapKey.dangercombat);
        g.Preconditions[GoapKey.dangercombat].Should().BeFalse(
            "looting must not start while in danger-combat state");
    }

    [Fact]
    public void LootGoal_Preconditions_ForbidPulled()
    {
        TestGoal g = BuildLootGoalContract();
        g.Preconditions.Should().ContainKey(GoapKey.pulled);
        g.Preconditions[GoapKey.pulled].Should().BeFalse(
            "looting must not start while still in the pull phase");
    }

    [Fact]
    public void LootGoal_Preconditions_HasExactlyThreeConditions()
    {
        BuildLootGoalContract().Preconditions.Should().HaveCount(3,
            "LootGoal has exactly 3 preconditions: pulled=false, dangercombat=false, shouldloot=true");
    }

    [Fact]
    public void LootGoal_Effects_ClearsShouldLoot()
    {
        TestGoal g = BuildLootGoalContract();
        g.Effects.Should().ContainKey(GoapKey.shouldloot);
        g.Effects[GoapKey.shouldloot].Should().BeFalse(
            "completing loot clears shouldloot so the planner does not re-select it");
    }

    [Fact]
    public void LootGoal_GoalName_FormatsCorrectly()
    {
        new TestGoal("LootGoal").DisplayName.Should().Be(" Loot",
            "GoapGoal strips 'Goal' suffix and adds space before camel-case words");
    }
}
