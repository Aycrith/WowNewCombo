using Core.Goals;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Goals;

public sealed class CorpseConsumedGoalTests
{
    [Fact]
    public void CanRunForLootState_WhenLootDisabled_AllowsCorpseConsumed()
    {
        bool canRun = CorpseConsumedGoal.CanRunForLootState(lootEnabled: false, lootableCorpseCount: 2);

        canRun.Should().BeTrue();
    }

    [Fact]
    public void CanRunForLootState_WhenLootEnabledAndPendingLootExists_BlocksCorpseConsumed()
    {
        bool canRun = CorpseConsumedGoal.CanRunForLootState(lootEnabled: true, lootableCorpseCount: 1);

        canRun.Should().BeFalse();
    }

    [Fact]
    public void CanRunForLootState_WhenLootEnabledAndNoPendingLootExists_AllowsCorpseConsumed()
    {
        bool canRun = CorpseConsumedGoal.CanRunForLootState(lootEnabled: true, lootableCorpseCount: 0);

        canRun.Should().BeTrue();
    }
}
