# WowClassicGrindBot Testing Guide

## Quick Start

```powershell
# Run all tests
.\Scripts\run-production-tests.ps1

# Run only unit tests
.\Scripts\run-production-tests.ps1 -Category Unit

# Run integration tests with MockWoWClient
.\Scripts\run-production-tests.ps1 -Category Integration

# Quick smoke test (under 2 minutes)
.\Scripts\run-production-tests.ps1 -Category Smoke
```

## Test Organization

### Test Projects

| Project | Type | Tests | Purpose |
|---------|------|-------|---------|
| **CoreUnitTests** | xUnit | ~1000+ | Unit and integration tests |
| **FrontendUnitTests** | xUnit + bunit | 29 | Blazor component tests |
| **CoreManualTests** | Console App | N/A | Manual integration runner |

### Test Categories

#### Unit Tests
Fast, isolated tests for individual components. Run with:
```powershell
.\Scripts\run-production-tests.ps1 -Category Unit
```

#### Integration Tests
Tests using MockWoWClient synthetic environment. Base class: `CoreUnitTests.Integration.IntegrationTestBase`

```powershell
.\Scripts\run-production-tests.ps1 -Category Integration
```

#### Evidence Tests
Tests with memory/performance measurements:
```powershell
.\Scripts\run-production-tests.ps1 -Category Evidence
```

#### Smoke Tests
Quick validation of critical paths (< 2 minutes):
```powershell
.\Scripts\run-production-tests.ps1 -Category Smoke
```

## MockWoWClient

The synthetic WoW client provides:
- Deterministic game state simulation
- Pixel-perfect DataToColor protocol emulation
- Failure injection for testing recovery systems
- Time control for reproducible tests

### Key Components

- `GameStateManager` - Central game state
- `FailureSimulationService` - Inject failures (stuck, death, hot zones)
- `SimulationClock` - Controlled time for reproducibility

## Writing Integration Tests

### Using IntegrationTestBase

```csharp
using CoreUnitTests.Integration;

public class MyIntegrationTests : IntegrationTestBase
{
    [Fact]
    public void MyScenario_TestCondition_ExpectedResult()
    {
        // Arrange - use GameState, FailureSimulation from base
        var npc = GameState.SpawnNpc("TestNPC", new Vector3(100, 100, 0), level: 10);

        // Act - trigger behavior
        FailureSimulation.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1);

        // Assert - use helper methods
        AssertPlayerAt(expectedPosition);
        AssertCombatState(expectedInCombat: false);
    }
}
```

### Event Capture Pattern

```csharp
[Fact]
public void EventCapture_Example()
{
    // Capture events using the base class helper
    var evt = CaptureEvent<SimulatedStuckEvent>(
        h => FailureSimulation.OnStuckSimulated += h,
        () => FailureSimulation.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1)
    );

    AssertEventFired(evt);
    Assert.Equal(UnstuckState.InitialAttempt, evt.State);
}
```

## Available Helpers

### IntegrationTestBase Helpers

| Method | Description |
|--------|-------------|
| `CaptureEvent<T>()` | Synchronously capture an event |
| `CaptureEventAsync<T>()` | Asynchronously capture an event |
| `AssertEventFired<T>()` | Assert an event was captured |
| `AssertPlayerAt()` | Assert player position with tolerance |
| `AssertCombatState()` | Assert combat state |

### Test Infrastructure

- `GameState` - Mock game state manager
- `FailureSimulation` - Failure injection service
- `Clock` - Controlled simulation clock

## Running Tests

### Full Test Suite

```powershell
# Debug mode (default)
.\Scripts\run-production-tests.ps1

# Release mode
.\Scripts\run-production-tests.ps1 -Configuration Release

# With build warnings as errors
.\Scripts\run-production-tests.ps1 -FailOnWarnings
```

### Individual Projects

```powershell
# Core unit tests only
dotnet test CoreUnitTests

# Frontend tests only
dotnet test FrontendUnitTests

# With filter
dotnet test CoreUnitTests --filter "FullyQualifiedName~CircuitBreaker"
```

## Test Evidence

Evidence reports are generated in `./test-evidence/`:

- `production-test-report.md` - Summary report
- `*Results.trx` - Detailed test results

## Troubleshooting

### Tests Failing

1. Check build first: `dotnet build MasterOfPuppets.sln`
2. Run smoke tests: `.\Scripts\run-production-tests.ps1 -Category Smoke`
3. Check specific category: `.\Scripts\run-production-tests.ps1 -Category Unit`

### MockWoWClient Issues

Ensure MockWoWClient project is built:
```powershell
dotnet build MockWoWClient
dotnet build CoreUnitTests
```

### Test Discovery Issues

Clean and rebuild:
```powershell
dotnet clean
dotnet build
```

## CI/CD Integration

```yaml
# Example GitHub Actions
- name: Run Production Tests
  run: |
    .\Scripts\run-production-tests.ps1 -Category All -Configuration Release -FailOnWarnings
```

## Contributing

When adding new tests:

1. Use `IntegrationTestBase` for MockWoWClient tests
2. Follow naming convention: `Method_Scenario_ExpectedResult`
3. Add `[Trait("Category", "Smoke")]` for critical path tests
4. Generate evidence for performance tests
5. Keep unit tests under 100ms when possible
