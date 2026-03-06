# WowClassicGrindBot Improvement Plans — Master Index

**Date:** 2026-03-04 | **Updated:** 2026-03-05
**Final Baseline:** 1846/1849 CoreUnitTests (3 skipped) + 58/58 FrontendUnitTests
**Branch:** dev

> **Status: ALL P0–P3 TASKS COMPLETE ✅ (including P3-5)**

---

## P0 Sprint — COMPLETED 2026-03-05

All P0 tasks committed in a single session. Total time: ~25 minutes.

| Task | Commit | Result |
|------|--------|--------|
| P0-1 | `2e645ad06` | Stable NuGet packages — no pre-release in production build |
| P0-2 | `94d3914cf` | MapId fix — stuck events now zone-aware; +1 regression test |
| P0-3 | `cce5e5a78` | DetectUnexpectedState implemented — LLM intervention active; +4 unit tests |
| P0-4 | `6fa6e7094` | Liquid geometry restored — water/ocean nav now included |
| P0-5 | `81053aef0` | GOAP bitmask — ~800K allocs/sec eliminated at 500 Hz; +1 regression test |

---

## P1 Sprint — COMPLETED 2026-03-05

| Task | Commit | Result |
|------|--------|--------|
| P1-1 | `a437bf715` | Goal name array cached; RecordNoPlanEvent allocations guarded |
| P1-2 | `a437bf715` | ConcurrentDictionary<Lazy<T>> in LLMClientFactory — race condition fixed |
| P1-3 | `a437bf715` | Thread.Sleep(2) removed from GOAP loop |
| P1-4 | `43f22714a` | GoalTimeouts static class — timeout constants centralized |
| P1-5 | `43f22714a` | NavSoakMetricsService output dir anchored to AppContext.BaseDirectory |

---

## P2 Sprint — COMPLETED 2026-03-05

| Task | Commit | Result |
|------|--------|--------|
| P2-1 | `d5aa2612f` | GlobalKillSwitch + individual disable tests added |
| P2-2 | `d5aa2612f` | GoapPlanner edge case tests added |
| P2-3 | `42c21b5c5` | NavSoak window rollover + FlushAsync artifact tests |
| P2-4 | `d5aa2612f` | IsSharpTurn boundary angle tests (90°, 15°, 45°) |
| P2-5 | `d5aa2612f` | GoapPlanner cache disabled safety tests |
| P2-6 | `bdcfbfad5` | DiagnosticsController null-service 503 tests |

---

## P3 Sprint — COMPLETED 2026-03-05

| Task | Commit | Result |
|------|--------|--------|
| P3-1 | `4788e775a` | Typo fix: SimplyfyRouteToWaypoint → SimplifyRouteToWaypoint |
| P3-2 | `ec697a8d5` | Navigation.cs partial-class split (Logging + HazardAvoidance extracted) |
| P3-3 | `4788e775a` | Feature flag re-enable criteria documented in plan guidance |
| P3-4 | `4788e775a` | GoapAgent.UpdateWorldState XML doc added |
| P3-5 | `2833e5653` | DiagnosticsController split into DiagnosticsFixController |

---

## Execution Order

```
COMPLETE: P0-1 → P0-2 → P0-3 → P0-4 → P0-5
COMPLETE: P1-1 → P1-2 → P1-3 → P1-4 → P1-5
COMPLETE: P2-1 → P2-2 → P2-3 → P2-4 → P2-5 → P2-6
COMPLETE: P3-1 → P3-2 → P3-3 → P3-4
COMPLETE: P3-5 (DiagnosticsController split)
```

---

## Plan Files

| ID | Priority | File | Summary | Status | Risk | Est. Time |
|----|----------|------|---------|--------|------|-----------|
| **P0-1** | CRITICAL | [P0-1](./2026-03-04-P0-1-upgrade-prerelease-packages.md) | Upgrade pre-release NuGet packages to stable | ✅ COMPLETE | Low | 3 min |
| **P0-2** | CRITICAL | [P0-2](./2026-03-04-P0-2-fix-failuresimulation-mapid.md) | Fix hardcoded `MapId = 0` in `FailureSimulationService.SimulateStuck()` | ✅ COMPLETE | Low | 4 min |
| **P0-3** | CRITICAL | [P0-3](./2026-03-04-P0-3-fix-hybridengine-detectunexpectedstate.md) | Implement `DetectUnexpectedState()` stub | ✅ COMPLETE | Medium | 5 min |
| **P0-4** | CRITICAL | [P0-4](./2026-03-04-P0-4-fix-ppather-liquid-memory-leak.md) | Re-enable `GetLiquidVertsAndTris` | ✅ COMPLETE | Low | 5 min |
| **P0-5** | HIGH | [P0-5](./2026-03-04-P0-5-goap-planner-hashset-alloc.md) | Replace per-recursion `HashSet<GoapGoal>` with `uint` bitmask | ✅ COMPLETE | Medium | 8 min |
| **P1-1** | HIGH | [P1-1](./2026-03-04-P1-1-goap-agent-string-alloc.md) | Cache goal name array + guard `RecordNoPlanEvent` | ✅ COMPLETE | Low | 5 min |
| **P1-2** | HIGH | [P1-2](./2026-03-04-P1-2-llmclientfactory-thread-safety.md) | `ConcurrentDictionary<Lazy<T>>` in `LLMClientFactory` | ✅ COMPLETE | Low | 5 min |
| **P1-3** | HIGH | [P1-3](./2026-03-04-P1-3-goap-remove-thread-sleep.md) | Remove `Thread.Sleep(2)` from GOAP loop | ✅ COMPLETE | Low | 2 min |
| **P1-4** | MEDIUM | [P1-4](./2026-03-04-P1-4-goal-timeouts-constants.md) | Extract timeout constants into `GoalTimeouts` static class | ✅ COMPLETE | Low | 5 min |
| **P1-5** | MEDIUM | [P1-5](./2026-03-04-P1-5-navsoak-outputdir-fix.md) | Anchor `NavSoakMetricsService` output to `AppContext.BaseDirectory` | ✅ COMPLETE | Low | 4 min |
| **P2** | MEDIUM | [P2](./2026-03-04-P2-tests-coverage.md) | 6 test coverage gaps (all 6 tasks complete) | ✅ COMPLETE | Low | ~20 min |
| **P3** | LOW | [P3](./2026-03-04-P3-refactoring.md) | Refactoring tasks (all 5 tasks complete) | ✅ COMPLETE | None | ~15 min |

---

## Key Files Reference

| File | Lines | Plans that Touched It |
|------|-------|-----------------------|
| `Directory.Packages.props` | 70 | P0-1 |
| `Core/GOAP/GoapAgent.cs` | ~676 | P1-1, P1-3, P3-4 |
| `Core/GOAP/GoapPlanner.cs` | ~230 | P0-5, P2-2 |
| `Core/GoalsComponent/Navigation.cs` | ~1105 | P3-1 (typo), P3-2 (split) |
| `Core/GoalsComponent/Navigation.Logging.cs` | NEW | P3-2 |
| `Core/GoalsComponent/Navigation.HazardAvoidance.cs` | NEW | P3-2 |
| `Core/Navigation/NavSoakMetricsService.cs` | ~464 | P1-5 |
| `Core/AI/HybridDecision/HybridDecisionEngine.cs` | ~370 | P0-3 |
| `Core/AI/LLM/LLMClientFactory.cs` | ~128 | P1-2 |
| `MockWoWClient/GameState/FailureSimulationService.cs` | ~266 | P0-2 |
| `Core/Goals/GoalTimeouts.cs` | NEW | P1-4 |
| `BlazorServer/runtime_feature_flags.json` | 169 | P3-3 |
| `Frontend/Controllers/DiagnosticsController.cs` | 1088 | P3-5 (diagnostic endpoints retained) |
| `Frontend/Controllers/DiagnosticsFixController.cs` | 673 | P3-5 (fix/mutating endpoints extracted) |

---

## Final Verification (All Complete)

```bash
# Clean build — 0 errors, 0 warnings
dotnet build MasterOfPuppets.sln --nologo -v quiet

# Full test suite
dotnet test --nologo -v quiet
# Result: 1846/1849 CoreUnitTests (3 skipped) + 58/58 FrontendUnitTests

# No pre-release packages
dotnet list package --include-prerelease | grep -iE "preview|beta|alpha|dev-|rc\."
# Result: (no output)
```
