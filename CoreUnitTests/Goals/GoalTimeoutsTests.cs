using Core.Goals;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Goals;

public sealed class GoalTimeoutsTests
{
    [Fact]
    public void MaxTimeToReachMeleeMs_IsExpectedValue()
    {
        GoalTimeouts.MaxTimeToReachMeleeMs.Should().Be(10_000,
            "all goal files rely on this value for melee approach timeout");
    }
}
