# Post-Implementation Review: Codex CLI Agent Plan-2-6-26-3pm

## Executive Summary

The Codex CLI agent implemented a 4-phase fix plan targeting three root cause failure chains. After exhaustive code review, build verification, unit testing, runtime testing, and historical log analysis, this review identifies **2 critical bugs introduced**, **1 incomplete fix**, **1 code quality concern**, and **3 fully correct implementations**. **None of the changes have been committed or deployed** — all 15 files remain in the working tree as uncommitted modifications.

**Verdict: The changes need targeted fixes before commit. They are NOT deploy-ready in their current state.**

---

## Test Evidence Summary

| Test Vector | Result | Evidence |
|---|---|---|
| Build (`dotnet build MasterOfPuppets.sln`) | **PASS** | 0 errors, 0 warnings, 7.92s |
| CoreUnitTests (`dotnet test CoreUnitTests`) | **PASS** | 161/161 passed |
| FrontendUnitTests (`dotnet test FrontendUnitTests`) | **PASS** | 7/7 passed |
| Runtime – config mode (no WoW) | **PARTIAL** | 3/7 nav routes return HTTP 200; 4/7 crash with HTTP 500 |
| Live WoW testing | **NOT PERFORMED** | Changes uncommitted; never deployed to production binary |
| Git status | **UNCOMMITTED** | All 15 files modified, unstaged, on `dev` branch at `d6d6b754` |

---

## Finding 1: CRITICAL — Config-Mode Navigation Causes 4 Crash Routes

### Severity: **CRITICAL — Regression**

### Description

The Codex agent expanded `GetConfigNavItems()` in `Frontend/Shared/MainLayout.razor` from wizard-only steps to 7 full page links. Four of these pages `@inject` DI services that are **only registered when WoW is running and configuration is complete**. When accessed in config mode, they crash with `InvalidOperationException`.

### Evidence

**Before (committed HEAD):**
```csharp
// GetConfigNavItems() only showed wizard steps:
navItems = new List<NavItem> {
    new() { Id = "0", IconName = IconName.StopBtn, Text = "Process Not Running" },
};
// No page routes were exposed in config mode
```

**After (working tree):**
```csharp
navItems = new List<NavItem> {
    new() { Id = "cfg-dashboard", Href = "/", Text = "Dashboard", Match = NavLinkMatch.All },
    new() { Id = "cfg-launch", Href = "/launch", Text = "Launch Wizard" },
    new() { Id = "cfg-settings", Href = "/Settings", Text = "Settings" },
    new() { Id = "cfg-combat-rotation", Href = "/combat-rotation", Text = "Combat Rotation" },
    new() { Id = "cfg-frame", Href = "/FrameConfiguration", Text = "Frame Configuration" },
    new() { Id = "cfg-keybindings", Href = "/KeyBindings", Text = "Key Bindings" },
    new() { Id = "cfg-raw", Href = "/RawPlayerReader", Text = "Raw Values" },
};
```

**Crash log evidence (from `out20260206_001.log` tail):**
```
System.InvalidOperationException: Cannot provide a value for property 'levelTracker'
on type 'Frontend.Pages.BotHeader'. There is no registered service of type 'Core.LevelTracker'.
```

**HTTP test results:**

| Route | HTTP Status | Works in Config Mode? |
|---|---|---|
| `/Settings` | 200 | YES |
| `/combat-rotation` | 200 | YES |
| `/launch` | 200 | YES |
| `/` (Dashboard) | 500 | **NO** — `LevelTracker` not registered |
| `/FrameConfiguration` | 500 | **NO** — `FrameConfigurator` not registered |
| `/KeyBindings` | 500 | **NO** — `KeyBindingsReader` not registered |
| `/RawPlayerReader` | 500 | **NO** — Multiple missing services |

### Root Cause

`BotHeader.razor` (used by `/` Dashboard/Index page) has 12 `@inject` directives including `LevelTracker`, `PlayerReader`, `CombatLog`, `BagReader`, `SpellBookReader`, `TalentReader`, `ActionBarCostReader`, `KeyBindingsReader`, `AddonBits`, `IBotController`, `WApi`, and `IAddonReader`. These are all registered in the WoW-is-running DI path (`DependencyInjection.AddWoWProcess()`), not in config mode.

### Fix Required

Either:
1. **Remove crash-prone routes** from config-mode nav (Dashboard, FrameConfiguration, KeyBindings, RawValues) — safest
2. **Register null/no-op service implementations** in config mode for critical types
3. **Add `@if` guards** in the Razor pages to handle missing services gracefully

**Recommended: Option 1** — revert to the original behavior where config-mode nav only shows wizard steps, plus the safe routes (Settings, Combat Rotation, Launch Wizard).

---

## Finding 2: INCOMPLETE — FrameConfig Width Tolerance Missing

### Severity: **HIGH — Fix Does Not Address Actual Failure**

### Description

The Codex agent added height tolerance (`± 100px`) but left width as an exact match. Historical log data proves the **actual failure case** involves a **width** mismatch (3856 vs 3840), not just height.

### Evidence

**Log evidence from `out20260206_001.log` line 311:**
```
[08:06:51:647 E] [Program] FrameConfig Rectangle [ X=0, Y=0, Width=3856, Height=2200 ]
```

**Stored config was:** `Width=3840, Height=2160`
**Actual window rect:** `Width=3856, Height=2200`

Differences: **Width Δ=16px**, Height Δ=40px

**Current code in `FrameConfig.IsValid()`:**
```csharp
bool sameWidth = config.Rect.Width == rect.Width;          // EXACT match — 3856 ≠ 3840 → FALSE
bool similarHeight = Math.Abs(config.Rect.Height - rect.Height) <= HeightTolerancePixels;  // |2160-2200| = 40 ≤ 100 → TRUE
bool sameRect = sameWidth && similarHeight;                // FALSE && TRUE → FALSE
```

The config will STILL be deleted because `sameWidth` fails. The height tolerance alone does not fix the problem.

### Root Cause

The 16px width difference (3840 → 3856) is likely caused by:
- DPI scaling artifacts on 4K monitors
- Window shadow/border calculations by Windows DWM
- The `GetWindowRect` P/Invoke including invisible window borders

### Fix Required

Add width tolerance as well:
```csharp
private const int WidthTolerancePixels = 20;
private const int HeightTolerancePixels = 100;

bool similarWidth = Math.Abs(config.Rect.Width - rect.Width) <= WidthTolerancePixels;
bool similarHeight = Math.Abs(config.Rect.Height - rect.Height) <= HeightTolerancePixels;
bool sameRect = similarWidth && similarHeight;
```

---

## Finding 3: CORRECT — Target Clearing Reorder (Phase 1)

### Severity: **N/A — Correctly Implemented**

### Description

`ConfigurableInput.PressClearTarget()` now calls `PressRandom(ClearTarget, token)` instead of `PressF11ClearTarget()`, making the configured keybinding (Alt+Insert) the primary path.

`ForceAggressiveClearTarget()` correctly implements the escalation order:
1. Configured binding via `PressRandom(ClearTarget)` — 3 retries
2. ESC key — 1 attempt
3. F11 fallback — 3 retries
4. `/cleartarget` chat command via `ExecGameCommand` — 1 attempt

### Evidence

**Before (committed HEAD at `055fa496`):**
```csharp
public async Task PressClearTarget(CancellationToken token)
{
    await PressF11ClearTarget(token);
}
```

**After (working tree):**
```csharp
public async Task PressClearTarget(CancellationToken token)
{
    await PressRandom(ClearTarget, token);
}
```

**Verification:** `git show 055fa496:Core/Input/ConfigurableInput.cs` confirms F11 was the primary path in the prior commit. The reorder is correct.

### DI Wiring Verification

All 8 goal classes that call `ForceAggressiveClearTarget` now have `ExecGameCommand execGameCommand` in their constructors:
- `BlacklistTargetGoal.cs` ✅
- `ReactCastError.cs` ✅
- `CombatTracker.cs` ✅
- `FleeGoal.cs` ✅
- `PullTargetGoal.cs` ✅
- `TargetPetTargetGoal.cs` ✅
- `TargetFocusTargetGoal.cs` ✅
- `FollowFocusGoal.cs` ✅

`ExecGameCommand` is registered as a singleton at `DependencyInjection.cs:314` and forwarded to the scoped GoalFactory container via `ForwardSingleton<ExecGameCommand>()` — DI chain is complete.

---

## Finding 4: CORRECT — Navigation Server Resilience (Phase 2)

### Severity: **N/A — Correctly Implemented**

### Description

Three improvements were made to navigation server management:

1. **Port cleanup:** `NavigationServerManager.TryTerminateProcessHoldingPort()` uses `netstat -ano` to find and kill zombie processes holding port 47110 before starting a new server.

2. **Auto-restart on crash:** `MonitorServerAsync()` now calls `EnsureRunningAsync()` after detecting unexpected process exit, with exponential backoff.

3. **Connection delay:** `RemotePathingAPIV3.ObserveConnection()` adds a 2.5s `InitialConnectDelayMs` before first TCP connection attempt, giving the navigation server time to bind.

### Design Notes

- The netstat-based approach is robust for Windows but platform-specific
- Guard against double-start with `_monitorCts` null check in `StartMonitoring()` is correct
- The `wasConnected` tracking in `ObserveConnection()` prevents unnecessary delays on first connection

---

## Finding 5: CORRECT — Diagnostic Logging for FrameConfig Deletion

### Severity: **N/A — Correctly Implemented**

### Description

`DependencyInjection.cs` now loads the existing config before deletion and logs:
- `StoredRect` vs `CurrentRect`
- `StoredAddonVersion` vs `CurrentAddonVersion`
- `StoredFrames` count

This provides the diagnostic data needed to debug future FrameConfig mismatches without the current guessing game.

---

## Finding 6: CODE QUALITY — Duplicated Port Cleanup Methods

### Severity: **MEDIUM — Code Smell**

### Description

`TryTerminateProcessHoldingPort()` and `GetListeningProcessIds()` are copy-pasted identically in two files:
- `Core/Startup/NavigationServerManager.cs` (lines 437-546)
- `Core/Startup/StartupOrchestrator.cs` (lines 632-740)

The only difference is the log message class name (`[NavigationServerManager]` vs `[StartupOrchestrator]`).

### Fix Required

Extract to a shared utility class:
```csharp
namespace Core.Startup;

public static class PortCleanupUtility
{
    public static void TryTerminateProcessHoldingPort(
        int port, string expectedProcessName, ILogger logger) { ... }

    public static HashSet<int> GetListeningProcessIds(int port) { ... }
}
```

---

## Finding 7: CODE QUALITY — FleeGoal Duplicate Constructor Parameter

### Severity: **LOW — Pre-existing, Functionally Harmless**

### Description

`FleeGoal.cs` has TWO `ClassConfiguration` parameters in its constructor:
```csharp
public FleeGoal(...,
    ClassConfiguration classConfiguration, Navigation playerNavigation,
    ClassConfiguration classConfig,
    ...)
```

- `classConfiguration` is used on line 51: `Keys = classConfiguration.Flee.Sequence;`
- `classConfig` is used on line 47: `this.classConfig = classConfig;`

DI injects the same singleton for both, so behavior is correct. But this is sloppy — the Codex agent should have cleaned this up when modifying the constructor. This was a pre-existing issue.

---

## Deployment State

### All changes are UNCOMMITTED

```
 M Core/DependencyInjection.cs
 M Core/DataFrame/FrameConfig.cs
 M Core/Goals/BlacklistTargetGoal.cs
 M Core/Goals/CombatTracker.cs
 M Core/Goals/FleeGoal.cs
 M Core/Goals/FollowFocusGoal.cs
 M Core/Goals/PullTargetGoal.cs
 M Core/Goals/ReactCastError.cs
 M Core/Goals/TargetFocusTargetGoal.cs
 M Core/Goals/TargetPetTargetGoal.cs
 M Core/Input/ConfigurableInput.cs
 M Core/PPather/RemotePathingAPIV3.cs
 M Core/Startup/NavigationServerManager.cs
 M Core/Startup/StartupOrchestrator.cs
 M Frontend/Shared/MainLayout.razor
```

Branch: `dev` at commit `d6d6b754`

**No production testing is possible until changes are committed, built, and deployed.**

---

## Action Items (Priority Order)

| # | Priority | Item | Files | Est. Effort |
|---|---|---|---|---|
| 1 | **P0** | Fix config-mode nav crashes — remove Dashboard, FrameConfiguration, KeyBindings, RawValues from `GetConfigNavItems()` | `Frontend/Shared/MainLayout.razor` | 15 min |
| 2 | **P0** | Add width tolerance to `FrameConfig.IsValid()` | `Core/DataFrame/FrameConfig.cs` | 10 min |
| 3 | **P1** | Extract `TryTerminateProcessHoldingPort`/`GetListeningProcessIds` to shared utility | `Core/Startup/NavigationServerManager.cs`, `Core/Startup/StartupOrchestrator.cs`, new `Core/Startup/PortCleanupUtility.cs` | 30 min |
| 4 | **P2** | Clean up `FleeGoal.cs` duplicate `ClassConfiguration` parameter | `Core/Goals/FleeGoal.cs` | 10 min |
| 5 | **P1** | Commit, rebuild, deploy, and test with live WoW client | All files | 60 min |

### Verification Commands After Fixes

```bash
# Build
dotnet build MasterOfPuppets.sln

# Unit tests
dotnet test CoreUnitTests
dotnet test FrontendUnitTests

# Runtime smoke test (config mode, no WoW)
# Start: dotnet run --project BlazorServer
# Verify: All nav links return HTTP 200
# Test: curl http://localhost:5000/Settings → 200
# Test: curl http://localhost:5000/ → 200 (after fix)
# Test: curl http://localhost:5000/launch → 200

# Live WoW test (after commit + rebuild)
# 1. Launch WoW Classic at 1920x1008 windowed
# 2. Start bot: dotnet run --project BlazorServer
# 3. Verify FrameConfig not deleted on startup (check logs for "FrameConfig mismatch")
# 4. Target a mob → verify Alt+Insert clears target (check logs for "PressRandom ClearTarget")
# 5. Monitor navigation server for >5 minutes (check logs for "crashed" or "restarting")
```

---

## Summary Scorecard

| Phase | Plan Claim | Actual Status | Verdict |
|---|---|---|---|
| Phase 1: Target clearing reorder | Use configured binding as primary | **Correctly implemented** | ✅ |
| Phase 1: ExecGameCommand DI injection | Add to 8 goal constructors | **Correctly implemented** | ✅ |
| Phase 2: Navigation server port cleanup | Kill zombies before start | **Correctly implemented** | ✅ |
| Phase 2: Auto-restart on crash | Monitor and restart | **Correctly implemented** | ✅ |
| Phase 2: Connection delay | 2.5s before first TCP connect | **Correctly implemented** | ✅ |
| Phase 3: FrameConfig height tolerance | Allow ±100px height difference | **Incomplete — width tolerance also needed** | ⚠️ |
| Phase 3: Diagnostic logging | Log stored vs current config before delete | **Correctly implemented** | ✅ |
| Phase 3: Nav expansion | Expose useful pages in config mode | **Introduces 4 crash routes** | ❌ |
| Phase 4: Code duplication | (Not in original plan) | **Port cleanup methods duplicated** | ⚠️ |
| Deployment | Commit and validate | **Never committed or deployed** | ❌ |
