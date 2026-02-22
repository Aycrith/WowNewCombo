# Resume & Complete Audit Remediation — WowClassicGrindBot

## Context

The previous sessions ran a comprehensive audit (scored 5.3/10) and began implementing fixes. The last 2 commits were:
- `d85f55fa5` — audit-driven fixes (security hardening, 233 new tests)
- `7a0dc6bf2` — dotnet format pass

The working tree has an **in-progress SRP refactor** (uncommitted): 4 new files + 3 modified files. This refactor splits `HybridLLMDecisionService` and `FailureAnalytics` into engine+listener+service triads. It has critical bugs that must be fixed before committing. Then we resume the audit roadmap.

---

## Current Working Tree State

### 4 Untracked New Files (to be committed after fixes):
- `Core/LLM/HybridLlmDecisionEngine.cs` — LLM business logic (200 lines, uses TimeProvider)
- `Core/LLM/HybridLlmEventListener.cs` — IGoapEventListener adapter for LLM (71 lines)
- `Core/Analytics/FailureAnalyticsEngine.cs` — Analytics business logic (260 lines, uses TimeProvider)
- `Core/Analytics/FailureAnalyticsEventListener.cs` — IGoapEventListener adapter for analytics (71 lines)

### 3 Modified Files (working tree, not staged):
- `Core/Extensions/Phase1ServiceCollectionExtensions.cs` — adds `HybridLlmDecisionEngine` + `HybridLlmEventListener` singletons
- `Core/GoalsFactory/GoalFactory.cs` — adds `FailureAnalyticsEngine` + `FailureAnalyticsEventListener` singletons
- `Core/LLM/HybridLLMDecisionService.cs` — stripped to thin lifecycle shell, injects `HybridLlmDecisionEngine`

---

## Critical Bugs to Fix Before Committing

### Bug 1: `CircuitBreaker<LLMDecision>` not registered in DI (STARTUP CRASH)

`HybridLlmDecisionEngine` requires `CircuitBreaker<LLMDecision>` as a constructor parameter (line 39), but it's never registered. `ICircuitBreakerFactory.GetOrCreate<LLMDecision>()` exists but isn't called anywhere for this type.

**Fix:** In `Phase1ServiceCollectionExtensions.cs`, after the `ILLMClient` registration, add a factory registration:
```csharp
services.AddSingleton(sp =>
{
    ICircuitBreakerFactory factory = sp.GetRequiredService<ICircuitBreakerFactory>();
    IOptionsMonitor<FeatureFlagsOptions> opts = sp.GetRequiredService<IOptionsMonitor<FeatureFlagsOptions>>();
    HybridLLMDecisionOptions llmOpts = opts.CurrentValue.HybridLLMDecision;
    return factory.GetOrCreate<LLMDecision>(
        "HybridLLM",
        static () => new LLMDecision("NoAction", "Circuit open", 0f),
        llmOpts.CircuitBreakerThreshold,
        TimeSpan.FromSeconds(llmOpts.CircuitBreakerCooldownSeconds));
});
```

**Files:** `Core/Extensions/Phase1ServiceCollectionExtensions.cs`

### Bug 2: `FailureAnalytics` (old monolith) still has ALL the business logic

With the new SRP split registered in `GoalFactory.cs`, the old `FailureAnalytics` hosted service is still registered AND still implements `IGoapEventListener`, creating duplicate event processing and double disk writes.

**Fix:** Gut `FailureAnalytics.cs` to delegate to `FailureAnalyticsEngine`. Remove its own event recording logic, stuck event subscription, and analytics state — keep only IHostedService lifecycle (start periodic flush timer, stop on cancel). Constructor changes: inject `FailureAnalyticsEngine engine` instead of duplicating PlayerReader/StuckDetector/etc.

**Files:** `Core/Analytics/FailureAnalytics.cs`

### Bug 3: `JsonSerializerOptions` allocated per call in `HybridLlmDecisionEngine`

`BuildGameStateContext()` at line 183 allocates `new JsonSerializerOptions { ... }` on every call. Should be `private static readonly`.

**Fix:** Add `private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };` and use it in `BuildGameStateContext`.

**Files:** `Core/LLM/HybridLlmDecisionEngine.cs`

---

## Audit Roadmap Items (post-SRP-fix)

From `docs/AUDIT_REPORT_2026-02-16.md` priority list:

### P1 — Critical Security (not yet done per audit)

| ID | Action | File(s) |
|----|--------|---------|
| P1.1 | Fix `ProfileMarketplaceService` HttpClient DI — use `services.AddHttpClient<ProfileMarketplaceService>()` and inject `HttpClient` via constructor/factory | `Core/AI/Marketplace/ProfileMarketplaceService.cs`, `Core/Extensions/Phase2ServiceCollectionExtensions.cs` |
| P1.2 | Fix token leakage — remove GitHub token from `DefaultRequestHeaders`; apply per-request auth via `HttpRequestMessage` headers only after validating the download URL's host | `Core/AI/Marketplace/ProfileMarketplaceService.cs` |
| P1.3 | Add path traversal defense — wrap `SanitizeFileName` to call `Path.GetFileName()` + `Path.GetFullPath()` containment check | `Core/AI/Marketplace/ProfileMarketplaceService.cs` |
| P1.5 | Add prompt injection defense — add length limit (e.g. 2000 chars) and strip/escape special characters from user `description` before embedding in LLM prompt | `Core/AI/AIProfileGeneratorService.cs` |

### P2 — High (test coverage gaps)

| ID | Action | File(s) |
|----|--------|---------|
| P1.4 | Add BehaviorTree tests (aim 20+ tests covering nodes, engine, feature-flag disable) | New: `CoreUnitTests/BehaviorTree/BehaviorTreeTests.cs` |
| P2.1 | Add Diagnostics tests (ExecutionTracer ring buffer, GoapEventHistory) | New: `CoreUnitTests/Diagnostics/DiagnosticsTests.cs` |
| P2.2 | Add Movement tests (OscillationDetector, HumanizedMover) | New: `CoreUnitTests/GoalsComponent/MovementTests.cs` |
| P2.4 | Add feature flag guard to `ProfileMarketplaceService` | `Core/AI/Marketplace/ProfileMarketplaceService.cs` |

### P3 — Medium

| ID | Action | File(s) |
|----|--------|---------|
| P3.1 | Add Marketplace + AI ProfileGenerator tests | New: `CoreUnitTests/Marketplace/MarketplaceTests.cs`, `CoreUnitTests/AI/AIProfileGeneratorTests.cs` |
| P3.2 | Add FailureAnalytics engine tests | New: `CoreUnitTests/Analytics/FailureAnalyticsEngineTests.cs` |
| P3.3 | Fix `LoadAllProfiles` benchmark sealed class error | `Benchmarks/LoadAllProfiles.cs` |

---

## Execution Order

1. **Fix Bug 1** — Register `CircuitBreaker<LLMDecision>` in DI (Phase1ServiceCollectionExtensions.cs)
2. **Fix Bug 3** — Static JsonSerializerOptions in HybridLlmDecisionEngine.cs
3. **Fix Bug 2** — Gut FailureAnalytics.cs to delegate to engine
4. **Build check** — `dotnet build Core/Core.csproj` — must have 0 errors
5. **Commit the SRP refactor** — stage all 7 files together
6. **P1.1** — Fix ProfileMarketplaceService HttpClient DI
7. **P1.2** — Fix token leakage in ProfileMarketplaceService
8. **P1.3** — Path traversal defense in ProfileMarketplaceService
9. **P1.5** — Prompt injection defense in AIProfileGeneratorService
10. **P1.4** — Add BehaviorTree tests
11. **Build + full test run** — `dotnet build && dotnet test`
12. **Commit security + test fixes**
13. **Continue with P2/P3** items (diagnostics tests, movement tests, marketplace tests)

---

## Key Files

| File | Role |
|------|------|
| `Core/Extensions/Phase1ServiceCollectionExtensions.cs` | Add CircuitBreaker<LLMDecision> DI registration |
| `Core/LLM/HybridLlmDecisionEngine.cs` | Fix static JsonSerializerOptions |
| `Core/Analytics/FailureAnalytics.cs` | Gut to delegate to FailureAnalyticsEngine |
| `Core/Resilience/ICircuitBreakerFactory.cs` | Factory to create the circuit breaker |
| `Core/LLM/LLMDecision.cs` | Type used in CircuitBreaker<LLMDecision> |
| `Core/AI/Marketplace/ProfileMarketplaceService.cs` | P1.1/P1.2/P1.3/P2.4 fixes |
| `Core/Extensions/Phase2ServiceCollectionExtensions.cs` | P1.1 HttpClient DI |
| `Core/AI/AIProfileGeneratorService.cs` | P1.5 prompt injection |

---

## Verification

After each commit group:
```bash
dotnet build Core/Core.csproj          # 0 errors required
dotnet build MasterOfPuppets.sln       # Full solution
dotnet test --project CoreUnitTests    # Must stay at 0 failures
```

After security fixes:
- Confirm no `DefaultRequestHeaders.Authorization` on shared HttpClient
- Confirm `SanitizeFileName` uses `Path.GetFileName()`
- Confirm `description` length is capped before LLM prompt construction

Target: raise audit score from 5.3/10 toward 7.5/10.
