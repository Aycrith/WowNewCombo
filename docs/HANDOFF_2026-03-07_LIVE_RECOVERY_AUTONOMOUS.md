# Live Recovery Autonomous Handoff - 2026-03-07

## 2026-03-09 Continuation Addendum
- Published branch baseline is still `31c8735c7`, but the active local workspace is now intentionally dirty with in-flight startup, loot, and supervisor fixes.
- Parallel feature track added: runtime dashboard route-goal control must continue in parallel with autonomous live validation. The front-end dashboard must allow stop, route clear, route selection, safe route apply, and optional resume through shared backend services/APIs.
- The real blocker chain is now:
  - startup/profile-load stability
  - action-bar/profile correctness
  - combat kill-to-loot handoff plus loot follow-through
  - `ValidateCombat`
  - `ValidateReroute`
  - `ValidateNoProgress`
  - dry-run and integrated soaks
- New first-class startup blockers confirmed by live evidence:
  - `ProfileLoadDisposedTimer`
  - `ActionBarShadowBoltSlot2Empty`
  - `WrongProfileLoaded`
  - `DoctorReadinessTimeout`
- Combat and loot should now be treated as one closure family until `ValidateCombat` is green. The bot is no longer allowed to declare combat “separate” from loot follow-through when evaluating live progress.
- The supervisor must use current live/session artifacts and the latest failure JSON files as the source of truth for blocker selection. The older `autonomy-smoke9` dry-run optimism is no longer sufficient.

## 2026-03-08 Baseline Addendum
- Published branch baseline: `31c8735c7` (`chore(profile): refresh blood elf warlock live profile`)
- Working tree at publish: clean
- Current blocker chain remains:
  - startup/readiness stability
  - `ValidateCombat`
  - `ValidateReroute`
  - `ValidateNoProgress`
  - integrated soaks
- The autonomous control loop now has incident persistence, correlated screenshots, guarded live demotion, retry budgeting, and API visibility.
- Autonomous repo mutation is still intentionally disabled. The loop is operating as a bounded supervisor, not a self-editing code executor.

## Mission
- Restore confidence that the `dev` baseline can support stable live validation on the local WoW client.
- Preserve already-closed work.
- Continue in strict dependency order: startup/readiness, service NPCs, controlled combat, reroute, no-progress, then soaks.
- Operate as an autonomous development orchestrator: inspect state, run targeted validation, collect evidence, classify the next blocker, implement the smallest fix, revalidate only the failed gate, repeat.

## Current State
- Branch: `dev`
- HEAD: `31c8735c7` (`chore(profile): refresh blood elf warlock live profile`)
- Local working tree: active and dirty with startup/profile-load, loot interaction, supervisor, and handoff updates
- Current live stack status as of the last status probe:
  - WoW client: running in the most recent local repros, but services were stopped before handoff
  - `BlazorServer`: stopped
  - `AmeisenNavigationServer`: stopped
  - port `5000`: closed
  - port `47110`: closed
- Runtime baseline:
  - profile: `BloodElf_Warlock_1-70_TBC.json`
  - nav profile: `stable-live`
  - reroute-only overlay: `triage-hazard`
- Control surfaces:
  - live orchestration: `Scripts/Agent-BotControl.ps1`
  - autonomous orchestration/reporting: `Scripts/Autonomous-BotSupervisor.ps1`
  - key evidence endpoint family: `/api/health`, `/api/launch/status`, `/api/bot/status`, `/api/session`, `/api/session/stats`, `/api/test/*`, `/api/diagnostics/navigation/*`

## What Was Landed This Session
### 1. Service NPC recovery is materially fixed
- Curated area service selection was restored as the primary source of service NPCs.
- Map-wide `NpcFlags` search was demoted to fallback.
- Service candidate identity and rejection now use service kind + entry + name, not just name.
- Vendor/service interaction now requires an approach/re-interact verification cycle before rejection.
- Main files:
  - `Core/Goals/AdhocNPCGoal.cs`
  - `Core/Database/AreaDB.cs`
  - `Core/Database/VendorLocations.cs`
  - `Core/Database/NpcServiceCandidate.cs`
  - `CoreUnitTests/GoalsComponent/AdhocNpcServiceRecoveryTests.cs`

### 2. Supervisor sequencing was corrected
- Default autonomous gate order is now combat-first:
  - `ValidateCombat`
  - `ValidateReroute`
  - `ValidateNoProgress`
  - `LiveSession`
- `Scripts/Autonomous-BotSupervisor.ps1` now aligns with the real blocker chain from live evidence.

### 2b. Autonomy evidence and failover scaffolding were expanded
- Failure analytics now persists bounded incident records alongside runtime failure events.
- Critical failures can request correlated screenshots with correlation IDs and incident IDs.
- The supervisor now persists run state, retry budgets, promotion state, kill-switch state, and incident queues.
- Additive `/api/autonomy/*` endpoints expose supervisor state, incidents, runs, control actions, and incident artifacts.
- Correlation IDs were added to the key runtime/status API surfaces used by the harness.

### 3. Combat evidence collection was corrected
- `ValidateCombat` no longer depends only on short artifact logs and server tails.
- Session manifest now records the active runtime `out*.log` snapshot at gate start.
- Combat parsing reads only the bytes written after the gate started, which fixed the previous evidence blind spot.
- Regexes for `Drain Life`, summon events, and healthstone create/use were tightened to count actual execution signals instead of generic requirement lines.
- Main file:
  - `Scripts/Agent-BotControl.ps1`

### 4. Combat reacquire behavior was improved, but not closed
- Immediate pet-handoff recovery was added before generic last-target recovery when recent combat progress exists.
- Nearest-target reacquire now waits for target acquisition instead of assuming a single press is enough.
- Main files:
  - `Core/Goals/CombatGoal.cs`
  - `CoreUnitTests/GoalsComponent/CombatPullCastingRuntimeTests.cs`

## Architectural Decisions and Why
- Hybrid/live readiness policy: launch-check authority remains the source of truth, not raw port state alone.
  - Reason: raw nav port state produced false blockers in Hybrid mode.
- Validation-first, stop-on-blocker:
  - Reason: live evidence was repeatedly contaminated when downstream gates ran before upstream blockers were closed.
- Combat before reroute:
  - Reason: reroute evidence is meaningless when combat still contaminates route-follow windows.
- Curated-first service NPC selection:
  - Reason: the newer map-wide creature-flag search regressed previously stable vendor/service behavior.
- Session-scoped runtime log capture:
  - Reason: combat summary undercounted real warlock actions because the gate was not reading the active runtime log stream.
- Bounded autonomy only:
  - Reason: guarded orchestration, evidence correlation, and failover support the live campaign directly; autonomous repo mutation does not yet have a safe baseline and stays disabled.
- No autonomous repo mutation yet:
  - Reason: the system is not stable enough to auto-apply changes safely without explicit gate closure first.

## Testing History and Results
### Local validation
- Latest local gate on `31c8735c7`:
  - `dotnet build MasterOfPuppets.sln -c Release --nologo -v quiet` -> pass
  - `dotnet test CoreUnitTests/CoreUnitTests.csproj -c Release --filter "FullyQualifiedName~CombatPullCastingRuntimeTests|FullyQualifiedName~FailureAnalytics"` -> pass (`77` passed)
  - `dotnet test FrontendUnitTests/FrontendUnitTests.csproj -c Release --nologo -v quiet` -> pass (`73` passed)

### Service NPC validation
- Fresh successful service-NPC evidence:
  - bootstrap: `logs/agentctl-20260307-151216-validation.json`
  - service success details in: `logs/agentctl-20260307-151216-blazor-stdout.log`
- Important successful behavior:
  - area-curated vendor selected
  - keyboard-only acquisition succeeded
  - interaction recovered from initial gossip miss
  - merchant window opened
  - sell path completed

### Supervisor validation
- A validation-only supervisor cycle ran under:
  - `logs/autonomous-supervisor/dev-live-main/cycles/cycle-20260307-161226/`
- Result:
  - synthetic baseline passed
  - first bootstrap failed with a launch navigation blocker
  - latest outputs were written correctly:
    - `status-latest.json`
    - `next-steps-latest.json`
    - `next-steps-latest.md`
    - `open-issues.json`

### Bootstrap validation
- First resumed supervisor bootstrap failed:
  - artifact: `logs/autonomous-supervisor/dev-live-main/cycles/cycle-20260307-161226/cycle-summary.json`
  - blocker message: launch navigation check blocking, `RemoteV3 not connected; using local fallback (MPQ OK)`
- One direct clean retry then passed:
  - artifact: `logs/agentctl-20260307-161822-validation.json`
- Conclusion:
  - startup/readiness is still operationally fragile, but the specific blocker did not reproduce on the one allowed retry.

### Combat validation
- Earlier post-service-recovery combat run:
  - `logs/dev-npc-service-combat-20260307-1/agentctl-20260307-151502-validatecombat-summary.json`
  - result:
    - `KillsDelta=30`
    - `RangedStandoffMaintained=true`
    - `RangedFillerDominant=true`
    - remaining blocker: lost-target/reacquire
- Latest combat run from the frozen candidate:
  - `logs/dev-live-main-combat-retry-20260307/agentctl-20260307-161945-validatecombat-summary.json`
  - `logs/dev-live-main-combat-retry-20260307/agentctl-20260307-161945-combat-validation.json`
  - `logs/dev-live-main-combat-retry-20260307/20260307-163447-combat-validation-end-server-out-tail.txt`
- Latest result:
  - `KillsDelta=27`
  - `SpellCoverageComplete=true`
  - `CurseOfAgony` opener evidence strong
  - `Shoot=243`, `ShadowBolt=0`
  - `BodyPullFallbackCount=0`
  - `RangedStandoffMaintained=true`
  - `RangedFillerDominant=true`
  - `BadAttackFacingCount=0`
  - `LostTargetBurstCountWindow=0`
  - `LostTargetReacquireAttemptCountWindow=28`
  - `LostTargetReacquireSuccessCountWindow=0`
  - `LostTargetReacquireSuccessRatio=0.0`
  - `DrainLifeCastCount=6`
- Important conclusion:
  - the telemetry fix worked
  - the dominant blocker is now kill-to-loot / end-of-combat target handoff accounting, not body-pull drift, not facing spam, and not the previous combat evidence blind spot

## Known Bugs, Blockers, and Leads
### Active blocker: startup/profile-load stability
- Symptoms:
  - `POST /api/bot/profile/load` can fail with `Cannot access a disposed object. Object name: 'System.Timers.Timer'.`
  - startup can also fail clean readiness because `Shadow Bolt` is missing from slot `2`
  - live repros have also shown rogue/profile drift instead of the requested warlock profile
- Evidence:
  - `logs/live-session-20260308-125446/agentctl-20260308-125446-start-failure.json`
  - `logs/live-session-20260308-040302/agentctl-20260308-040302-startandvalidate-failure.json`
  - `BlazorServer/bin/Release/net10.0/out20260308.log`
- Current hypothesis:
  - a disposed Blazor route timer subscriber can still fault profile-load startup, while action-bar/profile correctness is a separate readiness family that must be classified explicitly instead of buried under generic launch failure.
- Best lead:
  - harden `RouteComponent`/profile-load event handling, expose requested/applied profile + profile-load failure kind in `/api/launch/status`, and teach the supervisor to rank startup blockers ahead of combat.

### Active blocker: combat plus loot follow-through
- Symptoms:
  - `CombatGoal` still emits kill-transition lost-target noise in live evidence.
  - loot interaction is improved but still not consistently closing every stacked or cursor-driven corpse opportunity.
- Evidence:
  - `BlazorServer/bin/Release/net10.0/out20260308.log`
  - recent combat validation summaries under `logs/live-session-*`
- Current hypothesis:
  - combat and loot are still coupled at the end-of-fight handoff, so evaluating them independently is masking the actual blocker family.
- Best lead:
  - keep fixes local to `CombatGoal` and `LootGoal`, use short 5-10 kill guarded live windows, and stop after two same-reason failures.

### Active blocker: combat target-loss accounting around kill transitions
- Symptom:
  - `CombatGoal` still records lost-target/reacquire attempts during kill credit, dead-target clear, and loot handoff windows.
- Evidence:
  - `logs/dev-live-main-combat-retry-20260307/agentctl-20260307-161945-validatecombat-summary.json`
  - `logs/dev-live-main-combat-retry-20260307/20260307-163447-combat-validation-end-server-out-tail.txt`
- Current hypothesis:
  - the runtime still classifies expected end-of-fight target drops as reacquire failures before the system fully transitions into corpse consumption / loot state.
- Best lead:
  - inspect `CombatGoal.Update()`, `HandleDeadTargetLoss()`, `TryRecoverTargetAfterLoss()`, and the timing interplay with `CombatTracker` leaving combat and `LootGoal` reacquiring corpse/last-target.

### Operational fragility: intermittent bootstrap readiness mismatch
- Symptom:
  - one supervisor bootstrap failed even though the process stack and bot became active.
- Evidence:
  - `logs/autonomous-supervisor/dev-live-main/cycles/cycle-20260307-161226/cycle-summary.json`
- Current hypothesis:
  - launch navigation readiness can still oscillate between Hybrid-ready and a local-fallback non-OK state during early startup.
- Best lead:
  - inspect the exact readiness window in `StartAndValidate` and how `/api/launch/status` is sampled during startup; do not change this unless the mismatch reproduces twice with the same symptom.

### Preserved but not active blockers
- Session API cached/live downgrade behavior: previously fixed and not disproven this session.
- Service NPC acquisition: freshly improved and no longer the active blocker.
- Reroute/no-progress/soaks: still downstream; do not reopen until combat closes.

## Files and Paths the Next Agent Must Know
- Changed baseline files:
  - `Core/Goals/AdhocNPCGoal.cs`
  - `Core/Database/AreaDB.cs`
  - `Core/Database/NpcServiceCandidate.cs`
  - `Core/Database/VendorLocations.cs`
  - `Core/Goals/CombatGoal.cs`
  - `Scripts/Agent-BotControl.ps1`
  - `Scripts/Autonomous-BotSupervisor.ps1`
  - `CoreUnitTests/GoalsComponent/AdhocNpcServiceRecoveryTests.cs`
  - `CoreUnitTests/GoalsComponent/CombatPullCastingRuntimeTests.cs`
- Current supervisor latest outputs:
  - `logs/autonomous-supervisor/dev-live-main/status-latest.json`
  - `logs/autonomous-supervisor/dev-live-main/next-steps-latest.json`
  - `logs/autonomous-supervisor/dev-live-main/next-steps-latest.md`
  - `logs/autonomous-supervisor/dev-live-main/open-issues.json`
- Important historical evidence:
  - `logs/agentctl-20260307-151216-validation.json`
  - `logs/agentctl-20260307-151216-blazor-stdout.log`
  - `logs/agentctl-20260307-161822-validation.json`
  - `logs/dev-live-main-combat-retry-20260307/agentctl-20260307-161945-validatecombat-summary.json`
  - `logs/dev-live-main-combat-retry-20260307/20260307-163447-combat-validation-end-server-out-tail.txt`
  - `logs/autonomous-supervisor/dev-live-main/cycles/cycle-20260307-161226/cycle-summary.json`
- Runtime/config paths:
  - `BlazorServer/bin/Release/net10.0/runtime_feature_flags.json`
  - `BlazorServer/bin/Release/net10.0/out20260307.log`

## Environment and Dependencies
- OS: Windows
- Shell: PowerShell / pwsh
- SDK: .NET 10
- Live target: local WoW Anniversary client
- Standard ports:
  - web/API: `5000`
  - navigation: `47110`
- Manual preflight still required before live runs:
  - client fully in-world
  - alive
  - nonzero coordinates
  - route-eligible zone
  - chat closed
  - action bar initialized
  - keybindings readable

## Recommended Immediate Next Steps
1. Start from the current clean stopped state and bring up one clean bootstrap only after manual client normalization.
2. Remediate combat target-loss accounting around kill-to-loot transitions.
3. Add or extend targeted unit coverage around that handoff logic before spending more live time.
4. Re-run only `ValidateCombat`.
5. If combat closes, resume:
   - `ValidateReroute`
   - `ValidateNoProgress`
   - soak 1
   - soak 2
   - soak 3
6. If combat fails again for the same target-handoff reason, keep the focus narrow; do not reopen vendor/service, startup, or ranged-standoff work unless new evidence disproves them.

## Operating Contract for the Next Agent
- Treat all bot services as offline until proven otherwise.
- Use `Agent-BotControl.ps1` as the control plane for bootstrap, stop, evidence collection, and live gate execution.
- Use `Autonomous-BotSupervisor.ps1` for cycle artifacts and next-step synthesis, but do not rely on it as the sole source of truth when a direct post-fix gate rerun is faster and cleaner.
- Keep autonomous repo mutation disabled until bootstrap, service NPC flow, and controlled combat are all freshly green.
- After every remediation:
  - run local validation first
  - rerun only the exact failed gate
  - stop after two failures for the same gate/reason

## One-Line Situation Summary
- The project is on a clean published `dev` baseline at `31c8735c7`; service NPC interaction is fixed, combat telemetry is fixed, the autonomy layer is bounded and observable, startup can still wobble once, and the real next blocker is combat reacquire accounting during kill-to-loot transitions.
