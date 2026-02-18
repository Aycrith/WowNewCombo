# MockWoWClient Integration Testing Framework

## Overview

The MockWoWClient framework provides a headless simulation environment for testing WoW bot functionality without requiring an actual game client. It simulates game state, input processing, and addon communication to enable reliable, fast integration tests.

## Architecture

```
MockWoWClient/
├── Contracts/
│   └── AddonConstants.cs          # Pixel encoding constants matching addon
├── GameState/
│   ├── GameStateManager.cs        # Central state management
│   ├── Entities.cs                # Player, NPC, Corpse entities
│   └── SimulationClock.cs         # Time advancement for async tests
├── InputHandling/
│   ├── InputProcessor.cs          # VK key simulation
│   ├── GameStateFrameMapper.cs    # Pixel grid rendering
│   └── ChatCommandHandler.cs      # Console command processing
└── Rendering/
    ├── MockWowScreen.cs           # Screen capture simulation
    └── PixelGridRenderer.cs       # RGB pixel encoding
```

## Quick Start

### Basic Test Structure

```csharp
[EndToEndScenario("FeatureName")]
public sealed class MyScenario : TestScenarioBase
{
    public MyScenario(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task MyTest()
    {
        // Arrange - Set up initial state
        SpawnNpc("Wolf", level: 10, health: 100, position: (10, 0, 10), hostile: true);
        GameState.Player.Position = (0, 0, 0);

        // Act - Simulate input
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        await Task.Delay(100); // Wait for async processing

        // Assert - Verify state changes
        GameState.HasTarget.Should().BeTrue();
    }
}
```

## Core Concepts

### 1. Game State Management

The `GameStateManager` tracks all game state including:
- Player position, health, mana, buffs
- Target information
- Combat state
- NPCs and corpses in the world
- Corpse positions for skinning/loot

**Key Properties:**
```csharp
GameState.Player          // Player entity with position, health, etc.
GameState.Target          // Current target entity
GameState.InCombat        // Combat state
GameState.HasTarget       // Target acquisition state
GameState.PlayerLocation  // Current zone/map
```

### 2. Entity Spawning

Spawn NPCs, corpses, and other entities:

```csharp
// Spawn hostile NPC
var npc = SpawnNpc(
    name: "Wolf",
    level: 10,
    health: 100,
    position: (10.0f, 0f, 10.0f),
    hostile: true
);

// Spawn lootable corpse
var corpse = SpawnCorpse(
    name: "Wolf",
    position: (5.0f, 0f, 5.0f),
    hasLoot: true,
    isSkinnable: true
);
```

### 3. Input Simulation

Simulate keyboard input using VK codes:

```csharp
// Simulate TAB for targeting
MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);

// Simulate combat key
MockClient.InputProcessor.KeyDown(0x31); // '1' key

// Hold and release modifiers
MockClient.InputProcessor.KeyDown(InputProcessor.VK_SHIFT);
MockClient.InputProcessor.KeyDown(0x31);
MockClient.InputProcessor.KeyUp(InputProcessor.VK_SHIFT);
```

**Common VK Codes:**
- `VK_TAB` (0x09) - Target nearest
- `VK_SHIFT` (0x10) - Modifier
- `VK_SPACE` (0x20) - Jump
- `0x31-0x39` - Number keys 1-9

### 4. Time Advancement

For async operations, use either:

```csharp
// Real-time delay (preferred for realistic timing)
await Task.Delay(100);

// Fast simulation time advancement
AdvanceTime(TimeSpan.FromSeconds(5));

// Frame-based advancement (for frame-sensitive tests)
AdvanceFrames(60); // Advance 60 frames
```

### 5. Pixel Grid Encoding

The mock client encodes game state as RGB pixels for addon communication:

```csharp
// Get pixel grid from screen capture
var grid = MockClient.Screen.CaptureGrid();

// Decode specific values
var frameIndex = PixelEncoding.DecodeFrameIndex(grid);
var playerHealth = PixelEncoding.DecodePlayerHealth(grid);
```

## Test Patterns

### Combat Testing

```csharp
[Fact]
public async Task Combat_Test()
{
    // Setup
    SpawnNpc("Enemy", 10, 100, (5, 0, 5), hostile: true);
    GameState.Player.Position = (0, 0, 0);

    // Target enemy
    MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
    await Task.Delay(50);
    GameState.HasTarget.Should().BeTrue();

    // Enter combat
    GameState.StartCombat();
    GameState.InCombat.Should().BeTrue();

    // Deal damage
    GameState.Target.Health -= 30;
    GameState.Target.Health.Should().Be(70);
}
```

### Navigation Testing

```csharp
[Fact]
public async Task Navigation_Test()
{
    // Set route
    var waypoints = new[] {
        (0f, 0f, 0f),
        (10f, 0f, 10f),
        (20f, 0f, 20f)
    };

    // Move along path
    foreach (var waypoint in waypoints)
    {
        GameState.Player.Position = waypoint;
        await Task.Delay(100);
    }

    // Verify arrival
    GameState.Player.Position.X.Should().Be(20f);
}
```

### Loot/Skinning Testing

```csharp
[Fact]
public async Task Skinning_Test()
{
    // Create lootable corpse
    var corpse = SpawnCorpse(
        "Wolf",
        position: GameState.Player.Position,
        hasLoot: true,
        isSkinnable: true
    );

    // Set target directly (TAB won't target corpses)
    GameState.SetTarget(corpse);
    GameState.ShouldLoot.Should().BeTrue();

    // Loot
    GameState.Player.Loot();
    corpse.HasLoot.Should().BeFalse();

    // Skin
    GameState.Player.Skin();
    corpse.IsSkinnable.Should().BeFalse();
}
```

### Stuck Detection Testing

```csharp
[Fact]
public async Task StuckDetection_Test()
{
    // Initialize breadcrumb tracking
    GameState.StuckDetector.StartTracking();

    // Move in small increments (less than threshold)
    for (int i = 0; i < 10; i++)
    {
        GameState.Player.Position = (i * 0.4f, 0, 0); // 0.4 units per step
        await Task.Delay(100);
    }

    // Verify stuck detected
    GameState.StuckDetector.IsStuck.Should().BeTrue();
}
```

## Helper Methods

The `TestScenarioBase` provides useful helpers:

```csharp
// Wait for condition with timeout
await WaitForConditionAsync(
    () => GameState.HasTarget,
    timeoutMs: 1000,
    message: "Target not acquired"
);

// Create NPC at relative position
var npc = SpawnNpcRelative("Enemy", 10, 100, (10, 0, 0), hostile: true);

// Assert with automatic message
AssertHasTarget("Target should be acquired after TAB");
AssertInCombat("Should enter combat after attack");

// Verify position within tolerance
AssertPosition((10f, 0f, 10f), tolerance: 0.1f);
```

## Best Practices

### 1. Use Realistic Timing

```csharp
// Good - realistic delays
await Task.Delay(100); // Simulate human reaction time

// Avoid - instant completion unless testing sync code
// await Task.Yield(); // Too fast for async operations
```

### 2. Set Targets Directly When Needed

TAB only targets live hostile NPCs. For corpses or specific targets:

```csharp
// Target corpse directly
GameState.SetTarget(corpse);

// Target by name
GameState.SetTargetByName("Wolf");
```

### 3. Proper Cleanup

Tests should clean up state if needed:

```csharp
public override void Dispose()
{
    GameState.ClearNpcs();
    GameState.ClearCorpses();
    GameState.ExitCombat();
    base.Dispose();
}
```

### 4. Async Assertions

Always await async operations before asserting:

```csharp
// Good
MockClient.InputProcessor.KeyDown(key);
await Task.Delay(100);
GameState.InCombat.Should().BeTrue();

// Bad - race condition
MockClient.InputProcessor.KeyDown(key);
GameState.InCombat.Should().BeTrue(); // May assert before combat starts
```

### 5. Frame-Accurate Testing

For pixel encoding tests:

```csharp
// Advance specific number of frames
AdvanceFrames(3);

// Verify frame counter incremented
var frame = PixelEncoding.DecodeFrameIndex(grid);
frame.Should().Be(3);
```

## Common Pitfalls

### Pitfall 1: TAB targeting limitations

```csharp
// TAB won't target dead NPCs
SpawnNpc("DeadEnemy", 10, 0, (5, 0, 5), hostile: true); // Health = 0
MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
await Task.Delay(100);
GameState.HasTarget.Should().BeFalse(); // Won't target dead

// Solution: Use SetTarget for specific targeting
```

### Pitfall 2: Combat state requirements

```csharp
// StartCombat requires a target
GameState.StartCombat(); // May throw if no target

// Solution: Ensure target exists first
GameState.SetTarget(npc);
GameState.StartCombat();
```

### Pitfall 3: Async timing issues

```csharp
// Race condition - test may pass or fail
GameState.Player.Position = (10, 0, 0);
GameState.Player.Position.X.Should().Be(10); // May be 0 if async

// Solution: Use WaitForConditionAsync
await WaitForConditionAsync(
    () => GameState.Player.Position.X > 9.9f,
    timeoutMs: 1000
);
```

### Pitfall 4: Pixel encoding endianness

```csharp
// Verify byte order matches addon
var encoded = PixelEncoding.EncodeFrameIndex(0x010203);
// Should be RGB: 0x01, 0x02, 0x03
```

## Troubleshooting

### Test Hangs Indefinitely

```csharp
// Add timeout to prevent infinite hangs
await Task.WhenAny(
    WaitForConditionAsync(() => condition),
    Task.Delay(TimeSpan.FromSeconds(5))
).Should().BeCompletedAsync();
```

### Pixel Values Don't Match

```csharp
// Debug: Print actual vs expected
var actual = PixelEncoding.DecodeHealth(grid);
output.WriteLine($"Expected: {expected}, Actual: {actual}");
```

### Target Not Found

```csharp
// Check available targets
output.WriteLine($"NPCs: {string.Join(", ", GameState.Npcs.Select(n => n.Name))}");
output.WriteLine($"Target range: {GameState.Player.DistanceTo(npc)}");
```

## Integration with CI/CD

Tests are designed to run in CI:

```yaml
# Example GitHub Actions
- name: Run Integration Tests
  run: dotnet test CoreUnitTests --filter "FullyQualifiedName~EndToEnd" --no-build
```

**Key Features:**
- No external dependencies (no WoW client needed)
- Deterministic - same inputs produce same outputs
- Fast - typical test runs in 50-100ms
- Parallelizable - tests don't share mutable state

## Future Enhancements

- [ ] Network latency simulation
- [ ] Multi-player scenarios
- [ ] Performance benchmarks
- [ ] Visual debugging output
- [ ] Replay system for failed tests

## Related Documentation

- `Core/GOAP/` - Goal-Oriented Action Planning system
- `Core/Goals/` - Individual goal implementations
- `Addons/DataToColor/` - Lua addon documentation

---

**Last Updated:** 2025-02-08  
**Maintainer:** Development Team  
**Version:** 1.0
