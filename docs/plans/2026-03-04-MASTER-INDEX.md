# WowClassicGrindBot Improvement Plans — Master Index

**Date:** 2026-03-04
**Baseline:** 1720/1723 CoreUnitTests + 29/29 FrontendUnitTests
**Branch:** dev

> **For Claude:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement any plan task-by-task. Start with the P0 plans first.

---

## Execution Order

```
P0-1 → P0-2 → P0-3 → P0-4 → P0-5
P1-1 → P1-2 → P1-3 → P1-4 → P1-5
P2 (all tasks in 2026-03-04-P2-tests-coverage.md)
P3 (all tasks in 2026-03-04-P3-refactoring.md)
```

---

## Plan Files

| ID | Priority | File | Summary | Risk | Est. Time |
|----|----------|------|---------|------|-----------|
| **P0-1** | CRITICAL | [P0-1-upgrade-prerelease-packages.md](./2026-03-04-P0-1-upgrade-prerelease-packages.md) | Upgrade `Newtonsoft.Json` 13.0.5-beta1 → 13.0.4 and `Serilog.Expressions` 5.0.1-dev → 5.0.0 | Low | 3 min |
| **P0-2** | CRITICAL | [P0-2-fix-failuresimulation-mapid.md](./2026-03-04-P0-2-fix-failuresimulation-mapid.md) | Fix hardcoded `MapId = 0` in `FailureSimulationService.SimulateStuck()` | Low | 4 min |
| **P0-3** | CRITICAL | [P0-3-fix-hybridengine-detectunexpectedstate.md](./2026-03-04-P0-3-fix-hybridengine-detectunexpectedstate.md) | Implement `DetectUnexpectedState()` stub — HybridLLMDecision feature is ON but detection is a no-op | Medium | 5 min |
| **P0-4** | CRITICAL | [P0-4-fix-ppather-liquid-memory-leak.md](./2026-03-04-P0-4-fix-ppather-liquid-memory-leak.md) | Re-enable `GetLiquidVertsAndTris` — water/ocean nav geometry silently excluded | Low | 5 min |
| **P0-5** | HIGH | [P0-5-goap-planner-hashset-alloc.md](./2026-03-04-P0-5-goap-planner-hashset-alloc.md) | Replace per-recursion `HashSet<GoapGoal>` with `uint` bitmask in `BuildGraph` — ~1600 allocs/plan eliminated | Medium | 8 min |
| **P1-1** | HIGH | [P1-1-goap-agent-string-alloc.md](./2026-03-04-P1-1-goap-agent-string-alloc.md) | Cache goal name array + guard `RecordNoPlanEvent` block — eliminates `ToList()` and `ToString()` in GOAP hot path | Low | 5 min |
| **P1-2** | HIGH | [P1-2-llmclientfactory-thread-safety.md](./2026-03-04-P1-2-llmclientfactory-thread-safety.md) | Replace racy double-checked lock with `ConcurrentDictionary<Lazy<T>>` in `LLMClientFactory` | Low | 5 min |
| **P1-3** | HIGH | [P1-3-goap-remove-thread-sleep.md](./2026-03-04-P1-3-goap-remove-thread-sleep.md) | Remove `Thread.Sleep(2)` at lines 292 and 356 — unnecessary 15.6ms OS timer penalty | Low | 2 min |
| **P1-4** | MEDIUM | [P1-4-goal-timeouts-constants.md](./2026-03-04-P1-4-goal-timeouts-constants.md) | Extract `MAX_TIME_TO_REACH_MELEE` etc. into shared `GoalTimeouts` static class | Low | 5 min |
| **P1-5** | MEDIUM | [P1-5-navsoak-outputdir-fix.md](./2026-03-04-P1-5-navsoak-outputdir-fix.md) | Anchor `NavSoakMetricsService` artifact output to `AppContext.BaseDirectory` on fallback | Low | 4 min |
| **P2** | MEDIUM | [P2-tests-coverage.md](./2026-03-04-P2-tests-coverage.md) | 6 test coverage gaps: feature flags GlobalKillSwitch, GOAP edge cases, planner cache docs, NavSoak rollover, IsSharpTurn boundary, DiagnosticsController endpoints | Low | ~20 min total |
| **P3** | LOW | [P3-refactoring.md](./2026-03-04-P3-refactoring.md) | Typo fix (`SimplyfyRouteToWaypoint`), Navigation.cs partial-class split, feature flag docs, GoapAgent XML docs, DiagnosticsController split | None | ~15 min total |

---

## Key Files Reference

| File | Lines | Plans that Touch It |
|------|-------|---------------------|
| `Directory.Packages.props` | 70 | P0-1 |
| `Core/GOAP/GoapAgent.cs` | 676 | P1-1, P1-3, P3-4 |
| `Core/GOAP/GoapPlanner.cs` | 230 | P0-5, P2 (cache doc) |
| `Core/GoalsComponent/Navigation.cs` | 1425 | P3-1 (typo), P3-2 (split) |
| `Core/Navigation/NavSoakMetricsService.cs` | 464 | P1-5 |
| `Core/AI/HybridDecision/HybridDecisionEngine.cs` | 370 | P0-3 |
| `Core/AI/LLM/LLMClientFactory.cs` | 128 | P1-2 |
| `MockWoWClient/GameState/FailureSimulationService.cs` | 266 | P0-2 |
| `CoreManualTests/PPatherV2/PPatherV2.cs` | ~250 | P0-4 |
| `PPather/Triangles/GameV2/Adt.cs` | ~450 | P0-4 |
| `Core/Goals/GoalTimeouts.cs` | NEW | P1-4 |
| `BlazorServer/runtime_feature_flags.json` | 169 | P3-3 |

---

## TDD Template (every task)

```bash
# 1. Write failing test
dotnet test CoreUnitTests --filter "FullyQualifiedName~<TestMethod>" --verbosity detailed
# Expected: FAIL

# 2. Implement fix
# (edit files)

# 3. Confirm pass
dotnet test CoreUnitTests --filter "FullyQualifiedName~<TestMethod>" --verbosity detailed
# Expected: PASS

# 4. No regressions
dotnet test MasterOfPuppets.sln --verbosity minimal
# Expected: >= 1720 CoreUnitTests + 29 FrontendUnitTests

# 5. Commit
git add <files>
git commit -m "<type>(<scope>): <description>"
```

---

## Final Verification

After all plans complete:

```bash
# 1. Clean build
dotnet build MasterOfPuppets.sln

# 2. Full test suite (expect ~1740+ total)
dotnet test MasterOfPuppets.sln --verbosity minimal

# 3. No pre-release packages
dotnet list package --include-prerelease | grep -iE "preview|beta|alpha|dev-|rc\."

# 4. Performance benchmarks (no regression)
dotnet run --project Benchmarks -c Release

# 5. Manual bot validation
dotnet run --project BlazorServer -c Release
curl -X POST http://localhost:5000/api/diagnostics/flush-soak
# Verify: logs/soak-nav-*.json artifact created
```
