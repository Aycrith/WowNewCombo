# AI Agent Handoff Prompt: WowClassicGrindBot Fix Completion

## Mission

You are an AI coding agent tasked with **completing the fix implementation** for WowClassicGrindBot after a comprehensive post-implementation review has identified critical issues in uncommitted code. Your objective is to fix 2 critical bugs, resolve code quality issues, commit the corrected changes, and validate with live testing.

## Critical Context Documents (READ FIRST)

**You MUST read these documents in order before proceeding:**

1. `C:\WowClassicGrindBot\AGENTS.md` — Codebase guidelines, build commands, performance patterns, .NET 10/C# 14 conventions
2. `C:\WowClassicGrindBot\docs\ImplementationSessionHistory\Plan-2-6-26-3pm.md` — Original plan from Codex CLI agent attempting to fix 3 root causes
3. `C:\WowClassicGrindBot\docs\ImplementationSessionHistory\Plan-2-6-26-3pmREVIEWANALYSIS.md` — Initial review analysis
4. `C:\WowClassicGrindBot\docs\POST_IMPLEMENTATION_REVIEW.md` — **Comprehensive review with evidence-based findings** (your primary directive)

## Current State

### Git Status
```
Branch: dev (commit d6d6b754)
Status: 15 files modified, ALL UNCOMMITTED
Build: Passes (0 errors, 0 warnings)
Unit Tests: CoreUnitTests (161/161), FrontendUnitTests (7/7) — ALL PASS
Production: NEVER DEPLOYED (changes only in working tree)
```

### Critical Findings from Review

**CRITICAL BUG #1: Config-Mode Navigation Crashes (HTTP 500 on 4 routes)**
- **File:** `Frontend/Shared/MainLayout.razor`
- **Problem:** `GetConfigNavItems()` exposes 7 page links in config mode (when WoW not running). Four routes (Dashboard `/`, `/FrameConfiguration`, `/KeyBindings`, `/RawPlayerReader`) crash with `InvalidOperationException` because they `@inject` DI services (LevelTracker, FrameConfigurator, etc.) only registered when WoW is running.
- **Evidence:** HTTP 500 errors on routes, log shows "Cannot provide a value for property 'levelTracker' on type 'Frontend.Pages.BotHeader'"
- **Original behavior:** Config mode nav showed ONLY wizard steps, no page links — this is a REGRESSION.

**CRITICAL BUG #2: FrameConfig Width Tolerance Missing**
- **File:** `Core/DataFrame/FrameConfig.cs`
- **Problem:** `IsValid()` method added height tolerance (`±100px`) but left width as EXACT MATCH. Historical log evidence proves actual failure was `Width=3856 vs 3840` (Δ=16px), likely DPI scaling on 4K monitors.
- **Evidence:** `out20260206_001.log` line 311 shows `Width=3856, Height=2200` vs stored `Width=3840, Height=2160`
- **Current code:** `bool sameWidth = config.Rect.Width == rect.Width;` — will STILL delete config on width mismatch despite height tolerance.

**CODE QUALITY #1: Duplicated Methods**
- **Files:** `Core/Startup/NavigationServerManager.cs` (lines 437-546), `Core/Startup/StartupOrchestrator.cs` (lines 632-740)
- **Problem:** `TryTerminateProcessHoldingPort()` and `GetListeningProcessIds()` are copy-pasted identically (only log class name differs)
- **Fix:** Extract to shared utility `Core/Startup/PortCleanupUtility.cs`

**CODE QUALITY #2: FleeGoal Duplicate Parameter**
- **File:** `Core/Goals/FleeGoal.cs`
- **Problem:** Constructor has TWO `ClassConfiguration` parameters (`classConfiguration` and `classConfig`). Functionally harmless (DI injects same singleton) but sloppy.
- **Status:** Pre-existing issue, low priority

### What's Correct (DO NOT BREAK)

These implementations are CORRECT per the review — do not modify:
1. ✅ `ConfigurableInput.PressClearTarget()` — correctly uses `PressRandom(ClearTarget)` instead of F11
2. ✅ `ForceAggressiveClearTarget()` escalation order — configured binding → ESC → F11 → /cleartarget
3. ✅ ExecGameCommand DI injection in 8 goal classes — all constructors correctly wired
4. ✅ Navigation server port cleanup using netstat
5. ✅ Navigation server auto-restart on crash
6. ✅ RemotePathingAPIV3 connection delay (2.5s)
7. ✅ Diagnostic logging for FrameConfig deletion (StoredRect vs CurrentRect)

## Your Task: Complete the Fixes

### PHASE 1: Fix Critical Bugs (P0 — BLOCKING)

**Task 1.1: Fix Config-Mode Nav Crashes**
- **File:** `Frontend/Shared/MainLayout.razor`
- **Action:** Modify `GetConfigNavItems()` to remove crash-prone routes from config-mode nav
- **Recommended approach:** Keep ONLY the safe routes (Settings, Combat Rotation, Launch Wizard) + wizard steps. Remove Dashboard, FrameConfiguration, KeyBindings, RawValues.
- **Alternative:** Register no-op/null services for LevelTracker, FrameConfigurator, etc. in config mode (more risky)
- **Validation:** After fix, verify `curl http://localhost:5000/` returns HTTP 200 in config mode (no WoW running)

**Task 1.2: Add Width Tolerance to FrameConfig**
- **File:** `Core/DataFrame/FrameConfig.cs`
- **Action:** Change `IsValid()` method to allow width tolerance (±20px recommended based on 16px observed delta)
- **Code change:**
  ```csharp
  private const int WidthTolerancePixels = 20;
  private const int HeightTolerancePixels = 100;
  
  bool similarWidth = Math.Abs(config.Rect.Width - rect.Width) <= WidthTolerancePixels;
  bool similarHeight = Math.Abs(config.Rect.Height - rect.Height) <= HeightTolerancePixels;
  bool sameRect = similarWidth && similarHeight;
  ```
- **Validation:** Check that `3856` vs `3840` (Δ=16) would now pass validation

### PHASE 2: Fix Code Quality Issues (P1)

**Task 2.1: Extract Duplicated Port Cleanup**
- **Files:** `Core/Startup/NavigationServerManager.cs`, `Core/Startup/StartupOrchestrator.cs`
- **Action:** Create new `Core/Startup/PortCleanupUtility.cs` with static methods
- **Method signatures:**
  ```csharp
  public static void TryTerminateProcessHoldingPort(int port, string expectedProcessName, ILogger logger)
  public static HashSet<int> GetListeningProcessIds(int port)
  ```
- **Update call sites:** Both classes should call `PortCleanupUtility.TryTerminateProcessHoldingPort(...)` passing their logger

**Task 2.2: Clean FleeGoal Duplicate Parameter (OPTIONAL)**
- **File:** `Core/Goals/FleeGoal.cs`
- **Action:** Remove the duplicate `ClassConfiguration classConfiguration` parameter, use only `classConfig`, update line 51 to `Keys = classConfig.Flee.Sequence;`
- **Priority:** LOW — skip if time-constrained

### PHASE 3: Build, Test, Commit

**Task 3.1: Build Verification**
```bash
dotnet build MasterOfPuppets.sln
# Expected: 0 errors, 0 warnings
```

**Task 3.2: Unit Test Verification**
```bash
dotnet test CoreUnitTests
dotnet test FrontendUnitTests
# Expected: All tests pass (161+7)
```

**Task 3.3: Runtime Smoke Test (Config Mode)**
```bash
# Start BlazorServer without WoW running
dotnet run --project BlazorServer

# In another terminal, test all nav routes return 200:
curl http://localhost:5000/
curl http://localhost:5000/Settings
curl http://localhost:5000/combat-rotation
curl http://localhost:5000/launch
# All should return HTTP 200 (no 500 errors)
```

**Task 3.4: Git Commit**
```bash
git add -A
git commit -m "fix: resolve config-mode nav crashes and FrameConfig width tolerance

- Fix GetConfigNavItems to only expose safe routes in config mode
- Add width tolerance (±20px) to FrameConfig.IsValid() for DPI/DWM border variance
- Extract duplicated port cleanup methods to PortCleanupUtility
- Preserve all correct Phase 1/2 implementations (target clearing, nav resilience)

Fixes critical regressions identified in POST_IMPLEMENTATION_REVIEW.md"
```

### PHASE 4: Live WoW Validation (REQUIRED)

**Prerequisites:**
- Changes must be committed and built (binary in `BlazorServer/bin/Debug/net10.0/`)
- WoW Classic must be running at **1920x1008 windowed** (per original plan)

**Test Cases:**
1. **FrameConfig persistence:** Launch bot with WoW running → check logs for "FrameConfig mismatch" messages → should NOT delete config if width delta <20px
2. **Target clearing:** Target a mob → press configured clear target binding (Alt+Insert) → verify logs show `PressRandom ClearTarget` not `PressF11`
3. **Blacklist clearing:** Blacklist a target → verify `ForceAggressiveClearTarget` clears immediately (check logs for escalation stages)
4. **Navigation server uptime:** Monitor logs for >5 minutes → verify no "AmeisenNavigationServer crashed" or ACCESS_VIOLATION errors
5. **Route following:** Set a path via PathingAPI → verify bot follows without navigation server restarts

**Evidence Collection:**
- Save log file with timestamps
- Record any errors/crashes
- Document test outcomes in `docs/VALIDATION_REPORT_2026-02-06.md`

## Instructions for Execution

1. **Read all 4 context documents** (AGENTS.md, Plan, Analysis, Review) before writing any code
2. **Verify git status** — confirm all 15 files are modified, uncommitted
3. **Fix the 2 critical bugs** (Task 1.1, 1.2) using `multi_replace_string_in_file` for efficiency
4. **Extract port cleanup duplication** (Task 2.1) — create new file + update 2 call sites
5. **Run build + tests** to confirm no regressions
6. **Test config-mode nav** routes via curl or browser to verify HTTP 200 responses
7. **Commit with descriptive message** referencing the POST_IMPLEMENTATION_REVIEW.md findings
8. **Rebuild for live testing** (`dotnet build`)
9. **Test with live WoW client** per Phase 4 test cases
10. **Document results** — create validation report with evidence (logs, screenshots)

## Critical Constraints

- **DO NOT revert correct code:** The target clearing reorder, ExecGameCommand DI injection, navigation server resilience, and diagnostic logging are all correct — preserve them.
- **Follow AGENTS.md conventions:** PascalCase, explicit types, file-scoped namespaces, structured logging with Serilog
- **Include 3-5 lines context** in all `replace_string_in_file` calls for unambiguous matching
- **Use multi_replace_string_in_file** when making independent edits to multiple files
- **DO NOT create summary markdown files** unless specifically needed for deliverables (validation report is required)
- **Verify DI registration** if modifying service usage — check `Core/DependencyInjection.cs` for registration patterns

## Success Criteria

Your work is complete when:
1. ✅ Config-mode nav routes all return HTTP 200 (no crashes)
2. ✅ FrameConfig width tolerance added (±20px)
3. ✅ Port cleanup methods extracted to shared utility
4. ✅ Build passes with 0 errors, 0 warnings
5. ✅ All unit tests pass (161+7)
6. ✅ Changes committed to git with descriptive message
7. ✅ Live WoW testing completed with documented evidence
8. ✅ No regressions introduced to correct Phase 1/2 code

## Failure Modes to Avoid

- **DO NOT guess** at uncommitted file contents — always read the working tree files, not HEAD
- **DO NOT skip live testing** — the entire point of this exercise is production validation
- **DO NOT introduce new crashes** — test each change incrementally
- **DO NOT batch commit completions** — commit once after all fixes are tested
- **DO NOT trust the plan** — trust the POST_IMPLEMENTATION_REVIEW.md evidence-based findings

## Reference Information

**Project:** WowClassicGrindBot (GOAP-based WoW Classic bot)  
**Framework:** .NET 10 (C# 14), Blazor Server, SignalR  
**Build:** `dotnet build MasterOfPuppets.sln`  
**Run:** `dotnet run --project BlazorServer`  
**Tests:** `dotnet test CoreUnitTests && dotnet test FrontendUnitTests`  
**Workspace:** `c:\WowClassicGrindBot`  
**Branch:** `dev`  

**Key Files Modified (Working Tree):**
- `Core/DependencyInjection.cs` — Diagnostic logging added
- `Core/DataFrame/FrameConfig.cs` — Height tolerance added (NEEDS WIDTH TOO)
- `Core/Goals/*.cs` — 8 files with ExecGameCommand injection (CORRECT)
- `Core/Input/ConfigurableInput.cs` — Target clearing reorder (CORRECT)
- `Core/PPather/RemotePathingAPIV3.cs` — Connection delay (CORRECT)
- `Core/Startup/NavigationServerManager.cs` — Port cleanup, auto-restart (CORRECT but DUPLICATED)
- `Core/Startup/StartupOrchestrator.cs` — Port cleanup (DUPLICATED)
- `Frontend/Shared/MainLayout.razor` — Nav expansion (CAUSES CRASHES)

---

**When you are ready, respond with:**
1. Confirmation you've read all 4 context documents
2. Summary of the 2 critical fixes you will implement
3. Your plan for validation (build → test → commit → live WoW testing)

Then proceed systematically through PHASE 1 → PHASE 2 → PHASE 3 → PHASE 4 until all success criteria are met.
