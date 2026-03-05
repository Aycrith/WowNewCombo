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

using Microsoft.Extensions.Logging.Abstractions;

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
public sealed class BotFailureScenarioTests : IntegrationTestBase
{
    private readonly BreadcrumbTracker _breadcrumbTracker;
    private readonly SmartBlacklist _blacklist;
    private readonly string _testDir;

    public BotFailureScenarioTests()
    {
        _breadcrumbTracker = new BreadcrumbTracker(maxSize: 50, minDistance: 3f);

        // Setup test directory for SmartBlacklist
        _testDir = Path.Combine(Path.GetTempPath(), $"BotFailureTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        var options = new SmartBlacklistOptions
        {
            MaxEntries = 100,
            AutoSaveIntervalMinutes = 0, // Disable for tests
            AutoSaveOnChange = false,
            LogBlacklistHits = false
        };

        var logger = NullLogger<SmartBlacklist>.Instance;
        _blacklist = new SmartBlacklist(logger, options, Path.Combine(_testDir, "test_blacklist.json"));
    }

    [Fact]
    public void SimulateStuck_ShouldCreateStuckEventWithCorrectPosition()
    {
        // Arrange
        SimulatedStuckEvent? capturedEvent = null;
        FailureSimulation.OnStuckSimulated += evt => capturedEvent = evt;

        Vector3 expectedPosition = new(100f, 200f, 0f);
        GameState.Player.Position = expectedPosition;
        GameState.Player.Direction = 45f;

        // Act
        FailureSimulation.SimulateStuck(UnstuckState.InitialAttempt, durationMs: 3000, attemptCount: 1);

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
        FailureSimulation.OnStuckSimulated += evt => capturedEvent = evt;

        GameState.Player.Position = Vector3.Zero;

        // Act
        FailureSimulation.SimulateStuck(UnstuckState.InitialAttempt, durationMs: 5000, attemptCount: 3);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.IsSpinning.Should().BeTrue();
    }

    [Fact]
    public void SimulateDeath_ShouldCreateDeathEventAndKillPlayer()
    {
        // Arrange
        SimulatedDeathEvent? capturedEvent = null;
        FailureSimulation.OnDeathSimulated += evt => capturedEvent = evt;

        Vector3 expectedPosition = new(500f, 500f, 0f);
        GameState.Player.Position = expectedPosition;
        GameState.Player.Health = 100;
        GameState.Player.Level = 10;

        // Act
        FailureSimulation.SimulateDeath("Killed by Hogger");

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Position.Should().Be(expectedPosition);
        capturedEvent.Cause.Should().Be("Killed by Hogger");
        capturedEvent.Level.Should().Be(10);
        GameState.Player.IsDead.Should().BeTrue();
    }

    [Fact]
    public void SimulateHotZone_ShouldCreateHotZoneWithMultipleFailures()
    {
        // Arrange
        SimulatedHotZone? capturedZone = null;
        int stuckCount = 0;

        FailureSimulation.OnHotZoneCreated += zone => capturedZone = zone;
        FailureSimulation.OnStuckSimulated += _ => stuckCount++;

        GameState.Player.Position = new Vector3(1000f, 1000f, 0f);

        // Act
        FailureSimulation.SimulateHotZone(FailureType.Stuck, failureCount: 5, radius: 15f);

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
        GameState.Player.Position = Vector3.Zero;
        GameState.Player.Level = 10;

        // Act
        List<NpcEntity> spawnedMobs = FailureSimulation.SimulateMultiMobAggro(mobCount: 5, maxDistance: 20f);

        // Assert
        spawnedMobs.Should().HaveCount(5);
        GameState.Npcs.Should().HaveCount(5);
        GameState.InCombat.Should().BeTrue();
        GameState.CurrentTarget.Should().NotBeNull();

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
        FailureSimulation.OnRehabSimulated += evt => capturedEvent = evt;

        Vector3 rehabPosition = new(2000f, 2000f, 0f);

        // Act
        FailureSimulation.SimulateRehabilitation(rehabPosition, radius: 30f);

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
        GameState.Player.Position = stuckPosition;

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
        GameState.Player.Position = hostilePos;

        NpcEntity hostileNpc = GameState.SpawnNpc("Hostile NPC", level: 10, health: 100, position: hostilePos + new Vector3(5f, 0f, 0f));

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
        GameState.Player.Position = Vector3.Zero;

        // Simulate 3 stuck events
        FailureSimulation.SimulateStuck(UnstuckState.InitialAttempt, attemptCount: 1);
        FailureSimulation.SimulateStuck(UnstuckState.StrafeAttempt, attemptCount: 2);
        FailureSimulation.SimulateStuck(UnstuckState.ReverseAttempt, attemptCount: 3);

        // Act
        IReadOnlyList<SimulatedStuckEvent> recentEvents = FailureSimulation.GetRecentStuckEvents(TimeSpan.FromMinutes(1));

        // Assert
        recentEvents.Should().HaveCount(3);
    }

    [Fact]
    public void FailureSimulation_ClearHistory_ShouldRemoveAllEvents()
    {
        // Arrange
        GameState.Player.Position = Vector3.Zero;
        FailureSimulation.SimulateStuck(UnstuckState.InitialAttempt);
        FailureSimulation.SimulateDeath("Test death");

        // Act
        FailureSimulation.ClearHistory();

        // Assert
        IReadOnlyList<SimulatedStuckEvent> stuckEvents = FailureSimulation.GetRecentStuckEvents(TimeSpan.FromMinutes(1));
        IReadOnlyList<SimulatedDeathEvent> deathEvents = FailureSimulation.GetRecentDeathEvents(TimeSpan.FromMinutes(1));

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

        FailureSimulation.OnStuckSimulated += evt => stuckEvents.Add(evt);
        FailureSimulation.OnDeathSimulated += evt => deathEvents.Add(evt);
        FailureSimulation.OnHotZoneCreated += zone => hotZone = zone;

        // Act - simulate getting stuck
        GameState.Player.Position = new Vector3(100f, 100f, 0f);
        FailureSimulation.SimulateStuck(UnstuckState.BreadcrumbBacktrack, durationMs: 10000, attemptCount: 5);

        // Simulate death at same location
        FailureSimulation.SimulateDeath("Died while stuck");

        // Simulate hot zone creation (3 deaths in same area)
        FailureSimulation.SimulateHotZone(FailureType.Death, failureCount: 3, radius: 10f);

        // Assert
        stuckEvents.Should().HaveCount(1);
        stuckEvents[0].State.Should().Be(UnstuckState.BreadcrumbBacktrack);

        deathEvents.Should().HaveCount(4); // 1 direct + 3 from hot zone
        hotZone.Should().NotBeNull();
        hotZone!.FailureCount.Should().Be(3);
        hotZone.PrimaryType.Should().Be(FailureType.Death);
    }

    [Fact]
    public void SimulateStuck_ShouldCreateStuckEventWithCurrentMapId()
    {
        // Arrange
        int expectedMapId = 1941; // Eversong Woods
        GameState.MapId = expectedMapId;
        SimulatedStuckEvent? capturedEvent = null;
        FailureSimulation.OnStuckSimulated += evt => capturedEvent = evt;

        // Act
        FailureSimulation.SimulateStuck(UnstuckState.InitialAttempt);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.MapId.Should().Be(expectedMapId,
            "stuck events must reflect the current zone for zone-specific analysis");
    }

    protected override void CleanupTestData()
    {
        _blacklist.Dispose();
        base.CleanupTestData();
    }
}
