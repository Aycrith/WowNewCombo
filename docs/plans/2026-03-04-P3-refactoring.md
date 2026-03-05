# P3: Refactoring and Maintainability

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Improve code maintainability through typo fixes, partial-class splits, documentation, and dead-code cleanup. Zero behavior changes in all tasks.

**Priority:** P3 — LOW risk, LOW effort, meaningful developer experience improvements

---

## P3-1: Fix Typo SimplyfyRouteToWaypoint → SimplifyRouteToWaypoint

### Context

`Core/GoalsComponent/Navigation.cs` has a misspelled method name:

| Location | Line | Code |
|----------|------|------|
| Call site | 606 | `SimplyfyRouteToWaypoint();` |
| Definition | 916 | `private void SimplyfyRouteToWaypoint()` |
| Internal log | 935 | references the name in a debug string |

### Implementation

**Step 1: Verify all occurrences**
```bash
grep -n "SimplyfyRouteToWaypoint" Core/GoalsComponent/Navigation.cs
```

**Step 2: Rename in Navigation.cs** — change all 3 occurrences from `SimplyfyRouteToWaypoint` to `SimplifyRouteToWaypoint`.

**Step 3: Verify build catches any missed references**
```bash
dotnet build Core
```

**Step 4: Run tests**
```bash
dotnet test MasterOfPuppets.sln --verbosity minimal
```

**Step 5: Commit**
```bash
git add Core/GoalsComponent/Navigation.cs
git commit -m "fix(nav): correct SimplyfyRouteToWaypoint typo to SimplifyRouteToWaypoint"
```

---

## P3-2: Split Navigation.cs into Partial-Class Files by Region

### Context

`Core/GoalsComponent/Navigation.cs` is 1,425 lines. The class declaration at line 26:
```csharp
public sealed partial class Navigation : IDisposable
```

Three `#region` blocks to extract:
- `#region Logging` (line 1115) — ~38 lines of `[LoggerMessage]` source-generator attributes
- `#region Humanization` (line 1155) — ~3 lines of humanization integration
- `#region HazardAvoidance` (line 1159) — 266+ lines of hazard avoidance logic

**This is purely a file organization change. Zero behavior change.**

### Files

- Modify: `Core/GoalsComponent/Navigation.cs` (remove moved sections)
- Create: `Core/GoalsComponent/Navigation.Logging.cs`
- Create: `Core/GoalsComponent/Navigation.HazardAvoidance.cs`

### Implementation

**Step 1: Create `Navigation.Logging.cs`**

```csharp
using Microsoft.Extensions.Logging;

namespace Core.Goals;

public sealed partial class Navigation
{
    // Logging infrastructure — [LoggerMessage] source-generator attributes
    // Moved from Navigation.cs #region Logging (lines 1115-1153)
    // EventId range: 0040-0045

    [LoggerMessage(EventId = 0040, Level = LogLevel.Warning,
        Message = "...")]  // PASTE EXACT CONTENT FROM Navigation.cs lines 1117-1153
    private partial void LogPathfinderFailed(string reason);

    // ... copy remaining 5 LoggerMessage attributes verbatim
}
```

**IMPORTANT:** Copy the EXACT content of `#region Logging` from `Navigation.cs` — do not paraphrase or reconstruct. Read the lines and paste exactly.

**Step 2: Create `Navigation.HazardAvoidance.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Extensions.Logging;

namespace Core.Goals;

public sealed partial class Navigation
{
    // Hazard avoidance methods
    // Moved from Navigation.cs #region HazardAvoidance (lines 1159-1425)

    // PASTE EXACT CONTENT FROM Navigation.cs lines 1159-1425
}
```

**Step 3: In Navigation.cs, delete lines 1113-1425 (all three regions)**

This leaves the main file at ~1,112 lines.

**Step 4: Build**
```bash
dotnet build Core
```
Expected: 0 errors. Partial classes are compiler-merged automatically — the `[LoggerMessage]` partial methods generate code that references fields in the main file.

**Step 5: Run tests**
```bash
dotnet test MasterOfPuppets.sln --verbosity minimal
```

**Step 6: Commit**
```bash
git add Core/GoalsComponent/Navigation.cs Core/GoalsComponent/Navigation.Logging.cs Core/GoalsComponent/Navigation.HazardAvoidance.cs
git commit -m "refactor(nav): split Navigation.cs into focused partial-class files - Logging and HazardAvoidance"
```

### Verification

After splitting, the three files should compile to the same assembly output. Check with:
```bash
dotnet build Core --verbosity diagnostic 2>&1 | grep "Navigation"
```

---

## P3-3: Document Re-enable Criteria for Disabled Feature Flags

### Context

`BlazorServer/runtime_feature_flags.json` has 4 disabled features with no explanation of when/how to re-enable them. This creates operational debt.

**Disabled features:**
1. `HazardAvoidance` (lines 30-40) — `"Enabled": false`
2. `Humanization` (lines 41-73) — `"Enabled": false`
3. `BehaviorTreeCombat` (lines 94-99) — `"Enabled": false`
4. `CombatRotationOptimizer` (lines 126-135) — `"Enabled": false`

### Implementation

**File:** `BlazorServer/runtime_feature_flags.json`

Add `"Description"` fields to each disabled feature:

For **HazardAvoidance**:
```json
"HazardAvoidance": {
  "Enabled": false,
  "Description": "DISABLED: Re-enable after completing a 60+ minute NavSoak session with RepeatStuckRate < 0.1 and no FrontBypassBreakerActive events. Verify with: POST /api/diagnostics/flush-soak → check logs/soak-nav-*.json windows.",
  "DBSCANEpsilon": 12.0,
  ...existing fields...
}
```

For **Humanization**:
```json
"Humanization": {
  "Enabled": false,
  "Description": "DISABLED: Validated at Level 1-3 TBC Warlock (2026-02-28). Re-enable after verifying InputSecurity + Humanization interaction on current WoW client build. Test with 30+ minute soak, confirm no key-repeat detection.",
  ...existing fields...
}
```

For **BehaviorTreeCombat**:
```json
"BehaviorTreeCombat": {
  "Enabled": false,
  "Description": "DISABLED: FallbackToGOAP=true provides safety net. Re-enable only after all 19 CoreUnitTests/EndToEnd/Scenarios/ tests pass with this feature on. Currently experimental.",
  "FallbackToGOAP": true
}
```

For **CombatRotationOptimizer**:
```json
"CombatRotationOptimizer": {
  "Enabled": false,
  "Description": "DISABLED: ResourceForecasting is experimental. Re-enable after ScoringBenchmark shows < 5% regression (dotnet run --project Benchmarks -c Release --filter ScoringBenchmark). Check kill rate vs baseline.",
  "MetricsIntervalMs": 30000,
  "EnableResourceForecasting": true
}
```

**Step 1: Edit the JSON file**

Add `"Description"` as the first property inside each disabled feature block.

**Step 2: Validate JSON is parseable**
```bash
dotnet build BlazorServer
```
(Any JSON syntax error will cause deserialization failure at startup.)

**Step 3: Commit**
```bash
git add BlazorServer/runtime_feature_flags.json
git commit -m "docs(flags): add re-enable criteria descriptions for HazardAvoidance, Humanization, BehaviorTreeCombat, CombatRotationOptimizer"
```

---

## P3-4: Document GoapAgent.UpdateWorldState Bitfield

### Context

`Core/GOAP/GoapAgent.cs:389-447` — `UpdateWorldState()` packs 24+ boolean flags with no grouping or documentation. Adding XML docs enables future developers to safely add/modify GoapKey assignments.

### Implementation

**File:** `Core/GOAP/GoapAgent.cs`

**Step 1: Read UpdateWorldState() lines 389-447 to inventory all GoapKey assignments**

**Step 2: Add XML summary before the method**

```csharp
/// <summary>
/// Packs the current game state into <see cref="worldStateBits"/> for GOAP planner evaluation.
/// Called every planning tick. All GoapKey bit assignments happen here.
/// <para>
/// Key groups:
/// </para>
/// <list type="bullet">
/// <item><description><b>Target:</b> HasTarget, TargetIsAlive, TargetIsDead, InMeleeRange, InRangedRange, AutoAttacking</description></item>
/// <item><description><b>Combat:</b> Combat, DamageTaken, DamageDone, DangerCombat, IsAlive</description></item>
/// <item><description><b>Loot/Interact:</b> ShouldLoot, Looting, ShouldSkin, Skinning</description></item>
/// <item><description><b>Resources:</b> BagFull, Mana, Health, Energy, Rage, ComboPoints</description></item>
/// <item><description><b>Position/Zone:</b> GoalInRange, WrongZone</description></item>
/// </list>
/// <para>
/// NOTE: Form/stance changes also call <see cref="GoapPlanner.InvalidateCache"/> to prevent
/// stale goal plans from being reused after a form switch.
/// </para>
/// </summary>
private void UpdateWorldState()
```

**Step 3: Add section comments inside the method body**

Group assignments by domain with inline comments:
```csharp
// --- Target state ---
worldStateBits[...] = playerReader.HasTarget;
// ... etc

// --- Combat state ---
worldStateBits[...] = bits.Combat();
// ... etc
```

**Step 4: Build**
```bash
dotnet build Core
```

**Step 5: Commit**
```bash
git add Core/GOAP/GoapAgent.cs
git commit -m "docs(goap): add XML summary and grouped comments to UpdateWorldState bitfield encoding"
```

---

## P3-5: Split DiagnosticsController (Optional — Large File)

### Context

`Frontend/Controllers/DiagnosticsController.cs` (1,664 lines) contains endpoints for multiple concerns. The input-mode endpoints (lines 1600-1664) are the easiest to extract.

**Only do this if time allows — it's the lowest priority P3 task.**

### Implementation

**Step 1: Read DiagnosticsController.cs header (lines 1-50) for constructor parameters and dependencies**

**Step 2: Create `Frontend/Controllers/DiagnosticsFixController.cs`**

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Frontend.Controllers;

/// <summary>Fix/mutating diagnostics endpoints extracted from DiagnosticsController.</summary>
[ApiController]
[Route("api/diagnostics")]
public sealed class DiagnosticsFixController : ControllerBase
{
    private readonly ILogger<DiagnosticsFixController> logger;
    // Add only dependencies used by fix/mutating endpoints.

    public DiagnosticsFixController(
        ILogger<DiagnosticsFixController> logger,
        /* only injected services needed by mailbox/interact, fix/*, and input-mode */)
    {
        this.logger = logger;
    }

    // PASTE moved mutating endpoints from DiagnosticsController.cs:
    // mailbox/interact, fix/*, and input-mode methods.
}
```

**Step 3: Delete moved mutating endpoints from `DiagnosticsController.cs`**

**Step 4: Build and test**
```bash
dotnet build Frontend
dotnet test MasterOfPuppets.sln --verbosity minimal
```

**Step 5: Commit**
```bash
git add Frontend/Controllers/DiagnosticsController.cs Frontend/Controllers/DiagnosticsFixController.cs
git commit -m "refactor(api): split diagnostics read/fix endpoints into DiagnosticsFixController"
```

### Implemented Variant (2026-03-05)

P3-5 was implemented as shown above using `Frontend/Controllers/DiagnosticsFixController.cs`.

- Base route stayed `api/diagnostics`.
- All fix/mutating endpoints were moved (`mailbox/interact`, `fix/*`, and `input-mode`).
- API paths remained unchanged after the extraction.
- `DiagnosticsController.TryNormalizeSupportedSlashCommand` intentionally stayed public static
  for cross-controller safe-list validation.

---

## Execution Order

```
P3-1 (typo fix, 1 min)
  → P3-2 (partial class split, 5 min)
    → P3-3 (feature flag docs, 3 min)
      → P3-4 (GoapAgent XML docs, 3 min)
        → P3-5 (controller split, 5 min — optional)
```

All tasks are independent and can be done in any order. Full test suite after each commit:
```bash
dotnet test MasterOfPuppets.sln --verbosity minimal
```
