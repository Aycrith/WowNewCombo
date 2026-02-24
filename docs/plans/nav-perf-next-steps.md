# Navigation & Performance Next Steps
> Generated: 2026-02-24 | Branch: dev

This plan covers remaining issues identified after the large navigation
hardening batch committed through `dc1c13bb9`. Items are grouped by phase:
Phase 1 = quick wins (1–3 days), Phase 2 = medium effort, Phase 3 = deep/architectural.

For every item: **Problem → Root Cause → Proposed Solution → Expected Outcome → Risk/Complexity**

All findings are marked as confirmed (read from code) or speculative.

---

## Phase 1 — Quick Wins

### 1.1 Forward-only enforcement in FindClosestRefillCandidate()

**File:** `Core/Goals/FollowRouteGoal.cs:601-628`

**Problem (confirmed)**
`FindClosestRefillCandidate()` iterates every segment of `chosenPath` starting
from `i = 0` (line 613), picking the segment with minimum perpendicular distance
from the player regardless of whether it is behind the player's last known
progress anchor. When the bot drifts 2–3 units past a waypoint, the next
`RefillWaypoints()` call can select a segment index *lower* than the previous
`refillSegmentAnchorIndex`, causing the route to regress.

`ScoreRefillCandidate()` applies a soft `RefillBackwardSegmentPenalty` (6.0 per
backward segment, grace of 1, at line 591), but this is a scoring penalty not
a hard constraint — if the perpendicular distance to an earlier segment is
6+ units smaller than the forward segment, regression wins.

**Root cause (confirmed)**
Loop at `FollowRouteGoal.cs:613` unconditionally starts from `i = 0`. The
anchor index is maintained correctly but only used as a scoring input, not a
loop bound.

**Proposed Solution**
Pass `refillSegmentAnchorIndex` into `FindClosestRefillCandidate()` as a
`int minSegmentIndex` parameter. Change the inner loop start:

```csharp
// Core/Goals/FollowRouteGoal.cs ~line 613
int loopStart = hasRefillProgressAnchor
    ? Math.Max(0, refillSegmentAnchorIndex - RefillBackwardSegmentGrace)
    : 0;
for (int i = loopStart; i < pathMap.Length - 1; i++)
```

`ScoreRefillCandidate()` backward penalty remains as a tie-breaker within the
grace window.

**Expected outcome**
Eliminates segment regression from mild positional drift. The bot no longer
re-approaches previously traversed waypoints when briefly overshooting a turn.

**Risk/Complexity:** Low. The anchor index is already maintained and correct
(set by `UpdateRefillProgressAnchor()` at line 522, reset on teleport at
line 505). One argument addition to `FindClosestRefillCandidate()`, one call site.

---

### 1.2 OscillationDetector — tighten detection window and lower threshold

**File:** `Core/GoalsComponent/OscillationDetector.cs`

**Problem (confirmed)**
`OSCILLATION_THRESHOLD = 6` reversals and `HEADING_HISTORY_SIZE = 12` entries
are required before `IsOscillating` returns true. The reset window is
`OSCILLATION_RESET_TIME_MS = 2000` ms. At 140ms heading cooldown gating,
12 samples = ~1.7 seconds minimum fill time — meaning the bot completes 2–3
full circles before the detector fires.

**Root cause (confirmed)**
The threshold was tuned for false-positive avoidance, but a false negative
(2–3 circles before recovery) costs far more than a false positive (~200ms
stop-and-resume).

**Proposed Solution**
Shrink the window and lower the threshold:

```csharp
private const int HEADING_HISTORY_SIZE = 8;       // was 12
private const int OSCILLATION_THRESHOLD = 4;      // was 6
private const double OSCILLATION_RESET_TIME_MS = 1500; // was 2000
```

Also relax the minimum-fill early-exit at the existing count check:

```csharp
if (headingHistory.Count < Math.Max(4, HEADING_HISTORY_SIZE / 2))
    return false;
```

**Expected outcome**
First oscillation detection fires after ~0.6s instead of ~1.7s. The
stop-and-reset interrupts the first circle rather than the third.

**Risk/Complexity:** Low. Pure constant changes plus one minor condition
tweak. The `MIN_ANGLE_CHANGE_FOR_OSCILLATION` (0.20 rad / 11.5°) filter
already guards against false positives on legitimate rapid course-corrections.

---

### 1.3 ReduceByDistance() — break on upcoming sharp turn while mounted

**File:** `Core/GoalsComponent/Navigation.cs:743-754`

**Problem (confirmed)**
`ReduceByDistance()` pops every `routeToNextWaypoint` entry within
`ReachedDistance(minDistance)` in a single `while` loop. While mounted,
`ReachedDistance()` returns `MinDistanceMount = 10`. On curved MMAP paths
(waypoints ~2–3 units apart), a mounted bot at 7 m/s can pop 4–5 intermediate
waypoints per tick, skipping past a required turn before `AdjustHeading()` can
steer.

`singlePop = true` already protects precision mode, but mounted play forces
`RequiresPreciseTracking()` to false at `Navigation.cs:688`.

**Root cause (confirmed)**
`singlePop` is controlled by `preciseTracking`; precision is always disabled
while mounted regardless of upcoming turn angle.

**Proposed Solution**
After popping a waypoint, check if the upcoming waypoint pair requires a
significant turn. If so, break regardless of mount state:

```csharp
// After existing routeToNextWaypoint.Pop()
if (!singlePop && routeToNextWaypoint.Count >= 2)
{
    // reuse TryGetUpcomingRoutePoints() pattern to avoid allocation
    if (IsSharpTurn(playerW, nextPoint, afterNextPoint, SimplifyPreserveTurnRadians))
        break;
}
```

**Expected outcome**
Mounted bots stop popping through turns, resulting in smoother course
corrections around curves.

**Risk/Complexity:** Low-Medium. Touches the inner Update() hot path.
The turn check adds a small enumeration cost per pop; with typical route
lengths of 5–20 waypoints this is negligible.

---

### 1.4 AdjustNextWaypointPointToClosest() — lower pop limit 5 → 2

**File:** `Core/GoalsComponent/Navigation.cs:370`

**Problem (confirmed)**
`Resume()` calls `AdjustNextWaypointPointToClosest()` in a `while` loop with
`removed < 5`. On a dense waypoint cluster (waypoints 2–3 units apart), this
can discard 5 consecutive waypoints in one Resume() call. This combines badly
with the closest-point regression in 1.1: resume skips forward waypoints, then
`RefillWaypoints()` regresses to the closest segment behind the new position.

**Root cause (confirmed)**
The limit of 5 was chosen conservatively but is too permissive. The method's
intent is to skip past a single already-passed waypoint, not to leap ahead.

**Proposed Solution**
Lower the limit from 5 to 2:

```csharp
// Core/GoalsComponent/Navigation.cs ~line 370
while (AdjustNextWaypointPointToClosest() && removed < 2) { removed++; }
```

**Expected outcome**
Resume never consumes more than 2 waypoints at once. The teleport detection
path (`RefillAnchorTeleportResetDistance` check) handles the case where the
player genuinely teleports past 3+ waypoints.

**Risk/Complexity:** Low. Single constant change.

---

## Phase 2 — Medium Effort

### 2.1 Adaptive ReachedDistance for mounted characters

**File:** `Core/GoalsComponent/Navigation.cs:659-676`

**Problem (confirmed)**
`ReachedDistance()` returns a flat `MinDistanceMount = 10` while mounted, with
no consideration for upcoming turn angle or path curvature. On gentle curves
with waypoints 3–5 units apart, a 10-unit radius means the player overshoots
2–3 waypoints before the turn is initiated. On sharp turns, the flat 10-unit
threshold is actually too small (character commits too late).

**Root cause (confirmed/speculative)**
`MinDistanceMount` predates the precision tracking subsystem. It was never
revisited when `RequiresPreciseTracking()` was added.

**Proposed Solution**
Compute the upcoming turn angle and scale the threshold:

```csharp
private float GetMountedReachedDistance()
{
    if (!TryGetUpcomingRoutePoints(out Vector3 curr, out Vector3 next))
        return MinDistanceMount;

    float angle = ComputeTurnAngle(playerWorldPos, curr, next);
    if (angle >= TightTurnPrecisionRadians)   // PI/5 = 36°
        return MinDistanceMount * 1.5f;        // look further ahead for sharp turns
    if (angle < SimplifyPreserveTurnRadians)  // PI/6 = 30°
        return MinDistanceMount * 0.6f;        // tighter on straights
    return MinDistanceMount;
}
```

**Expected outcome**
Mounted bots follow curved routes more accurately. Sharper turns are detected
earlier; straight segments use a tighter threshold to avoid premature popping.

**Risk/Complexity:** Medium. Requires understanding interaction between
`ReachedDistance`, `ReduceByDistance`, and `GetActiveReachedDistance`. Low
regression risk if scoped to the mounted branch only. Validate with deviation
metric from 2.2.

---

### 2.2 Route deviation metric in NavSoakMetricsService

**File:** `Core/GoalsComponent/Navigation.cs` (add event), `NavSoakMetricsService.cs` (subscribe)

**Problem (confirmed — metric is absent)**
`NavSoakMetricsService` tracks stuck events, detour activations, and reconnect
success rates but has no measure of *route deviation* — how far the player was
from the intended path during normal movement. A refill regression or overshoot
that does not produce a stuck event is invisible in soak data.

**Proposed Solution**
Add a per-Update() deviation sample. At each tick where
`routeToNextWaypoint.Count >= 2`, compute the perpendicular distance from
`playerWorldPos` to the current segment. Fire an event:

```csharp
// Navigation.cs — new event
public event Action<float>? OnDeviationSample;

// In Update(), after current-segment computation:
OnDeviationSample?.Invoke(perpendicularDistanceToRoute);
```

`NavSoakMetricsService` subscribes and tracks rolling max/avg per window.
Expose as `MaxRouteDeviation` and `AvgRouteDeviation` in soak window output.

**Expected outcome**
Regressions in route adherence become visible before they escalate to stuck
events. Enables fast iteration on constants without requiring full manual
observation.

**Risk/Complexity:** Medium. One `GetClosestPointOnLineSegment` call per tick
is cheap. Main complexity is the cross-module event wiring without creating
a direct Navigation → NavSoakMetricsService dependency.

---

### 2.3 PlayerDirection — remove hard 200ms cap in WaitForTurn()

**File:** `Core/GoalsComponent/PlayerDirection.cs:152, 180`

**Problem (confirmed)**
`CalculateTurnDuration()` uses a fixed `angle * 850f / PI` heuristic (line 180).
`WaitForTurn()` hard-caps the wait at 200ms (line 152). For a 150° turn:
`150/180 * 850 = ~708ms` requested, but only 200ms is waited. The retry loop
(MAX_TURN_RETRIES = 3) then fires 3 × 200ms = 600ms of stutter steering where
a single 700ms key-press would suffice.

**Root cause (confirmed)**
The 200ms cap was introduced to prevent blocking the Update loop, but the retry
loop was not designed to compensate for turns that genuinely require > 200ms.

**Proposed Solution**
Replace the hard 200ms cap with a per-attempt budget:

```csharp
// PlayerDirection.cs WaitForTurn
int waitTime = Math.Min(expectedDuration + TURN_VERIFICATION_DELAY_MS, 1000);
```

Reduce `MAX_TURN_RETRIES` from 3 to 2 to keep total max steering time
bounded at 2000ms.

**Expected outcome**
Large heading corrections (> 90°) complete in one attempt instead of 3 stutter
cycles. Reduces total steering time by ~400ms per large turn and eliminates
the characteristic directional stutter at waypoints.

**Risk/Complexity:** Medium. The longer per-attempt wait increases blocking
time in the navigation Update() call. Since the GOAP thread sleeps 2ms between
ticks (GoapAgent.cs:303), a 700ms blocking wait is acceptable — the character
must finish turning before moving anyway. Validate there is no GOAP starvation.

---

### 2.4 OscillationDetector — graduated cooldown scaling via confidence score

**File:** `Core/GoalsComponent/OscillationDetector.cs`, `Core/GoalsComponent/Navigation.cs:771-853`

**Problem (confirmed)**
The current integration only acts *after* oscillation is confirmed (binary).
A bot with 3 reversals (one below threshold) receives no adjustment; then
suddenly stops on the 4th. No graduated response exists.

**Proposed Solution**
Expose `OscillationConfidence` from the detector:

```csharp
// OscillationDetector.cs — new property
public float OscillationConfidence =>
    headingHistory.Count < 3 ? 0f :
    Math.Clamp((float)lastDirectionChanges / OSCILLATION_THRESHOLD, 0f, 1f);
```

Use in Navigation's `ShouldThrottleHeadingAdjustment()` to scale the cooldown:

```csharp
float confidence = oscillationDetector.OscillationConfidence;
TimeSpan scaledCooldown = baseCooldown * (1f + confidence * 3f); // up to 4x at full confidence
```

**Expected outcome**
Oscillation is dampened progressively. The circuit breaker still fires at full
threshold, but the bot naturally reduces steering frequency as it approaches
the threshold.

**Risk/Complexity:** Medium. Requires persisting `lastDirectionChanges` as a
field in `OscillationDetector` (currently a local). One field, one property,
one usage site.

---

## Phase 3 — Deep / Architectural

### 3.1 RouteSegmentTracker — forward-only invariant for sub-routes

**File:** `Core/GoalsComponent/Navigation.cs` (multiple write sites for `routeToNextWaypoint`)

**Problem (confirmed)**
`routeToNextWaypoint` (Stack<Vector3>, line 69) has no association with the
high-level `wayPoints` progress position. Its contents are replaced freely by
detours, bypasses, and path recalculations. `ApplyDynamicRoute()` and
`StitchDetourWithRemainingPath()` may produce stitched routes that overlap
previously-traversed segments.

**Root cause (confirmed)**
The stack has no forward-progress invariant. Detour stitching at line 1404
produces a deduplicated but unordered merge.

**Proposed Solution**
Introduce a `RouteSegmentTracker` value type tracking `(WaypointIndex, SubSegmentIndex)`.
Update on every `wayPoints.Pop()` and `routeToNextWaypoint` repopulate.
Validate detour endpoints advance the index forward; log warnings on regression.
Implement as diagnostic-only first; enforce later.

**Risk/Complexity:** High. Requires threading the tracker through 8+ write
sites in Navigation.cs. Implement as warning log first; add enforcement only
after soak validation.

---

### 3.2 GOAP planner — reduce per-tick allocations

**File:** `Core/GOAP/GoapPlanner.cs:77-106`, `Core/GOAP/GoapAgent.cs:263-334`

**Problem (confirmed)**
`BuildGraph()` allocates a `new HashSet<GoapGoal>(usable)` per recursive node
(line 97) and a `Node` object per goal (line 87). With 15–25 goals and a 2ms
sleep between ticks (GoapAgent.cs:303), this runs ~500 times/second when no
plan exists, creating sustained GC pressure.

**Three-step fix:**

**3.2a (Low risk — do first):** Cache the `usable` set. `CanRun()` outcomes
change rarely; only recompute when `WorldState` changes:

```csharp
private BitVector32 lastWorldStateForUsable;
private GoapGoal[] cachedUsable = [];

private GoapGoal[] GetUsableGoals(WorldState worldState)
{
    if (worldState == lastWorldStateForUsable && cachedUsable.Length > 0)
        return cachedUsable;
    lastWorldStateForUsable = worldState;
    cachedUsable = AvailableGoals.Where(g => g.CanRun()).ToArray();
    return cachedUsable;
}
```

**3.2b (Medium):** Replace `HashSet<GoapGoal> subset` in `BuildGraph()` with a
`long` bitset indexed by stable goal position in `AvailableGoals`. Eliminates
all HashSet allocations from the inner loop.

**3.2c (Medium-High):** Plan cache keyed on `WorldState`. If world state is
unchanged and the current plan is still executable, skip replanning entirely.

**Risk/Complexity:** 3.2a = Low, 3.2b = Medium, 3.2c = Medium-High (cache
invalidation must handle `CanRun()` methods that query external state beyond
`WorldState`).

---

### 3.3 Refill loop breaker — progress-aware condition

**File:** `Core/Goals/FollowRouteGoal.cs:526-574`

**Problem (confirmed)**
`ApplyRefillLoopBreaker()` triggers when `closestSegmentStartIndex` is the same
across N consecutive refill calls. But a bot moving slowly *through* a segment
legitimately revisits the same index on consecutive refills. The breaker fires
prematurely and advances `mapClosestPoint` by a full segment (~10–30 units),
potentially skipping a required turn.

**Proposed Solution**
Add a distance-to-last-anchor condition to the loop breaker:

```csharp
bool sameSegmentLoop =
    hasRecentRefillApply &&
    chosenReversed == lastRefillAppliedReversed &&
    closestSegmentStartIndex == lastRefillAppliedSegmentIndex &&
    mapClosestPoint.MapDistanceXYTo(lastRefillAppliedAnchorPoint) < OutDoorMinDistance && // [NEW]
    (now - lastRefillAppliedUtc) <= RefillSameSegmentLoopWindow;
```

The breaker now only triggers when the player is genuinely not progressing,
not just moving slowly through a segment.

**Risk/Complexity:** Medium-High. Adds a 7th field to the loop-breaker state
cluster (lines 84-87). Localized to `FollowRouteGoal.cs` but the condition
change needs careful testing for the slow-movement case (swimming, climbing).

---

### 3.4 Adaptive heading cooldown based on movement speed

**File:** `Core/GoalsComponent/Navigation.cs:835-853`

**Problem (speculative)**
`HeadingAdjustCooldown = 140ms` is not scaled by movement speed. At very low
speed (dismounted climbing, ~1–2 m/s), 140ms between corrections covers only
~0.2m — producing excessive micro-adjustments that resemble oscillation and
may trigger the OscillationDetector spuriously.

**Proposed Solution**
Estimate current speed from position delta between Update() ticks. Scale the
cooldown inversely:

```csharp
float estimatedSpeedMps = deltaPosition / deltaTimeSeconds;
TimeSpan adaptiveCooldown = HeadingAdjustCooldown *
    Math.Max(0.5f, Math.Min(2f, 2f / Math.Max(1f, estimatedSpeedMps)));
// 7 m/s → ~40ms  |  2 m/s → 140ms  |  1 m/s → 280ms
```

**Risk/Complexity:** High. Speed estimation from position deltas is sensitive
to the 50Hz addon update rate and may produce noisy values. Implement as a
feature-flag-gated experiment tracked through `NavSoakMetricsService` before
committing.

---

### 3.5 GoapCurrentGoalState — add duration and transition count

**File:** `Core/GOAP/GoapCurrentGoalState.cs`

**Problem (confirmed)**
`GoapCurrentGoalState` stores only `currentGoalName` and `lastUpdatedUtc`
(lines 10-11). There is no way to know how long the bot has been in the current
goal or how many goal transitions have occurred. Navigation diagnostics
(e.g., "bot has been in FollowRouteGoal for 300s with high stuck rate") require
log parsing.

**Proposed Solution**
Add goal age and transition count:

```csharp
private long goalStartTicks;  // Interlocked-safe DateTime.UtcNow.Ticks
private int transitionCount;

public TimeSpan CurrentGoalAge =>
    currentGoalName == "None" ? TimeSpan.Zero :
    TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Volatile.Read(ref goalStartTicks));

public int TransitionCount => Volatile.Read(ref transitionCount);
```

Update `SetCurrentGoalName()` to set `Volatile.Write(ref goalStartTicks, DateTime.UtcNow.Ticks)`
and `Interlocked.Increment(ref transitionCount)`.

Navigation.cs can then use `CurrentGoalAge` to make context-aware timeout
decisions (e.g., increase `FrontBypassBreakerCooldown` automatically when
stuck in `WalkToCorpseGoal` > 120s).

**Risk/Complexity:** Low-Medium. Self-contained file. Thread-safety requires
using `Interlocked` / `Volatile` on the tick count — `DateTime` cannot be
written atomically but `long` ticks can.

---

## Recommended Implementation Order

| Step | Item  | Why first                                          |
|------|-------|----------------------------------------------------|
| 1    | 1.2   | Fastest win, highest observed impact               |
| 2    | 1.1   | Eliminates the core regression path               |
| 3    | 1.4   | 1-line fix, prevents worst Resume() case          |
| 4    | 1.3   | Protects mounted turn coverage                    |
| 5    | 2.2   | Enable telemetry *before* tuning constants        |
| 6    | 2.4   | Progressive oscillation dampening                 |
| 7    | 2.1   | Mounted threshold, validate with soak data        |
| 8    | 2.3   | Steering latency fix, validate with soak data     |
| 9    | 3.2a  | GOAP GC pressure, low risk                        |
| 10   | 3.5   | Observability foundation for 3.1 and 3.4         |
| 11   | 3.3   | Refill loop breaker correctness                   |
| 12   | 3.1   | Sub-route regression tracker (diagnostic only)   |
| 13   | 3.2b  | GOAP bitset allocation elimination                |
| 14   | 3.4   | Adaptive cooldown (feature-flagged experiment)    |
| 15   | 3.2c  | GOAP plan cache                                   |

---

## Telemetry Checklist

After each Phase 1 change, run a 30-minute NavSoakMetricsService session:
- `FrontBypassActivations` per window should decrease (fewer stalls)
- `RepeatStuckCount / StuckEvents` should decrease (fewer repeat stucks)
- `TailRecalcFailures` should stay flat or decrease

After Phase 2, also verify:
- `MaxRouteDeviation` (new metric from 2.2) stays below 5 units in open-world terrain

---

## Confirmed vs. Speculative

| Item | Status |
|------|--------|
| FindClosestRefillCandidate() starts at i=0 always | **Confirmed** — `FollowRouteGoal.cs:613` |
| OSCILLATION_THRESHOLD needs 6 reversals + full 12-sample queue | **Confirmed** — `OscillationDetector.cs:22-24, 81` |
| ReduceByDistance() unlimited pop while mounted | **Confirmed** — `Navigation.cs:688, 743` |
| Resume() pop limit is 5 | **Confirmed** — `Navigation.cs:370` |
| ReachedDistance() returns flat MinDistanceMount=10 while mounted | **Confirmed** — `Navigation.cs:659-662` |
| WaitForTurn() hard cap is 200ms | **Confirmed** — `PlayerDirection.cs:152` |
| BuildGraph() allocates HashSet+Node per goal per tick | **Confirmed** — `GoapPlanner.cs:87, 97` |
| GoapCurrentGoalState has no duration or transition count | **Confirmed** — `GoapCurrentGoalState.cs:10-11` |
| NavSoakMetricsService has no deviation metric | **Confirmed** — no `OnDeviationSample` event exists |
| Actual oscillation behavior on live bot | **Speculative** — requires game session |
| Speed-proportional cooldown would reduce micro-adjustments | **Speculative** — needs profiling |
| GOAP plan caching speedup magnitude | **Speculative** — needs profiling |
