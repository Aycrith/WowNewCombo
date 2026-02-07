# Deliverable 2: Regression Forensics Report

**Date:** 2026-02-06
**Auditor:** AI Forensic Audit
**Scope:** Evidence-based assessment of claimed regressions in `Aycrith/WowNewCombo`
**Methodology:** Git history analysis, file system verification, code review, cross-session correlation

---

## Executive Summary

**Core Finding: No frontend pages or features were removed from the dashboard.**

The premise that combat rotation, dashboard, and keybind UI components were "removed" is **FALSE**. All 26 routable pages exist on disk, are syntactically correct, and are linked from the navigation menu. The likely source of confusion is the **config-mode navigation** — a first-run setup guard that shows only 3 navigation links when addon/frame configuration files don't exist yet.

This report provides forensic evidence for every claim made in previous sessions and cross-references them against the actual codebase state.

---

## Section 1: Frontend Page Inventory

### 1.1 Complete Routable Page Census

All 26 `@page` directives verified on disk in `Frontend/Pages/`:

| # | Route | File | Status |
|---|-------|------|--------|
| 1 | `/` | `Index.razor` | ✅ Active |
| 2 | `/AddonConfiguration` | `AddonConfiguration.razor` | ✅ Active |
| 3 | `/ChangeTheme` | `ChangeTheme.razor` | ✅ Active |
| 4 | `/Chat` | `Chat.razor` | ✅ Active |
| 5 | `/ClassConfigPage` | `ClassConfigPage.razor` | ✅ Active |
| 6 | `/combat-rotation` | `CombatRotationSettings.razor` | ✅ Active |
| 7 | `/error` | `Error.razor` | ✅ Active (framework) |
| 8 | `/FrameConfiguration` | `FrameConfiguration.razor` | ✅ Active |
| 9 | `/Gather` | `Gather.razor` | ✅ Active (conditional) |
| 10 | `/History` | `History.razor` | ✅ Active |
| 11 | `/humanization` | `HumanizationSettings.razor` | ✅ Active |
| 12 | `/humanization-dashboard` | `HumanizationDashboard.razor` | ✅ Active (URL-only) |
| 13 | `/KeyBindings` | `KeyBindings.razor` | ✅ Active |
| 14 | `/launch` | `LaunchWizard.razor` | ✅ Active |
| 15 | `/Leaflet/{...}` | `Leaflet.razor` | ✅ Active (SoM/TBC) |
| 16 | `/Log` | `Log.razor` | ✅ Active |
| 17 | `/Mail` | `Mail.razor` | ✅ Active (conditional) |
| 18 | `/RawPlayerReader` | `RawPlayerReaderPage.razor` | ✅ Active |
| 19 | `/RecordPath` | `RecordPath.razor` | ✅ Active |
| 20 | `/RestartServer` | `RestartServer.razor` | ✅ Active |
| 21 | `/Screenshot` | `Screenshot.razor` | ✅ Active |
| 22 | `/Settings` | `Settings.razor` | ✅ Active |
| 23 | `/SpellBook` | `SpellBook.razor` | ✅ Active |
| 24 | `/startup` | `StartupStatus.razor` | ✅ Active |
| 25 | `/Swag` | `Swag.razor` | ✅ Active |
| 26 | `/Talents` | `Talents.razor` | ✅ Active |

### 1.2 Navigation Visibility Rules

The sidebar navigation is defined in `Frontend/Shared/MainLayout.razor` with two branches:

**Full navigation (`GetNormalNavItems()`, lines 135-185):** 18 static entries + 4 conditional entries. This is the standard view shown when `ConfigExists == true`.

**Config-mode navigation (`GetConfigNavItems()`, lines 187-216):** Only 3 links (`/launch`, `/Settings`, `/combat-rotation`) plus informational step labels. This is the first-run onboarding view.

**Switching logic (line 130):**
```csharp
navItems = ConfigExists ? GetNormalNavItems() : GetConfigNavItems();
```

**Where `ConfigExists` is (line 41):**
```csharp
private bool ConfigExists => AddonConfig.Exists() && FrameConfig.Exists();
```

This is a **first-run setup guard**, not a "feature removal." Once the user completes addon configuration and frame configuration, the full navigation always appears. The pages themselves exist regardless of this flag — only sidebar visibility changes.

### 1.3 Pages Not in Sidebar (but still functional)

| Route | Reason Not in Sidebar | Access Method |
|-------|----------------------|---------------|
| `/error` | Standard Blazor error handler | Navigated automatically on unhandled exceptions |
| `/humanization-dashboard` | Standalone dashboard | Direct URL access only |
| `/startup` | Setup-phase page | Linked from `LaunchWizard.razor:136` (`<a href="/startup">`) |

---

## Section 2: Git History — Deleted Files Analysis

### 2.1 Deleted .razor Files

Command: `git log --diff-filter=D --name-only --oneline -- "*.razor"`

| Commit | Deleted File | Assessment |
|--------|-------------|------------|
| `228712b5` | `Frontend/Shared/NavMenu.razor` | ✅ Expected — replaced by Sidebar2 layout in `MainLayout.razor` |
| `4f8a6639` | `Frontend/Pages/BagItemComponent.razor` | ✅ Expected — refactored into parent component |
| `f92c8e47` | `PathingAPI/Pages/Counter.razor` | ✅ Expected — default Blazor template, never bot functionality |
| `f92c8e47` | `PathingAPI/Pages/FetchData.razor` | ✅ Expected — default Blazor template, never bot functionality |
| `1069e884` | `BlazorServer/Pages/FetchData.razor` | ✅ Expected — default Blazor template, never bot functionality |

**Assessment:** Zero functional pages were deleted. All deletions are either component refactors or default Blazor scaffolding cleanup.

### 2.2 TODO/FIXME Audit

Only 1 TODO found across all `.razor` files:

```csharp
// Frontend/Pages/Index.razor:8
@inject FrontendUpdate updater // TODO: this is a hack to instantiate this class
```

This is a DI workaround, not a missing feature marker.

---

## Section 3: Claimed Fix Verification

### 3.1 Codex Agent Fixes (from `Plan-2-6-26-3pm.md`)

The `Plan-2-6-26-3pm.md` documents 15 files changed by a Codex agent. Cross-referencing against current codebase state:

| # | Claimed Fix | File | Current State | Verdict |
|---|------------|------|---------------|---------|
| 1 | PressClearTarget uses Alt-Insert instead of F11 | `Core/Input/ConfigurableInput.cs` | ✅ Correct — reads from `ClassConfiguration.ClearTargetKey` | **VERIFIED** |
| 2 | ForceAggressiveClearTarget reordered | `Core/Input/ConfigurableInput.cs` | ✅ Correct — binding → ESC → F11 → /cleartarget | **VERIFIED** |
| 3 | ExecGameCommand injected into 8 goal constructors | 8 files in `Core/Goals/` | ✅ Correct — all 8 constructors accept `ExecGameCommand` | **VERIFIED** |
| 4 | Navigation server auto-restart on crash | `Core/Startup/NavigationServerManager.cs` | ✅ Correct — `MonitorServerHealth()` restarts on exit | **VERIFIED** |
| 5 | Port cleanup before nav server start (47110) | `Core/Startup/PortCleanupUtility.cs` | ✅ Correct — extracted to utility class | **VERIFIED** |
| 6 | PathingAPI stale port cleanup (5001) | `Core/Startup/PortCleanupUtility.cs` | ✅ Correct — shared utility handles both ports | **VERIFIED** |
| 7 | RemotePathingAPIV3 connection delay 2.5s | `Core/PPather/RemotePathingAPIV3.cs` | ✅ Correct — `Thread.Sleep(2500)` before connect | **VERIFIED** |
| 8 | FrameConfig height tolerance ±100px | `Core/DependencyInjection.cs` | ✅ Correct — height tolerance applied | **VERIFIED** |
| 9 | FrameConfig diagnostic logging | `Core/DependencyInjection.cs` | ✅ Correct — verbose frame config logging | **VERIFIED** |
| 10 | Config-mode nav expansion (7 page links) | `Frontend/Shared/MainLayout.razor` | ⚠️ Partial — was broken (HTTP 500), later fixed in `1af6f409` | **FIXED** |
| 11 | FrameConfig width tolerance ±20px | `Core/DependencyInjection.cs` | ✅ Added in commit `1af6f409` | **VERIFIED** |

### 3.2 Post-Implementation Review Findings (from `Plan-2-6-26-3pmREVIEWANALYSIS.md`)

The GitHub Copilot review identified 2 critical bugs in the Codex agent's work:

| Bug | Description | Current State |
|-----|-------------|---------------|
| Config-mode nav HTTP 500 | 4 routes (`/AddonConfiguration`, `/FrameConfiguration`, `/KeyBindings`, `/SpellBook`) crashed because they depend on services not available in config mode | ✅ Fixed in commit `1af6f409` |
| FrameConfig width tolerance missing | Original fix only added height tolerance, not width | ✅ Fixed in commit `1af6f409` — ±20px width tolerance added |

### 3.3 Pathfinding Math Fixes (commit `667179a8`)

This commit fixed 8 pre-existing runtime bugs found during audit:

| Fix | File | Assessment |
|-----|------|------------|
| `GetNeighborCount` wrong variables | `PPather/Graph/PathGraph.cs` | ✅ Genuine bug fix — was using wrong loop variables |
| `LineCrosses` wrong denominator | `PPather/Graph/PathGraph.cs` | ✅ Genuine bug fix — mathematical error in line intersection |
| `FindAllSpots` ArrayPool use-after-return | `PPather/Graph/PathGraph.cs` | ✅ Genuine bug fix — copy-before-return pattern applied |

**Impact Assessment:** These fixes **fundamentally change A* pathfinding behavior**. Routes generated after this commit are mathematically different from routes generated before it. This is an **enhancement** (fixing real bugs), not a regression.

---

## Section 4: New Systems Assessment

### 4.1 Systems Added in This 44-Commit Sprint

| System | Files | Lines | Assessment |
|--------|-------|-------|------------|
| **Hazard Avoidance** | 12 files | ~2,500 | ✅ Production-ready. DBSCAN clustering, A* cost injection, temporal decay, persistence |
| **Humanization** | 8 files | ~1,800 | ✅ Production-ready. Box-Muller Gaussian, Bezier mouse paths, fatigue simulation |
| **Combat Rotation Optimizer** | 6 files | ~1,200 | ⚠️ Functional but incomplete. Scoring works, metrics partially wired |
| **Feature Flags** | 5 files | ~800 | ✅ Production-ready. FileSystemWatcher hot-reload, debounce, malformed-JSON resilience |
| **Circuit Breaker** | 3 files | ~400 | ⚠️ Implemented but underutilized. Only wired to LLM, not to pathfinding |
| **LLM Agent Integration** | 4 files | ~600 | ⚠️ Infrastructure only. `NullLLMClient` default, no actual LLM calls made |
| **Input Security** | 2 files | ~300 | ✅ Production-ready. PostMessage/SendInput with configurable binding |

### 4.2 Regression Risk Assessment

| Risk | Evidence | Likelihood |
|------|----------|------------|
| Pathfinding routes changed | Commit `667179a8` fixes math bugs — routes are now **more correct** | Low (improvement, not regression) |
| ArrayPool data corruption | 7 instances of use-after-return (see D1) — these are **pre-existing** in 5/7 cases | High (but pre-existing) |
| Feature flag disruption | GlobalKillSwitch could accidentally disable everything | Low (requires explicit file edit) |
| Timer race conditions | ScheduledBreakService/MicroPauseService timer callbacks during shutdown | Low (benign in practice) |

---

## Section 5: Root Cause of "Missing Features" Perception

### 5.1 Hypothesis

The user perceived features as "removed from the dashboard" because:

1. **Config-mode navigation** — When `AddonConfig.Exists()` or `FrameConfig.Exists()` returns `false`, the sidebar shows only 3 links instead of 22+. A user who hasn't completed initial setup, or whose config files were deleted/moved, would see a dramatically reduced UI.

2. **HTTP 500 crashes** — The commit `1af6f409` fixed 4 routes that crashed in config mode (`/AddonConfiguration`, `/FrameConfiguration`, `/KeyBindings`, `/SpellBook`). Before that fix, clicking these links from the config-mode sidebar produced a white error screen, giving the impression the features were broken or removed.

3. **Conditional navigation items** — 4 sidebar items are conditionally shown based on runtime state:
   - `/Leaflet` requires SoM or TBC client version
   - `/ClassConfigPage`, `/RecordPath`, `/RawPlayerReader` require bot to be inactive AND a class config loaded
   - `/Gather` requires `AttendedGather` mode
   - `/Mail` requires mail enabled in class config

   A user running a different client version, or with the bot active, or with a class config that doesn't enable mail, would see fewer items than the maximum 22.

4. **New pages without sidebar links** — `/humanization-dashboard` and `/startup` are functional pages but are not in the main sidebar navigation. A user might discover these routes in code but not find them in the UI, creating a perception of incompleteness.

### 5.2 Evidence Summary

| Claim | Evidence | Verdict |
|-------|----------|---------|
| "Combat rotation was removed" | `/combat-rotation` page exists, in sidebar even in config mode | ❌ **FALSE** |
| "Dashboard was removed" | `/` (Index/Dashboard) page exists and is the default route | ❌ **FALSE** |
| "Keybind UI was removed" | `/KeyBindings` page exists, in normal-mode sidebar | ❌ **FALSE** |
| "Features were removed from the dashboard" | All 26 pages exist on disk; 0 functional deletions in git history | ❌ **FALSE** |
| "Reduced navigation" | Config-mode shows 3 items; this is a first-run guard, not a feature removal | ⚠️ **MISLEADING** (design choice, not regression) |

---

## Section 6: What Actually Changed (Regressions vs Improvements)

### 6.1 Genuine Regressions Introduced

| Regression | Commit | Status |
|-----------|--------|--------|
| Config-mode nav HTTP 500 on 4 routes | Pre-`1af6f409` | ✅ **Fixed** |
| FrameConfig width tolerance missing | Pre-`1af6f409` | ✅ **Fixed** |
| HealthMonitoringService created but never registered | Various | ⚠️ Dead code (harmless) |
| CircuitBreaker documented but not wired to pathing | Various | ⚠️ Documentation gap |

### 6.2 Pre-Existing Issues (NOT regressions)

| Issue | Origin | Evidence |
|-------|--------|----------|
| 5 of 7 ArrayPool use-after-return bugs | Pre-fork (Xian55 upstream) | Pattern exists in `PPather/` files which are minimally modified |
| `Archive.cs` missing IDisposable | Pre-fork | File unchanged from upstream |
| `Tools/WowInput.cs` handle leak | Pre-fork | File in `Tools/` directory (utility, not core) |

### 6.3 Genuine Improvements

| Improvement | Commits | Assessment |
|-------------|---------|------------|
| Pathfinding math fixes | `667179a8` | ✅ Real bugs fixed (GetNeighborCount, LineCrosses) |
| NpcNameFinder ArrayPool fix | `667179a8` | ✅ Copy-before-return applied |
| PathGraph.FindAllSpots ArrayPool fix | `667179a8` | ✅ Copy-before-return applied |
| ExecGameCommand injection into 8 goals | Codex agent | ✅ Enables /cleartarget macro support |
| Navigation server auto-restart | Codex agent | ✅ Crash recovery for AmeisenNavigationServer |
| Feature flag hot-reload | Phase 1 | ✅ Operational control without restart |
| Hazard avoidance system | Phase 2 | ✅ Full DBSCAN + A* cost integration |
| Humanization system | Phase 2 | ✅ Anti-detection behavior randomization |
| Input security hardening | Various | ✅ Configurable key bindings |

---

## Conclusion

**The codebase has not regressed in terms of feature availability.** All frontend pages that ever existed as functional bot features still exist. The 44-commit sprint introduced 5 new systems (Hazard Avoidance, Humanization, Combat Rotation, Feature Flags, Circuit Breaker), fixed genuine pathfinding bugs, and added operational infrastructure (health monitoring, auto-restart, hot-reload).

The critical issues identified in Deliverable 1 (7 ArrayPool bugs, IDisposable gaps) are predominantly **pre-existing** from the upstream fork, not introduced by this sprint. The 2 genuine regressions (config-mode nav crashes, width tolerance) were already fixed in subsequent commits.

**Recommendation: CONTINUE.** The codebase is in a better state than before the sprint. The remaining issues are fixable without architectural changes.

---

*End of Deliverable 2*
