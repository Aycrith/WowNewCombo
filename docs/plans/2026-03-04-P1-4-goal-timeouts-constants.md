# P1-4: Centralize Magic Timeout Constants in GoalTimeouts

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Extract duplicate magic number timeout constants from four goal files into a single `GoalTimeouts` static class, eliminating copy-paste drift and enabling future per-profile tuning.

**Priority:** P1 — MEDIUM maintainability

**Estimated time:** 5 minutes

---

## Context

### Duplicate constants confirmed across files

**`Core/Goals/AdhocNPCGoal.cs` (lines 43-50):**
```csharp
private const int MAX_TIME_TO_REACH_MELEE = 10000;
private const int TIMEOUT = 5000;
private const float NPC_DESTINATION_PROXIMITY = 12f;
private const int MAX_FAR_DESTINATION_RETRIES = 3;
private const float MAX_AUTO_NPC_TRAVEL_DISTANCE = 750f;
private const float MAX_AUTO_NPC_VERTICAL_DELTA = 60f;
```

**`Core/Goals/LootGoal.cs` (line 23):**
```csharp
private const int MAX_TIME_TO_REACH_MELEE = 10000;
```

**`Core/Goals/SkinningGoal.cs` (likely line 18):**
```csharp
private const int MAX_TIME_TO_REACH_MELEE = 10000;  // CHECK: verify this exists
```

**`Core/Goals/PullTargetGoal.cs` (lines 18-21):**
```csharp
private const int AcquireTargetTimeMs = 5000;
private const int MAX_PULL_DURATION = 15_000;
private const int RangedPullFailureAbortCount = 4;
private const int FaceTargetAssistCooldownMs = 350;
```

**`Core/Goals/FollowRouteGoal.cs` (lines 56, 62):**
```csharp
private const int MIN_TIME_TO_START_CYCLE_PROFESSION = 5000;
private const int CYCLE_PROFESSION_PERIOD = 8000;
```

### Which to centralize

Only constants that are **duplicated** or **operator-tunable** should move. File-specific constants (like `NPC_DESTINATION_PROXIMITY = 12f`) stay where they are.

---

## Files

1. **Create: `C:/WowClassicGrindBot/Core/Goals/GoalTimeouts.cs`**
2. **Modify: `Core/Goals/AdhocNPCGoal.cs`** (line 43)
3. **Modify: `Core/Goals/LootGoal.cs`** (line 23)
4. **Modify: `Core/Goals/SkinningGoal.cs`** (line 18 — verify first)
5. **Modify: `Core/Goals/PullTargetGoal.cs`** (lines 18-19)
6. **Modify: `Core/Goals/FollowRouteGoal.cs`** (lines 56, 62)
7. **Create: `C:/WowClassicGrindBot/CoreUnitTests/Goals/GoalTimeoutsTests.cs`**

---

## Step 1: Check SkinningGoal.cs for the constant
```bash
grep -n "MAX_TIME_TO_REACH_MELEE\|10000\|10_000" Core/Goals/SkinningGoal.cs
```

## Step 2: Write failing test (regression guard)

Create `CoreUnitTests/Goals/GoalTimeoutsTests.cs`:
```csharp
using Core.Goals;
using FluentAssertions;
using Xunit;

namespace CoreUnitTests.Goals;

/// <summary>
/// Regression guard: verifies GoalTimeouts constants have expected values.
/// These values match the original per-class constants and must not change without review.
/// </summary>
public sealed class GoalTimeoutsTests
{
    [Fact]
    public void GoalTimeouts_AllConstants_HaveExpectedProductionValues()
    {
        // These values were extracted from individual goal classes.
        // If you need to change them, ensure all goals that reference them
        // still behave correctly at the new value.
        GoalTimeouts.MaxTimeToReachMeleeMs.Should().Be(10_000,
            "AdhocNPCGoal, LootGoal, and SkinningGoal all used 10000");

        GoalTimeouts.MaxPullDurationMs.Should().Be(15_000,
            "PullTargetGoal used MAX_PULL_DURATION = 15_000");

        GoalTimeouts.AcquireTargetMs.Should().Be(5_000,
            "PullTargetGoal used AcquireTargetTimeMs = 5000");

        GoalTimeouts.MinTimeToStartCycleProfessionMs.Should().Be(5_000,
            "FollowRouteGoal used MIN_TIME_TO_START_CYCLE_PROFESSION = 5000");

        GoalTimeouts.CycleProfessionPeriodMs.Should().Be(8_000,
            "FollowRouteGoal used CYCLE_PROFESSION_PERIOD = 8000");
    }
}
```

Run to confirm compilation failure (GoalTimeouts doesn't exist yet):
```bash
dotnet test CoreUnitTests --filter "GoalTimeoutsTests" --verbosity detailed
```

## Step 3: Create Core/Goals/GoalTimeouts.cs

```csharp
namespace Core.Goals;

/// <summary>
/// Shared timing constants for goal implementations.
/// Centralizes magic numbers that were previously duplicated across multiple goal classes.
/// All values in milliseconds unless otherwise noted.
/// </summary>
internal static class GoalTimeouts
{
    /// <summary>
    /// Maximum milliseconds allowed to close to melee range before aborting the approach.
    /// Used by: <see cref="AdhocNPCGoal"/>, <see cref="LootGoal"/>, <see cref="SkinningGoal"/>.
    /// </summary>
    internal const int MaxTimeToReachMeleeMs = 10_000;

    /// <summary>
    /// Maximum milliseconds to wait for a full pull sequence to complete before abandoning target.
    /// Used by: <see cref="PullTargetGoal"/>.
    /// </summary>
    internal const int MaxPullDurationMs = 15_000;

    /// <summary>
    /// Maximum milliseconds to wait for target acquisition after initiating a pull cast.
    /// Used by: <see cref="PullTargetGoal"/>.
    /// </summary>
    internal const int AcquireTargetMs = 5_000;

    /// <summary>
    /// Minimum milliseconds of route travel that must elapse before profession cycling may begin.
    /// Prevents premature profession checks immediately after spawning on a route.
    /// Used by: <see cref="FollowRouteGoal"/>.
    /// </summary>
    internal const int MinTimeToStartCycleProfessionMs = 5_000;

    /// <summary>
    /// Period in milliseconds between profession-cycle checks while following a route.
    /// Used by: <see cref="FollowRouteGoal"/>.
    /// </summary>
    internal const int CycleProfessionPeriodMs = 8_000;
}
```

## Step 4: Run test to confirm it now compiles and passes
```bash
dotnet test CoreUnitTests --filter "GoalTimeoutsTests" --verbosity detailed
```
**Expected:** PASS.

## Step 5: Update goal files to reference GoalTimeouts

**AdhocNPCGoal.cs line 43:**
```csharp
// Before:
private const int MAX_TIME_TO_REACH_MELEE = 10000;
// After:
private const int MAX_TIME_TO_REACH_MELEE = GoalTimeouts.MaxTimeToReachMeleeMs;
```

**LootGoal.cs line 23:**
```csharp
private const int MAX_TIME_TO_REACH_MELEE = GoalTimeouts.MaxTimeToReachMeleeMs;
```

**SkinningGoal.cs (if constant found at Step 1):**
```csharp
private const int MAX_TIME_TO_REACH_MELEE = GoalTimeouts.MaxTimeToReachMeleeMs;
```

**PullTargetGoal.cs lines 18-19:**
```csharp
private const int AcquireTargetTimeMs = GoalTimeouts.AcquireTargetMs;
private const int MAX_PULL_DURATION = GoalTimeouts.MaxPullDurationMs;
```

**FollowRouteGoal.cs lines 56, 62:**
```csharp
private const int MIN_TIME_TO_START_CYCLE_PROFESSION = GoalTimeouts.MinTimeToStartCycleProfessionMs;
private const int CYCLE_PROFESSION_PERIOD = GoalTimeouts.CycleProfessionPeriodMs;
```

**Note:** Keep the original field names (`MAX_TIME_TO_REACH_MELEE` etc.) to minimize diff size — only the value source changes, not the name used in the method body.

## Step 6: Build
```bash
dotnet build Core
```
**Expected:** 0 errors. The `const` fields still work because they reference another `const` — C# allows `const X = OtherClass.ConstY` when `OtherClass.ConstY` is also `const`.

## Step 7: Full test suite
```bash
dotnet test MasterOfPuppets.sln --verbosity minimal
```

## Step 8: Commit
```bash
git add Core/Goals/GoalTimeouts.cs Core/Goals/AdhocNPCGoal.cs Core/Goals/LootGoal.cs Core/Goals/SkinningGoal.cs Core/Goals/PullTargetGoal.cs Core/Goals/FollowRouteGoal.cs CoreUnitTests/Goals/GoalTimeoutsTests.cs
git commit -m "refactor(goals): centralize timeout constants in GoalTimeouts - eliminates 5+ duplicate definitions"
```

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `const X = GoalTimeouts.Y` doesn't compile | Very Low | In C#, one `const` can reference another `const` — both get inlined at compile time |
| SkinningGoal doesn't have the constant | Low | `grep` it first; if absent, skip that file |
| Behavior change from value change | None | Values are identical to originals (regression test guards this) |
