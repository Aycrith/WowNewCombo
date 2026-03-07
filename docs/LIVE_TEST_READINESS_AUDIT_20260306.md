# Live-Test Readiness Audit - 2026-03-06

## Scope

- Audit target: `dev` `HEAD` `df2494f21`
- Audit window: `2026-02-28` through `2026-03-05`
- Focus: bot runtime, navigation, combat, profiles, scripts, runtime config, and live validation evidence
- Evidence sources:
  - recent commits in the recovery window
  - `docs/plans/*` plus the Feb 28 recovery and validation docs
  - automated verification at `HEAD`
  - live artifacts under `logs/warlock-live-*`

## Automated Verification

Commands run on `2026-03-06`:

```powershell
dotnet build MasterOfPuppets.sln --nologo -v quiet
dotnet test CoreUnitTests\CoreUnitTests.csproj --nologo -v quiet --no-build
dotnet test FrontendUnitTests\FrontendUnitTests.csproj --nologo -v quiet --no-build
dotnet test CoreUnitTests\CoreUnitTests.csproj --nologo -v quiet --no-build --filter "FullyQualifiedName~GoapAgentHysteresisTests|FullyQualifiedName~GoapPlannerExecutionOptionsTests|FullyQualifiedName~GoapPlannerTests|FullyQualifiedName~GoapCurrentGoalStateTests|FullyQualifiedName~FollowRouteGoalRefillTests|FullyQualifiedName~NavigationHelperTests|FullyQualifiedName~OscillationDetectorTests|FullyQualifiedName~StuckDetectorBreadcrumbTests|FullyQualifiedName~NavSoakMetricsServiceTests|FullyQualifiedName~NavSoakDeviationTests|FullyQualifiedName~CombatGoalTests|FullyQualifiedName~PullTargetGoalTests|FullyQualifiedName~ApproachTargetGoalTests|FullyQualifiedName~LootGoalTests"
dotnet test FrontendUnitTests\FrontendUnitTests.csproj --nologo -v quiet --no-build --filter "FullyQualifiedName~DiagnosticsController|FullyQualifiedName~DiagnosticsFixController|FullyQualifiedName~FeatureFlagController|FullyQualifiedName~BotApiController|FullyQualifiedName~SessionController"
```

Results:

- `dotnet build`: passed, `0` warnings, `0` errors
- `CoreUnitTests`: `1846/1849` passed, `3` skipped
- `FrontendUnitTests`: `58/58` passed
- targeted `CoreUnitTests`: `167/168` passed, `1` skipped
- targeted `FrontendUnitTests`: `29/29` passed

Notes:

- Current automated baseline is healthy.
- There is no direct `SessionController` unit test in `FrontendUnitTests`; the targeted frontend run passed by covering adjacent diagnostics and bot API controller tests.

## Plan Completion Matrix

### `docs/plans/2026-03-04-*`

| Item | Commit | Audit Result | Notes |
|---|---|---|---|
| `P0-1` | `2e645ad06` | Complete | Package stabilization reflected in current build baseline. |
| `P0-2` | `94d3914cf` | Complete | MapId fix documented complete in master index and retained at `HEAD`. |
| `P0-3` | `cce5e5a78` | Complete | `HybridDecisionEngine.DetectUnexpectedState` implemented. |
| `P0-4` | `6fa6e7094` | Complete | PPather liquid geometry restoration documented complete and preserved. |
| `P0-5` | `81053aef0` | Complete | `GoapPlanner.BuildGraph` bitmask path active at `HEAD`. |
| `P1-1` | `a437bf715` | Complete | `GoapAgent` allocation reduction retained. |
| `P1-2` | `a437bf715` | Complete | `LLMClientFactory` thread-safety fix retained. |
| `P1-3` | `a437bf715` | Complete | `Thread.Sleep(2)` removal retained. |
| `P1-4` | `43f22714a` | Complete | `GoalTimeouts.MaxTimeToReachMeleeMs` active in goals. |
| `P1-5` | `43f22714a` | Complete | `NavSoakMetricsService.ResolveOutputDir()` anchored to `AppContext.BaseDirectory`. |
| `P2-1` | `d5aa2612f` | Complete | Feature flag safety coverage present in test suite. |
| `P2-2` | `d5aa2612f` | Complete | GOAP planner edge-case coverage present. |
| `P2-3` | `42c21b5c5` | Complete | NavSoak rollover and flush coverage present. |
| `P2-4` | `d5aa2612f` | Complete | `Navigation.IsSharpTurn()` boundary coverage present. |
| `P2-5` | `d5aa2612f` | Complete | GOAP cache-disabled safety coverage present. |
| `P2-6` | `bdcfbfad5` | Complete | diagnostics null-service 503 coverage present. |
| `P3-1` | `4788e775a` | Complete | typo fix retained. |
| `P3-2` | `ec697a8d5` | Complete | `Navigation` partial-class split retained. |
| `P3-3` | `4788e775a` | Complete | plan guidance exists; runtime defaults intentionally remain broader than the plan prose. |
| `P3-4` | `4788e775a` | Complete | `GoapAgent.UpdateWorldState` XML docs retained. |
| `P3-5` | `2833e5653` | Complete | `DiagnosticsFixController` split retained. |

### `docs/plans/nav-perf-next-steps.md`

| Item | Audit Result | Notes |
|---|---|---|
| `1.1` | Complete | forward-only refill candidate guard landed |
| `1.2` | Complete | oscillation detector tuned to `8/4/1500` |
| `1.3` | Complete | mounted turn-preserving break retained |
| `1.4` | Complete | resume waypoint pop limit reduced |
| `2.1` | Complete | mounted reached-distance adapts to turn shape |
| `2.2` | Complete | route deviation wired into NavSoak metrics |
| `2.3` | Complete (superseded) | replaced by simplified turn model |
| `2.4` | Complete | default-off behind `NavigationExperiments` |
| `3.1` | Complete | `RouteSegmentTracker` landed, default-off |
| `3.2a` | Complete | usable-goal cache landed, default-off |
| `3.2b` | Complete | planner bitmask landed |
| `3.2c` | Complete | plan cache landed, default-off |
| `3.3` | Complete | progress-aware refill loop breaker retained |
| `3.4` | Complete | adaptive heading cooldown landed, default-off |
| `3.5` | Complete | `GoapCurrentGoalState` tracks age and transition count |

## Recent Runtime and Recovery Workstreams

| Workstream | Key Commits | Implemented | Internally Consistent | Validated in Automation | Validated in Live Evidence | Ready for Next Live Phase |
|---|---|---:|---:|---:|---:|---:|
| Baseline recovery: hysteresis, conservative stuck thresholds, detour disablement | `f25f27a53` | Yes | Yes | Yes | Yes | Yes |
| Navigation live-client hardening: refill, sharp-turn preservation, corpse path preservation | `26a6f2063`, `1959d602c` | Yes | Yes | Yes | Partially | Partially |
| GOAP stale cache and planner stabilization | `fea1e5169`, `81053aef0`, `ff20a23aa` | Yes | Yes | Yes | Partially | Partially |
| Runtime stabilization across pull, combat, casting, target handling | `7f585d9b2`, `78d76eb10` | Yes | Yes | Yes | Partially | Partially |
| Warlock live-validation config/profile layer | `1541d8e20`, `Scripts/Agent-BotControl.ps1` | Yes | No | Partially | Partially | No |
| Navigation reroute and telemetry closeout | `3cff1f72e`, `9b2d2ff1e`, `d772f897d`, `ff20a23aa` | Yes | Yes | Yes | No | No |

## Code and Contract Evidence

Key code paths verified in the current codebase:

- GOAP hysteresis remains active at `Core/GOAP/GoapAgent.cs:60` with threshold `3`.
- GOAP cache invalidation remains active at `Core/GOAP/GoapAgent.cs:345` and `Core/GOAP/GoapPlanner.cs:31`.
- planner cache controls remain default-off and per-call at `Core/GOAP/GoapPlannerExecutionOptions.cs:8-9`.
- `GoalTimeouts.MaxTimeToReachMeleeMs` is used by `AdhocNPCGoal`, `LootGoal`, and `SkinningGoal`.
- `NavSoakMetricsService.ResolveOutputDir()` anchors output under `AppContext.BaseDirectory` at `Core/Navigation/NavSoakMetricsService.cs:433-454`.
- `RouteSegmentTracker` is present and gated via `NavigationExperiments` at `Core/GoalsComponent/Navigation.cs:1028-1065`.
- `SessionController` returns `503` whenever `botController.GoapAgent` is null at `Frontend/Controllers/SessionController.cs:37`, `:72`, and `:87`.
- `ConfigBotController.GoapAgent` always returns `null` at `Core/ConfigBotController.cs:22`.
- `BlazorServer` selects `AddCoreConfiguration()` instead of `AddCoreNormal()` whenever `configurationComplete` is false at startup in `BlazorServer/Program.cs:242-272`.

Audit interpretation:

- The `/api/session/stats` failure mode is real and already encoded in controller behavior.
- The configuration-mode startup path is a plausible cause for `session/stats` failure when startup readiness is degraded, but the live evidence does not prove it is the only cause.

## Runtime Configuration Reconciliation

### Static checked-in surfaces

| Surface | HazardAvoidance | StuckRecoveryV2 | CombatRotationOptimizer | StuckSensitivity | Other Drift |
|---|---:|---:|---:|---|---|
| Feb 28 validation docs | `true` | `true` | `true` | `0.2 / 5000` | baseline docs say all three advanced features were re-enabled |
| `BlazorServer/runtime_feature_flags.json` | `false` | `true` | `false` | `0.2 / 5000` | `ReactionMaxMs=800`, `BurstDampening=true`, `DebugMode=true` |
| `HeadlessServer/runtime_feature_flags.json` | `false` | `false` | `false` | `0.2 / 5000` | `ReactionMaxMs=500`, `BurstDampening=false`, `DebugMode=false` |

### Script-applied live surfaces

`Scripts/Agent-BotControl.ps1` applies runtime profiles at live-test time:

- `stable-live`: `HazardAvoidance=false`, `MinDistance=0.08`, `UnstuckAfterMs=3200`
- `triage-hazard`: inherits `stable-live` and flips `HazardAvoidance=true`
- `triage-predictive`: `HazardAvoidance=false`, `MinDistance=0.08`, `UnstuckAfterMs=2800`, predictive detection on

Audit result:

- The checked-in `BlazorServer` JSON is not the full source of truth for live behavior.
- The Feb 28 validation docs, the checked-in JSON, and the script-applied nav profiles do not currently describe one single authoritative runtime state.
- `HeadlessServer/runtime_feature_flags.json` is materially out of sync with `BlazorServer/runtime_feature_flags.json`.

## Live Evidence Reconciliation

### Positive evidence

- `logs/warlock-live-targeted-20260304-223052-validation-report.md` shows spell coverage for `Immolate`, `Corruption`, `Shadow Bolt`, and `Shoot`.
- `logs/warlock-live-targeted-20260304-223052-validation-report.md` records `SPELL_FAILED_MOVING=0` and `ERR_BADATTACKFACING=0` in the targeted run.
- `logs/warlock-live-castfix-targeted-20260305-013742-watchnav-hazard.json` shows `RouteFollowSampleCount=62`, exceeding the tracker threshold of `>=60` for a hazard-on watch window.
- `logs/warlock-live-castfix-targeted-20260305-013742-session-stats-final.json` shows `/api/session/stats` succeeding with `12` kills and `0` deaths.
- `logs/warlock-live-resume2-20260305-024205-watchnav-stable.json` shows `RouteFollowSampleCount=118` with `0` max deviation in that sample.

### Negative or still-open evidence

- `logs/warlock-live-targeted-20260304-223052-validation-report.md` explicitly concludes `Not Fully Validated`.
- `logs/warlock-live-targeted-20260304-223052-validation-report.md` records `/api/session/stats` failure with `503 GOAP agent not initialized`.
- `logs/warlock-live-20260304-191809-session-stats-final.json` and `logs/warlock-live-targeted-20260304-225451-session-stats-final.json` also show `GOAP agent not initialized`.
- multiple nav runtime and soak artifacts still show `currentWindowRerouteTriggerCount=0`, `currentWindowRerouteApplyCount=0`, and `currentWindowRerouteDropCount=0`.
- the targeted validation report still records `Lost target logs=11`.
- there is no single artifact closing the required wall or behind-target no-progress scenario.
- there is no single artifact closing the required `>=30` controlled combat encounters gate.
- there is no evidence of the Week 4 requirement for three `60` minute integrated soak runs with a final verdict matrix.

## Live-Test Gate Status

| Gate | Status | Evidence | Audit Verdict |
|---|---|---|---|
| `/api/session/stats` reliability while bot is active | Mixed | failures in `warlock-live-targeted-20260304-223052` and `warlock-live-20260304-191809`; success in `warlock-live-castfix-targeted-20260305-013742` and `warlock-live-resume2-20260305-024205` | Blocker, intermittent |
| Hazard-on route-follow threshold (`>=60` samples) | Met in at least one later run | `warlock-live-castfix-targeted-20260305-013742-watchnav-hazard.json` -> `62` | Partially closed |
| Reroute trigger/apply/drop proof | Not met | all reviewed runtime/soak artifacts show reroute counts `0` | Blocker |
| No detour-only collapse | Met in reviewed artifacts | reviewed soak/runtime artifacts show `currentWindowDetourOnlyCollapseCount=0` | Partially closed |
| Wall or behind-target no-progress escalation | Not met | targeted validation report calls this out as unvalidated | Blocker |
| Controlled combat sample with `>=30` encounters | Not met | spell coverage exists, but no single controlled `>=30` encounter run is evidenced | Blocker |
| Movement-cast and facing thresholds | Partially met | `SPELL_FAILED_MOVING=0`, `ERR_BADATTACKFACING=0`, but target-loss remains | Open follow-up |
| Integrated soak stability and baseline delta review | Not met | short soak artifacts exist; Week 4 tracker requirements are still open | Blocker |

## Final Verdict

### Implementation status

- `Implemented`: **PASS**
- `Internally Consistent`: **PARTIAL PASS**
- `Validated in Automation`: **PASS**
- `Validated in Live Evidence`: **PARTIAL PASS**
- `Ready for Next Live Phase`: **NO**

### Why the project is not yet ready for the next live phase

The codebase and tests support the claim that the planned work was implemented correctly. The blocker is no longer code completeness; it is live validation closure and runtime-state consistency.

The current blockers are:

1. intermittent `/api/session/stats` failure while other runtime endpoints remain healthy
2. no captured reroute opportunities, so reroute lifecycle logic is still unproven in live conditions
3. no closed wall or behind-target no-progress validation run
4. no single controlled combat artifact meeting the `>=30 encounters` threshold
5. drift between Feb 28 validation docs, checked-in runtime JSON, and script-applied nav profiles
6. `HeadlessServer` runtime flags remain out of sync with the main live stack

## Required Follow-Up Before Immediate Live Testing

1. Reproduce and close the intermittent `/api/session/stats` issue.
   - Add direct automated coverage for `SessionController`.
   - Capture whether the failure correlates with configuration-mode startup, session recreation, or another `GoapAgent` lifecycle gap.
2. Run a hazard-on route with known reroute opportunities.
   - Must produce non-zero reroute trigger/apply or trigger/drop counts.
   - Must still keep `DetourOnlyCollapseCountWindow = 0`.
3. Run a wall or behind-target scenario.
   - Must capture the short no-progress escalation path in logs or runtime snapshots.
4. Run one controlled combat session with `>=30` encounters.
   - Must retain spell coverage and acceptable target reacquire behavior.
5. Choose and document the authoritative live-test runtime profile.
   - Either align checked-in JSON with the scripted profile or explicitly declare the script profile as the operational source of truth for the next test phase.
6. Align `HeadlessServer/runtime_feature_flags.json` with the intended operational policy or explicitly mark it out of scope for the upcoming live phase.

## Immediate Testing Scope Once Blockers Clear

- hazard-off stable watch
- hazard-on reroute watch with forced opportunities
- controlled combat sample run
- integrated soak run with route-only metrics and final verdict matrix
