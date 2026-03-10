# Autonomous Operations Handoff (Next Agent)

## Current Entry Point
- Canonical live-state handoff: `docs/HANDOFF_2026-03-07_LIVE_RECOVERY_AUTONOMOUS.md`
- Acceptance tracker authority: `docs/WOW_LIVE_VALIDATION_EXECUTION_TRACKER.md`
- Branch baseline: `dev`
- Published baseline commit: `31c8735c7`
- Published working tree status: clean
- Current local workspace status: active and dirty; do not assume a frozen mutation-safe baseline

## Current Active Blocker
- Startup/profile-load stability is the first blocker to close.
- The immediate live failures to classify before any gate rerun are:
  - `ProfileLoadDisposedTimer`
  - `ActionBarShadowBoltSlot2Empty`
  - `WrongProfileLoaded`
  - `DoctorReadinessTimeout`
- Combat plus loot follow-through remains the next blocker family after startup is clean.

## Current Execution Order
1. Startup/profile-load stability
2. Action-bar/profile correctness
3. Combat kill-to-loot handoff plus loot follow-through
4. `ValidateCombat`
5. `ValidateReroute`
6. `ValidateNoProgress`
7. Integrated soak runs and final verdict

## Parallel Feature Track
- Runtime route-goal control from the dashboard is now an active parallel workstream and must continue alongside live validation.
- Required outcome:
  - user can stop the bot from the dashboard goal panel
  - user can clear the current route override
  - user can browse/select a new route file
  - user can apply the route switch safely and optionally resume the bot
- This work must stay wired through shared backend services and API endpoints, not a frontend-only mutation path.

## Control Plane
- Live orchestration: `Scripts/Agent-BotControl.ps1`
- Autonomous supervision and artifacts: `Scripts/Autonomous-BotSupervisor.ps1`
- The autonomy layer is bounded to guarded live admission, incident correlation, screenshot capture, failover sequencing, and next-step synthesis.
- Autonomous repo mutation remains disabled in this baseline.

## Operator Contract
1. Treat all bot services as offline until proven otherwise.
2. Verify process, port, API health, and launch readiness before activation.
3. Bring systems online in dependency order and verify stability at each step.
4. After each remediation, run local validation first and rerun only the failed live gate.
5. Stop after two failures for the same gate and reason.
6. Preserve artifacts for every state change, command, and validation result.

## Immediate Runbook
1. `pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Agent-BotControl.ps1 -Action Status`
2. Normalize the WoW client manually:
   - in-world
   - alive
   - route-eligible
   - chat closed
   - action bar initialized
   - keybinds readable
3. `pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Agent-BotControl.ps1 -Action StartAndValidate`
4. If startup fails, classify the failure from `*-failure.json` and `/api/launch/status` before retrying.
5. Use only one bounded action-bar bypassed start when the sole blocker is the known slot-2 `Shadow Bolt` drift.
6. After startup is clean, run the short combat/loot repro window, then `ValidateCombat`.
7. Only if combat closes, resume reroute, no-progress, and soak validation in that order.

## Notes
- The older navigation-first framing from the February 21 handoff is superseded by the March 7 live-recovery handoff and the tracker.
- The supervisor must prefer the latest live/session artifacts and failure JSON evidence over older dry-run-only optimism.
- Do not expand autonomous self-mutation, CI autopilot, or unrelated subsystem work until the live blocker chain above is green.
