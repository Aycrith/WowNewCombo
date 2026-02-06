# PRD: Combat Rotation Optimizer System

**Version:** 1.0  
**Date:** 2026-02-05  
**Status:** Implementation  
**Priority:** P1 — DPS Enhancement  
**Total Effort:** 32 hours

---

## Executive Summary

A feature-flagged, modular combat rotation optimization system that overlays dynamic weighted scoring onto the existing static `KeyAction[]` priority list. The system operates as a DPS-only specialist module within the existing GOAP combat goal, using the proven **SimulationCraft APL (Action Priority List) pattern** — scoring each ability per-tick against real-time game state (resources, cooldowns, buffs/debuffs, swing timers, GCD) instead of relying solely on static sequence order. It integrates via the existing `FeatureFlagService` hot-reload infrastructure, a new Blazor settings page following the `HumanizationSettings.razor` pattern, and inline extensions to existing `KeyAction` properties in `Json/class/` profiles. Initial scope is single-target DPS only; AoE switching and tank/healer roles are deferred to Phase 2+.

**Key Design Decisions:**
- **Weighted scoring overlay** — not a priority queue replacement — preserves JSON profile ordering as the default tiebreaker
- **Inline profile extension** — new optional `Weight`, `Priority`, and `ScoreConditions` fields on `KeyAction`, backward-compatible (old profiles run unmodified)
- **Single-target DPS only** — AoE deferred to Phase 2
- **Blazor dashboard + structured JSON log** for metrics output

---

## Industry Research & References

### SimulationCraft APL (Action Priority List) Pattern
- **Source:** [SimulationCraft Wiki — Action Lists](https://github.com/simulationcraft/simc/wiki/ActionLists) [1]
- **Pattern:** Priority-based action list with conditional expressions. Actions are evaluated top-to-bottom; first available action is executed. The APL model is the industry standard for WoW rotation modeling.
- **Key concepts adopted:**
  - Conditional expressions on abilities (`if=` conditions → `ScoreConditions`)
  - Resource pooling and forecasting (`pool_resource`, `energy.time_to_max`)
  - Cooldown-aware sequencing (`cooldown.X.remains`, `cooldown.X.ready`)
  - Spell queue window (`enable_spell_queue`, `spell_queue_window=400`)
  - Swing timer alignment (`swing.mh.remains`)
  - Action sub-lists for phase-based rotation switching

### SimulationCraft Conditional Expressions
- **Source:** [SimulationCraft Wiki — Action List Conditional Expressions](https://github.com/simulationcraft/simc/wiki/Action-List-Conditional-Expressions) [2]
- **Pattern:** Rich expression language for buff/debuff/cooldown/resource conditions. Our `RequirementFactory` already implements a subset; `ScoreConditions` extends this for weighted scoring.

### xivanalysis Module Architecture
- **Source:** [xivanalysis/xivanalysis](https://github.com/xivanalysis/xivanalysis) [3]
- **Pattern:** Modular per-job analysis with dependency-ordered modules and a core module group providing shared functionality. Each job module operates independently.
- **Key concepts adopted:**
  - `IRoleStrategy` plugin pattern — each role is an independent module
  - Core module provides shared state (analogous to `GameStateSnapshot`)
  - Job-specific modules extend core with specialized logic

### GOAP (Goal-Oriented Action Planning) Integration
- **Source:** Existing codebase `Core/GOAP/` [4]
- **Pattern:** The optimizer operates within, not against, the existing GOAP system. `CombatGoal` remains a GOAP goal; the optimizer only reorders its internal `KeyAction[]` iteration.

---

## User Stories

| ID | Story | Acceptance Criteria |
|----|-------|---------------------|
| US-1 | As a user, I want to enable/disable the rotation optimizer from the web UI without restarting | Toggle on settings page writes to `runtime_feature_flags.json`; `FeatureFlagService` hot-reloads within 2s; combat seamlessly falls back to static priority |
| US-2 | As a user, I want the optimizer to improve my DPS output without changing my class profile | Default weights produce measurably ≥ baseline DPS using existing `Combat.Sequence` ordering as baseline |
| US-3 | As a user, I want to see rotation performance metrics (estimated DPS, ability usage, efficiency) | Blazor page shows live metrics; JSON log captures per-session rotation stats |
| US-4 | As a profile author, I want to add weighted scoring rules to my class profiles | New optional `Weight`, `Priority`, `ScoreConditions` properties on `KeyAction`; old profiles without these fields work identically to current behavior |
| US-5 | As a developer, I want to extend the system for tank/healer roles in the future | `IRoleStrategy` interface with `DpsRoleStrategy` implementation; extension points documented |
| US-6 | As a user, I want the optimizer to respect GCD, spell queue window, and network latency | Scoring integrates `PlayerReader.GCD`, `NetworkLatency`, `SpellQueueTimeMs`; never queues abilities that can't fire |

---

## Functional Requirements

| ID | Requirement | Priority | Implementation Notes |
|----|-------------|----------|---------------------|
| FR-1 | Ability scoring engine evaluates each `KeyAction` per tick | P0 | Extend `CombatGoal.Update()` loop — when flag enabled, sort eligible `KeyAction[]` by computed score before iteration |
| FR-2 | Scoring function inputs: cooldown state, resource %, buff/debuff timers, swing timer, GCD, combo points, target HP% | P0 | All data available via `PlayerReader`, `ActionBarCooldownReader`, `AuraTimeReader`, `BuffStatus`, `CombatLog` |
| FR-3 | Feature flag toggle with hot-reload | P0 | New `CombatRotationOptimizerOptions` in `FeatureFlagsOptions.cs`, following `BehaviorTreeCombatOptions` pattern |
| FR-4 | Graceful fallback to static priority when disabled or on error | P0 | `FallbackToStaticPriority = true` default; try/catch in scorer returns original order on any exception |
| FR-5 | Per-KeyAction weight/priority/score conditions in JSON profiles | P1 | New optional properties on `KeyAction`; deserialized normally, default values ensure backward compat |
| FR-6 | Cooldown tracking with server-side awareness | P0 | Use existing `ActionBarCooldownReader` (cell 37) + `KeyAction.OnCooldown()` |
| FR-7 | Resource forecasting (simple linear projection) | P1 | Project resource at GCD-end: `currentResource + regenRate × gcdRemaining` |
| FR-8 | Buff/debuff-aware priority boosting | P1 | Score boost when relevant buff is expiring (`AuraTimeReader.GetRemainingTimeMs()`) or debuff is missing on target |
| FR-9 | Performance metrics collection and reporting | P1 | `RotationMetricsCollector` singleton; writes JSON log per session; Blazor page reads live |
| FR-10 | Configurable rotation profiles per class/spec | P1 | Existing `Json/class/` profiles extended inline; profile loading already handles optional JSON fields |
| FR-11 | Swing timer alignment for instant abilities | P2 | Score boost for instants that don't clip auto-attack (`MainHandSwing.ElapsedMs()`, `MainHandSpeedMs()`) |
| FR-12 | Extensible role strategy interface | P2 | `IRoleStrategy` with `ScoreAbility()` method; `DpsRoleStrategy` as initial implementation |

---

## Non-Functional Requirements

| ID | Requirement | Target | Validation Method |
|----|-------------|--------|-------------------|
| NFR-1 | Scoring overhead per tick | < 50μs for 20-ability rotation | BenchmarkDotNet in `Benchmarks/` project |
| NFR-2 | Zero allocations in hot path | No GC pressure per tick | `[SkipLocalsInit]`, `Span<T>`, struct scoring, `stackalloc` |
| NFR-3 | Backward compatibility | All 110 existing profiles load without changes | Integration test loading all `Json/class/` profiles |
| NFR-4 | Feature flag toggle latency | < 2 seconds from UI change to behavior change | Manual test via UI toggle |
| NFR-5 | No overhead when disabled | Zero perf cost when flag off | Benchmark comparison: disabled vs no-module |
| NFR-6 | Thread safety | Scoring state immutable per tick; no cross-thread mutation | Code review + stress test |

---

## Technical Specifications

### Architecture Diagram

```
┌─────────────────────────────────────────────────────┐
│                    GoapAgent Thread                  │
│  ┌──────────────────────────────────────────────┐   │
│  │ CombatGoal.Update()                          │   │
│  │  ┌─────────────┐   ┌──────────────────────┐  │   │
│  │  │ Feature Flag │──▶│ RotationOptimizer    │  │   │
│  │  │   Check      │   │  .Optimize()         │  │   │
│  │  └──────┬──────┘   │  ┌────────────────┐  │  │   │
│  │         │ disabled  │  │ AbilityScorer  │  │  │   │
│  │         ▼           │  │  .Score(key,   │  │  │   │
│  │  Original KeyAction │  │   gameState)   │  │  │   │
│  │  iteration order    │  └────────────────┘  │  │   │
│  │                     │  ┌────────────────┐  │  │   │
│  │                     │  │ GameStateSnap  │  │  │   │
│  │                     │  │  shot (struct) │  │  │   │
│  │                     │  └────────────────┘  │  │   │
│  │                     └──────────┬───────────┘  │   │
│  │                                │ sorted       │   │
│  │                                ▼              │   │
│  │                     CastingHandler            │   │
│  │                      .CastIfReady()           │   │
│  └──────────────────────────────────────────────┘   │
│                                                     │
│  ┌─────────────────┐  ┌────────────────────────┐    │
│  │ MetricsCollector│  │ IRoleStrategy          │    │
│  │  .Record()      │  │  └─ DpsRoleStrategy    │    │
│  └────────┬────────┘  │  └─ (future: Tank...)  │    │
│           │           └────────────────────────┘    │
└───────────┼─────────────────────────────────────────┘
            ▼
   ┌────────────────┐   ┌──────────────────────┐
   │ JSON Log File  │   │ Blazor Settings Page │
   │ (per session)  │   │  + Metrics Dashboard │
   └────────────────┘   └──────────────────────┘
```

### Scoring Algorithm

The scoring function computes a per-tick priority score for each `KeyAction` using a weighted-sum model following SimulationCraft's APL conditional expression philosophy (SimC Wiki [1][2]):

```
Score(ability, state) =
    BaseWeight(ability)                          // From JSON profile or 1.0 default
  × CooldownReadyBonus(ability)                  // 1.0 if ready, 0.0 if on CD
  × UsabilityGate(ability)                       // 1.0 if WhenUsable passes, 0.0 otherwise
  + ResourceEfficiencyBonus(ability, state)       // Higher score for resource-efficient abilities
  + BuffSynergyBonus(ability, state)              // Boost if relevant buff is active
  + DebuffMaintenanceBonus(ability, state)         // Boost if debuff missing/expiring on target
  + ExecutePhaseBonus(ability, state)             // Boost for execute abilities when target < threshold
  + SwingTimerAlignmentBonus(ability, state)       // (P2) Boost for instants that don't clip auto
  - OvercapPenalty(ability, state)                 // Penalize if resource would be wasted
  + SequencePositionTiebreaker(ability)            // Original JSON index / 1000 for deterministic ordering
```

Abilities with `CanRun() == false` get `float.MinValue` and are skipped. The `SequencePositionTiebreaker` ensures that when scores are equal, the original JSON ordering prevails — **guaranteeing identical behavior with default weights**.

### New File Structure

```
Core/
  CombatRotation/
    IRotationOptimizer.cs               # Interface for optimizer
    RotationOptimizer.cs                # Main scoring orchestrator
    AbilityScorer.cs                    # Scores individual KeyAction
    GameStateSnapshot.cs                # readonly struct, per-tick state capture
    RotationMetricsCollector.cs         # Metrics aggregation
    RotationMetrics.cs                  # Metrics data types
    DpsRoleStrategy.cs                  # DPS scoring implementation
    IRoleStrategy.cs                    # Interface for role-specific scoring
    CombatRotationOptimizerOptions.cs   # Options class for feature flag
    ScoreConditionEntry.cs              # JSON model for score conditions
    CombatRotationServiceExtensions.cs  # DI registration extension
Frontend/
  Pages/
    CombatRotationSettings.razor        # Settings + metrics dashboard page
  Services/
    CombatRotationAdminService.cs       # Writes optimizer config to runtime JSON
CoreUnitTests/
  CombatRotation/
    AbilityScorerTests.cs
    RotationOptimizerTests.cs
    GameStateSnapshotTests.cs
    BackwardCompatibilityTests.cs
Benchmarks/
  CombatRotation/
    ScoringBenchmark.cs
```

### KeyAction JSON Extensions

New optional fields on each `KeyAction` entry in `Combat.Sequence`:

```json
{
  "Name": "Heroic Strike",
  "Key": "2",
  "WhenUsable": true,
  "Weight": 1.5,
  "ScoreConditions": [
    { "Condition": "Rage > 60", "Bonus": 0.5 },
    { "Condition": "TargetHealth% < 20", "Bonus": 1.0 }
  ]
}
```

- `Weight` (float, default: 1.0) — base scoring weight
- `ScoreConditions` (array, default: empty) — conditional score bonuses evaluated via existing `RequirementFactory` expression parser

Profiles **without** these fields behave identically to current behavior.

### Feature Flag Configuration

Added to `runtime_feature_flags.json`:

```json
{
  "CombatRotationOptimizer": {
    "Enabled": false,
    "FallbackToStaticPriority": true,
    "BaseWeightMultiplier": 1.0,
    "EnableMetrics": true,
    "EnableResourceForecasting": true,
    "EnableSwingTimerAlignment": false,
    "MetricsFlushIntervalSeconds": 30,
    "MetricsOutputPath": "logs/rotation_metrics.json"
  }
}
```

### Backward Compatibility Guarantees

| Contract | Guarantee | Mechanism |
|----------|-----------|-----------|
| Existing profiles load without modification | 100% | New `KeyAction` properties have defaults; JSON deserialization ignores missing optional fields |
| Disabled optimizer = identical behavior | 100% | `IsEnabled` check before any scoring; original `Keys` span used directly |
| `CastingHandler` API unchanged | 100% | Optimizer only reorders the span before the existing iteration loop |
| `GoapGoal` interface unchanged | 100% | No changes to `GoapGoal`, `GoapPlanner`, `GoapAgent` |
| Save data structures preserved | 100% | No changes to `LocalGrindSessionDAO`, `SessionStat`, or serialization formats |
| All existing tests pass | 100% | No modification to existing test targets; new tests in new directories |

### Rollback Procedure

1. Set `"CombatRotationOptimizer": { "Enabled": false }` in `runtime_feature_flags.json` — immediate hot-reload, no restart
2. If code rollback needed: revert the single conditional branch in `CombatGoal.Update()` (3 lines) and remove DI registration call (1 line)

---

## Extension Points for Future Phases

| Phase | Feature | Extension Mechanism |
|-------|---------|---------------------|
| Phase 2 | AoE/Multi-target switching | New `AoeDpsRoleStrategy : IRoleStrategy` with `MobCount`-based threshold; `GameStateSnapshot` already captures `MobCount` |
| Phase 3 | Tank role support | `TankRoleStrategy : IRoleStrategy` — scores defensives, threat generation, survival abilities |
| Phase 4 | Healer role support | `HealerRoleStrategy : IRoleStrategy` — scores heals based on party health deficit (requires addon extension) |
| Phase 2+ | Proc-aware optimization | Add proc probability tables to `KeyAction`; score boost for proc consumers when proc is active |
| Phase 2+ | Resource forecasting v2 | Non-linear resource prediction using historical regen data from `RotationMetricsCollector` |

---

## References

1. SimulationCraft Wiki — Action Lists. https://github.com/simulationcraft/simc/wiki/ActionLists
2. SimulationCraft Wiki — Action List Conditional Expressions. https://github.com/simulationcraft/simc/wiki/Action-List-Conditional-Expressions
3. xivanalysis — FFXIV Performance Analysis Platform. https://github.com/xivanalysis/xivanalysis
4. WowClassicGrindBot GOAP System. `Core/GOAP/`
