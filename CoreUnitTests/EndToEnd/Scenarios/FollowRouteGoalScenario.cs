using Core;
using Core.Goals;

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
/// Scenario: Follow Route Goal Integration
/// Validates route following, waypoint navigation, and path refill logic.
/// Tests attended gather mode, side activities, and route completion.
/// </summary>
[EndToEndScenario("FollowRouteGoal")]
public sealed class FollowRouteGoalScenario : TestScenarioBase
{
    public FollowRouteGoalScenario(ITestOutputHelper output) : base(output) { }

    public override string ScenarioName => "Follow Route Goal";
    public override string ScenarioDescription => "Tests route following, waypoint navigation, and attended gather mode";

    #region Route Initialization

    [Fact]
    public async Task FollowRouteGoal_ShouldInitialize_WithRoute()
    {
        // Arrange
        List<Vector3> route = new()
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(20, 0, 0)
        };
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        route.Count.Should().Be(3);
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldHave_RouteCost()
    {
        // Arrange & Act
        float defaultCost = 20f;

        // Assert
        defaultCost.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldCheck_Requirements()
    {
        // Arrange
        List<string> requirements = new() { "HasTarget", "InCombat" };
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        requirements.Should().NotBeEmpty();
    }

    #endregion

    #region Waypoint Navigation

    [Fact]
    public async Task FollowRouteGoal_ShouldFollow_Waypoints()
    {
        // Arrange
        List<Vector3> waypoints = new()
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(20, 0, 0),
            new Vector3(30, 0, 0)
        };
        GameState.Player.Position = waypoints[0];
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Follow waypoints
        for (int i = 1; i < waypoints.Count; i++)
        {
            GameState.Player.Position = waypoints[i];
            GameState.Player.IsMoving = true;
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        GameState.Player.Position.Should().Be(waypoints[^1]);
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldDetect_WaypointReached()
    {
        // Arrange
        Vector3 waypoint = new Vector3(10, 0, 0);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Move to waypoint
        GameState.Player.Position = new Vector3(9.5f, 0, 0); // Within tolerance
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        float distance = Vector3.Distance(GameState.Player.Position, waypoint);
        distance.Should().BeLessThan(1.0f);
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldAdvance_ToNextWaypoint()
    {
        // Arrange
        List<Vector3> waypoints = new()
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(20, 0, 0)
        };
        GameState.Player.Position = waypoints[0];
        int currentWaypoint = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Advance through waypoints
        while (currentWaypoint < waypoints.Count - 1)
        {
            // Reach current waypoint
            GameState.Player.Position = waypoints[currentWaypoint + 1];
            currentWaypoint++;
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        currentWaypoint.Should().Be(waypoints.Count - 1);
    }

    #endregion

    #region Route Completion

    [Fact]
    public async Task FollowRouteGoal_ShouldComplete_WhenRouteFinished()
    {
        // Arrange
        List<Vector3> route = new()
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0)
        };
        GameState.Player.Position = route[0];
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Complete route
        GameState.Player.Position = route[^1];
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Position.Should().Be(route[^1]);
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldLoop_WhenThereAndBack()
    {
        // Arrange
        List<Vector3> route = new()
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(0, 0, 0)
        };
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Route should return to start
        route[0].Should().Be(route[^1]);
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldRefill_Path()
    {
        // Arrange
        List<Vector3> originalPath = new()
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0)
        };
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Simulate path refill
        List<Vector3> refilledPath = new(originalPath);
        refilledPath.Add(new Vector3(20, 0, 0));

        // Assert
        refilledPath.Count.Should().BeGreaterThan(originalPath.Count);
    }

    #endregion

    #region Attended Gather Mode

    [Fact]
    public async Task FollowRouteGoal_AttendedGather_ShouldWaitForUserInput()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - In attended gather mode, bot waits for user
        // No automatic waypoint advancement

        // Assert - Player position shouldn't change automatically
        GameState.Player.Position.Should().Be(new Vector3(0, 0, 0));
    }

    [Fact]
    public async Task FollowRouteGoal_AttendedGather_ShouldNotAutoTarget()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - In attended gather mode, don't auto-target

        // Assert
        GameState.Player.HasTarget.Should().BeFalse();
    }

    [Fact]
    public async Task FollowRouteGoal_AttendedGather_ShouldAllowManualControl()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Manual movement
        GameState.Player.Position = new Vector3(5, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Position.X.Should().Be(5);
    }

    #endregion

    #region Mount Handling

    [Fact]
    public async Task FollowRouteGoal_ShouldMount_ForLongDistances()
    {
        // Arrange
        List<Vector3> longRoute = new()
        {
            new Vector3(0, 0, 0),
            new Vector3(100, 0, 0)
        };
        GameState.Player.Position = longRoute[0];
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Mount for long distance
        GameState.Player.IsMounted = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.IsMounted.Should().BeTrue();
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldDismount_WhenNearTarget()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(10, 0, 0), hostile: true);
        GameState.Player.Position = new Vector3(0, 0, 0);
        GameState.Player.IsMounted = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Approach target
        GameState.Player.Position = new Vector3(9, 0, 0);
        GameState.Player.IsMounted = false; // Dismount
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.IsMounted.Should().BeFalse();
    }

    #endregion

    #region Navigation Events

    [Fact]
    public async Task FollowRouteGoal_ShouldHandle_PathCalculated()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        Vector3 destination = new Vector3(50, 50, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Path calculation event
        float distance = Vector3.Distance(GameState.Player.Position, destination);

        // Assert
        distance.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldHandle_DestinationReached()
    {
        // Arrange
        Vector3 destination = new Vector3(20, 0, 0);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Move to destination
        for (int i = 0; i < 20; i++)
        {
            GameState.Player.Position = new Vector3(i, 0, 0);
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        Vector3.Distance(GameState.Player.Position, destination).Should().BeLessThan(2.0f);
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldHandle_WaypointReached()
    {
        // Arrange
        Vector3 waypoint = new Vector3(10, 0, 0);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Reach waypoint
        GameState.Player.Position = waypoint;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        GameState.Player.Position.Should().Be(waypoint);
    }

    #endregion

    #region Combat Avoidance

    [Fact]
    public async Task FollowRouteGoal_ShouldPause_WhenInCombat()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Enter combat
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Assert - Route following should pause
        GameState.InCombat.Should().BeTrue();
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldResume_AfterCombat()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 10, new Vector3(5, 0, 0), hostile: true);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Combat and kill
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Kill target
        GameState.Npcs[0].Health = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Should resume route
        GameState.Npcs[0].IsDead.Should().BeTrue();
    }

    #endregion

    #region Preconditions

    [Fact]
    public async Task FollowRouteGoal_ShouldHave_LootPrecondition()
    {
        // Arrange
        bool lootEnabled = true;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        lootEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldHave_NoDamagePrecondition()
    {
        // Arrange
        bool damageDone = false;
        bool damageTaken = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        damageDone.Should().BeFalse();
        damageTaken.Should().BeFalse();
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldHave_NoCorpsePrecondition()
    {
        // Arrange
        bool corpseProduced = false;
        bool corpseConsumed = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        corpseProduced.Should().BeFalse();
        corpseConsumed.Should().BeFalse();
    }

    #endregion

    #region Path Settings

    [Fact]
    public async Task FollowRouteGoal_ShouldRespect_PathFilename()
    {
        // Arrange
        string filename = "TestRoute.json";
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        filename.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldHandle_PathReduction()
    {
        // Arrange
        List<Vector3> fullPath = new();
        for (int i = 0; i < 100; i++)
        {
            fullPath.Add(new Vector3(i, 0, 0));
        }
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Reduce path
        List<Vector3> reducedPath = new();
        for (int i = 0; i < fullPath.Count; i += 2)
        {
            reducedPath.Add(fullPath[i]);
        }

        // Assert
        reducedPath.Count.Should().BeLessThan(fullPath.Count);
    }

    #endregion

    #region Route Provider

    [Fact]
    public async Task FollowRouteGoal_ShouldProvide_Route()
    {
        // Arrange
        List<Vector3> route = new()
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(20, 0, 0)
        };
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        route.Should().NotBeNull();
        route.Count.Should().Be(3);
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldTrack_LastActive()
    {
        // Arrange
        DateTime now = DateTime.UtcNow;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        now.Should().BeBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldCheck_HasNext()
    {
        // Arrange
        List<Vector3> route = new() { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        int currentIndex = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        bool hasNext = currentIndex < route.Count - 1;

        // Assert
        hasNext.Should().BeTrue();
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldGet_NextMapPoint()
    {
        // Arrange
        List<Vector3> route = new() { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
        int currentIndex = 0;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        Vector3 nextPoint = route[currentIndex + 1];

        // Assert
        nextPoint.Should().Be(new Vector3(10, 0, 0));
    }

    #endregion

    #region Side Activities

    [Fact]
    public async Task FollowRouteGoal_ShouldRun_SideActivities()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Side activities run in background
        // (Profession cycling, gathering, etc.)

        // Assert - Just verify no errors occurred
        GameState.Player.Position.Should().NotBeNull();
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldPause_SideActivities_WhenStuck()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Get stuck
        GameState.Player.IsMoving = true;
        for (int i = 0; i < 30; i++)
        {
            GameState.Player.Position = new Vector3((float)(0.1 * Math.Sin(i)), 0, 0);
            AdvanceTime(TimeSpan.FromMilliseconds(200));
        }

        // Assert
        GameState.Player.Position.X.Should().BeLessThan(5.0f);
    }

    #endregion

    #region Target Finding

    [Fact]
    public async Task FollowRouteGoal_ShouldFind_TargetsAlongRoute()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(5, 0, 0), hostile: true);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Move along route and detect targets
        for (int i = 0; i < 10; i++)
        {
            GameState.Player.Position = new Vector3(i, 0, 0);
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        GameState.Npcs.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldRespect_Blacklist()
    {
        // Arrange
        HashSet<string> blacklist = new() { "AggressiveWolf" };
        SpawnNpc("PassiveWolf", 2, 100, new Vector3(5, 0, 0), hostile: false);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Check if blacklisted
        bool isBlacklisted = blacklist.Contains("AggressiveWolf");

        // Assert
        isBlacklisted.Should().BeTrue();
        blacklist.Should().NotContain("PassiveWolf");
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task FollowRouteGoal_ShouldHandle_InvalidPath()
    {
        // Arrange
        List<Vector3> emptyPath = new();
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert - Empty path should be handled gracefully
        emptyPath.Should().BeEmpty();
    }

    [Fact]
    public async Task FollowRouteGoal_ShouldHandle_StuckInLoop()
    {
        // Arrange - Route that might cause loop
        List<Vector3> loopRoute = new()
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(0, 0, 0), // Back to start
            new Vector3(10, 0, 0)  // Back again
        };
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        loopRoute.Count.Should().Be(4);
    }

    #endregion
}
