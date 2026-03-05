# P0-2: Fix FailureSimulationService Hardcoded MapId = 0

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace hardcoded `MapId = 0` in `SimulateStuck()` with the actual current map ID from the injected game state, so zone-specific stuck-detection integration tests are zone-aware.

**Priority:** P0 — CRITICAL (wrong MapId masks zone-specific bugs in tests)

**Estimated time:** 4 minutes

---

## Context

`MockWoWClient/GameState/FailureSimulationService.cs` creates a `SimulatedStuckEvent` inside `SimulateStuck()` with `MapId = 0` and a TODO comment:

```csharp
// Around line 45 in SimulateStuck():
SimulatedStuckEvent stuckEvent = new()
{
    Id = Guid.NewGuid(),
    Timestamp = _clock.CurrentTime,
    Position = position,
    Direction = direction,
    MapId = 0, // TODO: Get actual map ID
    ...
};
```

The `FailureSimulationService` constructor (lines 27-31) already injects `GameStateManager gameState`. This service is consumed by `CoreUnitTests/Integration/BotFailureScenarioTests.cs` which has 12 integration tests (309 lines). All stuck events will have `MapId = 0`, making them indistinguishable across zones.

**Constructor signature (confirmed at lines 27-31):**
```csharp
public FailureSimulationService(GameStateManager gameState, SimulationClock clock)
{
    _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
    _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    ...
}
```

---

## Files to Modify

1. **`C:/WowClassicGrindBot/MockWoWClient/GameState/FailureSimulationService.cs`** — fix the MapId
2. **`C:/WowClassicGrindBot/CoreUnitTests/Integration/BotFailureScenarioTests.cs`** — add regression test

---

## Step 1: Read FailureSimulationService.cs to find exact property

Before editing, read the file to determine:
- What property on `_gameState` holds the current map ID
- Options: `_gameState.Player.MapId`, `_gameState.World.CurrentMapId`, `_gameState.World.MapId`

```bash
grep -n "MapId\|CurrentMap\|WorldMap" MockWoWClient/GameState/FailureSimulationService.cs
grep -n "MapId\|CurrentMap" MockWoWClient/GameState/GameStateManager.cs
```

## Step 2: Write failing test in BotFailureScenarioTests.cs

Add after the last existing test (~line 265 before `CleanupTestData`):

```csharp
[Fact]
public void SimulateStuck_ShouldCreateStuckEventWithCurrentMapId()
{
    // Arrange — set a non-zero map ID in the mock game state
    int expectedMapId = 1941; // Eversong Woods (TBC starting zone)
    // Use whatever setter exists on GameStateManager for current map
    // e.g.: _gameState.World.SetMapId(expectedMapId);
    //       _gameState.SetCurrentMapId(expectedMapId);
    //       _gameState.Player.MapId = expectedMapId;

    // Act
    _failureSimulationService.SimulateStuck(
        _gameState.Player.Position,
        direction: System.Numerics.Vector3.UnitX);

    // Assert
    IReadOnlyList<SimulatedStuckEvent> events =
        _failureSimulationService.GetRecentStuckEvents(TimeSpan.FromMinutes(1));

    events.Should().HaveCount(1);
    events[0].MapId.Should().Be(expectedMapId,
        "stuck events must reflect the current zone for zone-specific analysis");
}
```

**Note:** The exact setter call depends on `GameStateManager`'s API. Read the class before writing the test.

## Step 3: Run to confirm failure
```bash
dotnet test CoreUnitTests --filter "SimulateStuck_ShouldCreateStuckEventWithCurrentMapId" --verbosity detailed
```
**Expected:** FAIL — `Expected events[0].MapId to be 1941 but found 0`

## Step 4: Fix FailureSimulationService (~line 45)

Find the `MapId = 0` line and replace:

```csharp
// Before:
MapId = 0, // TODO: Get actual map ID

// After — use the appropriate property path from Step 1:
MapId = _gameState.World.CurrentMapId,
// OR: MapId = _gameState.Player.MapId,
```

## Step 5: Run test
```bash
dotnet test CoreUnitTests --filter "SimulateStuck_ShouldCreateStuckEventWithCurrentMapId" --verbosity detailed
```
**Expected:** PASS

## Step 6: Full suite
```bash
dotnet test MasterOfPuppets.sln --verbosity minimal
```
**Expected:** All 12 existing BotFailureScenarioTests still pass + 1 new test.

## Step 7: Commit
```bash
git add MockWoWClient/GameState/FailureSimulationService.cs CoreUnitTests/Integration/BotFailureScenarioTests.cs
git commit -m "fix(testing): FailureSimulationService reads MapId from game state instead of hardcoding 0"
```

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| GameStateManager doesn't expose MapId setter | Low | Read GameStateManager.cs first; use whatever property exists |
| Existing tests break because they expected MapId = 0 | Low | Check existing tests don't assert MapId == 0 explicitly |
