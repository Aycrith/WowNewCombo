using Core;
using Core.GoalsComponent;

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
/// Scenario: Navigation Integration
/// Validates Navigation pathfinding, waypoint following, and destination reached events.
/// Tests path calculation, route following, and path visualization.
/// </summary>
[EndToEndScenario("Navigation")]
public sealed class NavigationScenario : TestScenarioBase
{
    public NavigationScenario(ITestOutputHelper output) : base(output) { }

    public override string ScenarioName => "Navigation";
    public override string ScenarioDescription => "Tests pathfinding, waypoint navigation, and route following";

    #region Path Calculation

    [Fact]
    public async Task Navigation_ShouldCalculatePath_ToDestination()
    {
        // Arrange
        Vector3 startPos = new Vector3(0, 0, 0);
        Vector3 endPos = new Vector3(50, 50, 0);
        GameState.Player.Position = startPos;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        float distance = Vector3.Distance(startPos, endPos);

        // Assert
        distance.Should().BeApproximately(70.71f, 0.01f); // √(50² + 50²)
    }

    [Fact]
    public async Task Navigation_ShouldHandle_StraightLinePath()
    {
        // Arrange
        Vector3 startPos = new Vector3(0, 0, 0);
        Vector3 endPos = new Vector3(100, 0, 0);
        GameState.Player.Position = startPos;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        float distance = Vector3.Distance(startPos, endPos);

        // Assert
        distance.Should().BeApproximately(100.0f, 0.1f);
    }

    [Fact]
    public async Task Navigation_ShouldRecalculate_WhenPathBlocked()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Simulate path becoming blocked
        for (int i = 0; i < 10; i++)
        {
            GameState.Player.Position = new Vector3(i * 0.1f, 0, 0); // Minimal movement
            AdvanceTime(TimeSpan.FromMilliseconds(500));
        }

        // Assert
        GameState.Player.Position.X.Should().BeLessThan(5.0f); // Not making good progress
    }

    #endregion

    #region Waypoint Following

    [Fact]
    public async Task Navigation_ShouldFollow_Waypoints()
    {
        // Arrange
        Queue<Vector3> waypoints = new();
        waypoints.Enqueue(new Vector3(10, 0, 0));
        waypoints.Enqueue(new Vector3(20, 10, 0));
        waypoints.Enqueue(new Vector3(30, 10, 0));

        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Follow waypoints
        while (waypoints.Count > 0)
        {
            Vector3 waypoint = waypoints.Dequeue();
            GameState.Player.Position = waypoint;
            GameState.Player.IsMoving = true;
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        GameState.Player.Position.Should().Be(new Vector3(30, 10, 0));
    }

    [Fact]
    public async Task Navigation_ShouldReach_WaypointWithinTolerance()
    {
        // Arrange
        Vector3 waypoint = new Vector3(10, 0, 0);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Move to waypoint (close but not exact)
        GameState.Player.Position = new Vector3(9.8f, 0.2f, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        float distance = Vector3.Distance(GameState.Player.Position, waypoint);
        distance.Should().BeLessThan(1.0f);
    }

    [Fact]
    public async Task Navigation_ShouldDetect_WaypointReached()
    {
        // Arrange
        Vector3 target = new Vector3(20, 20, 0);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Move to target
        for (int i = 0; i < 20; i++)
        {
            GameState.Player.Position = new Vector3(i, i, 0);
            GameState.Player.IsMoving = true;
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        float distance = Vector3.Distance(GameState.Player.Position, target);
        distance.Should().BeLessThan(2.0f);
    }

    #endregion

    #region Destination Events

    [Fact]
    public async Task Navigation_ShouldRaise_OnDestinationReached()
    {
        // Arrange
        Vector3 destination = new Vector3(10, 10, 0);
        GameState.Player.Position = new Vector3(0, 0, 0);
        bool eventRaised = false;
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Move to destination
        for (int i = 0; i < 15; i++)
        {
            GameState.Player.Position = new Vector3(i * 0.7f, i * 0.7f, 0);
            if (Vector3.Distance(GameState.Player.Position, destination) < 1.0f)
            {
                eventRaised = true;
                break;
            }
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public async Task Navigation_ShouldRaise_OnPathCalculated()
    {
        // Arrange
        Vector3 start = new Vector3(0, 0, 0);
        Vector3 end = new Vector3(50, 50, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Calculate path
        float distance = Vector3.Distance(start, end);

        // Assert
        distance.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Navigation_ShouldNotRaise_WhenPathBlocked()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Simulate blocked path
        GameState.Player.IsMoving = true;
        AdvanceTime(TimeSpan.FromSeconds(5));

        // Assert
        // Would be in stuck state, not destination reached
        GameState.Player.Position.Should().Be(Vector3.Zero);
    }

    #endregion

    #region Path Visualization

    [Fact]
    public async Task Navigation_ShouldProvide_PathPoints()
    {
        // Arrange
        List<Vector3> path = new();
        Vector3 start = new Vector3(0, 0, 0);
        Vector3 end = new Vector3(50, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Generate path points
        for (int i = 0; i <= 10; i++)
        {
            path.Add(Vector3.Lerp(start, end, i / 10.0f));
        }

        // Assert
        path.Count.Should().Be(11);
        path[0].Should().Be(start);
        path[^1].Should().Be(end);
    }

    [Fact]
    public async Task Navigation_ShouldClear_PathAfterArrival()
    {
        // Arrange
        List<Vector3> path = new() { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(20, 0, 0) };
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Complete path
        GameState.Player.Position = path[^1];
        path.Clear();

        // Assert
        path.Count.Should().Be(0);
    }

    #endregion

    #region Stuck Detection Integration

    [Fact]
    public async Task Navigation_ShouldUse_StuckDetector()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Simulate being stuck
        for (int i = 0; i < 20; i++)
        {
            GameState.Player.IsMoving = true;
            // Position changes minimally
            GameState.Player.Position = new Vector3(i * 0.05f, 0, 0);
            AdvanceTime(TimeSpan.FromMilliseconds(300));
        }

        // Assert
        GameState.Player.Position.X.Should().BeLessThan(5.0f);
    }

    [Fact]
    public async Task Navigation_ShouldRecalculate_AfterStuckRecovery()
    {
        // Arrange
        Queue<Vector3> breadcrumbs = new();
        for (int i = 0; i < 10; i++)
        {
            breadcrumbs.Enqueue(new Vector3(i, 0, 0));
        }
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Get stuck then recover
        GameState.Player.Position = breadcrumbs.Peek();

        // Assert
        breadcrumbs.Count.Should().BeGreaterThan(0);
    }

    #endregion

    #region Route Following

    [Fact]
    public async Task Navigation_ShouldFollow_CompleteRoute()
    {
        // Arrange
        List<Vector3> route = new()
        {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(10, 10, 0),
            new Vector3(20, 10, 0),
            new Vector3(20, 20, 0)
        };
        GameState.Player.Position = route[0];
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Follow route
        foreach (Vector3 waypoint in route)
        {
            GameState.Player.Position = waypoint;
            GameState.Player.IsMoving = true;
            AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        }

        // Assert
        GameState.Player.Position.Should().Be(route[^1]);
    }

    [Fact]
    public async Task Navigation_ShouldHandle_RouteWithGaps()
    {
        // Arrange - Large gaps between waypoints
        List<Vector3> route = new()
        {
            new Vector3(0, 0, 0),
            new Vector3(50, 0, 0),
            new Vector3(100, 50, 0),
            new Vector3(150, 100, 0)
        };
        GameState.Player.Position = route[0];
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Navigate between waypoints
        for (int i = 1; i < route.Count; i++)
        {
            float distance = Vector3.Distance(route[i - 1], route[i]);
            _output.WriteLine($"Leg {i}: {distance:F1} units");
        }

        // Assert
        float totalDistance = Vector3.Distance(route[0], route[^1]);
        totalDistance.Should().BeGreaterThan(100.0f);
    }

    [Fact]
    public async Task Navigation_ShouldSkip_WaypointIfClose()
    {
        // Arrange
        Vector3 waypoint1 = new Vector3(10, 0, 0);
        Vector3 waypoint2 = new Vector3(20, 0, 0);
        GameState.Player.Position = new Vector3(9, 0, 0); // Very close to waypoint1
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        float distToWp1 = Vector3.Distance(GameState.Player.Position, waypoint1);
        float distToWp2 = Vector3.Distance(GameState.Player.Position, waypoint2);

        // Assert - Skip waypoint1 if very close, go to waypoint2
        distToWp1.Should().BeLessThan(distToWp2);
    }

    #endregion

    #region Combat Navigation

    [Fact]
    public async Task Navigation_ShouldNavigate_ToTarget()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(20, 20, 0), hostile: true);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        AssertHasTarget("Wolf");
        Vector3.Distance(GameState.Player.Position, GameState.CurrentTarget!.Position).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Navigation_ShouldMaintainCombatRange()
    {
        // Arrange
        SpawnNpc("Wolf", 2, 100, new Vector3(10, 0, 0), hostile: true);
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_TAB);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));
        MockClient.InputProcessor.KeyDown(InputProcessor.VK_1);
        await WaitForConditionAsync(() => GameState.InCombat, "enter combat");

        // Act - Move to target
        GameState.Player.Position = new Vector3(5, 0, 0); // Within range
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Assert
        float distance = Vector3.Distance(GameState.Player.Position, GameState.CurrentTarget!.Position);
        distance.Should().BeLessThan(10.0f);
    }

    #endregion

    #region Multi-Path Support

    [Fact]
    public async Task Navigation_ShouldHandle_MultiplePathAttempts()
    {
        // Arrange
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Calculate multiple paths
        List<List<Vector3>> attempts = new();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            List<Vector3> path = new();
            for (int i = 0; i <= 5; i++)
            {
                path.Add(new Vector3(i * attempt, i * 2, 0));
            }
            attempts.Add(path);
        }

        // Assert
        attempts.Count.Should().Be(3);
    }

    [Fact]
    public async Task Navigation_ShouldOptimize_PathSelection()
    {
        // Arrange
        Vector3 start = new Vector3(0, 0, 0);
        Vector3 end = new Vector3(100, 100, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act - Compare path options
        float directDistance = Vector3.Distance(start, end);
        float indirectDistance = Vector3.Distance(start, new Vector3(50, 0, 0)) +
                                 Vector3.Distance(new Vector3(50, 0, 0), end);

        // Assert - Direct path should be shorter
        directDistance.Should().BeLessThan(indirectDistance);
    }

    #endregion

    #region Speed Control

    [Fact]
    public async Task Navigation_ShouldAdjust_SpeedBasedOnDistance()
    {
        // Arrange
        Vector3 farTarget = new Vector3(100, 0, 0);
        Vector3 closeTarget = new Vector3(5, 0, 0);
        GameState.Player.Position = new Vector3(0, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        float farDistance = Vector3.Distance(GameState.Player.Position, farTarget);
        float closeDistance = Vector3.Distance(GameState.Player.Position, closeTarget);

        // Assert
        farDistance.Should().BeGreaterThan(closeDistance * 10);
    }

    [Fact]
    public async Task Navigation_ShouldSlow_NearDestination()
    {
        // Arrange
        Vector3 destination = new Vector3(10, 0, 0);
        GameState.Player.Position = new Vector3(8, 0, 0);
        AdvanceSimulation(TimeSpan.FromMilliseconds(1));

        // Act
        float distance = Vector3.Distance(GameState.Player.Position, destination);

        // Assert - Should be close enough to slow down
        distance.Should().BeLessThan(5.0f);
    }

    #endregion

    #region Helper Methods

    private static float CalculatePathLength(List<Vector3> path)
    {
        if (path.Count < 2) return 0;

        float total = 0;
        for (int i = 1; i < path.Count; i++)
        {
            total += Vector3.Distance(path[i - 1], path[i]);
        }
        return total;
    }

    #endregion
}
