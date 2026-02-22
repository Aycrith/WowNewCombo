# Autonomous Operations Handoff (Next Agent)

## Operator Kickoff Prompt (Copy/Paste)
Use this at the start of each iteration:

```text
All bot processes are currently offline and dormant. You are responsible for bringing each process back online in a controlled, sequenced, and methodical manner according to your established operational hierarchy. Do not assume any process is running. Verify system state before proceeding with any reactivation. Bring systems online in the correct dependency order, confirming stability at each stage before advancing.

Once systems are restored, resume your continuous improvement operations which include but are not limited to: auditing and updating bot behavioral profiles, rebalancing mob level distributions to appropriate ranges, refining decision logic and response patterns, optimizing resource allocation across active bot processes, identifying and resolving performance bottlenecks, and stress testing all changes in a controlled manner before full deployment.

Maintain detailed internal logging of every action taken, every system state observed, every configuration change made, and every test result recorded. Apply iterative improvements based on live feedback and observed outcomes. Treat this as an ongoing autonomous operation with no defined endpoint — your directive is perpetual optimization, stability, and intelligent self-correction across all managed bot systems.
```

**Date:** 2026-02-21  
**Branch:** `dev`  
**Primary Focus:** Navigation intelligence, stuck recovery quality, hazard-driven rerouting quality, runtime stability.

## Operator Contract (Must Be Enforced Every Iteration)
The next agent is expected to execute each cycle under this contract:

1. Treat all bot services as offline until proven otherwise.
2. Verify live state before any activation (processes, ports, API health, launch readiness).
3. Bring systems online in dependency order and verify stability at each step.
4. Resume continuous improvement operations after stable startup.
5. Record detailed artifacts for every action/state/change/test.
6. Repeat perpetually with no terminal endpoint unless explicitly interrupted by user.

## Per-Iteration Acceptance Criteria
An iteration is only complete if all checks below pass and are logged:

1. `State Verification`
- Confirm process/port/API status with evidence.
- No assumptions about running services.

2. `Ordered Startup`
- Use controlled sequence (recommended control plane: `Scripts/Agent-BotControl.ps1`):
  - stale process cleanup
  - navigation server up (`47110` listening)
  - `BlazorServer` up (`/api/health`, startup stage `Ready`)
  - launch overrides/profile/fixes
  - launch readiness gates green
  - bot start + bot active confirmation

3. `Stability Gate`
- `/api/health` healthy
- `/api/launch/status` consistent with intended run mode
- `/api/bot/status` matches expected active/idle state
- no immediate crash/restart loop

4. `Navigation Improvement Gate`
- Validate that stuck handling produces useful route changes (not only trigger events).
- Validate reroute integration with current route tail (recalc + merge, not blind node append).
- Track and report:
  - front-bypass activations
  - successful reconnects
  - repeat-stuck-at-same-position rate (before/after windows)

5. `Artifact Gate`
- Persist machine-readable evidence (JSON + logs) for each iteration.
- Include command transcript references, test results, soak metrics, and unresolved blockers.

## What Was Implemented (Most Recent)
### Navigation behavior upgrades
1. Dynamic stuck reroute was upgraded from pure node stitching to route integration:
- recalculate tail route from reconnect point to active destination using pathfinder
- optionally re-apply hazard detour on recalculated tail
- merge local detour + recalculated tail with duplicate suppression
- fallback to stitched route only if tail recalculation is unavailable

2. Added/updated helpers in `Core/GoalsComponent/Navigation.cs`:
- `BuildIntegratedDynamicRoute(...)`
- `TryRecalculateTailRoute(...)`
- `MergeRouteSegments(...)`
- dynamic route application now uses integration path for both hazard detours and front-obstacle bypass

### Tests added/updated
- `CoreUnitTests/GoalsComponent/NavigationDynamicBypassTests.cs`
  - merge segment behavior
  - duplicate reconnect suppression
  - front-bypass geometry and side alternation
- Existing route rehabilitator quality tests remain in:
  - `CoreUnitTests/Hazard/RouteRehabilitatorTests.cs`

## Latest Evidence Snapshot
### Soak artifact
- `logs/soak-nav-20260219-143150.json`

### Reported metrics from that soak
- First 10m window:
  - `FrontBypassActivations=2`
  - `SuccessfulReconnects=29`
  - `StuckEvents=6`
  - `RepeatStuckRate=0.3333`
- Second 10m window:
  - `FrontBypassActivations=3`
  - `SuccessfulReconnects=19`
  - `StuckEvents=6`
  - `RepeatStuckRate=0.0000`
- Total 20m:
  - `FrontBypassActivations=5`
  - `SuccessfulReconnects=48`
  - `StuckEvents=12`
  - `RepeatStuckRate=0.1667`

### Latest runtime status observed
- Services up (`BlazorServer`, nav server, WoW process), but readiness degraded after restart:
  - `CanStartBot=False`
  - blocking checks observed: key bindings timeout and action bar textures not initialized
  - bot inactive at that snapshot

## Immediate Runbook for Next Agent
1. `Baseline`
- `pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Agent-BotControl.ps1 -Action Status`

2. `Controlled restart + validate`
- `pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Agent-BotControl.ps1 -Action StartAndValidate`

3. `If launch blockers persist`
- Re-run startup fixes path via control plane and re-check launch status.
- Validate addon handshake/keybinding/actionbar checks specifically.
- Do not start soak until readiness gates are stable.

4. `Soak + metrics`
- Run 20m soak in two 10m windows.
- Report required three metrics and delta trend.
- Save JSON evidence under `logs/`.

5. `Iterate`
- Tune thresholds/logic only after evidence review.
- Re-test targeted unit tests + runtime soak.

## Required Test Commands (Minimum)
- `dotnet test CoreUnitTests/CoreUnitTests.csproj -c Release --filter "FullyQualifiedName~NavigationDynamicBypassTests|FullyQualifiedName~RouteRehabilitatorTests"`
- `dotnet build MasterOfPuppets.sln -c Release -nr:false`

## Handoff Notes
- Treat navigation quality as top priority.
- Any reroute that only appends waypoints without recalculating usable tail path is not acceptable.
- Keep dashboard-visible nav/hazard telemetry active so user can provide visual feedback.
