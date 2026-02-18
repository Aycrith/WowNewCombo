// Copyright (c) 2025 WowClassicGrindBot Contributors
// Licensed under the MIT License

using System;
using System.Collections.Generic;
using System.Numerics;

using Core;
using Core.Analytics;
using Core.Testing;

using FluentAssertions;

using MockWoWClient.GameState;

using Xunit;

namespace CoreUnitTests.Integration;

/// <summary>
/// Comprehensive tests validating MockWoWClient functionality.
/// These tests ensure the synthetic WoW client works correctly
/// so that all other tests can rely on it.
/// </summary>
public sealed class MockWoWClientComprehensiveTests : IntegrationTestBase
{
    [Fact]
    public void GameState_InitialState_PlayerAtOrigin()
    {
        AssertPlayerAt(Vector3.Zero);
        Assert.False(GameState.InCombat);
    }

    [Fact]
    public void GameState_PlayerPosition_SetAndRetrieve()
    {
        // Arrange
        var newPosition = new Vector3(100f, 200f, 50f);

        // Act
        GameState.Player.Position = newPosition;

        // Assert
        AssertPlayerAt(newPosition);
    }

    [Fact]
    public void GameState_PlayerDirection_SetAndRetrieve()
    {
        // Arrange
        const float expectedDirection = 45f;

        // Act
        GameState.Player.Direction = expectedDirection;

        // Assert
        GameState.Player.Direction.Should().Be(expectedDirection);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(1f)]
    [InlineData(5f)]
    public void AssertPlayerAt_Tolerance_WorksCorrectly(float tolerance)
    {
        // Arrange
        var expected = new Vector3(100f, 100f, 0f);
        GameState.Player.Position = expected + new Vector3(tolerance / 2, 0, 0);

        // Act & Assert - Should pass with tolerance
        AssertPlayerAt(expected, tolerance);
    }

    [Fact]
    public void FailureSimulation_StuckEvent_UpdatesPlayerState()
    {
        // Arrange
        var initialPos = GameState.Player.Position;
        SimulatedStuckEvent? capturedEvent = null;
        FailureSimulation.OnStuckSimulated += evt => capturedEvent = evt;

        // Act
        FailureSimulation.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Position.Should().Be(initialPos);
        capturedEvent.State.Should().Be(UnstuckState.InitialAttempt);
        capturedEvent.DurationMs.Should().Be(3000);
        capturedEvent.AttemptCount.Should().Be(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void FailureSimulation_MultipleStuckEvents_AllCaptured(int count)
    {
        // Arrange
        var events = new List<SimulatedStuckEvent>();
        FailureSimulation.OnStuckSimulated += evt => events.Add(evt);

        // Act
        for (int i = 0; i < count; i++)
        {
            FailureSimulation.SimulateStuck(UnstuckState.InitialAttempt, 1000, 1);
        }

        // Assert
        events.Should().HaveCount(count);
    }

    [Fact]
    public void FailureSimulation_DeathEvent_SetsPlayerDead()
    {
        // Arrange
        var deathPosition = new Vector3(500f, 500f, 0f);
        GameState.Player.Position = deathPosition;
        GameState.Player.Health = 100;
        SimulatedDeathEvent? capturedEvent = null;
        FailureSimulation.OnDeathSimulated += evt => capturedEvent = evt;

        // Act
        FailureSimulation.SimulateDeath("Test death");

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Position.Should().Be(deathPosition);
        capturedEvent.Cause.Should().Be("Test death");
        GameState.Player.IsDead.Should().BeTrue();
    }

    [Fact]
    public void FailureSimulation_HotZoneCreation_TriggersEvent()
    {
        // Arrange
        SimulatedHotZone? capturedZone = null;
        FailureSimulation.OnHotZoneCreated += zone => capturedZone = zone;

        var zoneCenter = new Vector3(100f, 100f, 0f);
        GameState.Player.Position = zoneCenter;

        // Act
        FailureSimulation.SimulateHotZone(FailureType.Stuck, failureCount: 5, radius: 25f);

        // Assert
        capturedZone.Should().NotBeNull();
        capturedZone!.FailureCount.Should().Be(5);
        capturedZone.Radius.Should().Be(25f);
        capturedZone.Center.Should().Be(zoneCenter);
    }

    [Fact]
    public void FailureSimulation_RehabEvent_UpdatesZoneState()
    {
        // Arrange
        var zonePos = new Vector3(100f, 100f, 0f);
        FailureSimulation.SimulateHotZone(FailureType.Stuck, failureCount: 5, radius: 25f);

        SimulatedRehabEvent? capturedEvent = null;
        FailureSimulation.OnRehabSimulated += evt => capturedEvent = evt;

        // Act
        FailureSimulation.SimulateRehabilitation(zonePos, 25f);

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Position.Should().Be(zonePos);
        capturedEvent.Radius.Should().Be(25f);
        capturedEvent.SeverityReduction.Should().Be(0.5f);
    }

    [Fact]
    public void GameState_NpcSpawn_CreatesEntity()
    {
        // Arrange
        var spawnPos = new Vector3(50f, 50f, 0f);

        // Act - Use correct signature: SpawnNpc(name, level, health, position, hostile)
        var npc = GameState.SpawnNpc("TestNPC", 10, 100, spawnPos, true);

        // Assert
        npc.Should().NotBeNull();
        npc.Position.Should().Be(spawnPos);
        npc.Level.Should().Be(10);
        npc.Name.Should().Be("TestNPC");
        GameState.Npcs.Should().Contain(npc);
    }

    [Fact]
    public void GameState_NpcSpawn_AssignsUniqueIds()
    {
        // Arrange & Act
        var npc1 = GameState.SpawnNpc("NPC1", 1, 10, Vector3.Zero, true);
        var npc2 = GameState.SpawnNpc("NPC2", 1, 10, Vector3.Zero, true);
        var npc3 = GameState.SpawnNpc("NPC3", 1, 10, Vector3.Zero, true);

        // Assert
        npc1.Id.Should().NotBe(npc2.Id);
        npc2.Id.Should().NotBe(npc3.Id);
        npc1.Id.Should().NotBe(npc3.Id);
    }

    [Fact]
    public void GameState_CombatSimulation_UpdatesState()
    {
        // Arrange - Spawn an NPC (which may start combat)
        var npc = GameState.SpawnNpc("Enemy", 5, 50, new Vector3(10f, 10f, 0f), true);

        // Assert
        GameState.Npcs.Should().Contain(npc);
    }

    [Fact]
    public void FailureSimulation_MultiMobAggro_SpawnsCorrectNumber()
    {
        // Arrange
        GameState.Player.Position = Vector3.Zero;
        GameState.Player.Level = 10;

        // Act
        var spawnedMobs = FailureSimulation.SimulateMultiMobAggro(mobCount: 5, maxDistance: 20f);

        // Assert
        spawnedMobs.Should().HaveCount(5);
        GameState.Npcs.Should().HaveCount(5);
    }

    [Fact]
    public void FailureSimulation_ClearHistory_RemovesAllEvents()
    {
        // Arrange
        GameState.Player.Position = Vector3.Zero;
        FailureSimulation.SimulateStuck(UnstuckState.InitialAttempt, 3000, 1);
        FailureSimulation.SimulateDeath("Test");
        FailureSimulation.SimulateHotZone(FailureType.Stuck, 3, 10f);

        // Act
        FailureSimulation.ClearHistory();

        // Assert
        FailureSimulation.GetRecentStuckEvents(TimeSpan.FromMinutes(1)).Should().BeEmpty();
        FailureSimulation.GetRecentDeathEvents(TimeSpan.FromMinutes(1)).Should().BeEmpty();
    }

    [Fact]
    public void EventCapture_CaptureEvent_WorksSynchronously()
    {
        // Arrange
        SimulatedStuckEvent? capturedEvent = null;

        // Act
        capturedEvent = CaptureEvent<SimulatedStuckEvent>(
            h => FailureSimulation.OnStuckSimulated += h,
            () => FailureSimulation.SimulateStuck(UnstuckState.InitialAttempt, 1000, 1)
        );

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent.State.Should().Be(UnstuckState.InitialAttempt);
    }

    [Fact]
    public void FullScenario_StuckDeathHotZone_Rehabilitation()
    {
        // This test validates the complete failure recovery workflow

        // Arrange
        var problemArea = new Vector3(1000f, 1000f, 0f);
        GameState.Player.Position = problemArea;

        var stuckEvents = new List<SimulatedStuckEvent>();
        var deathEvents = new List<SimulatedDeathEvent>();
        var hotZones = new List<SimulatedHotZone>();
        var rehabEvents = new List<SimulatedRehabEvent>();

        FailureSimulation.OnStuckSimulated += evt => stuckEvents.Add(evt);
        FailureSimulation.OnDeathSimulated += evt => deathEvents.Add(evt);
        FailureSimulation.OnHotZoneCreated += zone => hotZones.Add(zone);
        FailureSimulation.OnRehabSimulated += evt => rehabEvents.Add(evt);

        // Act - Simulate failures
        FailureSimulation.SimulateStuck(UnstuckState.InitialAttempt, 5000, 2);
        FailureSimulation.SimulateDeath("Died in hot zone");
        FailureSimulation.SimulateHotZone(FailureType.Death, failureCount: 3, radius: 20f);

        // Act - Simulate rehabilitation
        FailureSimulation.SimulateRehabilitation(problemArea, 25f);

        // Assert
        stuckEvents.Should().HaveCount(1);
        deathEvents.Should().HaveCount(4);
        hotZones.Should().HaveCount(1);
        rehabEvents.Should().HaveCount(1);
    }
}
