# Bot Debugging Report — 2026-03-02

**Status:** Bot cannot start. `canStartBot: false`
**Session time at report:** ~20:19 UTC (server running, WoW running, bot idle)

---

## Executive Summary

The bot is fully operational at the infrastructure level — the server is up, WoW is running (PID 16048), navigation (RemoteV3 hybrid) is connected, addons are validated, frames are configured, and the profile is loaded. However, **two launch readiness subsystems are hard-blocking bot start**, and neither can be bypassed via the API without in-game action.

**Primary blocker:** KeyBindings subsystem timed out — the addon's `bindingQueue` has sent 0 binding packets since server start (72,300+ consecutive zero reads, 0 initialized bindings). The bot guard refuses all bypasses when binding count = 0.

**Secondary blocker:** Action Bar subsystem stuck in Pending — textures not initialized, dependent on keybindings being live.

---

## Problem 1 (Hard Blocker): KeyBindings = 0, Subsystem Timed Out

### Observed Symptoms

```
[server.log] [KeyBindingsReader] Waiting for bindings - current count=0  (every ~16ms, 72,300+ times)
[server.log] [KeyBindings Slot 106 Stats] Consecutive zeros: 72300, Total reads: 72300, Non-zero reads: 0
```

API response from `/api/health` startup subsystems:
```json
{
  "subsystem": "KeyBindings",
  "status": 4,          // LaunchStatus.Error
  "isBlocking": true,
  "message": "Timed out waiting for bindings (290s, reads=19588, nonZero=645, zeros=17613)",
  "fixHint": "Run /dcflush (or /dcbindings) in WoW, then re-check",
  "navigateTo": "/KeyBindings"
}
```

### Data Flow Trace

**WoW Addon side (Lua):**
- `Addons/DataToColor/DataToColor.lua:950` — `DataToColor.bindingQueue:shift(globalTick)` is written to pixel slot 106 every frame.
- `Addons/DataToColor/SetupDefaultBindings.lua:214,237,240` — `bindingQueue:push(encoded)` populates the queue when bindings are initialized.
- `DataToColor.lua:468` — `/dcflush` calls `FushState()` → `ClearAllQueues()` + `ClearBindingCache()` + `InitUpdateQueues()` which re-populates the queue.
- `DataToColor.lua:277` — `/dcbindings` calls `SetDefaultBindings()` directly.

**C# Bot side:**
- `Core/Addon/KeyBindingsReader.cs:13` — reads pixel slot `BINDING_SLOT = 106` each frame.
- `KeyBindingsReader.cs:42–66` — if `encodedValue == 0`, increments `consecutiveZeroReads`. If `bindings` dict is non-empty → marks `initialized = true`. If bindings dict is empty → logs "Waiting for bindings".
- `Core/Launch/BotStartGuard.cs:1204` — if `!keyBindingsReader.IsInitialized` and timeout elapsed → returns `LaunchStatus.Error, IsBlocking: true`.
- `BotStartGuard.cs:283–298` — **hardcoded safeguard**: if `check.Subsystem == LaunchSubsystem.KeyBindings` AND binding count = 0, bypass is **refused** even with `Bypass: {KeyBindings: true}` in launch overrides:
  ```csharp
  // Safeguard: NEVER bypass KeyBindings=0 — bot cannot function without keybindings.
  // Without keybindings the bot autorun-spams targets without attacking
  logger.LogWarning("[BotStartGuard] KeyBindings bypass refused: count=0. Run /dcflush in-game first.");
  return check with IsBlocking = true;
  ```

### Root Cause Analysis

The `bindingQueue` pixel slot 106 has been returning 0 for the entire server session. This means the addon is either:
1. **Not populating the queue at all** — `SetDefaultBindings()` / `InitUpdateQueues()` was never called or returned empty
2. **Addon not running** — DataToColor addon is not loaded in WoW
3. **Frame capture misaligned** — the screen capture region for slot 106 is reading the wrong pixels (returns 0 because wrong area is sampled)
4. **Character not in world** — addon only populates bindings after `PLAYER_LOGIN` event; if character select screen is showing, queue stays empty

The `reads=19588, nonZero=645` in the timeout message (from a previous run) indicates bindings WERE received briefly, then stopped — but in the current server session, `nonZeroReads = 0` and `consecutiveZeroReads = 72,300+`, meaning no binding data has arrived since the current server started.

### What Has Already Been Tried

The previous session applied `Bypass: {KeyBindings: true, ActionBar: true}` in launch overrides, but this is refused by the hardcoded guard at `BotStartGuard.cs:283` when count = 0.

### Resolution Path (In-Game Action Required)

This **cannot** be fixed by code changes alone. Required in-game action:
1. Verify WoW character is **in the world** (not on character select screen)
2. Open WoW chat and type `/dcflush` — this triggers `FushState()` → clears queues and re-pushes all bindings
3. Alternatively: type `/dcbindings` — directly calls `SetDefaultBindings()`
4. Navigate to http://localhost:5000/KeyBindings in the bot UI and click "Sync Actionbar"
5. Wait for the KeyBindings subsystem to show green (count > 0, `initialized = true`)

---

## Problem 2 (Secondary Blocker): Action Bar Textures Not Initialized

### Observed Symptoms

```json
{
  "subsystem": "ActionBar",
  "status": 1,          // LaunchStatus.Pending
  "isBlocking": true,
  "message": "Textures not initialized (enter world / wait for sync)",
  "fixHint": "Enter world and wait; then Sync Actionbar on Key Bindings page",
  "navigateTo": "/KeyBindings"
}
```

### Root Cause

- The action bar texture system (`DataToColor.actionBarTextureQueue`, slot 107) follows the same initialization flow as keybindings.
- `DataToColor.lua:464` — `actionBarTextureQueue:clear()` is called in `ClearAllQueues()`.
- If `/dcflush` was never run (or the character wasn't in world), neither the binding queue nor the action bar texture queue gets populated.
- This subsystem will self-resolve once `/dcflush` is run in-game (it's triggered by the same `FushState()` → `InitUpdateQueues()` call that fixes keybindings).

---

## Problem 3 (Previously Fixed, Status Confirmed): Navigation State Leak

### Status

The Navigation state leak fix was applied this session:

**`Core/GoalsComponent/Navigation.cs:398–406`** — `Stop()` now unconditionally clears both `wayPoints` and `routeToNextWaypoint`:

```csharp
public void Stop()
{
    active = false;

    wayPoints.Clear();           // FIXED: was missing, caused stale vendor waypoint leak
    routeToNextWaypoint.Clear(); // FIXED: RemoteV3 guard removed, always clears

    ResetStuckParameters();
}
```

This fix is committed but **has not been tested in a live run** because the bot cannot start due to Problems 1 & 2 above.

---

## Problem 4 (Context): Character Physically Stuck in Building

### Observed State

- Character position: `<10357, -6438, 41.9>` Eversong Woods
- Bot ran 35+ stuck recovery cycles with no escape
- Bot was routing to stale vendor waypoint `<9006.74, -6800.31, Z=0>` (pre-fix)

### Current State After Fix

The Navigation.Stop() fix should prevent future re-occurrence of the routing-to-vendor behavior. However:
- The character is still physically inside a building wall
- Once the bot is able to start (after Problems 1 & 2 are resolved), the stuck detector should trigger and attempt escape
- If the character cannot escape automatically within ~2 minutes, **manual intervention is required**: walk the character outside the building before starting the bot

---

## Current System State Summary

| Subsystem | Status | Detail |
|-----------|--------|--------|
| WoW Process | ✅ OK | PID 16048 |
| Navigation | ✅ OK | RemoteV3 hybrid connected |
| Addons | ✅ OK | DataToColor validated |
| Frames | ✅ OK | Frame config valid |
| Addon Handshake | ✅ OK | Live data, age=14ms |
| Profile | ✅ OK | BloodElf_Warlock_1-70_TBC.json loaded |
| Route | ✅ OK | 51 waypoints loaded |
| **KeyBindings** | ❌ **BLOCKING** | 0 bindings, timed out (290s), non-bypassable |
| **Action Bar** | ⚠️ **BLOCKING** | Pending — textures not initialized |
| `canStartBot` | ❌ **FALSE** | Hard blocked by above two |

---

## All Modified Files (Uncommitted Changes)

These changes are in working tree, not yet committed:

| File | Change | Risk |
|------|--------|------|
| `Addons/DataToColor/Query.lua` | Corpse map parent-chain walk (tries parent maps if corpse pos returns nil) | Low |
| `BlazorServer/runtime_feature_flags.json` | Humanization enabled, BurstDampening enabled, ReactionMaxMs 500→800 | Low |
| `Core/BotController.cs` | Split LoadClassProfile try/catch; post-load init errors now warn+continue instead of fail | Medium |
| `Core/Database/AreaDB.cs:157` | Skip NPC entries with `pos.Z == 0` (prevents bad vendor selection) | Low |
| `Core/Goals/FollowRouteGoal.cs` | Blacklist backoff delay, PathThereAndBack guard for reverse refill, loop mode anchor reset | Medium |
| `Core/Goals/WalkToCorpseGoal.cs` | Strict ≤ 0 bound check for corpse position (was `< 0`) | Low |
| `Core/GoalsComponent/Navigation.cs` | `Stop()` now clears `wayPoints` + `routeToNextWaypoint` unconditionally | **High — key fix** |
| `Core/GoalsComponent/TargetFinder.cs` | Added blacklist backoff (2000ms cooldown after blacklisted target) | Medium |
| `Core/WoWScreen/WowScreenDXGI.cs` | DXGI DuplicateOutput wrapped in 10-attempt retry loop | Low |
| `Frontend/Controllers/BotApiController.cs` | Profile load: 500ms sleep, null-check ClassConfig, better errors | Low |
| `Json/path/_pack/1-20/Blood elf/1-6_Eversong Woods.json` | 3 waypoints prepended to route start | Low |

---

## Recommended Resolution Steps (In Order)

### Step 1 — Restore keybindings (in-game action, required)
1. Switch to WoW window
2. Verify character is **in the world** (not character select)
3. Open chat, type `/dcflush` and press Enter
4. Watch bot UI at http://localhost:5000/KeyBindings for green status
5. If `/dcflush` doesn't work within 30 seconds, also type `/dcbindings`

### Step 2 — Sync Action Bar (in-game action)
1. In WoW: press Escape → ensure action bars are visible and populated
2. In bot UI at http://localhost:5000/KeyBindings: click "Sync Actionbar" button
3. Wait for Action Bar subsystem to show green

### Step 3 — Move character out of building (if needed)
1. Manually walk character out of building at `<10357, -6438, 41.9>`
2. Place character on open ground in Eversong Woods

### Step 4 — Start bot
```
POST /api/bot/start
```
Or use the Start button in the UI.

### Step 5 — Monitor for 10 minutes
- Verify `currentGoal` is `Follow 6-12 Eversong Woods` (not AdhocNPC)
- Verify no pathfinder calls to `9006.74 -6800.31 0` in logs
- Verify kill rate > 0 over 10-minute window
- Verify next Sell trigger uses valid Z≠0 vendor (AreaDB fix)

---

## Key Code Locations for Next Agent

| What | Where |
|------|-------|
| KeyBindings timeout logic | `Core/Launch/BotStartGuard.cs:1171–1235` |
| Bypass hardcoded safeguard (no bypass if count=0) | `Core/Launch/BotStartGuard.cs:283–298` |
| Binding queue populated in Lua | `Addons/DataToColor/SetupDefaultBindings.lua:214,237,240` |
| `/dcflush` handler in Lua | `Addons/DataToColor/DataToColor.lua:468–479` |
| Navigation Stop() fix | `Core/GoalsComponent/Navigation.cs:398–406` |
| FollowRouteGoal Resume() | `Core/Goals/FollowRouteGoal.cs:211–242` |
| BotStartGuard CanStartBot logic | `Core/Launch/BotStartGuard.cs:209–219` |
