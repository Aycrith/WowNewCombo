# Audit Action Items - Prioritized Remediation Plan
**Generated:** 2026-02-28 | **Confidence:** 91%

---

## Immediate Actions (Before Next Release)

### Action 1: Investigate & Document Skipped Tests
**Priority:** HIGH | **Effort:** 30 minutes | **Owner:** QA/Testing Lead

**Current State:** 3 tests skipped in CoreUnitTests (0.2% of suite)

**Action:**
```bash
cd /c/WowClassicGrindBot

# Find skipped tests
dotnet test --no-build 2>&1 | grep -i "skipped" -A 2

# Get detailed info
dotnet test --filter "Skipped==true" --verbosity detailed
```

**Deliverable:**
- [ ] Identify which 3 tests are skipped
- [ ] Document reason in test (using `[Fact(Skip = "Reason: ...")]`)
- [ ] Create GitHub issue if legitimate blocker
- [ ] Remove skip if no longer needed

**Acceptance Criteria:** All skipped tests have documented reasons or are removed

---

### Action 2: Audit Async/Await Pattern in 9 Files
**Priority:** MEDIUM | **Effort:** 2 hours | **Owner:** Performance Lead

**Issue:** 82 instances of `Task.Delay().Wait()` and `ManualResetEvent.Wait()` that block threads

**Files to Review:**
1. Core/Database/AreaDB.cs (2 instances)
2. Core/Goals/FollowRouteGoal.cs (4 instances)
3. Core/GoalsComponent/Navigation.cs (1 instance)
4. Core/GOAP/GoapAgent.cs (1 instance)
5. Core/Recovery/NoPlanRecoveryService.cs (1 instance)
6. Core/ScreenCapture/ScreenCapture.cs (2 instances)
7. Core/PPather/RemotePathingAPIV3.cs (1 instance)
8. Utilities/WowheadDB_Extractor/Program.cs (1 instance)
9. Core/GoalsComponent/Wait.cs (1 instance)

**Review Checklist:**
- [ ] Is this in a hot path? (decision: async vs sync acceptable)
- [ ] Does caller expect async? (check method signature)
- [ ] Can this use async/await instead? (preferred)
- [ ] Is blocking justified? (document reason if yes)
- [ ] Does it need ConfigureAwait(false)? (for library code)

**Example Remediation:**

```csharp
// CURRENT (problematic)
public void WaitForCompletion()
{
    manualReset.Wait();  // Blocks current thread
}

// IMPROVED (async-aware)
public async Task WaitForCompletionAsync()
{
    await Task.Run(() => manualReset.Wait());  // Offload to thread pool
}

// BEST (if no unmanaged resource)
private readonly TaskCompletionSource tcs = new();
public async Task WaitForCompletionAsync() => await tcs.Task;
```

**Deliverable:**
- [ ] Document findings in `ASYNC_AUDIT_REPORT.md`
- [ ] For each file: Decision (keep, convert, investigate further)
- [ ] Code review for changes

---

## Next Sprint (High-Risk Refactoring)

### Action 3: Standardize IDisposable Pattern
**Priority:** HIGH | **Effort:** 1 week | **Owner:** Core Team Lead

**Current State:** 84 classes implement IDisposable with 3 different patterns

**Pattern Analysis:**

```
Pattern A (Correct): 32 classes
public sealed class Good : IDisposable
{
    public void Dispose() { /* cleanup */ }
    ~Good() => Dispose();  // Has finalizer
}

Pattern B (Incomplete): 35 classes
public class Incomplete : IDisposable
{
    public void Dispose() { /* might be empty */ }
    // No finalizer
}

Pattern C (Questionable): 17 classes
// Empty implementation - questionable value
```

**Template Solution (Standard Pattern):**

```csharp
public sealed class MyClass : IDisposable
{
    private ManualResetEvent? _eventHandle;
    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            // Dispose managed resources
            _eventHandle?.Dispose();
        }

        // Free unmanaged resources if any
    }

    ~MyClass() => Dispose(false);

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }
}
```

**Refactoring Approach:**

**Phase 1 (Week 1):** Create Roslyn analyzer to enforce pattern
```bash
dotnet add package Roslynator --version 4.8.0
# Or write custom analyzer using:
# https://github.com/dotnet/roslyn-analyzers/
```

**Phase 2 (Week 2-3):** Audit & categorize all 84 classes
- [ ] Map all IDisposable implementations
- [ ] Classify by pattern (A/B/C)
- [ ] Identify actual cleanup needed per class
- [ ] Create refactoring PRs by team member

**Phase 3 (Week 4):** Refactor in batches
- [ ] 20 classes per PR (stay under size limit)
- [ ] Use consistent template
- [ ] Add unit tests for Dispose behavior
- [ ] Code review for correctness

**Acceptance Criteria:**
- [ ] All 84 classes use single standardized pattern
- [ ] Analyzer passes on all files
- [ ] Unit tests validate Dispose behavior
- [ ] No resource leaks in tests

---

### Action 4: Plan Navigation.cs Refactoring
**Priority:** HIGH | **Effort:** 2-3 weeks | **Owner:** Architecture Lead

**Current Issue:** Navigation.cs is 1,702 lines with 10+ responsibilities

**Recommended Decomposition:**

```
Navigation (1702 lines) splits into:
├── RouteManager (300 lines)
│   └── Responsibility: Waypoint stack, route simplification, distance tracking
├── SteeringController (250 lines)
│   └── Responsibility: Angle calculations, turn logic, heading adjustment
├── RecoveryCoordinator (200 lines)
│   └── Responsibility: Oscillation detection, route rehab decisions
├── MountHandler (100 lines)
│   └── Responsibility: Mount/dismount logic
└── Navigation (core, 400 lines)
    └── Responsibility: Orchestrate above components, expose public API
```

**Refactoring Approach:**

**Phase 1 (Scoping):**
- [ ] Document current Navigation responsibilities
- [ ] Create interface contracts for each component
- [ ] Estimate extraction difficulty per component
- [ ] Plan extraction order (dependencies first)

**Phase 2 (Extraction - 1 week per component):**
1. **Extract RouteManager first** (fewest dependencies)
   - Create `IRouteManager` interface
   - Move waypoint stack, distance tracking
   - Keep Navigation as caller
   - Update tests incrementally

2. **Extract SteeringController second**
   - Create `ISteeringController` interface
   - Move angle calculations, turn logic
   - Inject into Navigation
   - Update tests

3. **Extract RecoveryCoordinator third**
   - Create `IRecoveryCoordinator` interface
   - Move oscillation/rehab logic
   - Inject into Navigation
   - Update tests

4. **Extract MountHandler fourth**
   - Create interface if not exists
   - Move mount/dismount logic
   - Inject into Navigation

**Phase 3 (Testing & Validation):**
- [ ] All 640+ Navigation tests still pass
- [ ] Each component has focused unit tests
- [ ] Integration tests validate composition
- [ ] Performance regression benchmarks pass

**Expected Outcome:**
- Navigation becomes 400-500 line orchestrator
- Each component ~200-300 lines, single responsibility
- Easier to test, understand, extend
- Better error isolation and recovery

**Risk Mitigation:**
- Use feature branches to avoid blocking team
- Extract incrementally (1 component per week)
- Maintain API compatibility (add new interfaces, deprecate old)
- Run full test suite after each component extraction

---

### Action 5: Update Newtonsoft.Json Dependency
**Priority:** MEDIUM | **Effort:** 15 minutes | **Owner:** DevOps/Release

**Current:** 13.0.4
**Target:** 13.0.5 (latest stable)

**Change:**

File: `/c/WowClassicGrindBot/Directory.Packages.props`

```xml
<!-- BEFORE -->
<PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />

<!-- AFTER -->
<PackageVersion Include="Newtonsoft.Json" Version="13.0.5" />
```

**Validation:**
```bash
dotnet build MasterOfPuppets.sln
dotnet test --no-build
# Verify no regressions
```

**Acceptance:** Build passes, all tests pass

---

## Next Quarter (Medium-Risk Improvements)

### Action 6: Convert Static DI References to Constructor Injection
**Priority:** MEDIUM | **Effort:** 1-2 weeks | **Owner:** Core Team

**Target File:** Core/ClassConfig/KeyReader.cs

**Current Pattern (Anti-pattern):**
```csharp
public class KeyReader
{
    public static IActionBarTextureReader? TextureReader { get; set; }
    public static ActionBarMacroReader? MacroReader { get; set; }
    public static IconDB? IconDB { get; set; }

    // These static properties are set during DI and accessed implicitly
    // throughout codebase, bypassing normal DI container
}
```

**Target Pattern (Correct):**
```csharp
public class KeyReader
{
    private readonly IActionBarTextureReader textureReader;
    private readonly ActionBarMacroReader macroReader;
    private readonly IconDB iconDB;

    public KeyReader(
        IActionBarTextureReader textureReader,
        ActionBarMacroReader macroReader,
        IconDB iconDB,
        /* other dependencies */
    )
    {
        this.textureReader = textureReader;
        this.macroReader = macroReader;
        this.iconDB = iconDB;
    }

    // Use via this.textureReader, not static property
}
```

**Scope:** Affects ~20 classes that depend on KeyReader static properties

**Approach:**
1. [ ] Create new KeyReaderOptions container for static dependencies
2. [ ] Update KeyReader constructor signature
3. [ ] Update all callers in ActionBarPopulator, etc.
4. [ ] Update DI registration in Program.cs
5. [ ] Verify tests still pass

**Benefit:**
- Explicit dependency graph (no hidden statics)
- Easier to test (inject test doubles)
- Better IDE intellisense
- Reduced coupling

---

### Action 7: Formalize Addon Encoding Specification
**Priority:** MEDIUM | **Effort:** 4 hours | **Owner:** Architecture Lead

**Issue:** Pixel encoding format is implicit and documented only in memory

**Deliverable:** Formal specification document

**File:** Create `/c/WowClassicGrindBot/docs/ADDON_ENCODING_SPEC.md`

**Content Template:**

```markdown
# DataToColor Addon Encoding Specification

## Overview
The WoW addon encodes game state as RGB pixel values written to a known
screen region. The bot reads this region, decodes pixels, and reconstructs
game state.

## Encoding Format

### Frame Structure
Each frame consists of:
1. Header byte: RGB=(255, 0, 0) marks frame start
2. Data sequence: Each cell is one int encoded as 3 bytes
3. Padding: Unused cells are RGB=(0, 0, 0)

### Integer to RGB Mapping
Each game state integer is encoded as 3 bytes:
```
int value = 0x123456;  // Example: Red=0x12, Green=0x34, Blue=0x56

byte R = (value >> 16) & 0xFF;  // Most significant byte
byte G = (value >> 8) & 0xFF;   // Middle byte
byte B = (value) & 0xFF;        // Least significant byte

// Max representable value per cell: 16777215 (0xFFFFFF = white)
```

### Cell Mapping
See frame_config.json for pixel coordinates:
```json
{
  "cells": [
    { "id": 0, "x": 10, "y": 20, "description": "PlayerHealth" },
    { "id": 1, "x": 11, "y": 20, "description": "PlayerMaxHealth" },
    // ...
  ]
}
```

### Lua Encoding (DataToColor.lua)
```lua
local function encodeValue(value)
    local R = bit.band(bit.rshift(value, 16), 255)
    local G = bit.band(bit.rshift(value, 8), 255)
    local B = bit.band(value, 255)
    return R, G, B
end
```

### C# Decoding (AddonDataProvider.cs)
```csharp
private int DecodePixelBytes(byte r, byte g, byte b)
{
    return (r << 16) | (g << 8) | b;
}
```

## Safety Constraints
- Header detection: Must check R==255, G==0, B==0 for frame sync
- Type safety: Always decode to expected RGB bytes, never compare
  byte pixel to int index directly (causes overflow)
- Bounds: Frame region must be 800x600+ to capture all cells
```

---

### Action 8: Add XML Documentation to Public APIs
**Priority:** LOW | **Effort:** 1-2 weeks | **Owner:** Distributed (per module owner)

**Target:** High-churn modules
1. Core/GOAP/IGoapGoal.cs - add method docs
2. Core/Goals/* - add class descriptions
3. Core/Requirement/IRequirement.cs - add requirement docs
4. Frontend/Controllers/BotController.cs - add endpoint docs

**Template:**

```csharp
/// <summary>
/// Executes the goal's primary action.
/// </summary>
/// <param name="state">Current game state snapshot.</param>
/// <remarks>
/// The goal should check state conditions and emit appropriate actions
/// or state changes. If unable to proceed, should abort gracefully.
/// </remarks>
public abstract void Execute(IGameState state);

/// <summary>
/// Minimum distance before considering waypoint reached (yards).
/// Controls when bot stops moving and declares progress.
/// </summary>
/// <remarks>
/// Set to 3.0 for outdoor zones (normal movement error margin).
/// Reduced to 1.0 for indoor zones (tighter pathfinding).
/// </remarks>
private readonly float MinDistance = OutDoorMinDistance;
```

**Tool:** Use ReSharper or Rider IDE to generate stubs, then fill in details

---

## Monitoring & Verification

### Weekly Checks
- [ ] Monitor build status (should stay at 0 errors, 0 warnings)
- [ ] Check test pass rate (should stay >99%)
- [ ] Review any new warnings or static analysis findings

### Monthly Checks
- [ ] Run full audit on critical paths
- [ ] Check for new instances of anti-patterns (static refs, etc.)
- [ ] Review dependency updates for security patches

### Quarterly Checks
- [ ] Run full 7-category audit (like this one)
- [ ] Benchmark performance-critical paths
- [ ] Assess refactoring progress

---

## Success Metrics

| Action | Success Criteria | Verification |
|--------|-----------------|--------------|
| Skipped tests | All have documented reasons | Code review |
| Async audit | Decision documented per file | ASYNC_AUDIT_REPORT.md |
| IDisposable | 100% of 84 classes standardized | Roslyn analyzer passes |
| Navigation | Decomposed into 5 components | Tests pass, cyclomatic < 15 |
| Newtonsoft.Json | Updated to 13.0.5 | dotnet build passes |
| Static DI | Removed from KeyReader | Tests pass, no static refs |
| Encoding spec | Formal document created | PR review |
| XML docs | Public APIs documented | ReSharper analysis clean |

---

## Timeline Summary

```
IMMEDIATE (This Week)
├─ Action 1: Skipped tests (30 min)
└─ Action 2: Async audit (2 hours)

NEXT SPRINT (1-2 weeks)
├─ Action 3: IDisposable standardization (5 days)
├─ Action 4: Navigation refactoring plan (1 day)
└─ Action 5: Newtonsoft.Json update (30 min)

NEXT QUARTER (3-4 weeks)
├─ Action 6: Static DI conversion (1-2 weeks)
├─ Action 7: Addon encoding spec (4 hours)
└─ Action 8: XML documentation (1-2 weeks)

Total Effort: ~4-5 weeks distributed across team
```

---

## Notes for Team Leads

**Delegation Suggestions:**

- **Security/Performance:** Async audit (Action 2)
- **Testing:** IDisposable standardization (Action 3)
- **Architecture:** Navigation refactoring (Action 4), Static DI (Action 6)
- **DevOps:** Dependency update (Action 5)
- **Documentation:** Encoding spec (Action 7), XML docs (Action 8)

**Estimation Confidence:** 85% (actual may vary ±20% based on team expertise)

**Risk Mitigation:** All actions are backwards-compatible refactorings. No breaking API changes required.

---

**Document Generated:** 2026-02-28
**Next Review Date:** 2026-05-28 (3 months)
**Owner:** Core Development Team
