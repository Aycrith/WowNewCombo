# P0-5: Eliminate Per-Recursion HashSet Allocation in GoapPlanner.BuildGraph

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace `new HashSet<GoapGoal>(usable)` allocated on every recursive call in `BuildGraph` with a `uint` bitmask exclusion pattern — reducing GOAP planning allocations to zero per `Plan()` call.

**Priority:** P0-HIGH — hot-path GC pressure at 500 Hz planning rate

**Estimated time:** 5-8 minutes

---

## Context

### Current implementation (`Core/GOAP/GoapPlanner.cs`)

**`Plan()` method (line 52):**
```csharp
public static Stack<GoapGoal> Plan(GoapGoal[] available, BitVector32 worldState, bool[] goal)
```

**`BuildGraph()` method (line 132):**
```csharp
private static int BuildGraph(
    Node parent,
    PriorityQueue<Node, float> leaves,
    HashSet<GoapGoal> usable,
    bool[] goal)
{
    foreach (GoapGoal action in usable)
    {
        if (!PopulateState(parent.State, action.Preconditions))
            continue;

        BitVector32 currentState = ApplyEffects(parent.State, action.Effects);
        float nodeCost = parent.RunningCost + action.CostOfPerformingAction;
        Node node = new(action, nodeCost, currentState, parent);

        if (InState(goal, currentState))
        {
            leaves.Enqueue(node, nodeCost);
        }
        else
        {
            HashSet<GoapGoal> subset = new(usable); // ← ALLOCATION ON EVERY CALL
            subset.Remove(action);
            BuildGraph(node, leaves, subset, goal);
        }
    }
    return leaves.Count;
}
```

**Thread-static cache fields (lines 20-30) — currently dead code:**
```csharp
[ThreadStatic] private static HashSet<GoapGoal>? cachedUsableGoals;
[ThreadStatic] private static GoapGoal[]? cachedAvailableGoals;
[ThreadStatic] private static BitVector32 cachedWorldStateBits;
[ThreadStatic] private static bool hasUsableGoalsCache;
```
These are never written because `EnableUsableGoalCache => false` (line 18).

### Allocation math

With 20-30 available goals and a 4-deep plan tree:
- Level 1: 25 calls × 1 HashSet = 25 allocations
- Level 2: 24 calls × 24 = 576 allocations
- Level 3: 23 × 23 = 529 allocations
- Level 4: 22 × 22 = 484 allocations
- **Total per Plan(): ~1,600+ HashSet allocations**

At 500 Hz GOAP rate = **800,000+ small allocations per second** → significant GC pressure.

### Fix approach: uint bitmask exclusion

Since goal counts are always < 32 in this codebase (28 goals confirmed), a `uint` bitmask fits all goals. Each bit represents whether goal `i` is still in the "usable" set for the current recursion branch. No heap allocation needed.

---

## Files

1. **`C:/WowClassicGrindBot/Core/GOAP/GoapPlanner.cs`** — refactor BuildGraph
2. **`C:/WowClassicGrindBot/CoreUnitTests/GOAP/GoapPlannerTests.cs`** — add regression guard test

---

## Step 1: Write regression guard test FIRST

Add to `GoapPlannerTests.cs` before making any changes:

```csharp
[Fact]
public void Plan_WithMultipleGoals_FindsOptimalPath_RegressionGuard()
{
    // This test captures current behavior before refactoring.
    // A→B→C chain: three goals, each requiring the previous effect.
    GoapGoal goalA = CreateGoal(
        preconditions: [],
        effects: [(GoapKey.IsAlive, true)],
        cost: 1f, canRun: true);

    GoapGoal goalB = CreateGoal(
        preconditions: [(GoapKey.IsAlive, true)],
        effects: [(GoapKey.HasTarget, true)],
        cost: 1f, canRun: true);

    GoapGoal goalC = CreateGoal(
        preconditions: [(GoapKey.HasTarget, true)],
        effects: [(GoapKey.Combat, true)],
        cost: 1f, canRun: true);

    BitVector32 worldState = CreateWorldState([]);
    bool[] goalState = CreateGoalState([(GoapKey.Combat, true)]);

    Stack<GoapGoal> plan = GoapPlanner.Plan([goalA, goalB, goalC], worldState, goalState);

    plan.Should().HaveCount(3);
    plan.Pop().Should().BeSameAs(goalA);  // first to execute
    plan.Pop().Should().BeSameAs(goalB);
    plan.Pop().Should().BeSameAs(goalC);  // last to execute
}
```

Run it to confirm it passes with the current implementation:
```bash
dotnet test CoreUnitTests --filter "Plan_WithMultipleGoals_FindsOptimalPath_RegressionGuard" --verbosity detailed
```

## Step 2: Refactor BuildGraph in GoapPlanner.cs

**New BuildGraph signature and body:**

```csharp
// Replace the old BuildGraph(Node, PriorityQueue, HashSet<GoapGoal>, bool[]) with:

private static void BuildGraph(
    Node parent,
    PriorityQueue<Node, float> leaves,
    GoapGoal[] usable,
    uint includeMask,   // bit i set = goal i is still candidate for this branch
    bool[] goal)
{
    for (int i = 0; i < usable.Length; i++)
    {
        if ((includeMask & (1u << i)) == 0)
            continue; // this goal already used in current path

        GoapGoal action = usable[i];

        if (!action.CanRun())
            continue;

        if (!PopulateState(parent.State, action.Preconditions))
            continue;

        BitVector32 currentState = ApplyEffects(parent.State, action.Effects);
        float nodeCost = parent.RunningCost + action.CostOfPerformingAction;
        Node node = new(action, nodeCost, currentState, parent);

        if (InState(goal, currentState))
        {
            leaves.Enqueue(node, nodeCost);
        }
        else
        {
            // Exclude goal i from future recursion in this branch — zero allocation
            uint nextMask = includeMask & ~(1u << i);
            BuildGraph(node, leaves, usable, nextMask, goal);
        }
    }
}
```

**Update Plan() to use the new signature:**

In `Plan()` (line 52), replace the `BuildGraph` call:

```csharp
// Old:
HashSet<GoapGoal> usable = new();
foreach (GoapGoal goal in available)
{
    if (goal.CanRun())
        usable.Add(goal);
}
// ...
BuildGraph(start, leaves, usable, goalState);

// New:
// Collect usable goals into array for bitmask indexing
int usableCount = 0;
GoapGoal[] usableBuffer = new GoapGoal[available.Length]; // stack-sized, typically 28 goals
foreach (GoapGoal g in available)
{
    if (g.CanRun())
        usableBuffer[usableCount++] = g;
}
GoapGoal[] usable = usableBuffer[..usableCount]; // slice to actual count (collection expression)

// Build initial mask with all usable goals included
uint allMask = usable.Length < 32
    ? (1u << usable.Length) - 1u
    : uint.MaxValue;

BuildGraph(start, leaves, usable, allMask, goalState);
```

**Important:** The `CanRun()` check was previously inside `BuildGraph` (via the HashSet only containing runnable goals). In the new version, keep the `CanRun()` check inside `BuildGraph` as shown above so goals that become unable-to-run mid-planning are still excluded.

## Step 3: Run regression test
```bash
dotnet test CoreUnitTests --filter "Plan_WithMultipleGoals_FindsOptimalPath_RegressionGuard" --verbosity detailed
```
**Expected:** PASS with new implementation.

## Step 4: Full GOAP test suite
```bash
dotnet test CoreUnitTests --filter "FullyQualifiedName~GoapPlanner" --verbosity detailed
```
**Expected:** All existing GoapPlanner tests pass.

## Step 5: Full solution tests
```bash
dotnet test MasterOfPuppets.sln --verbosity minimal
```
**Expected:** No regressions.

## Step 6: Commit
```bash
git add Core/GOAP/GoapPlanner.cs CoreUnitTests/GOAP/GoapPlannerTests.cs
git commit -m "perf(goap): replace per-recursion HashSet<GoapGoal> with uint bitmask in BuildGraph - zero alloc"
```

---

## Constraint: Goal count must be < 32

The `uint` bitmask supports at most 32 goals. Current goal count: **28**. If goals ever exceed 32, this will silently exclude goals beyond bit 31.

**Add a guard in Plan():**
```csharp
if (available.Length > 31)
    throw new InvalidOperationException(
        $"GoapPlanner bitmask supports at most 31 goals; got {available.Length}. " +
        "Increase bitmask type to ulong to support up to 63 goals.");
```

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Bitmask logic inverted (wrong goals excluded) | Medium | Run all 735 lines of GoapPlannerTests; regression guard test |
| CanRun() called twice (once in Plan, once in BuildGraph) | Low | Keep CanRun() only in BuildGraph; remove from Plan() filter |
| Goal count exceeds 32 in future | Low | Add explicit guard with clear error message |
| Plan quality changes (different optimal path) | Low | The A* cost minimization is unchanged; same optimal result |
