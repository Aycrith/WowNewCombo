# Session Issues Report — February 18, 2026

**Bot System**: WowClassicGrindBot (BlazorServer + Core + AmeisenNavigationServer)  
**Session Scope**: Full bot startup through live client testing — Blood Elf Rogue, Eversong Woods / Ghostlands (TBC, Map 530)  
**Status at report time**: All components currently DOWN; WoW client running (PID 9168), BlazorServer stopped, NavServer stopped  
**Verdict**: The bot has not successfully completed a single full automation cycle this session

> Update (February 18, 2026, 10:03 PM EST): This baseline report has been superseded by a successful StartAndValidate run (`logs/agentctl-20260218-220303-validation.json`, `OverallPass=true`). See addendum at the end for current status.

---

## Executive Summary

The bot has never functioned correctly in this session. Every attempt to run it has been blocked by one or more compounding failures:

1. A TBC version detection bug prevented any addon data from reading correctly
2. A race condition in startup validation caused infinite loops
3. A DI registration error crashes bot creation on every profile load attempt
4. A DXGI screen capture crash loop causes BlazorServer to restart continuously (~63 crashes in one run)
5. The navigation server repeatedly disconnects, opening the circuit breaker
6. When the circuit breaker is open, the bot walks in straight lines into terrain/ocean
7. WalkToCorpseGoal had no fallback for unreachable corpses, creating death loops

The result: the bot appears to start briefly (shows a goal name in the API) but is not actually navigating. It either stands still or walks in a straight line until it dies.

---

## Issue Inventory

### ISSUE-001 — TBC Version Detection Wrong
**Status**: ✅ FIXED  
**Severity**: Critical — all addon data reads wrong  
**File**: `SharedLib/StartupConfig/StartupClientVersion.cs`  
**Root Cause**: Version string parsing compared `Major` (int = 205) to string "1" via switch/if logic. The TBC anniversary client reports version `205.5.6589.5`, which was not recognized as `tbc`. Defaulted to empty/wrong expansion, so all TBC-specific DB lookups (WorldMapArea for UIMapId=1941→MapID=530, etc.) failed silently.  
**Fix Applied**: Added `case 205:` branch returning `"tbc"` expansion string  
**Verification**: Confirmed UIMapId=1941 now resolves to MapID=530 (Eversong Woods) correctly

---

### ISSUE-002 — Addon Validation Loop (BotStartGuard Race Condition)
**Status**: ✅ FIXED  
**Severity**: Critical — startup never completes  
**File**: `Core/Launch/BotStartGuard.cs`  
**Root Cause**: `TryGetCachedAddonValidation` started a new background task each time it was called, but the completion detection was racing with task restart, causing infinite retry loops. Startup check for addon validation never completed.  
**Fix Applied**: Refactored to single-run task with proper completion tracking  
**Verification**: Startup now reaches "Startup complete" and completes addon validation

---

### ISSUE-003 — WalkToCorpseGoal No Spirit Healer Fallback
**Status**: ✅ FIXED  
**Severity**: High — creates infinite death loop  
**File**: `Core/Goals/WalkToCorpseGoal.cs`  
**Root Cause**: When corpse was in the ocean (MapPos X or Y < 0, outside zone bounds), `SetWayPoints([corpseLocation])` passed negative map-% coordinates. `IsMapPoint` check in Navigation.cs requires 0–100, so negative values were treated as raw world coords, producing completely wrong navigation targets. Additionally, there was no timeout — if corpse was in ocean and nav failed, the goal looped forever.  
**Fix Applied**:
- Added `corpseOutOfBounds` check: if `corpseLocation.X < 0 || X > 100 || Y < 0 || Y > 100` → route to spirit healer world position instead
- Added `CorpseWalkTimeoutSec = 90` — after 90s without reaching corpse, route to spirit healer
- Stored `spiritHealerWorldPos` from `areaDB.FindClosestCreatureByNpcFlag(NpcFlags.SpiritHealer)` in `OnEnter()` for use in the timeout  
**Verification**: Code review only — not live-tested yet due to other blocking issues

---

### ISSUE-004 — send-reload.ps1 Used ShowWindow(9) — Broke WoW Window Size
**Status**: ✅ FIXED  
**Severity**: High — invalidates DXGI capture after every reload  
**File**: `send-reload.ps1`  
**Root Cause**: Old script used `ShowWindow(hwnd, 9)` (SW_RESTORE) to bring WoW to foreground. This resized/repositioned the WoW window, which invalidated the Direct3D/DXGI swap chain capture context. After any reload sent via this script, DXGI threw `E_INVALIDARG` in a tight loop, crashing BlazorServer repeatedly.  
**Fix Applied**: Replaced script with `WM_CHAR` PostMessage approach (same as `send-dcflush.ps1`) — no `ShowWindow`, no window resize. Takes WoW's MainWindowHandle directly.  
**Note**: The window-size problem the user encountered was caused by this. The fix was to cycle windowed → windowed fullscreen via `/console gxRestart` commands, then the bot flow continued using the corrected send-reload.ps1

---

### ISSUE-005 — DXGI E_INVALIDARG Crash Loop
**Status**: ⚠️ ROOT CAUSE FIXED (ShowWindow removed), but permanent recovery not implemented  
**Severity**: Critical — BlazorServer crashes every few seconds  
**Files**: Screen capture service (DXGI/WowScreenDXGI), `BlazorServer` host configuration  
**Root Cause**: After WoW window was resized (by ShowWindow or mode changes), the DXGI swap chain became invalid. The background capture service threw `SharpGen.Runtime.SharpGenException: HRESULT: [0x80070057] E_INVALIDARG/Invalid arguments` in rapid succession. The host is configured with `HostOptions.BackgroundServiceExceptionBehavior = StopHost`, so ANY unhandled exception in a background service stops the entire application. This triggered ~63 crash-restart cycles in one run.  
**What's Fixed**: DXGI capture is no longer invalidated by send-reload.ps1 (ShowWindow removed)  
**What's NOT Fixed**:
- If WoW window mode changes for ANY reason, the crash loop can resume
- `BackgroundServiceExceptionBehavior = StopHost` means no self-recovery
- **Recommended Fix**: Wrap DXGI capture in try/catch with retry/reinit logic rather than letting exception propagate to host

---

### ISSUE-006 — FailureAnalyticsEventListener DI Singleton/Scoped Conflict
**Status**: 🔶 UNCONFIRMED — may be resolved in current binary  
**Severity**: Critical — blocks bot creation entirely on profile load  
**File**: `Core/GoalsFactory/GoalFactory.cs`, `Core/BotController.cs`  
**Error**:
```
Cannot consume scoped service 'Core.ConfigurableInput' from singleton
'Core.Analytics.FailureAnalyticsEventListener'
```
**Analysis**: The DI validation at `BuildServiceProvider(ValidateOnBuild=true, ValidateScopes=true)` reported `FailureAnalyticsEventListener` as `Lifetime: Singleton`. Current source code at `GoalFactory.cs:82` shows `services.AddScoped<Analytics.FailureAnalyticsEventListener>()`. The error appeared in logs from ~12:16 PM today — the BINARY running at that time may have been compiled from a different code state where the service was accidentally `AddSingleton`. After our rebuild at ~8 PM today, the binary now compiles the correct `AddScoped` registration.  
**Action Required**: Verify — load a profile, attempt bot start, check if this error reappears in logs  
**If Still Broken**: The dependency chain `FailureAnalyticsEventListener` → `StuckDetector` → `ConfigurableInput` suggests investigating whether any of these registrations changed. Emergency fix: change analytics to not depend on `StuckDetector` (remove optional parameter, wire stuck events separately in GoapAgent)

---

### ISSUE-007 — Navigation Server Connectivity and Circuit Breaker Opening
**Status**: 🔴 UNRESOLVED — root cause not fully eliminated  
**Severity**: Critical — directly causes "bot walks into ocean" behavior  
**Files**: `Core/PPather/HybridPather.cs`, `Core/Resilience/CircuitBreaker.cs`, nav server config  
**What Happens**:
1. AmeisenNavigationServer starts on port 47110
2. RemotePathingAPIV3 connects (TBC path correctly: UIMapId 1941 → MapID 530)
3. Nav server crashes or TCP connection drops (observed repeatedly in logs)
4. HybridPather sees failed path response → throws `InvalidOperationException`
5. After 5 failures (`PathfindingThreshold=5`), circuit breaker enters `Open` state
6. All subsequent path requests return `Array.Empty<Vector3>` in ~0.04ms (CB short-circuit)
7. CB stays Open for 60s (`PathfindingCooldownSeconds=60`), then HalfOpen, then tests one path — if that path also fails (it will if player is in bad position), CB reopens
8. Bot is stuck in a loop of requesting paths, getting empty arrays, requesting again

**Evidence**: Log showed `Pathfinder - Trivial: False | 0.04ms` — 0.04ms is the CB short-circuit, NOT a real path computation  
**Key Config**: `CircuitBreaker.PathfindingThreshold=5`, `CircuitBreaker.PathfindingCooldownSeconds=60`  
**Observed**: Nav server was crashing and restarting repeatedly during session

**Why Bot Walks to Ocean When CB is Open**:
In `Navigation.cs → Update()`:
```
if (routeToNextWaypoint.Count == 0) → RefillRouteToNextWaypoint(token) → return
```
When `RefillRouteToNextWaypoint` calls the pather and gets `Array.Empty`, `routeToNextWaypoint` remains empty. On the SAME frame that `RefillRouteToNextWaypoint` is called, `input.StartForward(true)` has already been invoked, so the bot is walking forward. On the NEXT `Update()`, `routeToNextWaypoint` is still empty → calls `RefillRouteToNextWaypoint` again → returns. Bot never stops walking forward. Direction is toward whatever waypoint was last set. If that waypoint is east (toward ocean), bot walks east forever.

**Things Tried**: Restarting BlazorServer clears CB state (in-memory). But if the nav server is unstable, CB reopens within seconds of the first path attempt.  
**Recommended Fix**: 
1. Investigate why nav server crashes — check `Navigation/config.cfg` sMmapsPath, whether tiles load correctly for map 530
2. Add `input.StopForward(false)` before returning from path-empty condition in Navigation.cs
3. Or: when CB is Open, set goal to "wait" rather than continuing to walk

---

### ISSUE-008 — Navigation Straight-Line Movement When Paths Empty
**Status**: 🔴 UNRESOLVED — behavior when CB open  
**Severity**: High — bot walks into terrain, water, off cliffs  
**File**: `Core/GoalsComponent/Navigation.cs`  
**Root Cause**: When `routeToNextWaypoint.Count == 0` (no computed path), `Navigation.Update()` calls `RefillRouteToNextWaypoint` and returns early — BUT `input.StartForward(true)` is called at line ~180. The bot keeps walking forward in whatever direction it was facing. If nav fails permanently (CB open), the bot walks indefinitely in the last heading direction.  
**Recommended Fix**: In `RefillRouteToNextWaypoint`, if path result is empty AND circuit breaker is open → stop forward movement (`input.StopForward(false)`) and wait for CB to reset

---

### ISSUE-009 — TBC Zone vs Classic Zone Navigation
**Status**: 🔶 ADVISORY — architecture question  
**User Question**: "Would testing a character in a classic zone fix the navigation problem?"  
**Answer**: **The navigation failure is NOT primarily TBC-zone-specific.** Here is the analysis:

| Factor | Classic Zones (Maps 0, 1) | TBC Zones (Map 530) |
|--------|--------------------------|---------------------|
| MMAP files present | ✅ Yes, many | ✅ Yes, ~200 .mmtile files for map 530 |
| Nav server path computation | Works if server is up | Works if server is up + player position valid |
| If nav server crashes | CB opens → straight-line → same failure | CB opens → straight-line → same failure |
| Zone MMAP quality | Mature, well-tested | Present but less tested |

**The core problem** is nav server instability causing the circuit breaker to open. This would manifest the same way in any zone.  
**However**: Testing with a classic zone character WOULD provide useful information:
- If classic zones work perfectly → confirms TBC MMAP coverage is the issue (possible with ocean/boundary areas of map 530)
- If classic zones also fail → confirms general nav server instability
- **Recommendation**: Yes, test with a classic zone character. Use a level 1 zone like Elwynn Forest (MapID=0) or Durotar (MapID=1) to isolate whether the problem is zone-specific

---

### ISSUE-010 — KeyBindingsReader Spin Loop (count=0 after restart)
**Status**: 🔴 UNRESOLVED  
**Severity**: High — prevents startup validation from completing  
**File**: `Core/Addon/KeyBindingsReader.cs` (or related)  
**Root Cause**: After /reload or BlazorServer restart, `KeyBindingsReader` logs `Waiting for bindings - current count=0` at ~60 times/second indefinitely. This was the last line in several crash logs, suggesting BlazorServer dies while waiting for bindings. The `/dcflush` command is required to populate bindings — it forces DataToColor addon to flush its keybinding cache. If `/dcflush` is not sent promptly after startup (or if the addon hasn't sent data since the addon handshake stale), bindings never populate.  
**Workaround**: Always send `/dcflush` in-game after any BlazorServer restart or `/reload`  
**Recommended Fix**: Add timeout to KeyBindingsReader — if no bindings after 60s, log error and allow proceeding (with warning) rather than spinning forever

---

### ISSUE-011 — Addon Handshake Goes Stale After Window Changes
**Status**: 🔴 UNRESOLVED — needs robust DXGI recovery  
**Severity**: High — blocks bot start  
**Root Cause**: When WoW window is resized or mode changes, the DXGI swap chain capture becomes invalid. The addon sends pixel data continuously, but BlazorServer can no longer read those pixels (gets zeros or garbage). The `AddonHandshake` ticker pixel stops changing, so the system reports "Addon data is stale (Xms since last tick)".  
**Direct Cause in Session**: `ShowWindow(9)` in old send-reload.ps1 triggered this. Even after that was fixed, DXGI kept failing because the initial window mode cycle invalidated it.  
**Workaround**: Restart BlazorServer after any window mode change to reinitialize DXGI capture  
**Recommended Fix**: Implement DXGI capture watchdog — detect `E_INVALIDARG` and re-acquire the swap chain within the capture service rather than crashing

---

### ISSUE-012 — Port 5000 Already in Use on Restart
**Status**: 🔴 ONGOING RISK  
**Severity**: Medium — causes restart failures  
**Error**: `System.IO.IOException: Failed to bind to address http://127.0.0.1:5000: address already in use`  
**Root Cause**: When BlazorServer is stopped via `Stop-Process -Force`, the process dies but the port 5000 socket can linger in TIME_WAIT state for ~30s. If BlazorServer is restarted immediately, it fails to bind.  
**Workaround**: Insert `Start-Sleep 3` after stopping before restarting  
**Recommended Fix**: Add `SO_REUSEADDR` or increase port release delay tolerance in Kestrel config

---

### ISSUE-013 — Player Character in Ocean (Map Boundary Violation)
**Status**: ⚠️ REQUIRES MANUAL RECOVERY IN GAME  
**Severity**: High — nav has no valid polys at ocean position  
**Detail**: During early broken-nav phase (Issue-009), the bot walked east from Eversong Woods into the ocean (WorldY=-4311, zone east boundary at Y=-4487.5). The MMAP tile covering the ocean position (17,41) has only 5,188 bytes — minimal coverage for a mostly-water area. The AmeisenNavigationServer cannot find walkable polygons at Z≈0 in the ocean, returning empty paths for both start and end positions in ocean.  
**Resolution**: The character MUST be manually walked/healed back onto land, OR resurrected at spirit healer (for ghost state). Only then will navigation function correctly.  
**Current State**: Unknown — WoW is running (PID 9168) but character position not checked since session ended

---

## System State at Report Time

| Component | Status | Notes |
|-----------|--------|-------|
| WoW Client | ✅ Running (PID 9168) | WTF/Config.wtf changed this session (window mode cycle) |
| BlazorServer | ❌ Stopped | Last run ~12 PM, DXGI crash loop |
| AmeisenNavigationServer | ❌ Stopped | Was auto-managed, repeatedly crashed |
| Bot Active | ❌ No | Never successfully ran an automation cycle |
| Character Position | ⚠️ Unknown | Last confirmed in/near ocean |
| Route Loaded | N/A | `6-12_Eversong Woods.json` or `9-12_Ghostlands.json` |

---

## What Was Built/Fixed This Session

| Item | File(s) Changed | Rebuilt |
|------|----------------|---------|
| TBC version detection | `SharedLib/StartupConfig/StartupClientVersion.cs` | ✅ Yes |
| Addon validation loop | `Core/Launch/BotStartGuard.cs` | ✅ Yes |
| WalkToCorpseGoal spirit healer fallback | `Core/Goals/WalkToCorpseGoal.cs` | ✅ Yes |
| send-reload.ps1 safe input method | `send-reload.ps1` | N/A (script) |

---

## What Was NOT Fixed (Active Problems)

1. **DXGI crash loop** — No exception handling in capture service; any window event = crash loop
2. **Nav server instability** — Root cause of crashes unknown; nav server dies and restarts
3. **Navigation empty path = forward walk** — No stop when paths fail
4. **KeyBindingsReader spin loop** — No timeout, can block startup indefinitely
5. **Circuit breaker settings too aggressive** — Threshold=5 trips too fast; consider increasing to 10 or 20
6. **No automated startup sequence** — Every session requires manual: start server → /reload → /dcflush → load profile → bypass checks → start bot (8+ manual steps)
7. **Character in ocean** — Manual intervention required in-game

---

## Recommended Priority Order for Next Session

### P0 — Blockers (must fix to get ANY automation working)
1. **Verify DI error (Issue-006)** — Start BlazorServer, load profile, attempt bot start. If DI error reappears, change `FailureAnalyticsEventListener` to not consume `StuckDetector` in constructor (wire via GoapAgent event subscription instead)
2. **Fix DXGI capture recovery (Issue-005)** — Wrap DXGI calls in try/catch in `WowScreenDXGI`, add device-lost recovery (re-acquire swap chain), remove `StopHost` behavior or add specific DXGI exception guard

### P1 — Critical Navigation Fixes
3. **Fix Navigation straight-line walk on empty paths (Issue-008)** — In Navigation.cs, when pather returns empty array AND CB is open, call `input.StopForward(false)` before returning from Update()
4. **Increase CB threshold (Issue-007)** — Change `PathfindingThreshold` from 5 to 15 in runtime_feature_flags.json to avoid tripping on transient nav server reconnects
5. **Investigate nav server crash cause** — Check nav server logs, verify MMAP path config, ensure map 530 tiles load correctly

### P2 — Reliability
6. **Add KeyBindingsReader timeout (Issue-010)** — Prevent infinite spin on count=0
7. **Test with classic zone character (Issue-009)** — Use Elwynn Forest / Durotar character to confirm if nav works at all before diagnosing TBC-specific issues

### P3 — Automation
8. **Create startup automation script** — Automate the full sequence: start BlazorServer → wait for DXGI init → send /dcflush → wait for bindings → load profile → apply overrides → start bot → monitor health
9. **Add DXGI watchdog service** — Periodic check that sentinel pixels are changing; if stale for >5s, trigger DXGI device reacquisition

---

## Startup Sequence (Current Manual Process)

```
1.  Ensure WoW is running and character is in the world (not loading screen, not in ocean)
2.  Start AmeisenNavigationServer (auto-managed by BlazorServer, but verify port 47110 available)
3.  Kill any old BlazorServer: Stop-Process -Name BlazorServer -Force
4.  Wait 3 seconds (port release)
5.  Start new BlazorServer: Start-Process BlazorServer.exe -WorkingDirectory ...
6.  Wait ~10 seconds for startup
7.  Send /dcflush to WoW: .\send-dcflush.ps1
8.  Wait 15-20 seconds for keybindings to populate (expect ~56 bindings)
9.  Load profile: POST /api/bot/profile/load {fileName: "BloodElf_Rogue_8-60_TBC.json"}
10. Apply overrides: POST /api/launch/overrides {Bypass: {"8": true}} (action bar bypass for empty slots)
11. Start bot: POST /api/bot/start
12. Monitor: GET /api/bot/status every 5s — confirm isActive=true AND currentGoal is a real goal name
13. Watch logs for: "Trivial: False | Xms" (real path) vs "Trivial: False | 0.04ms" (CB open = dead)
```

---

## Report Generated
**Date**: February 18, 2026  
**Session Duration**: ~11 hours (10:00 AM — 9:00 PM EST, with user away ~4-7 hours)  
**Test Client**: WoW Anniversary TBC Phase, Blood Elf Rogue
**Summary**: 0/∞ successful bot runs. Every single start attempt failed due to one or more of the issues above.

---

## Addendum — Post-Fix Validation (February 18, 2026, 10:03 PM EST)

### Current Outcome
- One-command workflow now works with explicit ActionBar bypass:
  - `pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Agent-BotControl.ps1 -Action StartAndValidate -Profile BloodElf_Rogue_8-60_TBC.json -BypassActionBar`
- Validation passed end-to-end: `logs/agentctl-20260218-220303-validation.json` (`OverallPass=true`)
- Bot active and running profile route after startup (`/api/bot/status` active, goal populated)

### Additional Fixes Applied After Original Report
- `Core/GoalsComponent/Navigation.cs`
  - Stopped aggressive multi-waypoint popping (`ReduceByDistance` now pops at most one waypoint per update)
  - Added tighter reach tolerance on dense segments (bridge/ledge precision handling)
- `Core/GoalsFactory/GoalFactory.cs`
  - Replaced naive every-other-point reduction with corner-preserving reduction to keep critical turns
- `Json/class/BloodElf_Rogue_8-60_TBC.json`
  - Disabled `PathReduceSteps` for early Eversong/Ghostlands route bands (levels 8-12)
- `Scripts/Agent-BotControl.ps1`
  - Added `-BypassActionBar` switch so StartAndValidate can run as a single command despite known ActionBar gate issue

### Live Monitoring Snapshot (Post-Fix)
- 90s follow-route monitor:
  - `UIMapId` stable at `1941` (no zone drift)
  - `Swimming=false` throughout
  - `ChatInputVisible=false` throughout
  - No pathfinder empty-path churn detected in sampled tail

### Remaining Known Issue
- ActionBar readiness still flags `Stealth` slot as empty without override:
  - `/api/diagnostics/actionbar` reports `issueCount=1` (`Stealth`, slot `1`, `EmptySlot`, `canResolve=true`)
  - Operational workaround is now built into one-command flow via `-BypassActionBar`
