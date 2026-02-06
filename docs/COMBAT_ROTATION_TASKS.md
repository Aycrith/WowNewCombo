# Combat Rotation Optimizer — Implementation Tasks

**Reference PRD:** [PRD_COMBAT_ROTATION_OPTIMIZER.md](PRD_COMBAT_ROTATION_OPTIMIZER.md)  
**Total Effort:** 32 hours  
**Priority:** P1 — DPS Enhancement  
**Status:** ⚠️ BACKEND COMPLETE / FRONTEND NEEDS UI/UX POLISH (2026-02-06)

### Build Status
- `dotnet build MasterOfPuppets.sln` — **0 errors**
- `dotnet test CoreUnitTests` — **132/132 passing**

### Handoff Document
**Next Session:** See [HANDOFF_COMBAT_ROTATION_FRONTEND.md](HANDOFF_COMBAT_ROTATION_FRONTEND.md) for frontend integration work (~1.5 hours remaining)

---

## Phase 1: Core Types & Infrastructure (6 hours)

### Task 1.1: Create GameStateSnapshot (1h)

**File:** `Core/CombatRotation/GameStateSnapshot.cs`

Zero-allocation readonly record struct that captures all combat-relevant
state from `PlayerReader`, `AddonBits`, and `CombatLog` each tick.

**Fields:** HealthPercent, ResourcePercent, ResourceCurrent, ResourceMax,
ComboPoints, TargetHealthPercent, GcdRemainingMs, NetworkLatencyMs,
SpellQueueMs, MainHandSwingElapsedMs, MainHandSpeedMs, MobCount,
InCombat, TargetAlive, IsTargetCasting, TickTimestamp

**Verification:**
```bash
dotnet build Core/Core.csproj
```

---

### Task 1.2: Create IRoleStrategy Interface (0.5h)

**File:** `Core/CombatRotation/IRoleStrategy.cs`

Interface defining the pluggable scoring strategy for role-specific
ability evaluation.

```csharp
namespace Core.CombatRotation;

public interface IRoleStrategy
{
    float ScoreAbility(KeyAction action, in GameStateSnapshot state, int sequenceIndex);
    string RoleName { get; }
}
```

---

### Task 1.3: Create ScoreConditionEntry (0.5h)

**File:** `Core/CombatRotation/ScoreConditionEntry.cs`

JSON-serializable model for per-ability conditional score bonuses.

```csharp
namespace Core.CombatRotation;

public sealed class ScoreConditionEntry
{
    public string Condition { get; set; } = string.Empty;
    public float Bonus { get; set; }
}
```

---

### Task 1.4: Create CombatRotationOptimizerOptions (1h)

**File:** `Core/CombatRotation/CombatRotationOptimizerOptions.cs`

Feature flag options class following `BehaviorTreeCombatOptions` pattern.

---

### Task 1.5: Extend FeatureFlagsOptions (0.5h)

**File:** `Core/FeatureFlags/FeatureFlagsOptions.cs`

Add `CombatRotationOptimizer` property of type
`CombatRotationOptimizerOptions` to the `FeatureFlagsOptions` class.

---

### Task 1.6: Extend FeatureFlagService (0.5h)

**File:** `Core/FeatureFlags/FeatureFlagService.cs`

Add `IsCombatRotationOptimizerEnabled` convenience property.

---

### Task 1.7: Update runtime_feature_flags.json (0.5h)

**File:** `BlazorServer/runtime_feature_flags.json`

Add `CombatRotationOptimizer` section with all defaults (`Enabled: false`).

---

### Task 1.8: Create IRotationOptimizer Interface (0.5h)

**File:** `Core/CombatRotation/IRotationOptimizer.cs`

```csharp
namespace Core.CombatRotation;

public interface IRotationOptimizer
{
    void Optimize(KeyAction[] keys, in GameStateSnapshot state, Span<int> sortedIndices);
    void RecordCastResult(KeyAction action, bool success);
    bool IsEnabled { get; }
}
```

---

### Task 1.9: Extend KeyAction with Weight/ScoreConditions (1h)

**File:** `Core/ClassConfig/KeyAction.cs`

Add optional `Weight` (float, default 1.0f) and `ScoreConditions`
(List<ScoreConditionEntry>, default empty) properties. Ensure JSON
deserialization handles missing fields gracefully.

**Verification:**
```bash
dotnet build Core/Core.csproj
dotnet test CoreUnitTests  # all existing tests still pass
```

---

## Phase 2: Scoring Engine (8 hours)

### Task 2.1: Create DpsRoleStrategy (4h)

**File:** `Core/CombatRotation/DpsRoleStrategy.cs`

Implements `IRoleStrategy` with the weighted-sum scoring algorithm:
- BaseWeight × CooldownReadyBonus × UsabilityGate
- + ResourceEfficiencyBonus + BuffSynergyBonus
- + DebuffMaintenanceBonus + ExecutePhaseBonus
- - OvercapPenalty + SequencePositionTiebreaker

All computation uses `in GameStateSnapshot` — zero allocation.

---

### Task 2.2: Create AbilityScorer (2h)

**File:** `Core/CombatRotation/AbilityScorer.cs`

Static helper that evaluates `ScoreConditions` entries using the existing
`RequirementFactory` expression parser. Returns aggregate bonus score
for a given ability.

---

### Task 2.3: Create RotationOptimizer (2h)

**File:** `Core/CombatRotation/RotationOptimizer.cs`

Main orchestrator implementing `IRotationOptimizer`:
1. Checks `FeatureFlagService.IsCombatRotationOptimizerEnabled`
2. Captures `GameStateSnapshot`
3. Scores all abilities via `IRoleStrategy`
4. Produces sorted indices in caller-provided `Span<int>`
5. Falls back to original order on any exception

Uses `[SkipLocalsInit]` and `stackalloc` for zero-allocation sorting.

---

## Phase 3: Integration (6 hours)

### Task 3.1: Create CombatRotationServiceExtensions (1h)

**File:** `Core/CombatRotation/CombatRotationServiceExtensions.cs`

DI extension method `AddCombatRotationOptimizer()` registering all services.

```csharp
public static IServiceCollection AddCombatRotationOptimizer(
    this IServiceCollection services)
{
    services.TryAddSingleton<IRoleStrategy, DpsRoleStrategy>();
    services.TryAddSingleton<IRotationOptimizer, RotationOptimizer>();
    services.TryAddSingleton<RotationMetricsCollector>();
    services.AddHostedService(
        sp => sp.GetRequiredService<RotationMetricsCollector>());
    return services;
}
```

---

### Task 3.2: Integrate into CombatGoal (2h)

**File:** `Core/Goals/CombatGoal.cs`

Inject `IRotationOptimizer` via constructor. Add ~10 lines in `Update()`:
- Snapshot state from `PlayerReader` + `AddonBits`
- If optimizer enabled, score and get sorted indices
- Iterate `Keys` in sorted order instead of sequential
- Guarded by `IsEnabled` check — zero overhead when disabled

---

### Task 3.3: Register in GoalFactory (0.5h)

**File:** `Core/GoalsFactory/GoalFactory.cs`

Forward `IRotationOptimizer` from parent container into scoped container so
`CombatGoal` can receive it via constructor injection.

---

### Task 3.4: Register in BlazorServer/Program.cs (0.5h)

**File:** `BlazorServer/Program.cs`

Add `services.AddCombatRotationOptimizer()` after `AddPhase1Features()`.

---

### Task 3.5: Register in HeadlessServer/Program.cs (0.5h)

**File:** `HeadlessServer/Program.cs`

Add `services.AddCombatRotationOptimizer()` call.

---

### Task 3.6: Create RotationMetrics Types (0.5h)

**File:** `Core/CombatRotation/RotationMetrics.cs`

Data types for metrics: `AbilityUsageStat`, `RotationSessionMetrics`.

---

### Task 3.7: Create RotationMetricsCollector (1h)

**File:** `Core/CombatRotation/RotationMetricsCollector.cs`

Singleton `IHostedService` that accumulates per-tick cast decisions,
ability usage counts, and flushes to JSON log at configurable interval.

---

## Phase 4: Frontend (4 hours)

### Task 4.1: Create CombatRotationAdminService (1.5h)

**File:** `Frontend/Services/CombatRotationAdminService.cs`

Following `HumanizationAdminService` pattern: reads/writes
`CombatRotationOptimizer` section of `runtime_feature_flags.json`.
Atomic file write via temp file + move.

---

### Task 4.2: Create CombatRotationSettings.razor (2h)

**File:** `Frontend/Pages/CombatRotationSettings.razor`

Blazor page at `/combat-rotation`:
- Enable/disable toggle
- Parameter tuning (BaseWeightMultiplier, resource forecasting, swing timer)
- Live metrics dashboard (ability usage, estimated efficiency)
- Save button writes via admin service

---

### Task 4.3: Register Frontend Services (0.5h)

**File:** `Frontend/DependencyInjection.cs`

Add `services.AddSingleton<CombatRotationAdminService>()`.

---

## Phase 5: Testing & Benchmarks (6 hours)

### Task 5.1: GameStateSnapshot Tests (1h)

**File:** `CoreUnitTests/CombatRotation/GameStateSnapshotTests.cs`

Test struct creation, default values, immutability.

---

### Task 5.2: AbilityScorer Tests (1.5h)

**File:** `CoreUnitTests/CombatRotation/AbilityScorerTests.cs`

Test scoring logic: cooldown gating, resource bonuses, execute phase,
buff synergy, overcap penalty.

---

### Task 5.3: RotationOptimizer Tests (1.5h)

**File:** `CoreUnitTests/CombatRotation/RotationOptimizerTests.cs`

Test sorting correctness, fallback behavior, disabled passthrough.

---

### Task 5.4: Backward Compatibility Tests (1h)

**File:** `CoreUnitTests/CombatRotation/BackwardCompatibilityTests.cs`

Load all 110 `Json/class/` profiles, verify no deserialization errors.
Verify default weights produce original sequence ordering.

---

### Task 5.5: Scoring Benchmark (1h)

**File:** `Benchmarks/CombatRotation/ScoringBenchmark.cs`

BenchmarkDotNet test: score 20-ability rotation. Target: < 50μs, 0 allocs.

---

## Phase 6: Sample Profiles & Documentation (2 hours)

### Task 6.1: Update Sample Profiles (1h)

Update 2-3 `Json/class/` profiles with `Weight` and `ScoreConditions`
entries as reference implementations (Warrior, Rogue, Mage).

---

### Task 6.2: Verify Full Build (1h)

```bash
dotnet build MasterOfPuppets.sln
dotnet test
dotnet run --project Benchmarks -c Release -- --filter "*ScoringBenchmark*"
```

Manual validation:
- [ ] Launch BlazorServer, navigate to `/combat-rotation`
- [ ] Verify toggle shows "Disabled" by default
- [ ] Enable toggle → verify `runtime_feature_flags.json` updated within 2s
- [ ] Run a combat session → verify abilities fire and metrics appear
- [ ] Disable toggle mid-combat → verify immediate fallback to static priority
- [ ] Load a profile without `Weight`/`ScoreConditions` → verify identical behavior
