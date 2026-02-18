# Comprehensive Project Audit Report

**Project:** WowClassicGrindBot
**Date:** 2026-02-16
**Branch:** dev
**Commit:** 6fd203a48

---

## Executive Summary

| Dimension | Score | Rating |
|-----------|-------|--------|
| Build Health | **6/10** | Acceptable |
| Test Execution | **9/10** | Excellent |
| Test Coverage | **4/10** | Below Acceptable |
| Code Quality | **4/10** | Below Acceptable |
| Security | **3/10** | Poor |
| Performance | **6/10** | Acceptable |
| Architecture | **5/10** | Marginal |
| **Overall** | **5.3/10** | **Marginal** |

**Key Strengths:** Solid test framework (1,393 tests, 99.8% pass rate), well-structured CI pipeline, comprehensive Phase 1 infrastructure, zero build errors.

**Critical Gaps:** Security vulnerabilities in Marketplace/AI code, 10+ new systems with 0% test coverage, HttpClient DI anti-pattern, 7x `continue-on-error` in CI, format check fails.

---

## Phase 0: Pre-Flight Baseline

| Metric | Value |
|--------|-------|
| Debug Build | 0 errors, 47 warnings |
| Release Build | 0 errors, 48 warnings |
| Total Tests Discovered | 1,393 |
| Solution Projects | 16 |

---

## Phase 1: Build Health — Score: 6/10

### Warning Inventory (47 warnings)

| Category | Count | Files |
|----------|-------|-------|
| SYSLIB1045 (GeneratedRegex) | 16 | `RegexExtensionTests.cs` |
| CS8602 (null deref) | 4 | `SmartBlacklistTests.cs` |
| CS8625 (null to non-nullable) | 1 | `RouteRerouterVisualizationIntegrationTests.cs` |
| CS8629 (nullable value type) | 1 | `BotFailureScenarioTests.cs` |
| CS8767 (nullability mismatch) | 1 | `NoPlanRecoveryServiceTests.cs` |
| CS0067 (unused event) | 1 | `GameStateManager.cs` |
| CS0219 (unused variable) | 1 | `RouteRerouterEvidenceTests.cs` |
| Other warnings | ~22 | Various (mostly in tests) |

**Analysis:** Most warnings are in test code (acceptable) or the SYSLIB1045 regex recommendation (low priority). No warnings in production Core/Game code is good.

### Format Check: FAIL

`dotnet format --verify-no-changes` exits with code 2. Massive whitespace formatting issues across legacy codebase files. New code appears mostly formatted correctly.

### CI Pipeline Review

**File:** `.github/workflows/dotnet_build.yml`

| Job | Status | Issues |
|-----|--------|--------|
| smoke-tests | Enforced | Clean |
| full-tests | Enforced | Coverage thresholds set very low (line: 10%, branch: 5%) |
| benchmarks | `continue-on-error: true` | Benchmark failures silently pass |
| code-quality | 2x `continue-on-error: true` | Warnings-as-errors AND format check both non-blocking |
| security-scan | 4x `continue-on-error: true` | Vuln scan + CodeQL all non-blocking |

**Total `continue-on-error: true`:** 7 instances across 3 jobs. This means code quality, security, and benchmarks never actually block a PR.

### DI Wiring: PASS

All feature phases registered in `BlazorServer/Program.cs`:
- `AddPhase1Features` (line 225)
- `AddHazardAvoidance` (line 228)
- `AddPhase2Features` (line 231)
- `AddPhase3Features` (line 234)
- `AddHumanizationServices` (line 237)
- `AddCombatRotationOptimizer` (line 240)

### Scoring Breakdown

| Criterion | Points | Awarded | Justification |
|-----------|--------|---------|---------------|
| 0 build errors | +3 | +3 | Clean build |
| <50 warnings | +2 | +2 | 47 warnings (barely qualifies) |
| Format passes | +2 | +0 | Fails with exit code 2 |
| CI enforces quality | +2 | +0 | 7x `continue-on-error` nullifies enforcement |
| DI complete | +1 | +1 | All phases registered |

---

## Phase 2: Test Execution — Score: 9/10

### Full Test Run Results

| Test Project | Total | Passed | Failed | Skipped |
|-------------|-------|--------|--------|---------|
| CoreUnitTests | 1,364 | 1,361 | 0 | 3 |
| FrontendUnitTests | 29 | 29 | 0 | 0 |
| **Total** | **1,393** | **1,390** | **0** | **3** |

**Pass Rate:** 99.8% (100% of non-skipped)
**Total Time:** ~3.5 minutes

### Skipped Tests (3)

1. `BreadcrumbTrackerTests` — "Timing-dependent test - requires integration testing" (2 tests)
2. `GoapPlannerTests` — "Performance test - may hit recursion depth" (1 test)

All skips are documented with valid reasons.

### E2E Tests

EndToEnd tests in `CoreUnitTests` run successfully (~23s for CombatGoalScenario suite). The RouteRerouterEvidenceTests include long-running stability tests (1-minute operations, thread safety) that all pass.

### Placeholder/Stub Test Check

No `Assert.True(true)` stubs found. All tests contain meaningful assertions.

### Scoring Breakdown

| Criterion | Points | Awarded | Justification |
|-----------|--------|---------|---------------|
| 100% pass rate | +4 | +4 | All non-skipped tests pass |
| No flaky tests | +2 | +2 | Single run shows no flakes |
| E2E completes | +2 | +2 | EndToEnd + stability tests pass within timeout |
| No placeholder stubs | +2 | +1 | Clean, but 3 skipped tests exist |

---

## Phase 3: Test Coverage — Score: 4/10

### Test Distribution by System

| System | Test Files | Test Count | Coverage Status |
|--------|-----------|------------|-----------------|
| Phase 1 Infra (CircuitBreaker, LRU, FeatureFlags) | 5 | ~58 | Covered |
| Hazard Avoidance | 6 | ~43 | Well Covered |
| Humanization | 6 | ~52 | Covered |
| Combat Rotation Optimizer | 9 | ~75 | Well Covered |
| GOAP Planning | 1 | ~101 | Well Covered |
| Route Rehab/Rerouter | 3+ | ~38 | Covered |
| Recovery (NoPlan) | 1 | ~24 | Covered |
| LLM Integration | 1 | ~16 | Partial (records only) |
| **BehaviorTree** | **0** | **0** | **ZERO** |
| **Diagnostics (ExecutionTracer, GoapEventHistory)** | **0** | **0** | **ZERO** |
| **Movement (OscillationDetector, HumanizedMover)** | **0** | **0** | **ZERO** |
| **AI ProfileGenerator** | **0** | **0** | **ZERO** |
| **Marketplace** | **0** | **0** | **ZERO** |
| **FailureAnalytics** | **0** | **0** | **ZERO** |

### Zero-Coverage Systems (Critical Gaps)

| System | Source Files | Priority |
|--------|-------------|----------|
| BehaviorTree | 8 files (nodes, engine, converter, context) | P1 — Pure logic, trivially testable |
| AI ProfileGenerator | 2 files (service, validator) | P1 — Security-sensitive |
| Marketplace | 2 files (service, listing) | P1 — Security-sensitive |
| Diagnostics | 4 files (tracer, event history) | P2 — Ring buffer needs verification |
| Movement | 3 files (oscillation, humanized mover) | P2 — Algorithm correctness |
| FailureAnalytics | 1 file | P3 — Hosted service |

### Scoring Breakdown

| Criterion | Points | Awarded | Justification |
|-----------|--------|---------|---------------|
| >60% overall coverage | +4 | +1 | Estimated 30-40% based on test distribution |
| All new systems ≥1 test | +2 | +0 | 6+ systems at zero |
| BehaviorTree covered | +1 | +0 | 0 tests |
| AI covered | +1 | +0 | 0 tests |
| Frontend pages tested | +2 | +3* | FrontendUnitTests has 29 tests |

*Adjusted: Frontend tests exist but new AI/Marketplace pages untested.

---

## Phase 4: Code Quality — Score: 4/10

### Finding 4.1: HttpClient DI Anti-Pattern (MEDIUM)

**Files:** `Phase2ServiceCollectionExtensions.cs:68`, `ProfileMarketplaceService.cs:37-58`

`ProfileMarketplaceService` is registered as singleton and takes raw `HttpClient` via constructor. No `HttpClient` is registered in DI — will fail at runtime. Should use `IHttpClientFactory` via `services.AddHttpClient<T>()`.

### Finding 4.2: Dead Dependency — spellDb (LOW)

**File:** `AIProfileGeneratorService.cs:24,37-38`

`SpellDB spellDb` is injected but never used. `GetRelevantSpells` is static and returns hardcoded lists. Comment confirms: "In a real implementation, this would query the SpellDB."

### Finding 4.3: Manual JSON via StringBuilder (LOW)

**File:** `HybridLLMDecisionService.cs:175-201`

`BuildGameStateContext` manually constructs JSON with `StringBuilder`. Fragile if values contain special characters. Should use `System.Text.Json`.

### Finding 4.4: SRP Violations (MEDIUM)

- `HybridLLMDecisionService` — BackgroundService + IGoapEventListener + cache management + context building
- `FailureAnalytics` — IHostedService + IDisposable + IGoapEventListener + persistence + analysis

### Finding 4.5: ICircuitBreakerFactory Embedded (LOW)

**File:** `Phase1ServiceCollectionExtensions.cs:79-134`

Interface and implementation defined inside the DI extensions file instead of `Core/Resilience/`. Wrong namespace (`Core.Extensions` vs `Core.Resilience`).

### Finding 4.6: TimeProvider Inconsistency (MEDIUM)

`DateTime.UtcNow` used directly in new code:
- `FailureAnalytics.cs` (lines 158, 250)
- `HybridDecisionEngine.cs` (lines 340, 351, 358)
- `GameStateSerializer.cs` (line 61)
- `ProfileMarketplaceService.cs` (lines 241, 277)
- `RotationMetricsCollector.cs` (line 114)

Only `FatigueSimulator` and `AIProfileGeneratorService` use `TimeProvider` correctly.

### Finding 4.7: TODO Comments (LOW)

3 TODO comments in `GameStateSerializer.cs` and `HybridDecisionEngine.cs` indicate incomplete stubs.

### Scoring Breakdown

| Criterion | Points | Awarded | Justification |
|-----------|--------|---------|---------------|
| No critical bugs | +3 | +1 | HttpClient DI broken at runtime |
| TimeProvider consistent | +2 | +0 | 1 of 6+ new services uses it |
| APIs documented | +2 | +2 | XML docs present on public methods |
| Complexity <10/method | +2 | +1 | Most methods fine, some complex |
| No TODOs | +1 | +0 | 3 TODO comments |

---

## Phase 5: Security — Score: 3/10

### Finding 5.1: Token Leakage via DefaultRequestHeaders (HIGH)

**File:** `ProfileMarketplaceService.cs:53-58,152`

GitHub token set on `HttpClient.DefaultRequestHeaders.Authorization`. Downloads from `listing.DownloadUrl` which comes from untrusted external data. An attacker-controlled listing could point `DownloadUrl` to their server and capture the Bearer token.

### Finding 5.2: Prompt Injection (HIGH)

**File:** `AIProfileGeneratorService.cs:156-207`

User-provided `description` embedded directly into LLM prompt via string interpolation with no sanitization, no length limits, and no structural prompt separation.

### Finding 5.3: Path Traversal — Accidentally Safe (MEDIUM)

**File:** `ProfileMarketplaceService.cs:318-327`

`SanitizeFileName` strips invalid filename chars but not `../`. On Windows, `/` is in `GetInvalidFileNameChars()` so `../` becomes `.._` (accidentally safe). No `Path.GetFullPath` containment check. No `Path.GetFileName()` defense-in-depth.

### Finding 5.4: Unbounded Deserialization (MEDIUM)

**File:** `ProfileMarketplaceService.cs:152,256,264-270`

External HTTP responses and base64 content decoded without size limits. Could cause OOM with malicious payloads.

### Finding 5.5: Legacy Vulnerable Packages (LOW)

**File:** `Directory.Packages.props:25,37`

- `System.Net.Http 4.3.4` — CVE-2018-8292 (credential leak). Redundant on .NET 10.
- `System.Text.RegularExpressions 4.3.1` — CVE-2019-0820 (ReDoS). Redundant on .NET 10.
- `System.Text.Json 8.0.4` — Pinned to old version, should use in-box .NET 10 version.

**Note:** `dotnet list package --vulnerable` reports **no active vulnerabilities** because these packages aren't currently referenced by any project. But they're latent risks.

### Scoring Breakdown

| Criterion | Points | Awarded | Justification |
|-----------|--------|---------|---------------|
| No vulnerable packages | +3 | +2 | No active CVEs, but latent risks in props |
| Path traversal fixed | +2 | +1 | Accidentally safe on Windows only |
| Input sanitization | +2 | +0 | Zero sanitization on LLM prompts |
| No secrets exposure | +1 | +0 | Token on DefaultRequestHeaders leaks |
| Proper HTTP patterns | +2 | +0 | Direct HttpClient, no size limits |

---

## Phase 6: Performance — Score: 6/10

### Benchmark Results

| Benchmark | Status | Result |
|-----------|--------|--------|
| ScoringBenchmark | PASS | Sub-nanosecond, 0 allocation |
| MousePathBenchmarks | PASS | ShortPath ~300ns, LongPath ~1μs |
| LoadAllProfiles | FAIL | Sealed class validation error |
| PPather (6 suites) | Not run | Would need individual --filter |
| Time_Compare | Not run | Available |
| RequirementFactory | Not run | Available |

**ScoringBenchmark Target (<50μs/tick):** PASS — measured at 0.5ns, well under target.

### Memory Leak Risks

| Component | Risk | Mitigation |
|-----------|------|------------|
| `HybridLLMDecisionService.decisionCache` | Dictionary grows unbounded | **RISK: No eviction** |
| `FailureAnalytics.sessionEvents` | List never truncated | **RISK: Memory grows** |
| `GoapEventHistory` | Ring buffer (1000 entries) | Safe by design |
| `HybridDecisionEngine` cache | Has expiry-based eviction | Cleanup runs on access only |

### Missing Benchmarks

No benchmarks exist for: BehaviorTree tick, CircuitBreaker concurrent load, OscillationDetector hot path, SmartBlacklist lookup.

### Scoring Breakdown

| Criterion | Points | Awarded | Justification |
|-----------|--------|---------|---------------|
| All benchmarks pass | +3 | +1 | LoadAllProfiles fails (sealed class) |
| ScoringBenchmark <50μs | +2 | +2 | 0.5ns — well under target |
| No memory leaks | +2 | +1 | 2 unbounded caches identified |
| Adequate allocation | +1 | +1 | Zero allocation in hot paths |
| New system benchmarks | +2 | +1 | Only existing benchmarks, none for new systems |

---

## Phase 7: Architecture — Score: 5/10

### DI Registration Chain

```
Program.cs → ConfigureServices
  ├── AddStartupConfigurations
  ├── AddStartupOrchestration
  ├── AddWoWProcess
  ├── AddCoreBase
  ├── AddPhase1Features → FeatureFlagService, CircuitBreakerFactory, BreadcrumbTracker, NullLLMClient, HybridLLMDecisionService
  ├── AddHazardAvoidance → HazardEventCollector, LocalHazardDao, RouteRehabilitator, etc.
  ├── AddPhase2Features → LLMClientFactory, AIProfileGeneratorService, ProfileValidator, ProfileMarketplaceService
  ├── AddPhase3Features → BehaviorTreeCombatEngineFactory, HybridDecisionEngine
  ├── AddHumanizationServices → FatigueSimulator, HumanizationProvider, etc.
  ├── AddCombatRotationOptimizer → RotationOptimizer, MetricsCollector, etc.
  ├── AddCoreNormal / AddCoreConfiguration (conditional)
  ├── AddFrontend
  └── AddCoreFrontend
```

### Feature Flag Discipline

| Service | Flag Check | Verdict |
|---------|-----------|---------|
| HazardEventCollector | Per-event check | PASS |
| HybridLLMDecisionService | Per-event check | PASS |
| AIProfileGeneratorService | Delegates to LLM client | PASS |
| **ProfileMarketplaceService** | **No flag check** | **FAIL** |

### Separation of Concerns Issues

| Class | Responsibilities | Verdict |
|-------|-----------------|---------|
| HybridLLMDecisionService | BackgroundService + IGoapEventListener + cache + context builder | FAIL (4 responsibilities) |
| FailureAnalytics | IHostedService + IDisposable + IGoapEventListener + persistence + analysis | FAIL (5 responsibilities) |
| Phase1ServiceCollectionExtensions | DI registration + ICircuitBreakerFactory + CircuitBreakerFactory | FAIL (mixed concerns) |

### Service Lifetime Issues

| Issue | Location | Severity |
|-------|----------|----------|
| Singleton with HttpClient (no factory) | ProfileMarketplaceService | HIGH — DNS rotation/socket exhaustion |
| Phase3 resolves GoapAgent in factory lambda | Phase3ServiceCollectionExtensions:56 | MEDIUM — Runtime resolution |

### CI Pipeline Enforcement

7 `continue-on-error: true` across code-quality, security-scan, and benchmarks jobs means only smoke tests and full test suite actually gate PRs. Coverage thresholds are set at 10% line / 5% branch — effectively meaningless.

### Scoring Breakdown

| Criterion | Points | Awarded | Justification |
|-----------|--------|---------|---------------|
| All services registered | +2 | +2 | All phases wired in Program.cs |
| No DI bugs | +2 | +0 | HttpClient missing from DI, will fail at runtime |
| Feature flags everywhere | +2 | +1 | ProfileMarketplaceService missing flag |
| SRP respected | +2 | +0 | 3 classes with multiple responsibilities |
| Interfaces in own files | +1 | +1 | Most are, except ICircuitBreakerFactory |
| GOAP fix tested | +1 | +1 | 4 tests verify chain planning fix |

---

## Phase 8: Per-System Scores

| # | System | Files | Tests | Score | Key Issue |
|---|--------|-------|-------|-------|-----------|
| 1 | Phase 1 Infra | ~10 | 58 | **7/10** | ICircuitBreakerFactory in wrong file |
| 2 | Startup Orchestration | ~4 | ~12 | **5/10** | LaunchReadiness record tests only |
| 3 | Hazard Avoidance | ~8 | 43 | **8/10** | Well tested, good integration tests |
| 4 | Humanization | ~8 | 52 | **7/10** | MicroPause/ScheduledBreak untested |
| 5 | LLM Integration | ~6 | 16 | **3/10** | Only record/model tests; service logic untested |
| 6 | Diagnostics | ~5 | 0 | **1/10** | Zero tests, ring buffer needs verification |
| 7 | GOAP Chain Planning | ~3 | 101 | **8/10** | Fix verified by comprehensive test suite |
| 8 | Route Rehab/Recovery | ~5 | 62 | **7/10** | FailureAnalytics untested |
| 9 | Combat Rotation | ~10 | 75 | **7/10** | Missing Tank/Healer strategy tests |
| 10 | BehaviorTree | 8 | 0 | **0/10** | Pure logic, trivially testable, zero tests |
| 11 | Movement Coordination | 3 | 0 | **1/10** | Algorithm code with zero tests |
| 12 | AI + Marketplace | 4 | 0 | **0/10** | Security issues + zero tests |

**Weighted Average:** ~4.5/10

---

## Prioritized Improvement Roadmap

### P1 — Critical (Do First)

| ID | Action | Files | Effort | Impact |
|----|--------|-------|--------|--------|
| P1.1 | Fix HttpClient DI in ProfileMarketplaceService | `Phase2ServiceCollectionExtensions.cs`, `ProfileMarketplaceService.cs` | 30 min | Fixes runtime crash |
| P1.2 | Fix token leakage — use per-request headers or validate download URLs | `ProfileMarketplaceService.cs` | 30 min | Fixes security vulnerability |
| P1.3 | Add path traversal defense (Path.GetFileName + containment check) | `ProfileMarketplaceService.cs` | 15 min | Fixes security vulnerability |
| P1.4 | Add BehaviorTree node tests (aim: 30 tests) | New: `CoreUnitTests/BehaviorTree/*.cs` | 1 hr | 8 files, pure logic |
| P1.5 | Add prompt injection sanitization (length limit + char allow-list) | `AIProfileGeneratorService.cs` | 30 min | Fixes security vulnerability |

### P2 — High (This Sprint)

| ID | Action | Files | Effort | Impact |
|----|--------|-------|--------|--------|
| P2.1 | Add ExecutionTracer + GoapEventHistory tests | New: `CoreUnitTests/Diagnostics/*.cs` | 45 min | 4 untested files |
| P2.2 | Add OscillationDetector + HumanizedMover tests | New: `CoreUnitTests/GoalsComponent/*.cs` | 45 min | Algorithm verification |
| P2.3 | Add HybridLLMDecisionService tests | New: `CoreUnitTests/LLM/HybridLLMDecisionServiceTests.cs` | 1 hr | Core service untested |
| P2.4 | Add feature flag guard to ProfileMarketplaceService | `ProfileMarketplaceService.cs` | 15 min | Architecture consistency |
| P2.5 | Fix unbounded cache in HybridLLMDecisionService | `HybridLLMDecisionService.cs` | 30 min | Memory leak prevention |
| P2.6 | Fix unbounded list in FailureAnalytics | `FailureAnalytics.cs` | 30 min | Memory leak prevention |
| P2.7 | Raise CI coverage thresholds to 25% and remove `continue-on-error` from code-quality | `.github/workflows/dotnet_build.yml` | 30 min | CI enforcement |

### P3 — Medium (Next Sprint)

| ID | Action | Files | Effort | Impact |
|----|--------|-------|--------|--------|
| P3.1 | Add Marketplace + AIProfileGenerator tests | New: `CoreUnitTests/Marketplace/*.cs`, `CoreUnitTests/AI/*.cs` | 1 hr | Security-sensitive code |
| P3.2 | Add FailureAnalytics tests | New: `CoreUnitTests/Analytics/FailureAnalyticsTests.cs` | 30 min | Hosted service |
| P3.3 | Fix LoadAllProfiles benchmark (sealed class issue) | `Benchmarks/LoadAllProfiles.cs` | 15 min | CI benchmarks |
| P3.4 | Add benchmarks for BT tick, CircuitBreaker, OscillationDetector | New: `Benchmarks/*.cs` | 1 hr | Performance regression |
| P3.5 | Replace StringBuilder JSON with System.Text.Json | `HybridLLMDecisionService.cs` | 30 min | Code quality |
| P3.6 | Use TimeProvider consistently across new services | Multiple files | 1 hr | Testability |

### P4 — Low (Ongoing)

| ID | Action | Impact |
|----|--------|--------|
| P4.1 | Move ICircuitBreakerFactory to `Core/Resilience/ICircuitBreakerFactory.cs` | File organization |
| P4.2 | Remove dead `spellDb` parameter from AIProfileGeneratorService | Dead code cleanup |
| P4.3 | Remove `System.Net.Http 4.3.4` and `System.Text.RegularExpressions 4.3.1` from Directory.Packages.props | Latent vulnerability |
| P4.4 | Update `System.Text.Json` from 8.0.4 to in-box version | Dependency cleanup |
| P4.5 | Add MicroPauseService + ScheduledBreakService tests | Coverage gap |
| P4.6 | Add Tank/Healer combat rotation strategy tests | Coverage gap |
| P4.7 | Split HybridLLMDecisionService into separate concerns | SRP compliance |
| P4.8 | Split FailureAnalytics into separate concerns | SRP compliance |
| P4.9 | Remove `continue-on-error` from security-scan steps | CI enforcement |
| P4.10 | Run `dotnet format` to fix whitespace issues | Format compliance |

---

## Verification Checklist

After implementing P1 fixes:

- [ ] `dotnet build MasterOfPuppets.sln -c Release` — zero errors, <50 warnings
- [ ] `dotnet test MasterOfPuppets.sln` — 100% pass rate
- [ ] BehaviorTree has ≥30 tests passing
- [ ] `ProfileMarketplaceService` uses `IHttpClientFactory`
- [ ] `SanitizeFileName` uses `Path.GetFileName` + containment check
- [ ] LLM prompt input has length limit and sanitization
- [ ] No token on `DefaultRequestHeaders`

After implementing P2 fixes:

- [ ] All new systems have ≥1 test
- [ ] CI coverage threshold at 25%
- [ ] Code-quality job blocks on failure
- [ ] No unbounded caches in new services
- [ ] `ProfileMarketplaceService` checks feature flags

---

## Raw Data

### Build Output
- Debug: 47 warnings, 0 errors
- Release: 48 warnings, 0 errors
- Format: FAIL (exit code 2, thousands of whitespace issues in legacy files)

### Test Results
- CoreUnitTests: 1,364 total (1,361 passed, 3 skipped)
- FrontendUnitTests: 29 total (29 passed)
- Total: 1,393 tests, 0 failures

### Vulnerable Package Scan
- `dotnet list package --vulnerable --include-transitive`: **No vulnerabilities detected**

### Benchmark Results
- ScoringBenchmark: 0.5ns mean, 0 allocation (target: <50μs) — PASS
- MousePathBenchmarks: ShortPath ~300ns, LongPath ~1μs — PASS
- LoadAllProfiles: FAIL (sealed class validation)

### CI `continue-on-error` Locations
1. Line 212: Benchmark run
2. Line 247: Warnings-as-errors build
3. Line 251: Format check
4. Lines 272, 278, 282, 286: Security scan (4 steps)
