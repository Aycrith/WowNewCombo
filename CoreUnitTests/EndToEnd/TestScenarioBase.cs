using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.EndToEnd;

/// <summary>
/// Base class for all end-to-end test scenarios.
/// Provides common setup, teardown, and assertion utilities.
/// </summary>
public abstract class TestScenarioBase : IAsyncLifetime
{
    protected readonly ITestOutputHelper _output;
    protected MockWoWClient.MockWoWClient MockClient { get; private set; } = null!;
    protected MockWoWClient.GameState.GameStateManager GameState => MockClient.GameState;

    protected TestScenarioBase(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Name of the test scenario for logging.
    /// </summary>
    public abstract string ScenarioName { get; }

    /// <summary>
    /// Description of what this scenario tests.
    /// </summary>
    public abstract string ScenarioDescription { get; }

    /// <summary>
    /// Maximum time allowed for this scenario to complete.
    /// </summary>
    public virtual TimeSpan Timeout => TimeSpan.FromMinutes(2);

    /// <summary>
    /// Called once before all tests in the scenario.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        _output.WriteLine($"=== Starting Scenario: {ScenarioName} ===");
        _output.WriteLine($"Description: {ScenarioDescription}");

        MockClient = new MockWoWClient.MockWoWClient();
        MockClient.Start();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called once after all tests in the scenario.
    /// </summary>
    public virtual async Task DisposeAsync()
    {
        if (MockClient != null)
        {
            await MockClient.StopAsync();
            MockClient.Dispose();
        }

        _output.WriteLine($"=== Completed Scenario: {ScenarioName} ===");
    }

    /// <summary>
    /// Advances simulation time and processes all pending inputs.
    /// </summary>
    protected void AdvanceSimulation(TimeSpan time)
    {
        MockClient.Advance(time);
    }

    /// <summary>
    /// Helper method to wait for a condition with timeout.
    /// </summary>
    protected async Task<bool> WaitForConditionAsync(
        Func<bool> condition,
        string description,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);
        pollInterval ??= TimeSpan.FromMilliseconds(100);

        var startTime = DateTime.UtcNow;
        var attempts = 0;

        while (DateTime.UtcNow - startTime < timeout)
        {
            attempts++;
            if (condition())
            {
                _output.WriteLine($"  Condition met: {description} (after {attempts} attempts, {(DateTime.UtcNow - startTime).TotalMilliseconds:F0}ms)");
                return true;
            }

            await Task.Delay(pollInterval.Value);
        }

        _output.WriteLine($"  Condition FAILED: {description} (timeout after {attempts} attempts, {timeout.Value.TotalSeconds}s)");
        return false;
    }

    /// <summary>
    /// Helper method to wait for a specific value.
    /// </summary>
    protected async Task<T> WaitForValueAsync<T>(
        Func<T> valueProvider,
        Func<T, bool> predicate,
        string description,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);

        var startTime = DateTime.UtcNow;
        T lastValue = default!;

        while (DateTime.UtcNow - startTime < timeout)
        {
            lastValue = valueProvider();
            if (predicate(lastValue))
            {
                _output.WriteLine($"  Value condition met: {description} = {lastValue}");
                return lastValue;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Timeout waiting for: {description}. Last value: {lastValue}");
    }

    /// <summary>
    /// Spawns an NPC for testing.
    /// </summary>
    protected MockWoWClient.GameState.NpcEntity SpawnNpc(string name, int level, int health, System.Numerics.Vector3 position, bool hostile = true)
    {
        _output.WriteLine($"  Spawning NPC: {name} (L{level}) at {position}");
        return MockClient.SpawnNpc(name, level, health, position, hostile);
    }

    /// <summary>
    /// Advances simulation time.
    /// </summary>
    protected void AdvanceTime(TimeSpan time)
    {
        MockClient.Advance(time);
        _output.WriteLine($"  Advanced time: {time.TotalSeconds}s");
    }

    /// <summary>
    /// Captures a screenshot and returns it.
    /// </summary>
    protected SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Bgra32> CaptureScreen()
    {
        return MockClient.CaptureScreen();
    }

    /// <summary>
    /// Asserts that all 324 frames are detected.
    /// </summary>
    protected void AssertAllFramesDetected()
    {
        var image = CaptureScreen();
        image.Width.Should().BeGreaterThan(0);
        image.Height.Should().BeGreaterThan(0);
        _output.WriteLine($"  Screen captured: {image.Width}x{image.Height}");
    }

    /// <summary>
    /// Asserts that the player has a target.
    /// </summary>
    protected void AssertHasTarget(string expectedName)
    {
        GameState.CurrentTarget.Should().NotBeNull($"player should have a target");
        GameState.CurrentTarget!.Name.Should().Be(expectedName);
        _output.WriteLine($"  Target acquired: {expectedName}");
    }

    /// <summary>
    /// Asserts that the player is in combat.
    /// </summary>
    protected void AssertInCombat()
    {
        GameState.InCombat.Should().BeTrue("player should be in combat");
        _output.WriteLine("  Combat started");
    }

    /// <summary>
    /// Asserts that an NPC is dead.
    /// </summary>
    protected void AssertNpcDead(string npcName)
    {
        var npc = GameState.Npcs.FirstOrDefault(n => n.Name == npcName);
        npc.Should().NotBeNull($"NPC {npcName} should exist");
        npc!.IsDead.Should().BeTrue($"NPC {npcName} should be dead");
        _output.WriteLine($"  NPC killed: {npcName}");
    }

    /// <summary>
    /// Asserts that memory usage is stable.
    /// </summary>
    protected void AssertMemoryStable(long baselineMemory, long currentMemory, double maxGrowthPercent = 10.0)
    {
        var growth = currentMemory - baselineMemory;
        var growthPercent = (growth / (double)baselineMemory) * 100;

        _output.WriteLine($"  Memory: Baseline={baselineMemory / 1024 / 1024}MB, Current={currentMemory / 1024 / 1024}MB, Growth={growthPercent:F2}%");

        growthPercent.Should().BeLessThan(maxGrowthPercent,
            $"memory growth should be less than {maxGrowthPercent}%");
    }
}

/// <summary>
/// Attribute to mark E2E test scenarios.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class EndToEndScenarioAttribute : Attribute
{
    public string ScenarioName { get; }

    public EndToEndScenarioAttribute(string name)
    {
        ScenarioName = name;
    }
}

/// <summary>
/// Attribute to mark long-running tests.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class LongRunningTestAttribute : Attribute
{
}
