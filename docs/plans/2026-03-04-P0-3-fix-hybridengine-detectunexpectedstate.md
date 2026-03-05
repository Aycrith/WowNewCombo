# P0-3: Implement HybridDecisionEngine.DetectUnexpectedState

## STATUS: COMPLETED — commit `cce5e5a78` (2026-03-05)

**What was done:** `DetectUnexpectedState()` now returns `playerReader.HealthPercent() < 20 && !bits.Combat()`. `AddonBits` was injected into `HybridDecisionEngine` as parameter 6 in the constructor. DI registration updated in `Phase3ServiceCollectionExtensions.cs`.
**Tests added:** 4 unit tests in `CoreUnitTests/AI/HybridDecisionEngineTests.cs` (low-health-out-of-combat → true; full-health → false; low-health-in-combat → false; threshold boundary).
**Files modified:** `Core/AI/HybridDecision/HybridDecisionEngine.cs`, `Core/DependencyInjection/Phase3ServiceCollectionExtensions.cs`, new `CoreUnitTests/AI/HybridDecisionEngineTests.cs`.

---

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the always-`false` stub in `DetectUnexpectedState()` with real logic so the enabled `HybridLLMDecision` feature actually activates LLM decision-making for unexpected game states.

**Priority:** P0 — CRITICAL (feature is enabled in production flags but silently does nothing)

**Estimated time:** 5 minutes

---

## Context

`BlazorServer/runtime_feature_flags.json` line 101:
```json
"HybridLLMDecision": {
  "Enabled": true,
  "ConfidenceThreshold": 0.6,
  "MaxLatency": 2000
}
```

`Core/AI/HybridDecision/HybridDecisionEngine.cs` — the `DetectUnexpectedState()` method (~line 202):
```csharp
private bool DetectUnexpectedState()
{
    // TODO: Implement proper state detection
    return false;
}
```

`CalculateGoapConfidence()` (lines 140-164) deducts `-0.3f` from confidence when `DetectUnexpectedState()` returns `true`. Since it always returns `false`, the confidence is always the base `0.8f` minus repeat penalties only — the LLM edge-case threshold (`0.6`) is rarely reached.

**File structure confirmed:**
- `Core/AI/HybridDecision/HybridDecisionEngine.cs` — main class (370 lines)
- `Core/AI/HybridDecision/GameStateSerializer.cs`
- `Core/AI/HybridDecision/LLMResponseParser.cs`
- `Core/AI/LLM/LLMClientFactory.cs`

**Constructor signature** (lines 37-60):
```csharp
public HybridDecisionEngine(
    ILogger<HybridDecisionEngine> logger,
    GoapAgent goapAgent,
    ILLMClientFactory llmFactory,
    IOptions<HybridLLMDecisionOptions> options,
    IPlayerReader playerReader,
    /* possibly IAddonBits bits */)
```

---

## Files

1. **`C:/WowClassicGrindBot/Core/AI/HybridDecision/HybridDecisionEngine.cs`** — implement the method
2. **Create: `C:/WowClassicGrindBot/CoreUnitTests/AI/HybridDecisionEngineTests.cs`** — new test file

---

## Step 1: Read HybridDecisionEngine.cs constructor and field declarations

```bash
# Read lines 1-70 to see imports, fields, constructor
```

Identify:
- What `IPlayerReader` property provides health percent (`HealthPercent`, `Health.Pct`, etc.)
- Whether `IAddonBits` or similar is injected (look for `bits.Combat()` usage elsewhere in the class)
- The exact field names for the injected dependencies

## Step 2: Create test file CoreUnitTests/AI/HybridDecisionEngineTests.cs

```csharp
using Core.AI.HybridDecision;
using Core.Goals;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SharedLib;
using Xunit;

namespace CoreUnitTests.AI;

public sealed class HybridDecisionEngineTests
{
    [Fact]
    public void DetectUnexpectedState_LowHealthOutOfCombat_ReturnsTrue()
    {
        // Arrange — player is at 15% health and NOT in combat
        // This is "unexpected" — health should not drop below 20% outside combat
        Mock<IPlayerReader> readerMock = new();
        readerMock.Setup(r => r.HealthPercent).Returns(15);

        // If IAddonBits is separate, mock it too:
        // Mock<IAddonBits> bitsMock = new();
        // bitsMock.Setup(b => b.Combat()).Returns(false);

        HybridDecisionEngine engine = CreateMinimalEngine(readerMock.Object /*, bitsMock.Object */);

        bool result = InvokeDetectUnexpectedState(engine);

        result.Should().BeTrue(
            "a player at 15% health outside combat is an unexpected state requiring LLM intervention");
    }

    [Fact]
    public void DetectUnexpectedState_FullHealthInCombat_ReturnsFalse()
    {
        Mock<IPlayerReader> readerMock = new();
        readerMock.Setup(r => r.HealthPercent).Returns(100);

        HybridDecisionEngine engine = CreateMinimalEngine(readerMock.Object);

        bool result = InvokeDetectUnexpectedState(engine);

        result.Should().BeFalse("full health in combat is normal — no LLM needed");
    }

    [Fact]
    public void DetectUnexpectedState_LowHealthInCombat_ReturnsFalse()
    {
        // Low health DURING combat is expected — the bot is actively fighting
        Mock<IPlayerReader> readerMock = new();
        readerMock.Setup(r => r.HealthPercent).Returns(10);

        // If bits are separate, mock combat = true:
        // Mock<IAddonBits> bitsMock = new();
        // bitsMock.Setup(b => b.Combat()).Returns(true);

        HybridDecisionEngine engine = CreateMinimalEngine(readerMock.Object /*, inCombat: true */);

        bool result = InvokeDetectUnexpectedState(engine);

        result.Should().BeFalse("low health during active combat is expected, not unexpected");
    }

    // --- Helpers ---

    /// <summary>Creates engine with minimal dependencies for DetectUnexpectedState testing.</summary>
    private static HybridDecisionEngine CreateMinimalEngine(IPlayerReader playerReader)
    {
        // Adjust constructor parameters to match actual HybridDecisionEngine signature
        // Use NullLogger and mock/null out everything not needed for this method
        return new HybridDecisionEngine(
            NullLogger<HybridDecisionEngine>.Instance,
            goapAgent: null!,
            llmFactory: null!,
            options: Options.Create(new HybridLLMDecisionOptions
            {
                ConfidenceThreshold = 0.6f,
                MaxLatency = 2000
            }),
            playerReader: playerReader);
    }

    /// <summary>Invokes the private DetectUnexpectedState via reflection.</summary>
    private static bool InvokeDetectUnexpectedState(HybridDecisionEngine engine)
    {
        System.Reflection.MethodInfo method = typeof(HybridDecisionEngine)
            .GetMethod("DetectUnexpectedState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (bool)method.Invoke(engine, null)!;
    }
}
```

## Step 3: Run to confirm test fails
```bash
dotnet test CoreUnitTests --filter "FullyQualifiedName~HybridDecisionEngineTests" --verbosity detailed
```
**Expected:** `DetectUnexpectedState_LowHealthOutOfCombat_ReturnsTrue` FAILS because method returns `false`.

## Step 4: Implement DetectUnexpectedState in HybridDecisionEngine.cs

Find the stub (~line 202) and replace:

```csharp
// Before:
private bool DetectUnexpectedState()
{
    // TODO: Implement proper state detection
    return false;
}

// After:
private bool DetectUnexpectedState()
{
    // Unexpected = critically low health while NOT in combat.
    // During combat, low health is expected. Outside combat, it indicates
    // a routing failure, fall damage, or environment hazard — LLM can help.
    return _playerReader.HealthPercent < 20 && !_bits.Combat();
    // Note: if IAddonBits is not injected, substitute with whatever combat-state
    // accessor is available: playerReader.IsInCombat, addonBits.Combat(), etc.
}
```

**Important:** Check how `Combat()` state is accessed in the rest of `HybridDecisionEngine.cs`.
- If `IAddonBits` is already injected → use `_bits.Combat()`
- If `PlayerReader` exposes it → use `_playerReader.IsInCombat`
- If neither → search for `bits.Combat` pattern in `Core/Goals/*.cs` and match

## Step 5: Run tests
```bash
dotnet test CoreUnitTests --filter "FullyQualifiedName~HybridDecisionEngineTests" --verbosity detailed
```
**Expected:** All 3 tests PASS.

## Step 6: Full suite
```bash
dotnet test MasterOfPuppets.sln --verbosity minimal
```
**Expected:** No regressions.

## Step 7: Commit
```bash
git add Core/AI/HybridDecision/HybridDecisionEngine.cs CoreUnitTests/AI/HybridDecisionEngineTests.cs
git commit -m "fix(ai): implement DetectUnexpectedState - low health out of combat triggers LLM intervention"
```

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| IAddonBits not injected in HybridDecisionEngine | Medium | Read constructor first; use PlayerReader alternative if needed |
| Health threshold too aggressive (< 20) | Low | Threshold matches GOAP's existing flee-goal triggers; safe default |
| LLM latency causes game lag | Very Low | Feature already has MaxLatency: 2000ms circuit breaker |
