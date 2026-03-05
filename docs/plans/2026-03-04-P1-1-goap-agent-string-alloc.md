# P1-1: Eliminate Hot-Path String Allocations in GoapAgent No-Plan Path

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Cache the goal name array at startup and guard the `RecordNoPlanEvent` block to eliminate per-tick `List<string>` and `WorldState.ToString()` allocations in the GOAP planning loop.

**Priority:** P1 — HIGH performance (allocations in hot path)

**Estimated time:** 5 minutes

---

## Context

### Current code (`Core/GOAP/GoapAgent.cs`)

**Line 127 — AvailableGoals property:**
```csharp
public GoapGoal[] AvailableGoals { get; }
```
This array is **fixed after startup** — goals never change at runtime.

**Lines 350-353 — RecordNoPlanEvent call:**
```csharp
goapEventHistory?.RecordNoPlanEvent(
    WorldState.ToString(),                              // ← allocates string from BitVector32
    AvailableGoals.Select(g => g.Name).ToList(),       // ← allocates List<string> every call
    $"WorldState: {WorldState}");                       // ← allocates interpolated string
```

This block runs every planning tick that produces no plan — the worst-case frequency scenario. The allocations cause GC pauses that increase planning latency.

**The `goapEventHistory` field** is likely a `GoapEventHistory?` — nullable optional service. The `?.` operator already guards the call, but the arguments are **eagerly evaluated** before the null check in C#. This means the allocations happen even when `goapEventHistory` is null.

Wait — actually in C#, `goapEventHistory?.RecordNoPlanEvent(arg1, arg2, arg3)` evaluates the arguments ONLY if `goapEventHistory` is not null. So if `goapEventHistory` is null, there's no allocation. The problem only manifests when it IS non-null (diagnostics mode). **However**, during active soak sessions or debug runs, `goapEventHistory` IS non-null, and these allocations fire at the GOAP rate.

**Existing LoggerMessage attributes (lines 584-607) for reference:**
```csharp
[LoggerMessage(EventId = 0040, Level = LogLevel.Debug,
    Message = "[GoapAgent] New empty goal selected")]
private partial void LogNewEmptyGoal();

[LoggerMessage(EventId = 0041, Level = LogLevel.Debug,
    Message = "[GoapAgent] Goal changed: {GoalName}")]
private partial void LogNewGoal(string goalName);
```

---

## Files

1. **`C:/WowClassicGrindBot/Core/GOAP/GoapAgent.cs`** — cache field + update RecordNoPlanEvent call

---

## Step 1: Find exact location of AvailableGoals assignment

```bash
grep -n "AvailableGoals\s*=" Core/GOAP/GoapAgent.cs
```

Find where `AvailableGoals` is set (constructor or init). This is where we initialize the cache.

## Step 2: Add cached goal names field

After line 127 (`public GoapGoal[] AvailableGoals { get; }`), add:
```csharp
private string[] _availableGoalNames = Array.Empty<string>();
```

## Step 3: Initialize cache where AvailableGoals is assigned

In the constructor or wherever `AvailableGoals = ...` is assigned, add immediately after:
```csharp
_availableGoalNames = AvailableGoals.Select(static g => g.Name).ToArray();
```

Note: `static` lambda avoids closure allocation.

## Step 4: Update RecordNoPlanEvent call (lines 350-353)

Replace:
```csharp
goapEventHistory?.RecordNoPlanEvent(
    WorldState.ToString(),
    AvailableGoals.Select(g => g.Name).ToList(),
    $"WorldState: {WorldState}");
```

With:
```csharp
if (goapEventHistory is not null)
{
    goapEventHistory.RecordNoPlanEvent(
        WorldState.Data.ToString("X8"),     // hex format, no object.ToString() box
        _availableGoalNames,                // cached string[], not reallocated
        string.Empty);                      // no interpolated string
}
```

**Note:** Check the `RecordNoPlanEvent` signature to confirm it accepts `IReadOnlyList<string>` or `string[]`. If it only accepts `List<string>`, either:
- Change the parameter type to `IReadOnlyList<string>` (preferred), OR
- Keep `_availableGoalNames` as `IReadOnlyList<string>` and cast

## Step 5: Verify WorldState.Data property exists

`BitVector32` has a `.Data` property returning `int`. `ToString("X8")` formats it as 8-char hex.

If `WorldState` is not a `BitVector32` directly but a wrapper, find the underlying data property:
```bash
grep -n "WorldState\b" Core/GOAP/GoapAgent.cs | head -20
```

## Step 6: Add LoggerMessage for the no-plan debug event

In the `[LoggerMessage]` block (lines 584-607), add:
```csharp
[LoggerMessage(EventId = 2001, Level = LogLevel.Debug,
    Message = "[GoapAgent] No plan found. WorldState=0x{WorldStateBits:X8} Goals={GoalCount}")]
private partial void LogNoPlanFound(uint worldStateBits, int goalCount);
```

If there's a `logger.LogDebug(...)` near the RecordNoPlanEvent call, replace it with:
```csharp
LogNoPlanFound(unchecked((uint)WorldState.Data), AvailableGoals.Length);
```

## Step 7: Build
```bash
dotnet build Core
```
**Expected:** 0 errors. Source generator compiles the `[LoggerMessage]` partial method.

## Step 8: Full test suite
```bash
dotnet test MasterOfPuppets.sln --verbosity minimal
```

## Step 9: Commit
```bash
git add Core/GOAP/GoapAgent.cs
git commit -m "perf(goap): cache goal name array at startup, eliminate ToList/ToString allocs in no-plan path"
```

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| RecordNoPlanEvent signature incompatible with string[] | Low | Read the method signature first; cast or change param type |
| WorldState.Data cast causes sign issues | Very Low | Use `unchecked((uint)WorldState.Data)` for safe uint conversion |
| _availableGoalNames used before initialization | Very Low | Initialize in same constructor that sets AvailableGoals |
