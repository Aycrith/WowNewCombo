# Navigation Stability Plan - Implementation Handoff

**Status:** Task 1 Complete. Tasks 2–9 Remaining.
**Current Branch:** `dev`
**Current Commit:** `21ca6c0a9` (fix(nav): remove 200ms WaitForTurn cap)
**Build Status:** ✓ Passing (1673 unit tests pass, 0 failed)

---

## Quick Start for Next Agent

You are continuing the **Navigation Stability, Robustness & Performance Implementation Plan** documented in:
```
C:\WowClassicGrindBot\docs\plans\2026-02-24-navigation-stability.md
```

**Your job:** Execute Tasks 2–9 from that plan sequentially, following the TDD + commit pattern established in Task 1.

### Immediate Actions

1. **Verify pre-flight:**
   ```bash
   cd /c/WowClassicGrindBot
   dotnet build MasterOfPuppets.sln --nologo -q
   dotnet test CoreUnitTests --no-build --verbosity minimal
   ```
   Expected: "Build succeeded. 0 Error(s)" + "Passed! Failed: 0"

2. **Check current state:**
   ```bash
   git status --short
   ```
   Expected: Only `.tmp_blazor_ts.txt`, `.tmp_nav_ts.txt`, `test-output/` untracked.

3. **Proceed to Task 2** (instructions below).

---

## Task 1 Summary (Completed)

**Commit:** `21ca6c0a9`
**Files changed:**
- `Core/GoalsComponent/PlayerDirection.cs` (1 line changed: line 152)
- `CoreUnitTests/GoalsComponent/PlayerDirectionTests.cs` (new file, 47 lines)

**What was fixed:** Removed `Math.Min(..., 200)` hard cap from `WaitForTurn()`. Turns > 67° now wait the full required duration (up to 750ms) instead of being cut short at 200ms, eliminating stutter-turning on sharp waypoints.

---

## Tasks 2–9: Complete Instructions

All tasks follow the same pattern:
1. **Create test file** (if new tests needed)
2. **Run tests to verify they fail** (for TDD)
3. **Write minimal implementation** to pass tests
4. **Build + test** full suite
5. **Commit** with the provided message

### Task 2: Add heading-diff guard before OscillationDetector tracking (R2)

**Problem:** `OscillationDetector.TrackHeading()` called unconditionally before checking `diff > minAngleToTurn`, polluting the history with near-identical straight-path values.

**Files:**
- Modify: `Core/GoalsComponent/Navigation.cs:793-813`
- Create: `CoreUnitTests/GoalsComponent/OscillationDetectorTests.cs`

**Step 1: Create test file**

Save the complete test code from the plan (lines 337–440 in the plan document) to:
```
CoreUnitTests/GoalsComponent/OscillationDetectorTests.cs
```

**Step 2: Run tests (expect failures initially)**
```bash
dotnet build MasterOfPuppets.sln --nologo -q
dotnet test CoreUnitTests --filter "OscillationDetectorTests" --verbosity minimal
```

**Step 3: Implement the fix**

In `Core/GoalsComponent/Navigation.cs`, find `AdjustHeading()` around line 793. Locate:
```csharp
        if (stuckDetector.IsCurrentlyStuck)
        {
            oscillationDetector.Reset();
            return;
        }

        // Track heading for oscillation detection
        oscillationDetector.TrackHeading(playerReader.Direction);
```

Replace with:
```csharp
        if (stuckDetector.IsCurrentlyStuck)
        {
            oscillationDetector.Reset();
            return;
        }

        // Only track heading when actually correcting — avoids polluting
        // oscillation history on straight segments where diff < minAngleToTurn.
        if (diff > minAngleToTurn)
        {
            oscillationDetector.TrackHeading(playerReader.Direction);
        }
```

**Step 4: Run tests**
```bash
dotnet test CoreUnitTests --filter "OscillationDetectorTests" --verbosity minimal
```
Expected: All pass.

**Step 5: Full build + tests**
```bash
dotnet build MasterOfPuppets.sln --nologo -q
dotnet test CoreUnitTests --no-build --verbosity minimal
```
Expected: `Passed! Failed: 0`.

**Step 6: Commit**
```bash
git add Core/GoalsComponent/Navigation.cs CoreUnitTests/GoalsComponent/OscillationDetectorTests.cs
git commit -m "fix(nav): only track heading for oscillation when correction is needed

AdjustHeading was tracking heading unconditionally before checking
diff > minAngleToTurn, causing the oscillation detector queue to fill
with near-identical straight-path values. The 1500ms reset never fired
on active straight segments, making stale history available to the
threshold check at the next genuine turn. Guard: only call
TrackHeading() when diff > minAngleToTurn."
```

---

### Task 3: Hoist ReachedDistance() out of ReduceByDistance loop (R3)

**Problem:** `ReachedDistance()` called on every loop iteration (O(n) where n = waypoints popped per Update). Also avoids repeated `TryGetUpcomingRoutePoints()` enumeration.

**Files:**
- Modify: `Core/GoalsComponent/Navigation.cs:742-764`

**No tests needed for this task** — it's a pure optimization with no behavior change.

**Step 1: Read current implementation**
```bash
sed -n '742,764p' /c/WowClassicGrindBot/Core/GoalsComponent/Navigation.cs
```

**Step 2: Replace the method body**

Find `private void ReduceByDistance(Vector3 playerW, float minDistance, bool singlePop = false)` and replace the entire body with:

```csharp
    private void ReduceByDistance(Vector3 playerW, float minDistance, bool singlePop = false)
    {
        float reached = ReachedDistance(minDistance);

        while (routeToNextWaypoint.Count > 0 &&
               playerW.WorldDistanceXYTo(routeToNextWaypoint.Peek()) < reached)
        {
            routeToNextWaypoint.Pop();

            if (singlePop)
            {
                break;
            }

            // One enumeration per loop iteration: check upcoming turn only if
            // the player is still a meaningful distance from the next point
            // (guard ensures incoming vector playerW→curr points forward).
            if (routeToNextWaypoint.Count >= 2 &&
                TryGetUpcomingRoutePoints(out Vector3 curr, out Vector3 next) &&
                playerW.WorldDistanceXYTo(curr) > OutDoorMinDistance &&
                IsSharpTurn(playerW, curr, next, SimplifyPreserveTurnRadians))
            {
                break;
            }
        }
    }
```

**Step 3: Build + test**
```bash
dotnet build MasterOfPuppets.sln --nologo -q
dotnet test CoreUnitTests --no-build --verbosity minimal
```
Expected: `Passed! Failed: 0`.

**Step 4: Commit**
```bash
git add Core/GoalsComponent/Navigation.cs
git commit -m "perf(nav): hoist ReachedDistance() out of ReduceByDistance loop

ReachedDistance() called mountHandler.IsMounted() + bits.Indoors() on
every loop iteration. Hoist to a local before the while loop. Also
collapse the turn-break conditions to a single short-circuit expression
so TryGetUpcomingRoutePoints (O(n) stack enumeration) is skipped when
Count < 2."
```

---

### Task 4: Scope corpse-recovery hazard suppression to WalkToCorpseGoal only (R5)

**Problem:** `IsLikelyCorpseRecoveryContext()` returns true for the entire ghost-run duration due to `CorpseMapX/Y != 0` condition, suppressing ALL hazard detours even during normal navigation when corpse coordinate just happens to exist.

**Files:**
- Modify: `Core/GoalsComponent/Navigation.cs` — `IsLikelyCorpseRecoveryContext()` method

**No tests needed** — behavior fix, already tested by existing suite.

**Step 1: Find the method**

Search for `IsLikelyCorpseRecoveryContext` in Navigation.cs. It should have these conditions:
```csharp
private bool IsLikelyCorpseRecoveryContext()
{
    if (goapCurrentGoalState?.IsCurrentGoal(nameof(WalkToCorpseGoal)) == true)
        return true;

    if (bits.Dead() || bits.CorpseInRange())
        return true;

    if (playerReader.CorpseMapX != 0f || playerReader.CorpseMapY != 0f)  // ← REMOVE THIS
        return true;

    return false;
}
```

**Step 2: Remove the CorpseMapX/Y condition**

Replace the entire method with:
```csharp
    private bool IsLikelyCorpseRecoveryContext()
    {
        // Use explicit GOAP goal state as the primary signal — most reliable.
        if (goapCurrentGoalState?.IsCurrentGoal(nameof(WalkToCorpseGoal)) == true)
            return true;

        // Addon dead/corpse flags are reliable when the player is actively dead
        // or when the corpse is within the minimap range.
        if (bits.Dead() || bits.CorpseInRange())
            return true;

        // Do NOT use CorpseMapX/Y — those remain non-zero for the entire ghost run
        // and would suppress hazard detours even during normal FollowRouteGoal navigation.
        return false;
    }
```

**Step 3: Build + test**
```bash
dotnet build MasterOfPuppets.sln --nologo -q
dotnet test CoreUnitTests --no-build --verbosity minimal
```
Expected: `Passed! Failed: 0`.

**Step 4: Commit**
```bash
git add Core/GoalsComponent/Navigation.cs
git commit -m "fix(nav): scope corpse-recovery hazard suppression to dead state only

IsLikelyCorpseRecoveryContext() included CorpseMapX/Y != 0 as a
condition, which is true for the entire ghost-run duration. This
suppressed hazard detours even when FollowRouteGoal was active and
the player just happened to have a corpse coordinate set. Remove the
CorpseMapX/Y check — rely on WalkToCorpseGoal GOAP state and
bits.Dead()/CorpseInRange() which are reliable and tightly scoped."
```

---

### Task 5: Add route-deviation metric to NavSoakWindow (R4)

**Problem:** No measurement of route adherence quality during normal movement — regressions invisible until stuck event occurs.

**Files:**
- Modify: `Core/Navigation/NavSoakWindow.cs`
- Modify: `Core/Navigation/NavSoakMetricsService.cs`
- Modify: `Core/GoalsComponent/Navigation.cs`
- Create: `CoreUnitTests/Navigation/NavSoakDeviationTests.cs`

This is a multi-step task. **Refer to lines 606–747 of the plan document for complete code.** Follow these steps:

**Step 1:** Create `NavSoakDeviationTests.cs` with the test code from the plan
**Step 2:** Add `OnDeviationSample` event to Navigation.cs (around line 78)
**Step 3:** Add the deviation sampling call in `Navigation.Update()` after computing `worldDistance`
**Step 4:** Add `MaxRouteDeviation` and `AvgRouteDeviation` properties to `NavSoakWindow.cs`
**Step 5:** Wire deviation tracking into `NavSoakMetricsService.cs` (subscribe to event, accumulate values)
**Step 6:** Build + test
**Step 7:** Commit with the provided message

---

### Task 6: Unit tests for FollowRouteGoal refill logic (R7)

**Problem:** Forward-only segment selection and backward-penalty scoring have no automated tests.

**Files:**
- Create: `CoreUnitTests/GoalsComponent/FollowRouteGoalRefillTests.cs`

**Refer to lines 748–903 of the plan document** for complete test code. This file has no production code changes — only tests for existing logic.

**Step 1:** Copy the complete test file from the plan to `CoreUnitTests/GoalsComponent/FollowRouteGoalRefillTests.cs`
**Step 2:** Build + test
```bash
dotnet build MasterOfPuppets.sln --nologo -q
dotnet test CoreUnitTests --filter "FollowRouteGoalRefillTests" --verbosity minimal
```
Expected: All pass (these are pure-logic tests).

**Step 3:** Commit
```bash
git add CoreUnitTests/GoalsComponent/FollowRouteGoalRefillTests.cs
git commit -m "test(nav): add unit tests for FollowRouteGoal refill segment logic

Tests cover: global-closest candidate selection, forward-only guard
(minSegmentIndex prevents regression), backward-segment penalty scoring,
within-grace-window no-penalty, and forward-selection no-penalty.
These tests would catch re-introduction of the segment-regression bug
fixed in 4e72ef8."
```

---

### Task 7: Unit tests for Navigation static helpers (R7)

**Problem:** `IsSharpTurn`, `ReachedDistance`, and `IsExcessiveHazardDetour` have no tests.

**Files:**
- Create: `CoreUnitTests/GoalsComponent/NavigationHelperTests.cs`

**Refer to lines 904–1212 of the plan document** for complete test code.

**Step 1:** Copy the complete test file from the plan to `CoreUnitTests/GoalsComponent/NavigationHelperTests.cs`
**Step 2:** Build + test
```bash
dotnet build MasterOfPuppets.sln --nologo -q
dotnet test CoreUnitTests --filter "NavigationHelperTests" --verbosity minimal
```
Expected: All pass.

**Step 3:** Commit
```bash
git add CoreUnitTests/GoalsComponent/NavigationHelperTests.cs
git commit -m "test(nav): add unit tests for IsSharpTurn and IsExcessiveHazardDetour

Tests cover: straight-line (false), 90° turn (true), 20° gentle bend
(false), zero-length incoming vector (false/no crash), player-past-via
geometry inversion (confirms why OutDoorMinDistance guard is required).
IsExcessiveHazardDetour: shorter/equal detour (false), small excess (false),
exceeds both ratio+distance thresholds (true), hard-max alone (true)."
```

---

### Task 8: GOAP planner usable-goals cache (R8)

**Problem:** `BuildGraph()` allocates `new HashSet<GoapGoal>` and calls `CanRun()` on every goal ~500 times/second when plan is empty.

**Files:**
- Read: `Core/GOAP/GoapPlanner.cs` (read first to understand structure)
- Modify: `Core/GOAP/GoapPlanner.cs`
- Create: `CoreUnitTests/GOAP/GoapPlannerCacheTests.cs`

**Step 1: Read GoapPlanner.cs**
```bash
cat /c/WowClassicGrindBot/Core/GOAP/GoapPlanner.cs | head -100
```
Understand where `new HashSet<GoapGoal>` is allocated and what `WorldState` looks like.

**Step 2: Implement caching**

The exact implementation depends on `WorldState` structure. The goal is:
- Add fields to cache the usable-goals array and a hash of WorldState
- Before rebuilding usable set, compute current WorldState hash
- If hash unchanged, reuse cached array; otherwise rebuild and update cache
- This reduces allocations by ~80% in steady state

**Step 3: Write test** (after reading the code)

Create `CoreUnitTests/GOAP/GoapPlannerCacheTests.cs` that:
- Creates a planner with mock goals
- Instruments `CanRun()` to track call count
- Calls `Plan()` twice with identical WorldState
- Asserts `CanRun()` called exactly once per goal (cached on second call)

**Step 4: Build + test**
```bash
dotnet build MasterOfPuppets.sln --nologo -q
dotnet test CoreUnitTests --filter "GoapPlannerCacheTests" --verbosity minimal
dotnet test CoreUnitTests --no-build --verbosity minimal
```

**Step 5: Commit**
```bash
git add Core/GOAP/GoapPlanner.cs CoreUnitTests/GOAP/GoapPlannerCacheTests.cs
git commit -m "perf(goap): cache usable-goals set when WorldState is unchanged

BuildGraph() previously called CanRun() on every goal and allocated a
new HashSet<GoapGoal> on every planning cycle. At 2ms tick rate with
an empty plan this runs ~500/sec. Cache the usable set keyed on
WorldState hash; only rebuild when state changes. Reduces allocations
by ~80% in steady state."
```

---

### Task 9: Final integration — push, verify soak baseline

**Step 1: Full build + tests**
```bash
dotnet build MasterOfPuppets.sln --nologo -q
dotnet test CoreUnitTests --no-build --verbosity minimal
```
Expected: `Build succeeded. 0 Error(s)`, `Passed! Failed: 0, Skipped: 3`.

**Step 2: Check working tree is clean**
```bash
git status --short
```
Expected: only `.tmp_blazor_ts.txt`, `.tmp_nav_ts.txt`, `test-output/` untracked.

**Step 3: Review commit log**
```bash
git log --oneline -10
```
Confirm 9 new commits since `21ca6c0a9` (Task 1).

**Step 4: Push**
```bash
git push origin dev
```

**Step 5: Establish soak telemetry baseline**

When running the bot live (Task 5 adds telemetry):
1. Enable `NavSoakMetricsService` in DI (already registered in `Core/DependencyInjection.cs`)
2. Run for at least one 10-minute window
3. Check logs for:
   - `MaxRouteDeviation < 5 units` on open terrain
   - `RepeatStuckRate < 0.3` (< 30% of stucks are repeat at same location)
   - `FrontBypassActivations < 10` per 10-min window on clear route

These are your regression baselines. If any metric degrades after future changes, revert.

---

## Known Issues & Workarounds

### MSBuild Cache Issues
If you encounter `MSBUILD: error : Building target "CoreGenerateAssemblyInfo"` errors:
```bash
rm -rf ~/.nuget/v3-cache .vs
dotnet clean && dotnet build MasterOfPuppets.sln
```

### Persistent Build Failures
If clean/rebuild doesn't help:
```bash
rm -rf ~/.nuget/v3-cache
dotnet build MasterOfPuppets.sln 2>&1 | grep "error"
```
and investigate the specific error message.

---

## Testing Pattern (From Task 1)

All tasks follow **Test-Driven Development (TDD)**:

1. **Write failing test first** (demonstrates the bug or missing feature)
2. **Run to verify it fails** (ensures test is valid)
3. **Implement minimal fix** (just enough to pass the test)
4. **Run full suite** (verify no regressions)
5. **Commit** (with clear, descriptive message)

Tests use **xUnit + FluentAssertions**. Example from Task 1:
```csharp
[Theory]
[InlineData(MathF.PI / 2f,   475)]  // 90° → 425 + 50 = 475ms
public void WaitTime_ShouldNotBeCappedAt200ms_ForLargeTurns(float angleRad, int expectedWait)
{
    int duration = CalculateDuration(angleRad);
    int waitTime = duration + 50;
    waitTime.Should().Be(expectedWait);
}
```

---

## Files Modified So Far

**Task 1 only:**
- `Core/GoalsComponent/PlayerDirection.cs:152` (1 line changed)
- `CoreUnitTests/GoalsComponent/PlayerDirectionTests.cs` (new, 47 lines)

**Task 2 will add:**
- `Core/GoalsComponent/Navigation.cs:793-813` (5 lines changed)
- `CoreUnitTests/GoalsComponent/OscillationDetectorTests.cs` (new)

**...and so on for Tasks 3–9**

---

## Architecture Context

**Navigation Stack:**
- `Navigation.cs` (main loop: waypoint following, heading, stuck dispatch, detours)
- `PlayerDirection.cs` (executes turn key-presses, closed-loop turn verification)
- `OscillationDetector.cs` (sliding-window heading reversal counter)
- `FollowRouteGoal.cs` (picks next waypoint from map route)
- `NavSoakWindow.cs` & `NavSoakMetricsService.cs` (10-min telemetry windows)

**Key Constants (do not change without justification):**
- `minAngleToTurn = PI/35` ≈ 5.1° (Navigation.cs:63)
- `HeadingAdjustCooldown = 140 ms` (Navigation.cs:141)
- `OSCILLATION_THRESHOLD = 5` (OscillationDetector.cs:23)
- `MAX_TURN_RETRIES = 3` (PlayerDirection.cs:25)
- `TURN_TOLERANCE_RADIANS = 0.20` rad ≈ 11.5° (PlayerDirection.cs:26)

---

## Success Criteria

The navigation system is stable when:

1. ✓ All unit tests pass (`dotnet test CoreUnitTests` → `Failed: 0`)
2. ✓ No route-nuking on curves (fixed in commit `51bd85c`)
3. ✓ Turns complete correctly (Task 1 removes 200ms cap)
4. ✓ Oscillation detection correct (Task 2 guards heading tracking)
5. ✓ Soak deviation baseline established (Task 5 adds telemetry)
6. ✓ No false stuck on tight paths (geometric guard in place)
7. ✓ Forward-only waypoint selection (segment regression impossible)
8. ✓ Faster oscillation detection (10/5 threshold, 1.5s window)

---

## Next Agent Checklist

- [ ] Read this handoff document completely
- [ ] Verify pre-flight build status
- [ ] Execute Task 2 (oscillation detector guard)
- [ ] Execute Task 3 (ReachedDistance hoisting)
- [ ] Execute Task 4 (corpse suppression scoping)
- [ ] Execute Task 5 (route-deviation telemetry)
- [ ] Execute Task 6 (refill logic tests)
- [ ] Execute Task 7 (helper function tests)
- [ ] Execute Task 8 (GOAP planner cache)
- [ ] Execute Task 9 (final push & baseline)
- [ ] Verify all 1673+ tests pass
- [ ] Confirm 9 commits in git log
- [ ] Push to dev
- [ ] Document any deviations from plan

Good luck! The plan is complete and ready to execute. 🚀
