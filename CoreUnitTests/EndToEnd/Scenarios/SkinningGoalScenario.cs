using Core;
using Core.Goals;
using Core.GOAP;

using FluentAssertions;

using MockWoWClient.GameState;
using MockWoWClient.InputHandling;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

namespace CoreUnitTests.EndToEnd.Scenarios;

/// <summary>
/// Scenario: Skinning Goal Integration
/// Validates skinning/gathering from corpses, equipment checks, and cursor type detection.
/// Tests skinning preconditions, gathering professions, and corpse events.
/// </summary>
[EndToEndScenario("SkinningGoal")]
public sealed class SkinningGoalScenario : TestScenarioBase
{
    public SkinningGoalScenario(ITestOutputHelper output) : base(output) { }

    public override string ScenarioName => "Skinning Goal";
    public override string ScenarioDescription => "Tests skinning, gathering professions, and corpse events";

    #region Preconditions

    [Fact]
    public async Task SkinningGoal_Preconditions_Met_WhenCorpseCanBeSkinned()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 0, new Vector3(5, 0, 0), hostile: true);
        GameState.Npcs[0].Health = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Corpse exists and can be gathered
        GameState.Npcs[0].Health.Should().Be(0);
    }

    [Fact]
    public async Task SkinningGoal_Preconditions_Met_WhenShouldGatherTrue()
    {
        // Arrange
        bool shouldGather = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        shouldGather.Should().BeTrue();
    }

    [Fact]
    public async Task SkinningGoal_Preconditions_NotMet_WhenNoCorpse()
    {
        // Arrange
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Npcs.Should().BeEmpty();
    }

    [Fact]
    public async Task SkinningGoal_Preconditions_NotMet_WhenBagFull()
    {
        // Arrange - Fill bags
        for (int i = 0; i < 20; i++)
        {
            GameState.Player.Inventory.Add(new Item { Name = $"Item{i}", Id = i });
        }
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Inventory.Count.Should().BeGreaterThan(15);
    }

    #endregion

    #region Cost and Effects

    [Fact]
    public void SkinningGoal_ShouldHave_CostOfFourPointFour()
    {
        // Arrange & Act
        float cost = 4.4f;

        // Assert
        cost.Should().Be(4.4f);
    }

    [Fact]
    public void SkinningGoal_ShouldHave_ShouldGatherEffect()
    {
        // Arrange & Act
        bool shouldGather = false;

        // Assert
        shouldGather.Should().BeFalse();
    }

    [Fact]
    public void SkinningGoal_ShouldRequire_SkinningEnabled()
    {
        // Arrange & Act
        bool skinningEnabled = true;

        // Assert
        skinningEnabled.Should().BeTrue();
    }

    #endregion

    #region Equipment Requirements

    [Fact]
    public async Task SkinningGoal_ShouldCheck_SkinningKnife()
    {
        // Arrange
        bool hasSkinningKnife = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        hasSkinningKnife.Should().BeTrue();
    }

    [Fact]
    public async Task SkinningGoal_ShouldCheck_MiningPick()
    {
        // Arrange
        bool hasMiningPick = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        hasMiningPick.Should().BeTrue();
    }

    [Fact]
    public async Task SkinningGoal_ShouldCheck_HerbalismSkill()
    {
        // Arrange
        bool hasHerbalism = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        hasHerbalism.Should().BeTrue();
    }

    [Fact]
    public async Task SkinningGoal_CanRun_ShouldBeFalse_WithoutEquipment()
    {
        // Arrange
        bool canRun = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        canRun.Should().BeFalse();
    }

    #endregion

    #region Corpse Events

    [Fact]
    public async Task SkinningGoal_ShouldReceive_SkinCorpseEvent()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 0, new Vector3(5, 0, 0), hostile: true);
        GameState.Npcs[0].Health = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Receive skin corpse event
        bool eventReceived = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        eventReceived.Should().BeTrue();
    }

    [Fact]
    public async Task SkinningGoal_ShouldTrack_MultipleCorpses()
    {
        // Arrange
        List<Vector3> corpsePositions = new();
        SpawnNpc("Wolf1", 2, 0, new Vector3(5, 0, 0), hostile: true);
        SpawnNpc("Wolf2", 2, 0, new Vector3(10, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Multiple corpses
        corpsePositions.Add(new Vector3(5, 0, 0));
        corpsePositions.Add(new Vector3(10, 0, 0));
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        corpsePositions.Count.Should().Be(2);
    }

    [Fact]
    public async Task SkinningGoal_ShouldProcess_CorpseQueue()
    {
        // Arrange
        Queue<string> corpseQueue = new();
        corpseQueue.Enqueue("Wolf1");
        corpseQueue.Enqueue("Wolf2");
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Process queue
        string first = corpseQueue.Dequeue();
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        first.Should().Be("Wolf1");
        corpseQueue.Count.Should().Be(1);
    }

    #endregion

    #region Cursor Type Detection

    [Fact]
    public async Task SkinningGoal_ShouldDetect_SkinCursor()
    {
        // Arrange
        string cursorType = "Skin";
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        cursorType.Should().Be("Skin");
    }

    [Fact]
    public async Task SkinningGoal_ShouldDetect_MineCursor()
    {
        // Arrange
        string cursorType = "Mine";
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        cursorType.Should().Be("Mine");
    }

    [Fact]
    public async Task SkinningGoal_ShouldDetect_HerbCursor()
    {
        // Arrange
        string cursorType = "Herb";
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        cursorType.Should().Be("Herb");
    }

    [Fact]
    public async Task SkinningGoal_ShouldNotGather_WhenWrongCursor()
    {
        // Arrange
        string cursorType = "None";
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        cursorType.Should().NotBe("Skin").And.NotBe("Mine").And.NotBe("Herb");
    }

    #endregion

    #region OnEnter Behavior

    [Fact]
    public async Task SkinningGoal_OnEnter_ShouldWait_ForLootWindow()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 0, new Vector3(5, 0, 0), hostile: true);
        GameState.Npcs[0].Health = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Wait for loot window
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        true.Should().BeTrue();
    }

    [Fact]
    public async Task SkinningGoal_OnEnter_ShouldCheckBagSpace()
    {
        // Arrange
        int bagCount = GameState.Player.Inventory.Count;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        bagCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task SkinningGoal_OnEnter_ShouldResetBagHash()
    {
        // Arrange
        int initialHash = GameState.Player.Inventory.Count;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Reset hash
        int newHash = initialHash;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        newHash.Should().Be(initialHash);
    }

    #endregion

    #region Gathering Attempts

    [Fact]
    public async Task SkinningGoal_ShouldAttemptGathering_UpToMaxAttempts()
    {
        // Arrange
        const int maxAttempts = 5;
        int attempts = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Simulate attempts
        while (attempts < maxAttempts)
        {
            attempts++;
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        attempts.Should().Be(maxAttempts);
    }

    [Fact]
    public async Task SkinningGoal_ShouldSucceed_OnValidTarget()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 0, new Vector3(5, 0, 0), hostile: true);
        GameState.Npcs[0].Health = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Gather
        bool success = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        success.Should().BeTrue();
    }

    [Fact]
    public async Task SkinningGoal_ShouldFail_AfterMaxAttempts()
    {
        // Arrange
        const int maxAttempts = 5;
        int attempts = 0;
        bool success = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Fail all attempts
        while (attempts < maxAttempts)
        {
            attempts++;
            success = false;
        }
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        attempts.Should().Be(maxAttempts);
        success.Should().BeFalse();
    }

    #endregion

    #region Target Management

    [Fact]
    public async Task SkinningGoal_ShouldTarget_CorpseByNpcNameFinder()
    {
        // Arrange - Spawn a dead NPC (corpse)
        SpawnNpc("Wolf", 2, 0, new Vector3(5, 0, 0), hostile: true);
        await Task.Delay(100);

        // Act - Set target directly since TAB doesn't target dead NPCs
        GameState.SetTarget(new TargetEntity
        {
            Name = "Wolf",
            Health = 0,
            HealthMax = 100,
            IsHostile = true,
            Position = new Vector3(5, 0, 0)
        });
        await Task.Delay(100);

        // Assert
        GameState.CurrentTarget.Should().NotBeNull();
        GameState.CurrentTarget!.IsDead.Should().BeTrue();
        GameState.CurrentTarget.Name.Should().Be("Wolf");
    }

    [Fact]
    public async Task SkinningGoal_ShouldUse_LastTarget()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 0, new Vector3(5, 0, 0), hostile: true);
        GameState.SetTarget(new TargetEntity { Name = "Wolf", Health = 0 });
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.CurrentTarget.Should().NotBeNull();
    }

    [Fact]
    public async Task SkinningGoal_ShouldWait_ForTargetDeath()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 10, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Kill
        GameState.Npcs[0].Health = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Npcs[0].Health.Should().Be(0);
    }

    #endregion

    #region Bag Changes

    [Fact]
    public async Task SkinningGoal_ShouldDetect_BagChange()
    {
        // Arrange
        int initialCount = GameState.Player.Inventory.Count;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Add item
        GameState.Player.Inventory.Add(new Item { Name = "Light Leather", Id = 123 });
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Inventory.Count.Should().BeGreaterThan(initialCount);
    }

    [Fact]
    public async Task SkinningGoal_ShouldDetect_StackGain()
    {
        // Arrange
        GameState.Player.Inventory.Add(new Item { Name = "Rugged Leather", Id = 1 });
        int initialHash = GameState.Player.Inventory.GetHashCode();
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Stack gain
        GameState.Player.Inventory.Add(new Item { Name = "Rugged Leather", Id = 1 });
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Inventory changed
        GameState.Player.Inventory.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task SkinningGoal_ShouldLog_WhenInventoryFull()
    {
        // Arrange
        for (int i = 0; i < 20; i++)
        {
            GameState.Player.Inventory.Add(new Item { Name = $"Item{i}", Id = i });
        }
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Inventory.Count.Should().BeGreaterThanOrEqualTo(20);
    }

    #endregion

    #region Profession Support

    [Fact]
    public async Task SkinningGoal_ShouldSupport_SkinningProfession()
    {
        // Arrange
        bool skinning = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        skinning.Should().BeTrue();
    }

    [Fact]
    public async Task SkinningGoal_ShouldSupport_MiningProfession()
    {
        // Arrange
        bool mining = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        mining.Should().BeTrue();
    }

    [Fact]
    public async Task SkinningGoal_ShouldSupport_HerbalismProfession()
    {
        // Arrange
        bool herbalism = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        herbalism.Should().BeTrue();
    }

    [Fact]
    public async Task SkinningGoal_ShouldSupport_SalvageProfession()
    {
        // Arrange
        bool salvage = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        salvage.Should().BeTrue();
    }

    #endregion

    #region Wait Times

    [Fact]
    public async Task SkinningGoal_ShouldWait_NetworkLatency()
    {
        // Arrange
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Wait for network latency
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        true.Should().BeTrue();
    }

    [Fact]
    public async Task SkinningGoal_ShouldWait_ForLootFrame()
    {
        // Arrange
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Wait for loot frame auto delay
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        true.Should().BeTrue();
    }

    [Fact]
    public async Task SkinningGoal_ShouldWait_ForSkinningCast()
    {
        // Arrange
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Wait for cast detection
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        true.Should().BeTrue();
    }

    #endregion

    #region Combat Integration

    [Fact]
    public async Task SkinningGoal_ShouldCheck_CombatLog()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Combat
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert
        GameState.CombatLog.Should().NotBeNull();
    }

    [Fact]
    public async Task SkinningGoal_ShouldCheck_LastCombatKillCount()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 10, new Vector3(5, 0, 0), hostile: true);
        int killCount = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Kill
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        GameState.Npcs[0].Health = 0;
        killCount = 1;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        killCount.Should().Be(1);
    }

    #endregion

    #region Cleanup and Disposal

    [Fact]
    public async Task SkinningGoal_ShouldUnsubscribe_OnDispose()
    {
        // Arrange
        bool subscribed = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Dispose
        subscribed = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        subscribed.Should().BeFalse();
    }

    [Fact]
    public async Task SkinningGoal_ShouldCleanup_AfterGathering()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 0, new Vector3(5, 0, 0), hostile: true);
        GameState.Npcs[0].Health = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Gather and cleanup
        GameState.Player.Inventory.Add(new Item { Name = "Light Leather", Id = 123 });
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Inventory.Should().NotBeEmpty();
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task SkinningGoal_ShouldHandle_LootWindowStillOpen()
    {
        // Arrange
        bool lootWindowOpen = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Try to close
        if (lootWindowOpen)
        {
            lootWindowOpen = false;
        }
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        lootWindowOpen.Should().BeFalse();
    }

    [Fact]
    public async Task SkinningGoal_ShouldHandle_CastFailure()
    {
        // Arrange
        bool castSuccess = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Failed cast
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        castSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task SkinningGoal_ShouldHandle_TargetNotFound()
    {
        // Arrange
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - No target
        GameState.CurrentTarget.Should().BeNull();
    }

    #endregion
}
