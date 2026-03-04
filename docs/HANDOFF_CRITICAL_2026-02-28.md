# Critical Handoff: Navigation/Autonomy Regression Recovery

Date: 2026-02-28  
Branch: `live-soak-20260224-nav-stability`  
Status: **Testing campaign failed to produce reliable live recovery**  

## 1) Total Situation Overview

The current live-testing cycle is blocked. The WoW client was closed after repeated regressions (spinning, path churn, deaths, non-recovery behavior). Services are currently offline.

Current runtime state:
- `BlazorServer`: stopped
- `NavServer`: stopped
- `WoW`: stopped
- `http://localhost:5000`: unavailable

Current repo state:
- Large dirty worktree with mixed-scope changes (startup, diagnostics API, navigation core, GOAP, scripts, profile JSON).
- 23 modified files + 4 new files not committed.
- Recent commit tip before dirty work is `6d6ef2fd3` (`fix(startup): checkpoint frame-config fallback and stage7 recovery timing fixes`).

Build/test baseline at handoff time:
- `dotnet build MasterOfPuppets.sln --nologo`: pass
- `dotnet test CoreUnitTests --no-build --verbosity minimal`: pass (`1713 passed, 3 skipped`)
- `dotnet test FrontendUnitTests --no-build --verbosity minimal`: pass (`38 passed`)

Important: green unit tests did **not** correlate to stable live behavior.

## 2) All Problems Encountered (Exhaustive)

### Startup/Readiness/Orchestration failures
- DTC freeze/hang episodes (startup frame handshake issues, `GlobalTime=0`, frame data zeros) required reload-oriented recovery logic.
- `StartAndValidate` had intermittent non-zero/silent exits in some runs (minimal/no console diagnostics).
- Readiness frequently blocked on keybindings/actionbar despite command dispatch, indicating dispatch != applied state.
- Chat-input and frame state edge cases still introduced manual dependency in practical runs.
- WoW process/window readiness detection was inconsistent across restarts/reloads.

### Automation autonomy failures
- Fire-and-forget readiness fix flow previously treated command send as success.
- Manual interventions were repeatedly needed (`/reload`, `/dcbindings`, action bar correction), violating autonomy objective.
- Invalid live runs were sometimes counted as progress despite no route-follow evidence.

### GOAP/goal execution failures
- Repeated `Adhoc` replanning events observed for long spans, reducing reliable transition into sustained `FollowRoute`.
- Evidence in `BlazorServer/bin/Release/net10.0/out20260227.log` shows heavy `New Plan=  Adhoc` churn.
- Bot could stay effectively non-productive or bounce between transient goals.

### Navigation/path-following regressions
- User-visible behavior: step-forward/turn-back loops, circling, unstable heading correction, poor forward progress.
- False stuck churn observed historically and in logs (`TimeoutNoProgress` trigger chains).
- Route clearing and refill churn observed (`Clear route to waypoint!` and segment refill loop on segment 149).
- Evidence:
  - `out20260227.log`: repeated `Refill loop detected on segment 149 (x2) - advancing route anchor`
  - `out20260224.log`: repeated `TRIGGERING UNSTUCK` and `Clear route to waypoint! Stucked for ...`
  - `out20260228.log`: repeated `PlayerDirection` retries and kill-switch event.
- Potential content issue reported live: waypoint appears inside terrain/zonewall/hill (path data defect possibility).

### Combat/runtime behavior regressions (live-observed)
- Pull behavior and combat flow reported degraded (`throw` pull not behaving correctly).
- Recovery/sustain behavior degraded (not eating to full HP as expected).
- Character could chain-pull into deaths during navigation churn.

### Corpse/death loop failures
- Bot state often ended in dead/corpse contexts with no effective recovery progression.
- Example invalid window: watch run dominated by `Walk To Corpse`, no route-follow samples.

### Kill-switch false stop interference
- `out20260228.log` includes `Soft stop hotkey detected` immediately followed by bot stop.
- This caused hidden run invalidation (bot inactive while diagnostics still looked partially healthy).

### Evidence/measurement quality failures
- Some windows were corpse-only or non-route windows but still produced aggregate values.
- “Pass” validation JSON could coexist with poor real movement quality.
- Route-specific quality gates were not enforced early enough in prior loops.

## 3) All Areas Requiring Improvement (Priority)

### P0 (must fix before any new live claims)
- Enforce **route-follow validity gate**: no navigation claim unless `RouteFollowSampleCount > 0`.
- Stabilize GOAP transition correctness so `Adhoc` does not lock/oscillate.
- Eliminate false stuck-trigger escalation during intentional turn correction.
- Remove/contain kill-switch false positives during unattended test execution.
- Guarantee autonomous readiness repair is effect-verified, not dispatch-verified.

### P1
- Resolve route content defects (zonewall/hill/inside-geometry waypoints).
- Revalidate combat profile behavior (pull opener, eat/drink recovery logic).
- Harden corpse recovery flow and state transitions.
- Ensure startup scripts emit deterministic failure reasons (no silent non-zero exits).

### P2
- Improve telemetry semantics to separate route/combat/corpse windows by default.
- Reduce script complexity and split into tested modules.
- Reduce dirty-branch scope by isolating concerns into minimal commits.

## 4) Root Cause Analysis of Navigation Failures (No Net Improvement)

Navigation has not delivered net improvement in live outcomes because multiple root causes interacted and were not isolated per run:

High-confidence causes:
- Aggressive/incorrect stuck-path interaction historically caused no-progress false positives and churn.
- Turn correction and stuck recovery were coupled too tightly, producing oscillatory behavior on normal turn-heavy segments.
- GOAP executable-goal behavior likely returned stale/incorrect runnable sets in some states (cache correctness risk), contributing to `Adhoc` churn instead of stable route execution.
- Route refill loop behavior around specific segments (segment 149) produced repeated anchor churn.
- Validation pipeline allowed non-route windows to masquerade as progress.

Medium-confidence causes:
- Route data quality issue on active profile path (hill/zonewall waypoint) exacerbated movement failure.
- `PlayerDirection` retry behavior may still create visible robotic correction under some latency/path geometries.

Observed consequence:
- Iterations changed many layers simultaneously (GOAP/navigation/flags/startup/scripts/profile), making causality unclear and causing quality degradation instead of convergence.

## 5) Directive: Return to Core Repository Known-Working Patterns

This is mandatory for the next agent:

1. **Do not continue on the current dirty state as-is.**
- Create a fresh recovery branch from clean `dev` (or an explicitly chosen known-good commit baseline).
- Treat current branch as forensic reference only.

2. **Re-align navigation logic with original known-working patterns first.**
- Diff current navigation stack against stable baseline and revert non-essential deviations before introducing new tuning.
- Reintroduce changes one at a time with live A/B evidence.

3. **Disable/guard risky optimizations until proven safe.**
- GOAP usable-goal caching must remain disabled or correctly invalidated with full semantic keying.

4. **Use strict evidence gating.**
- No “success” without route-follow samples and explicit route-only metrics.
- Any corpse-only/combat-only window is `INVALID` for navigation claims.

5. **Use minimal-change recovery sequencing.**
- First recover startup/readiness autonomy.
- Then recover GOAP transition correctness.
- Then recover navigation movement stability.
- Then validate combat behavior.
- Then run soak.

## Critical Evidence References

- Branch/log state:
  - `git status --short` (23 modified + 4 untracked/new)
  - `git log --oneline -12` (tip `6d6ef2fd3`)
- Validation JSON showing “pass” despite later behavioral regressions:
  - `logs/agentctl-20260227-234100-validation.json`
- Invalid navigation window example:
  - `logs/live-session-20260228-004105/agentctl-20260228-004105-watchnav-summary.json`
  - `RouteFollowSampleCount=0`, `NavigationValidationValid=false`, dominant goal `Walk To Corpse`
- Adhoc churn + refill loop evidence:
  - `BlazorServer/bin/Release/net10.0/out20260227.log`
- Stuck/route clear churn evidence:
  - `BlazorServer/bin/Release/net10.0/out20260224.log`
  - `BlazorServer/bin/Release/net10.0/out20260226.log`
- Kill-switch interference evidence:
  - `BlazorServer/bin/Release/net10.0/out20260228.log` (`Soft stop hotkey detected`)

## Current Modified/New Files (for forensic diff only)

Modified:
- `BlazorServer/GlobalHotkeyKillSwitchService.cs`
- `BlazorServer/runtime_feature_flags.json`
- `Core/GOAP/GoapPlanner.cs`
- `Core/Goals/AdhocNPCGoal.cs`
- `Core/Goals/CombatGoal.cs`
- `Core/Goals/FollowRouteGoal.cs`
- `Core/Goals/WalkToCorpseGoal.cs`
- `Core/GoalsComponent/MountHandler.cs`
- `Core/GoalsComponent/Navigation.cs`
- `Core/GoalsComponent/StuckDetector.cs`
- `Core/Navigation/NavSoakMetricsService.cs`
- `CoreUnitTests/GOAP/GoapPlannerCacheTests.cs`
- `CoreUnitTests/Goals/GoapGoalTests.cs`
- `CoreUnitTests/GoalsComponent/FollowRouteGoalRefillTests.cs`
- `Frontend/Controllers/DiagnosticsController.cs`
- `Frontend/Controllers/FeatureFlagController.cs`
- `HeadlessServer/runtime_feature_flags.json`
- `Json/class/BloodElf_Rogue_8-60_TBC.json`
- `Scripts/Agent-BotControl.ps1`
- `send-dc-v2.ps1`
- `send-dc.ps1`
- `send-dcflush.ps1`
- `send-reload.ps1`

New:
- `Core/GoalsComponent/NavigationRuntimeSnapshots.cs`
- `FrontendUnitTests/Controllers/DiagnosticsControllerSlashFixTests.cs`
- `FrontendUnitTests/Controllers/FeatureFlagControllerGetAllTests.cs`
- `send-wowcmd.ps1`

## Required Starting Point for Next Agent

- Treat this handoff as blocking guidance.
- Rebase recovery work to clean baseline.
- Restore known-good navigation behavior before adding new optimization/tuning.
- Produce evidence per fix item, with route-only validity checks, before claiming progress.
