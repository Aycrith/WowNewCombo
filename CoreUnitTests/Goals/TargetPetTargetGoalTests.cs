using Core.GOAP;

using FluentAssertions;

using System.Collections.Generic;

using Xunit;

namespace CoreUnitTests.Goals;

public sealed class TargetPetTargetGoalTests
{
    private static TestGoal BuildTargetPetTargetGoalContract(bool keyboardOnly)
    {
        TestGoal g = new("TargetPetTargetGoal", cost: 4.01f);
        g.TestAddPrecondition(GoapKey.targetisalive, false);
        g.TestAddPrecondition(GoapKey.producedcorpse, false);
        g.TestAddPrecondition(GoapKey.shouldloot, false);

        if (keyboardOnly)
        {
            g.TestAddPrecondition(GoapKey.consumablecorpsenearby, false);
        }
        else
        {
            g.TestAddPrecondition(GoapKey.damagetakenordone, true);
        }

        g.TestAddPrecondition(GoapKey.pethastarget, true);
        g.TestAddEffect(GoapKey.hastarget, true);
        return g;
    }

    [Fact]
    public void KeyboardContract_BlocksPetTargetGoalWhileCorpseStateIsActive()
    {
        TestGoal g = BuildTargetPetTargetGoalContract(keyboardOnly: true);

        g.Preconditions.Should().Contain(new KeyValuePair<GoapKey, bool>(GoapKey.producedcorpse, false));
        g.Preconditions.Should().Contain(new KeyValuePair<GoapKey, bool>(GoapKey.shouldloot, false));
        g.Preconditions.Should().Contain(new KeyValuePair<GoapKey, bool>(GoapKey.consumablecorpsenearby, false));
    }

    [Fact]
    public void MouseContract_BlocksPetTargetGoalWhileLootIsPending()
    {
        TestGoal g = BuildTargetPetTargetGoalContract(keyboardOnly: false);

        g.Preconditions.Should().Contain(new KeyValuePair<GoapKey, bool>(GoapKey.producedcorpse, false));
        g.Preconditions.Should().Contain(new KeyValuePair<GoapKey, bool>(GoapKey.shouldloot, false));
        g.Preconditions.Should().Contain(new KeyValuePair<GoapKey, bool>(GoapKey.damagetakenordone, true));
    }
}
