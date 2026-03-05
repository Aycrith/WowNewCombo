# WoW Live Validation Execution Tracker

## Scope
- Commit focus: `3cff1f72e` + prior cloud-session warlock/runtime changes.
- Profile: `BloodElf_Warlock_1-70_TBC.json`.
- Runtime target: `http://localhost:5000`, nav port `47110`.
- Baseline comparator:
  - `AvgRepeatRate=0.2689`
  - `AvgMaxDev=165.466`
  - `P95MaxDev=297.954`
  - `AvgStuckEventsPerWindow=8.455`

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

## Incident Log
| Timestamp (UTC) | Category | Symptom | Root Cause | Fix | Evidence |
|---|---|---|---|---|---|
| 2026-03-05T03:48Z | GOAP not initialized | `GET /api/session/stats` returns 503 while `/api/health` and `/api/bot/status` are healthy and bot is active | Unknown (endpoint/runtime mismatch) | Pending (investigate stats endpoint dependency/init order) | `logs/warlock-live-targeted-20260304-223052-session-stats-final.json`, `logs/warlock-live-targeted-20260304-223052-validation-report.md` |

Categories:
- `socket address in use`
- `connection refused`
- `port not listening`
- `GOAP not initialized`

## Evidence Index
- Session artifacts: `logs/warlock-live-*`
- Validation reports: `logs/agentctl-*-validation.json`
- Watch summaries: `logs/*watchnav-summary.json`
- Soak artifacts: `logs/soak-nav-*.json`
- Targeted run report: `logs/warlock-live-targeted-20260304-223052-validation-report.md`

## Weekly Gate Decision
| Week | Gate Result | Notes | Deferred Items (owner/date) |
|---|---|---|---|
| Week 1 |  |  |  |
| Week 2 |  |  |  |
| Week 3 |  |  |  |
| Week 4 |  |  |  |
