# Nav/Perf Backlog Closure Note (2026-03-05)

## Scope
- Closed remaining nav/perf backlog items from `docs/plans/nav-perf-next-steps.md`.
- Final closure items delivered in this pass:
  - `2.4` oscillation confidence throttle
  - `3.1` route segment tracker (diagnostic-only)
  - `3.2a` usable-goal cache
  - `3.2c` GOAP plan cache
  - `3.4` adaptive heading cooldown by speed
- No HTTP route or public API behavior changes intended.

## Backlog Status
- Before this pass: `5` remaining items (`2.4`, `3.1`, `3.2a`, `3.2c`, `3.4`).
- After this pass: `0` remaining items.

## Feature Flags Introduced (Default OFF)
- `Features.NavigationExperiments`
  - `EnableOscillationConfidenceThrottle = false`
  - `EnableAdaptiveHeadingCooldown = false`
  - `EnableRouteSegmentTracker = false`
- `Features.GoapPlannerCaching`
  - `EnableUsableGoalCache = false`
  - `EnablePlanCache = false`

## Verification Commands
- `dotnet build-server shutdown`
- `dotnet build MasterOfPuppets.sln --nologo -v quiet`
- `dotnet test --nologo -v quiet`

## Verification Results
- Build: success, `0` warnings, `0` errors.
- Tests:
  - `FrontendUnitTests`: `58/58` passed
  - `CoreUnitTests`: `1846/1849` passed (`3` skipped)

## Notes
- Route-segment regression tracking is diagnostic-only in this phase (logging + runtime snapshot telemetry only).
- Planner caching is opt-in per call via `GoapPlannerExecutionOptions`, with `GoapAgent` wiring controlled by runtime feature flags.
