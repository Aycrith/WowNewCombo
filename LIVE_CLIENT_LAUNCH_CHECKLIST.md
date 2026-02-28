# Live Client Testing Launch Checklist

**Status:** ✅ Ready to launch
**Commit:** 26a6f2063
**Date:** 2026-02-28

---

## Pre-Launch (5 min)

- [ ] `dotnet build MasterOfPuppets.sln` → 0 errors ✅
- [ ] `dotnet test --no-build` → 1720/1723 passing ✅
- [ ] All code changes committed ✅
- [ ] No uncommitted changes: `git status --short` ✅

---

## Launch (5 min)

```bash
cd C:\WowClassicGrindBot
dotnet run --project BlazorServer -c Release
# Opens http://localhost:5000 in browser
```

---

## 15-Minute Quick Test

### Route Following (5 min)
1. Start bot on simple linear route
2. Verify: Bot moves smoothly forward, no backtracking
3. Trigger combat mid-route (manually cast a spell or walk into enemy)
4. After combat: Bot resumes at correct position (not at route start)
5. **Expected:** Green "FollowRoute" goal active, no backward segment jumps

### Mounted Movement (5 min)
1. Find a winding road (Darkshore cliffs, Barrens curves, Thousand Needles spirals)
2. Start bot on route through the winding path while mounted
3. Watch waypoint consumption rate
4. **Expected:** Bot navigates turns smoothly, doesn't skip waypoints, maintains heading

### Stuck Detection (5 min)
1. Manually position bot on open flat terrain
2. Disable movement (stop the bot, no inputs)
3. Stand still for 6+ seconds
4. **Expected:** Log message "StuckDetector: Initial stuck detection triggered" (~5000ms)
5. Stuck recovery attempts (turn, strafe, backtrack)
6. Bot resumes route from current position, not segment 0

---

## Extended Soak (60+ min)

**Setup:** Create infinite circular route (grind path)

**Monitor:**
```
Metrics to watch in logs:
- "Refill loop detected" → Should be ZERO
- "StuckDetector:" → Only legitimate stuck events
- No unexpected "goal switch" spam
```

**Success Criteria:**
- ✅ Zero false stuck triggers
- ✅ Route deviation <3.0 units (check SignalR diagnostics)
- ✅ No heading oscillation or jitter
- ✅ Smooth continuous movement

---

## If Issues Occur

**Bot backtracking:**
- Check log for "Refill" messages
- Expected: Anchor segment >= current segment
- If regression detected: Refill fix didn't apply

**Sharp turns being skipped while mounted:**
- Check `routeToNextWaypoint` size in debugger
- Expected: Sharp turns preserved in waypoint queue
- If turns missing: Mounted fix didn't apply

**False stuck triggers:**
- Check "StuckDetector" log level
- Expected: ~1 trigger per true stuck event
- If multiple triggers: Threshold too aggressive

**Suggestions if needed:**
1. Increase `StuckSensitivity.UnstuckAfterMs` (current: 5000)
2. Increase `StuckSensitivity.MinDistance` (current: 0.2)
3. Lower `PathSmoothing.RDPTolerance` if curve detail lost

---

## Rollback Plan

If critical issue found:
```bash
git revert 26a6f2063
dotnet build && dotnet test
```

Previous stable commit: `4dae7a639` (navigation recovery baseline)

---

## Documentation

- **Full Details:** `NAVIGATION_LIVE_CLIENT_READINESS.md`
- **Audit Report:** `COMPREHENSIVE_CODEBASE_AUDIT_2026-02-28.md`
- **Commit Message:** `git log -1` (26a6f2063)

---

## Success Indicator

✅ Bot completes 60+ minute soak test with:
- Zero false stuck triggers
- Zero backward segment regressions
- Smooth mounted navigation through winding paths
- All routes completed without interruption

**Estimated result:** Production-ready for long-term grinding sessions
