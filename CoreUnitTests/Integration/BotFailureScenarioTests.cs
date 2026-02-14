using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using Core;
using Core.Analytics;
using Core.FeatureFlags;
using Core.GoalsComponent;
using Core.GoalsComponent.Blacklist;
using Core.Testing;

using FluentAssertions;

using MockWoWClient.GameState;

using Xunit;

namespace CoreUnitTests.Integration;

/// <summary>
/// Integration tests for bot failure recovery features including:
/// - SmartBlacklist system
/// - Stuck detection with BreadcrumbTracker
/// - Failure simulation through MockWoWClient
/// - Hot zone creation and visualization
/// </summary>
public sealed class BotFailureScenarioTests : IDisposable
{
    private readonly SimulationClock _clock;
    private readonly GameStateManager _gameState;
    private readonly FailureSimulationService _failureSimulation;
    private readonly BreadcrumbTracker _breadcrumbTracker;
    private readonly SmartBlacklist _blacklist;
    private readonly string _testDir;

    public BotFailureScenarioTests()
    {
        _clock = new SimulationClock();
        _gameState = new GameStateManager(_clock);
        _failureSimulation = new FailureSimulationService(_gameState, _clock);
        _breadcrumbTracker = new BreadcrumbTracker(maxSize: 50, minDistance: 3f);

        // Setup test directory for SmartBlacklist
        _testDir = Path.Combine(Path.GetTempPath(), $"BotFailureTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        var options = new Core.FeatureFlags.SmartBlacklistOptions
        {
            MaxEntries = 100,
            AutoSaveIntervalMinutes = 0, // Disable for tests
            AutoSaveOnChange = false,
            LogBlacklistHits = false
        };

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<SmartBlacklist>.Instance;
        _blacklist = new SmartBlacklist(logger, options, Path.Combine(_testDir, "test_blacklist.json"));
    }

    [Fact]
    public void SimulateStuck_ShouldCreateStuckEventWithCorrectPosition()
    {
        // Arrange
        SimulatedStuckEvent? capturedEvent = null;
        _failureSimulation.OnStuckSimulated += evt => capturedEvent = evt;

        Vector3 expectedPosition = new(100f, 200f, 0f);
        _gameState.Player.Position = expectedPosition;
        _gameState.Player.Direction = 45f;

        // Act
        _failureSimulation.SimulateStuck(UnstuckState.InitialAttempt, durationMs: 3000, attemptCount: 1);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Position.Should().Be(expectedPosition);
        capturedEvent.Direction.Should().Be(45f);
        capturedEvent.State.Should().Be(UnstuckState.InitialAttempt);
        capturedEvent.DurationMs.Should().Be(3000);
        capturedEvent.AttemptCount.Should().Be(1);
        capturedEvent.IsFlashingMarker.Should().BeTrue();
    }

    [Fact]
    public void SimulateStuck_WithHighAttemptCount_ShouldMarkAsSpinning()
    {
        // Arrange
        SimulatedStuckEvent? capturedEvent = null;
        _failureSimulation.OnStuckSimulated += evt => capturedEvent = evt;

        _gameState.Player.Position = Vector3.Zero;

        // Act
        _failureSimulation.SimulateStuck(UnstuckState.InitialAttempt, durationMs: 5000, attemptCount: 3);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.IsSpinning.Should().BeTrue();
    }

    [Fact]
    public void SimulateDeath_ShouldCreateDeathEventAndKillPlayer()
    {
        // Arrange
        SimulatedDeathEvent? capturedEvent = null;
        _failureSimulation.OnDeathSimulated += evt => capturedEvent = evt;

        Vector3 expectedPosition = new(500f, 500f, 0f);
        _gameState.Player.Position = expectedPosition;
        _gameState.Player.Health = 100;
        _gameState.Player.Level = 10;

        // Act
        _failureSimulation.SimulateDeath("Killed by Hogger");

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Position.Should().Be(expectedPosition);
        capturedEvent.Cause.Should().Be("Killed by Hogger");
        capturedEvent.Level.Should().Be(10);
        _gameState.Player.IsDead.Should().BeTrue();
    }

    [Fact]
    public void SimulateHotZone_ShouldCreateHotZoneWithMultipleFailures()
    {
        // Arrange
        SimulatedHotZone? capturedZone = null;
        int stuckCount = 0;

        _failureSimulation.OnHotZoneCreated += zone => capturedZone = zone;
        _failureSimulation.OnStuckSimulated += _ => stuckCount++;

        _gameState.Player.Position = new Vector3(1000f, 1000f, 0f);

        // Act
        _failureSimulation.SimulateHotZone(FailureType.Stuck, failureCount: 5, radius: 15f);

        // Assert
        capturedZone.Should().NotBeNull();
        capturedZone!.FailureCount.Should().Be(5);
        capturedZone.Radius.Should().Be(15f);
        capturedZone.PrimaryType.Should().Be(FailureType.Stuck);
        stuckCount.Should().Be(5);
    }

    [Fact]
    public void SimulateMultiMobAggro_ShouldSpawnMobsAndStartCombat()
    {
        // Arrange
        _gameState.Player.Position = Vector3.Zero;
        _gameState.Player.Level = 10;

        // Act
        List<NpcEntity> spawnedMobs = _failureSimulation.SimulateMultiMobAggro(mobCount: 5, maxDistance: 20f);

        // Assert
        spawnedMobs.Should().HaveCount(5);
        _gameState.Npcs.Should().HaveCount(5);
        _gameState.InCombat.Should().BeTrue();
        _gameState.CurrentTarget.Should().NotBeNull();

        // Verify mobs are within maxDistance
        foreach (NpcEntity npc in spawnedMobs)
        {
            float distance = Vector3.Distance(Vector3.Zero, npc.Position);
            distance.Should().BeLessOrEqualTo(20f);
        }
    }

    [Fact]
    public void SimulateRehabilitation_ShouldCreateRehabEvent()
    {
        // Arrange
        SimulatedRehabEvent? capturedEvent = null;
        _failureSimulation.OnRehabSimulated += evt => capturedEvent = evt;

        Vector3 rehabPosition = new(2000f, 2000f, 0f);

        // Act
        _failureSimulation.SimulateRehabilitation(rehabPosition, radius: 30f);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Position.Should().Be(rehabPosition);
        capturedEvent.Radius.Should().Be(30f);
        capturedEvent.SeverityReduction.Should().Be(0.5f);
    }

    [Fact]
    public void StuckDetection_Integration_WithBreadcrumbTracker()
    {
        // Arrange - simulate player movement then getting stuck
        Vector3 stuckPosition = new(5000f, 5000f, 0f);
        _gameState.Player.Position = stuckPosition;

        // Record position in breadcrumb tracker
        _breadcrumbTracker.RecordPosition(stuckPosition, mapId: 1);
        _breadcrumbTracker.RecordPosition(stuckPosition + new Vector3(10f, 0f, 0f), mapId: 1);
        _breadcrumbTracker.RecordPosition(stuckPosition + new Vector3(20f, 0f, 0f), mapId: 1);

        // Act - get backtrack position
        BreadcrumbEntry? backtrackPos = _breadcrumbTracker.GetBacktrackPosition(2);

        // Assert
        backtrackPos.Should().NotBeNull();
        BreadcrumbEntry pos = backtrackPos.Value;
        pos.Position.Should().Be(stuckPosition + new Vector3(10f, 0f, 0f));
    }

    [Fact]
    public void SmartBlacklist_AddEntry_ShouldBeRetrievable()
    {
        // Arrange
        Vector3 hostilePos = new(3000f, 3000f, 0f);
        _gameState.Player.Position = hostilePos;

        NpcEntity hostileNpc = _gameState.SpawnNpc("Hostile NPC", level: 10, health: 100, position: hostilePos + new Vector3(5f, 0f, 0f));

        // Act - Add to blacklist
        _blacklist.Add(
            hostileNpc.Id.GetHashCode(),
            "Hostile NPC",
            BlacklistSeverity.Medium,
            "Aggro",
            hostilePos,
            1);

        // Assert
        _blacklist.Is(hostileNpc.Id.GetHashCode()).Should().BeTrue();
    }

    [Fact]
    public void FailureSimulation_GetRecentStuckEvents_ShouldReturnEventsWithinWindow()
    {
        // Arrange
        _gameState.Player.Position = Vector3.Zero;

        // Simulate 3 stuck events
        _failureSimulation.SimulateStuck(UnstuckState.InitialAttempt, attemptCount: 1);
        _failureSimulation.SimulateStuck(UnstuckState.StrafeAttempt, attemptCount: 2);
        _failureSimulation.SimulateStuck(UnstuckState.ReverseAttempt, attemptCount: 3);

        // Act
        IReadOnlyList<SimulatedStuckEvent> recentEvents = _failureSimulation.GetRecentStuckEvents(TimeSpan.FromMinutes(1));

        // Assert
        recentEvents.Should().HaveCount(3);
    }

    [Fact]
    public void FailureSimulation_ClearHistory_ShouldRemoveAllEvents()
    {
        // Arrange
        _gameState.Player.Position = Vector3.Zero;
        _failureSimulation.SimulateStuck(UnstuckState.InitialAttempt);
        _failureSimulation.SimulateDeath("Test death");

        // Act
        _failureSimulation.ClearHistory();

        // Assert
        IReadOnlyList<SimulatedStuckEvent> stuckEvents = _failureSimulation.GetRecentStuckEvents(TimeSpan.FromMinutes(1));
        IReadOnlyList<SimulatedDeathEvent> deathEvents = _failureSimulation.GetRecentDeathEvents(TimeSpan.FromMinutes(1));

        stuckEvents.Should().BeEmpty();
        deathEvents.Should().BeEmpty();
    }

    [Fact]
    public void FullScenario_StuckThenDeathThenHotZone()
    {
        // This test simulates a realistic scenario:
        // 1. Player gets stuck
        // 2. Player dies
        // 3. Multiple deaths create a hot zone

        // Arrange
        List<SimulatedStuckEvent> stuckEvents = [];
        List<SimulatedDeathEvent> deathEvents = [];
        SimulatedHotZone? hotZone = null;

        _failureSimulation.OnStuckSimulated += evt => stuckEvents.Add(evt);
        _failureSimulation.OnDeathSimulated += evt => deathEvents.Add(evt);
        _failureSimulation.OnHotZoneCreated += zone => hotZone = zone;

        // Act - simulate getting stuck
        _gameState.Player.Position = new Vector3(100f, 100f, 0f);
        _failureSimulation.SimulateStuck(UnstuckState.BreadcrumbBacktrack, durationMs: 10000, attemptCount: 5);

        // Simulate death at same location
        _failureSimulation.SimulateDeath("Died while stuck");

        // Simulate hot zone creation (3 deaths in same area)
        _failureSimulation.SimulateHotZone(FailureType.Death, failureCount: 3, radius: 10f);

        // Assert
        stuckEvents.Should().HaveCount(1);
        stuckEvents[0].State.Should().Be(UnstuckState.BreadcrumbBacktrack);

        deathEvents.Should().HaveCount(4); // 1 direct + 3 from hot zone
        hotZone.Should().NotBeNull();
        hotZone!.FailureCount.Should().Be(3);
        hotZone.PrimaryType.Should().Be(FailureType.Death);
    }

    public void Dispose()
    {
        _blacklist.Dispose();
    }
}
