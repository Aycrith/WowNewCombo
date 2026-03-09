# WoW Live Validation Execution Tracker

## Scope
- Commit focus: `3cff1f72e` + prior cloud-session warlock/runtime changes.
- Profile: `BloodElf_Warlock_1-70_TBC.json`.
- Runtime target: `http://localhost:5000`, nav port `47110`.
- Current local blocker addendum as of 2026-03-09:
  - startup/profile-load stability
  - action-bar/profile correctness
  - combat + loot closure
  - `ValidateCombat`
  - `ValidateReroute`
  - `ValidateNoProgress`
  - soaks
- Baseline comparator:
  - `AvgRepeatRate=0.2689`
  - `AvgMaxDev=165.466`
  - `P95MaxDev=297.954`
  - `AvgStuckEventsPerWindow=8.455`

## Runtime Baseline Policy
- Checked-in baseline:
  - `BlazorServer/runtime_feature_flags.json` and `HeadlessServer/runtime_feature_flags.json` now mirror the validated `stable-live` baseline.
  - Baseline values for the current phase: `HazardAvoidance=false`, `StuckSensitivity.MinDistance=0.08`, `UnstuckAfterMs=3200`, `EnablePredictiveDetection=false`, `PredictiveRiskThreshold=80`, `ApproachTimeoutMultiplier=1.5`.
- Script overlays:
  - `stable-live` and `triage-baseline`: no-op aliases to the checked-in baseline.
  - `triage-hazard`: baseline plus `HazardAvoidance=true`.
  - `triage-predictive`: baseline plus predictive stuck detection (`UnstuckAfterMs=2800`, `PredictiveRiskThreshold=75`, `ApproachTimeoutMultiplier=1.4`).
- Evidence contract:
  - Every live run must emit `session-manifest.json` with `RequestedNavProfile`, `AppliedNavProfile`, `ResolvedEffectiveFlags`, `RuntimeMode`, and `AgentAvailable`.
  - Validation actions must emit an action-specific `*-failure.json` artifact when they abort before closing a gate.
  - Evidence capture must include `/api/session`, `/api/session/stats`, `/api/diagnostics/navigation/runtime`, and `/api/diagnostics/navigation/reroute`.

## Current Execution Order
- Current blocker-first execution order on the published `dev` baseline is:
  - startup/profile-load stability
  - action-bar/profile correctness
  - combat kill-to-loot handoff plus loot follow-through
  - `ValidateCombat`
  - `ValidateReroute`
  - `ValidateNoProgress`
  - integrated soaks
- This ordering does not change the milestone thresholds below. The tracker remains the gate authority.

## Milestone Checklist

### Week 1 (Mar 5-11, 2026): Runtime stability foundation
- [ ] M1.1 API/socket/port failure classification complete (incident log updated).
- [ ] M1.2 Script help smoke stable (`-?` on 4 scripts <= 5s, exit 0).
- [ ] M1.3 Harness retry/fallback guards verified.
- [ ] Exit gate: 3 consecutive 30-minute dry runs with no health outage and no web/nav listener drop.

### Week 2 (Mar 12-18, 2026): Navigation reroute proof
- [ ] M2.1 Reroute lifecycle telemetry visible in runtime/soak snapshots.
- [ ] M2.2 Hazard-off and hazard-on paired runs completed.
- [ ] M2.3 Forced route reset/profile swap validates stale-reroute drop behavior.
- [ ] Exit gate: >=10 reroute opportunities, 0 detour-only collapse loops.

### Week 3 (Mar 19-25, 2026): Combat/rotation validation
- [ ] M3.1 Controlled combat sample run captures >=30 encounters.
- [ ] M3.2 Spell evidence includes `Immolate`, `Corruption`, `Shadow Bolt`, `Shoot`.
- [ ] M3.3 Target reacquire and movement-cast failure thresholds pass.
- [ ] Exit gate: all combat thresholds pass.

### Week 4 (Mar 26-Apr 1, 2026): Integrated soak + verdict
- [ ] M4.1 Three 60-minute integrated soak runs completed.
- [ ] M4.2 Baseline deltas computed and reviewed.
- [ ] M4.3 Final verdict matrix published (`Validated`, `Partially Validated`, `Failed`, `Not Validated`).
- [ ] Exit gate: no cluster left `Not Validated` without explicit defer-owner-date.

## Pass Thresholds
- Reliability:
  - `/api/health` availability >= 99%.
  - 0 unexpected web/nav listener drops in final week.
- Navigation:
  - Hazard-on windows: `RouteFollowSampleCount >= 60` per 120s watch.
  - `DetourOnlyCollapseCountWindow = 0`.
- Stuck/deviation:
  - `CurrentWindowRepeatStuckRate <= 0.30`.
  - `CurrentWindowMaxRouteDeviation <= 120`.
  - `CurrentWindowStuckEvents <= 8`.
- Combat:
  - >=30 encounters.
  - `SPELL_FAILED_MOVING <= 2 / 100 casts`.
  - no sustained ranged-pull refusal loops.
- Script operational smoke:
  - 4/4 pass for `-?` probes.

## Daily Checkpoint Log
| Date | Focus | Completed | Blockers | Next Day |
|---|---|---|---|---|
| 2026-03-05 | Week 1 + targeted 20m live gate | Implemented blocker-first code updates (movement cancel hardening, face-assist fallback, bounded lost-target handling, pull log label fix, short no-progress trigger, watchnav profile/route gate). Completed targeted live run tag `warlock-live-targeted-20260304-223052` with stable+hazard watch + 8m soak and full artifacts. | `/api/session/stats` returns `503 GOAP agent not initialized` despite active bot; reroute lifecycle had 0 opportunities; no direct wall-collision scenario captured in this run. | Run targeted wall/behind-target scenario injection pass; investigate `/api/session/stats` pipeline; execute hazard-on route with known reroute opportunities. |
| 2026-03-06 | Blocker remediation landing | Landed resilient session stats fallback, additive runtime-mode metadata, reroute probe diagnostics, stable-live baseline promotion, manifest/evidence expansion, new harness actions for reroute/no-progress/combat validation, and restored `-?` smoke compatibility for the core hazard-control scripts. | Fresh live evidence is still required to close the tracker gates. | Execute the new `ValidateReroute`, `ValidateNoProgress`, and `ValidateCombat` actions, then run the integrated soak sequence and publish week verdicts. |
| 2026-03-06 | Gate 1 closure + reroute live gate | Re-ran automated baseline (`dotnet build`, `CoreUnitTests`, `FrontendUnitTests`), verified summary/failure artifacts for the updated validation actions, and closed the `/api/session/stats` stop-path gate with two passing live reruns under `warlock-live-session-api-20260306-run1` and `warlock-live-session-api-20260306-run2`. Executed two cold-booted live reroute attempts under `warlock-live-reroute-20260306` and `warlock-live-reroute-retry2-20260306`. | `ValidateReroute` failed twice before hazard injection because `/api/diagnostics/navigation/reroute` did not expose `MapId`, `CurrentPosition`, and `ProbeTarget`. Per campaign policy, `ValidateNoProgress`, `ValidateCombat`, and the three soak runs were deferred. | Remediate the reroute diagnostic contract gap, rerun `ValidateReroute`, then resume no-progress, combat, and soak validation in order. |
| 2026-03-06 | Reroute harness hardening + live rerun | Landed corpse/combat-aware reroute preflight and sustained-follow gating, bounded hazard-phase rearm attempts, additive reroute summary fields (`PreflightPassed`, gate failure reasons, rearm attempts, rejected sample counts, live-state contamination), and reroute validation report persistence on failure. Re-ran automated baseline successfully and executed fresh live reroute tag `warlock-live-reroute-hardening-20260306`. | The reroute diagnostics contract gap is closed, but the live character never left `Walk To Corpse` / `dead=true`, so the hardened reroute preflight correctly aborted before hazard injection. No valid reroute opportunity was present for the one allowed rerun window. | Clear the live corpse/death recovery state, confirm clean alive `FollowRoute`, then rerun `ValidateReroute` from a fresh stopped/start state before attempting Week 3 or Week 4 gates. |
| 2026-03-08 | Baseline freeze + bounded autonomy publish | Published `dev` through `31c8735c7` with the combat kill-to-loot handoff fix, bounded autonomy incident/screenshot/failover scaffolding, and the current warlock live profile refresh. Re-ran local verification successfully (`dotnet build`, focused `CoreUnitTests`, `FrontendUnitTests`) before push. | The live campaign is still blocked on `ValidateCombat` kill-to-loot target-loss accounting; readiness/bootstrap can still wobble; reroute, no-progress, and soaks remain downstream by dependency. | Re-freeze the handoff docs, run one clean bootstrap, rerun `ValidateCombat`, and only resume reroute/no-progress/soaks if combat closes. |
| 2026-03-09 | Startup re-anchoring + guarded autonomy continuation | Re-anchored the supervisor and handoff plan around the actual March 8 blocker stack instead of the older dry-run-only optimism. Added explicit startup families for profile-load disposed timer, action-bar slot drift, wrong-profile load, and doctor readiness timeout; tightened launch/autonomy status surfaces so requested/applied profile and failure kind are externally visible. | Live progress is now blocked first by startup/profile-load instability before combat/loot can be measured cleanly. Combat and loot remain coupled as the next blocker family after startup is stable. | Close startup/profile-load stability first, then rerun the short combat/loot repro window and `ValidateCombat` before reopening reroute or no-progress. |

## Incident Log
| Timestamp (UTC) | Category | Symptom | Root Cause | Fix | Evidence |
|---|---|---|---|---|---|
| 2026-03-05T03:48Z | GOAP not initialized | `GET /api/session/stats` returns 503 while `/api/health` and `/api/bot/status` are healthy and bot is active | Session endpoints treated a non-active or transitioning GOAP agent as hard-live-only state instead of serving cached session data. | Closed on 2026-03-06: session stats cache + active-agent semantics landed, then two live Gate 1 reruns verified post-stop `StatsSource=cached`, `AgentAvailable=false`, and `RuntimeMode=live`. | `logs/warlock-live-session-api-20260306-run1/20260306-183241-post-stop-session-stats.json`, `logs/warlock-live-session-api-20260306-run1/20260306-183241-post-stop-bot-status.json`, `logs/warlock-live-session-api-20260306-run2/20260306-183547-post-stop-session-stats.json`, `logs/warlock-live-session-api-20260306-run2/20260306-183547-post-stop-bot-status.json` |
| 2026-03-07T00:06Z | diagnostics contract gap | `ValidateReroute` aborts before hazard injection even with an active live bot because deterministic reroute probe fields are missing from `/api/diagnostics/navigation/reroute`. | The live reroute diagnostics payload did not populate `MapId`, `CurrentPosition`, and `ProbeTarget` during two separate cold-booted attempts, preventing deterministic hazard placement. | Pending: investigate reroute snapshot population path, restore deterministic probe data, then rerun the reroute gate before any later campaign phases. | `logs/warlock-live-reroute-20260306/agentctl-20260306-190405-validatereroute-failure.json`, `logs/warlock-live-reroute-20260306/agentctl-20260306-190405-validatereroute-summary.json`, `logs/warlock-live-reroute-retry2-20260306/agentctl-20260306-190738-validatereroute-failure.json`, `logs/warlock-live-reroute-retry2-20260306/agentctl-20260306-190738-validatereroute-summary.json` |
| 2026-03-07T02:58Z | invalid live state | Hardened `ValidateReroute` aborts at preflight with `death/corpse contamination` instead of starting hazard injection. | The live session entered `Walk To Corpse` with `snapshot.dead=true` and never recovered to a clean `FollowRoute` state within the bounded preflight window, so the run was not a valid reroute proof attempt. | Closed harness classification gap on 2026-03-06: reroute gating now records explicit contamination reasons and blocks invalid hazard injection. Pending live recovery: clear corpse/death state and rerun reroute from a clean alive `FollowRoute` state. | `logs/warlock-live-reroute-hardening-20260306/agentctl-20260306-215730-reroute-preflight.json`, `logs/warlock-live-reroute-hardening-20260306/agentctl-20260306-215730-validatereroute-summary.json`, `logs/warlock-live-reroute-hardening-20260306/agentctl-20260306-215730-validatereroute-failure.json` |
| 2026-03-08T16:55Z | startup/profile-load | `POST /api/bot/profile/load` failed with `Cannot access a disposed object. Object name: 'System.Timers.Timer'.` during the requested warlock startup path. | A disposed UI timer subscriber in the route component was still able to fault profile-load startup, and the control plane previously collapsed this into generic bootstrap failure. | Pending guarded verification: harden profile-load event handling, expose requested/applied profile + profile-load failure kind in `/api/launch/status`, and prioritize this family ahead of combat in the supervisor. | `logs/live-session-20260308-125446/agentctl-20260308-125446-start-failure.json`, `BlazorServer/bin/Release/net10.0/out20260308.log` |
| 2026-03-08T08:06Z | startup/readiness | `StartAndValidate` failed with `Doctor readiness repair failed: ReadinessTimeout` while the active profile target remained the warlock live profile. | Startup/readiness failure was being recorded only as a generic abort instead of a first-class blocker family, obscuring the real order of work. | Pending guarded verification: classify `DoctorReadinessTimeout` explicitly, keep retries bounded, and only allow an action-bar bypass when slot-2 `Shadow Bolt` drift is the sole remaining blocker. | `logs/live-session-20260308-040302/agentctl-20260308-040302-startandvalidate-failure.json` |

Categories:
- `socket address in use`
- `connection refused`
- `port not listening`
- `GOAP not initialized`
- `diagnostics contract gap`
- `invalid live state`

## Evidence Index
- Session artifacts: `logs/warlock-live-*`
- Validation reports: `logs/agentctl-*-validation.json`
- Watch summaries: `logs/*watchnav-summary.json`
- Soak artifacts: `logs/soak-nav-*.json`
- Targeted run report: `logs/warlock-live-targeted-20260304-223052-validation-report.md`
- Manifest and effective runtime profile: `logs/*/session-manifest.json`, `logs/*/*-flags-profile-*.json`
- Validation failure artifacts: `logs/*/*-failure.json`
- Validation summary artifacts: `logs/*/*-summary.json`

## Weekly Gate Decision
| Week | Gate Result | Notes | Deferred Items (owner/date) |
|---|---|---|---|
| Week 1 | Partially Validated | Cold bootstrap is proven and Gate 1 session API reliability is now closed with two post-stop live reruns. The week exit gate remains open because the 3x 30-minute dry-run sequence has not been executed yet. | `3x 30-minute dry runs` (owner: Engineering, target: 2026-03-07) |
| Week 2 | Failed | The deterministic reroute diagnostics gap is closed and the harness is now corpse/combat-aware, but the latest live reroute run never reached a valid alive `FollowRoute` state. No hazard-on reroute proof or stale-drop proof was captured because the character remained in `Walk To Corpse` / `dead=true` throughout the bounded preflight window. | `Clear live corpse-state blocker and rerun Week 2 from a fresh alive FollowRoute session` (owner: Engineering, target: 2026-03-07) |
| Week 3 | Not Validated | Deferred because the Week 2 blocker stopped the campaign before `ValidateNoProgress` and `ValidateCombat`. | `Resume no-progress and combat gates after Week 2 closure` (owner: Engineering, target: 2026-03-07) |
| Week 4 | Not Validated | Deferred because Week 2 and Week 3 did not close, so the three 60-minute soak runs and final verdict sequence were not attempted in this pass. | `Run three 60-minute soaks and publish final verdict after navigation/combat closure` (owner: Engineering, target: 2026-03-08) |
