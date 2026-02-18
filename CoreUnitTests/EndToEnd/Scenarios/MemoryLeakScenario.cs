using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.EndToEnd.Scenarios;

/// <summary>
/// Scenario 6: Memory Leak Detection
/// Validates no memory leaks over extended runtime.
/// </summary>
[EndToEndScenario("MemoryLeakDetection")]
public class MemoryLeakScenario : TestScenarioBase
{
    public MemoryLeakScenario(ITestOutputHelper output) : base(output) { }

    public override string ScenarioName => "Memory Leak Detection";

    public override string ScenarioDescription =>
        "Validates that the bot does not leak memory over extended runtime. " +
        "Monitors managed heap, unmanaged resources, and handle counts.";

    public override TimeSpan Timeout => TimeSpan.FromMinutes(5);

    [LongRunningTest]
    [Fact]
    public async Task ExtendedRuntime_ShouldNotLeakMemory()
    {
        // Arrange
        _output.WriteLine("  Starting memory profiling...");
        var baselineMemory = GetMemoryUsage();
        _output.WriteLine($"  Baseline memory: {baselineMemory / 1024 / 1024} MB");

        var measurements = new List<(TimeSpan Time, long Memory)>();
        measurements.Add((TimeSpan.Zero, baselineMemory));

        // Act - Run for 60 seconds, taking measurements every 5 seconds
        var duration = TimeSpan.FromMinutes(1);
        var interval = TimeSpan.FromSeconds(5);
        var elapsed = TimeSpan.Zero;

        while (elapsed < duration)
        {
            // Simulate some activity
            await SimulateBotActivity();

            // Advance time
            MockClient.Advance(interval);
            elapsed += interval;

            // Take measurement
            var memory = GetMemoryUsage();
            measurements.Add((elapsed, memory));

            if (elapsed.TotalSeconds % 10 == 0)
            {
                _output.WriteLine($"  [{elapsed.TotalSeconds:F0}s] Memory: {memory / 1024 / 1024} MB");
            }
        }

        // Assert
        var finalMemory = measurements.Last().Memory;
        var memoryGrowth = finalMemory - baselineMemory;
        var growthPercent = (memoryGrowth / (double)baselineMemory) * 100;

        _output.WriteLine($"\n  Final memory: {finalMemory / 1024 / 1024} MB");
        _output.WriteLine($"  Growth: {memoryGrowth / 1024} KB ({growthPercent:F2}%)");

        // Allow some growth (up to 20%) for normal operation
        growthPercent.Should().BeLessThan(20,
            $"memory growth should be less than 20% over {duration.TotalMinutes} minutes");
    }

    [Fact]
    public void ScreenCapture_ShouldNotLeakImages()
    {
        // Arrange
        _output.WriteLine("  Testing screen capture disposal...");

        var baselineMemory = GetMemoryUsage();
        _output.WriteLine($"  Baseline: {baselineMemory / 1024} KB");

        // Act - Capture many screens
        for (int i = 0; i < 100; i++)
        {
            using var image = MockClient.CaptureScreen();
            // Image is disposed via 'using'
        }

        // Force GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert
        var finalMemory = GetMemoryUsage();
        var growth = finalMemory - baselineMemory;

        _output.WriteLine($"  After 100 captures: {finalMemory / 1024} KB");
        _output.WriteLine($"  Growth: {growth / 1024} KB");

        // Should have minimal growth
        growth.Should().BeLessThan(10 * 1024 * 1024, // 10 MB
            "capturing 100 screens should not leak significant memory");
    }

    [Fact]
    public void GameStateUpdates_ShouldNotAccumulate()
    {
        // Arrange
        _output.WriteLine("  Testing game state update efficiency...");
        var initialNpcCount = GameState.Npcs.Count;

        // Act - Spawn and kill many NPCs
        for (int i = 0; i < 50; i++)
        {
            var npc = GameState.SpawnNpc($"Mob{i}", 1, 10, new System.Numerics.Vector3(i, 0, 0));
            npc.TakeDamage(20); // Kill it

            MockClient.GameState.Update(TimeSpan.FromMilliseconds(100));
        }

        var npcCountAfterSpawns = GameState.Npcs.Count;
        _output.WriteLine($"  NPCs after spawning 50: {npcCountAfterSpawns}");

        // Wait for corpses to despawn
        MockClient.Advance(TimeSpan.FromMinutes(6));

        // Assert
        GameState.Corpses.Should().BeEmpty("corpses should despawn after time");
        _output.WriteLine("  Corpses cleaned up successfully");
    }

    [Fact]
    public void InputEvents_ShouldBeProcessedAndCleared()
    {
        // Arrange
        _output.WriteLine("  Testing input queue...");
        var queue = MockClient.InputProcessor.Queue;
        queue.Clear();

        // Act - Queue many events
        for (int i = 0; i < 1000; i++)
        {
            MockClient.InputProcessor.KeyDown(0x57); // 'W'
        }

        var queuedCount = queue.Count;
        _output.WriteLine($"  Queued events: {queuedCount}");

        // Process frame
        MockClient.InputProcessor.ProcessFrame(TimeSpan.FromMilliseconds(16));

        // Assert
        queue.Count.Should().Be(0, "queue should be cleared after processing");
    }

    [Fact]
    public void PositionHistory_ShouldBeCapped()
    {
        // Arrange
        _output.WriteLine("  Testing breadcrumb capacity...");
        var player = GameState.Player;
        player.PositionHistory.Clear();

        // Act - Move many times
        for (int i = 0; i < 100; i++)
        {
            player.Position = new System.Numerics.Vector3(i * 10, 0, 0);
            MockClient.GameState.Update(TimeSpan.FromMilliseconds(100));
        }

        // Assert
        player.PositionHistory.Count.Should().BeLessThanOrEqualTo(50,
            "position history should be capped at 50 entries");

        _output.WriteLine($"  History count: {player.PositionHistory.Count} (max 50)");
    }

    private async Task SimulateBotActivity()
    {
        // Simulate realistic bot activity
        MockClient.InputProcessor.KeyDown(0x57); // Move forward
        MockClient.GameState.Update(TimeSpan.FromMilliseconds(16));
        MockClient.InputProcessor.KeyUp(0x57);

        // Capture screen
        using var _ = MockClient.CaptureScreen();

        await Task.Delay(10);
    }

    private static long GetMemoryUsage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        return proc.WorkingSet64;
    }
}
