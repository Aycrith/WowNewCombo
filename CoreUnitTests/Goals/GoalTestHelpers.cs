using Core.GOAP;
using Core.Goals;

namespace CoreUnitTests.Goals;

/// <summary>
/// Shared test helper used by goal contract tests.
/// Mirrors the pattern in GoapGoalTests.cs but exposes TestGoal as an internal type
/// so it can be reused across multiple test files in this namespace.
/// </summary>
internal sealed class TestGoal : GoapGoal
{
    public override float Cost { get; }
    private readonly bool canRunResult;

    public TestGoal(string name, float cost = 1.0f, bool canRun = true) : base(name)
    {
        Cost = cost;
        canRunResult = canRun;
    }

    public override bool CanRun() => canRunResult;

    public void TestAddPrecondition(GoapKey key, bool value) => AddPrecondition(key, value);
    public void TestAddEffect(GoapKey key, bool value) => AddEffect(key, value);
}
