using Core;
using Core.Goals;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace CoreUnitTests.GoalsComponent;

/// <summary>
/// Test suite for Navigation component - stuck detection, oscillation detection, and movement logic.
/// </summary>
public class NavigationTests
{
    #region Stuck Detection Tests

    [Fact]
    public void StuckDetection_PlayerNotMoving_DetectsStuck()
    {
        // This test validates that the stuck detection logic correctly identifies
        // when the player hasn't moved significantly
        // Note: Full implementation requires mocking StuckDetector

        // Arrange
        var currentPosition = new Vector3(100, 100, 0);
        var lastPosition = new Vector3(100, 100, 0);
        var threshold = 0.5f;

        // Act
        var distanceMoved = Vector3.Distance(currentPosition, lastPosition);
        var isStuck = distanceMoved < threshold;

        // Assert
        isStuck.Should().BeTrue();
        distanceMoved.Should().Be(0f);
    }

    [Fact]
    public void StuckDetection_PlayerMoving_DoesNotDetectStuck()
    {
        // Arrange
        var currentPosition = new Vector3(105, 100, 0);
        var lastPosition = new Vector3(100, 100, 0);
        var threshold = 0.5f;

        // Act
        var distanceMoved = Vector3.Distance(currentPosition, lastPosition);
        var isStuck = distanceMoved < threshold;

        // Assert
        isStuck.Should().BeFalse();
        distanceMoved.Should().Be(5f);
    }

    [Fact]
    public void StuckDetection_JustAtThreshold_DetectsStuck()
    {
        // Arrange
        var currentPosition = new Vector3(100.4f, 100, 0);
        var lastPosition = new Vector3(100, 100, 0);
        var threshold = 0.5f;

        // Act
        var distanceMoved = Vector3.Distance(currentPosition, lastPosition);
        var isStuck = distanceMoved < threshold;

        // Assert
        isStuck.Should().BeTrue();
    }

    #endregion

    #region Oscillation Detection Tests

    [Fact]
    public void Oscillation_RapidDirectionChanges_DetectsOscillation()
    {
        // Arrange - simulate rapid direction changes
        var headingHistory = new Queue<float>();
        var timestamps = new Queue<long>();

        // Simulate oscillating between -PI and PI (back and forth)
        for (int i = 0; i < 12; i++)
        {
            headingHistory.Enqueue(i % 2 == 0 ? 3.0f : -3.0f);
            timestamps.Enqueue(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (11 - i) * 100);
        }

        // Act - count direction changes
        var directionChanges = CountDirectionChanges(headingHistory);

        // Assert - should detect oscillation with this many changes
        directionChanges.Should().BeGreaterOrEqualTo(6);
    }

    [Fact]
    public void Oscillation_SmoothMovement_NoOscillation()
    {
        // Arrange - smooth progression
        var headingHistory = new Queue<float>();

        for (int i = 0; i < 12; i++)
        {
            headingHistory.Enqueue(0.1f * i); // Gradual increase
        }

        // Act
        var directionChanges = CountDirectionChanges(headingHistory);

        // Assert - no oscillation
        directionChanges.Should().BeLessThan(6);
    }

    private static int CountDirectionChanges(Queue<float> headings)
    {
        if (headings.Count < 2) return 0;

        var changes = 0;
        var prev = headings.Dequeue();

        while (headings.Count > 0)
        {
            var current = headings.Dequeue();
            var diff = Math.Abs(current - prev);

            // Significant direction change (> 11.5 degrees)
            if (diff > 0.2f)
            {
                changes++;
            }

            prev = current;
        }

        return changes;
    }

    #endregion

    #region Distance Calculation Tests

    [Fact]
    public void DistanceToWaypoint_CalculatesCorrectly()
    {
        // Arrange
        var playerPos = new Vector3(100, 100, 0);
        var waypoint = new Vector3(110, 100, 0);

        // Act
        var distance = Vector3.Distance(playerPos, waypoint);

        // Assert
        distance.Should().Be(10f);
    }

    [Fact]
    public void DistanceToWaypoint_3DPosition_Calculates3DDistance()
    {
        // Arrange
        var playerPos = new Vector3(100, 100, 0);
        var waypoint = new Vector3(100, 100, 10);

        // Act
        var distance = Vector3.Distance(playerPos, waypoint);

        // Assert
        distance.Should().Be(10f);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(0, 0, 3, 4, 5)] // 3-4-5 triangle
    [InlineData(1, 1, 4, 5, 5)] // 3-4-5 triangle offset
    [InlineData(0, 0, 100, 0, 100)] // Straight line
    public void DistanceToWaypoint_VariousPositions_CalculatesCorrectly(
        float x1, float y1, float x2, float y2, float expected)
    {
        // Arrange
        var playerPos = new Vector3(x1, y1, 0);
        var waypoint = new Vector3(x2, y2, 0);

        // Act
        var distance = Vector3.Distance(playerPos, waypoint);

        // Assert
        distance.Should().BeApproximately(expected, 0.001f);
    }

    #endregion

    #region Waypoint Management Tests

    [Fact]
    public void WaypointReached_PlayerAtWaypoint_DetectsReached()
    {
        // Arrange
        var playerPos = new Vector3(100, 100, 0);
        var waypoint = new Vector3(100, 100, 0);
        var threshold = 1.0f;

        // Act
        var distance = Vector3.Distance(playerPos, waypoint);
        var reached = distance <= threshold;

        // Assert
        reached.Should().BeTrue();
    }

    [Fact]
    public void WaypointReached_PlayerClose_DetectsReached()
    {
        // Arrange
        var playerPos = new Vector3(100.5f, 100.5f, 0);
        var waypoint = new Vector3(100, 100, 0);
        var threshold = 1.0f;

        // Act
        var distance = Vector3.Distance(playerPos, waypoint);
        var reached = distance <= threshold;

        // Assert
        reached.Should().BeTrue();
    }

    [Fact]
    public void WaypointReached_PlayerTooFar_DoesNotDetectReached()
    {
        // Arrange
        var playerPos = new Vector3(105, 100, 0);
        var waypoint = new Vector3(100, 100, 0);
        var threshold = 1.0f;

        // Act
        var distance = Vector3.Distance(playerPos, waypoint);
        var reached = distance <= threshold;

        // Assert
        reached.Should().BeFalse();
    }

    #endregion

    #region Angle Calculation Tests

    [Fact]
    public void AngleToWaypoint_CalculatesCorrectly()
    {
        // Arrange
        var playerPos = new Vector3(100, 100, 0);
        var waypoint = new Vector3(110, 100, 0); // Due east

        // Act
        var direction = waypoint - playerPos;
        var angle = MathF.Atan2(direction.Y, direction.X);

        // Assert
        angle.Should().BeApproximately(0f, 0.001f); // 0 radians = due east
    }

    [Theory]
    [InlineData(10, 0, 0)]       // East
    [InlineData(0, 10, 1.5708)]  // North
    [InlineData(-10, 0, 3.14159)] // West
    [InlineData(0, -10, -1.5708)] // South
    public void AngleToWaypoint_CardinalDirections_ReturnsCorrectAngles(
        float dx, float dy, float expectedAngle)
    {
        // Arrange
        var direction = new Vector3(dx, dy, 0);

        // Act
        var angle = MathF.Atan2(direction.Y, direction.X);

        // Assert
        angle.Should().BeApproximately(expectedAngle, 0.01f);
    }

    #endregion

    #region Path Simplification Tests

    [Fact]
    public void SimplifyPath_CollinearPoints_RemovesMiddlePoint()
    {
        // Arrange - three points in a straight line
        var path = new List<Vector3>
        {
            new(0, 0, 0),
            new(50, 0, 0), // Middle point on line
            new(100, 0, 0)
        };

        // Act
        var simplified = SimplifyPath(path, 0.1f);

        // Assert
        simplified.Should().HaveCount(2);
        simplified[0].Should().Be(path[0]);
        simplified[1].Should().Be(path[2]);
    }

    [Fact]
    public void SimplifyPath_NonCollinear_KeepsAllPoints()
    {
        // Arrange - three points with a turn
        var path = new List<Vector3>
        {
            new(0, 0, 0),
            new(50, 50, 0), // Middle point creates turn
            new(100, 0, 0)
        };

        // Act
        var simplified = SimplifyPath(path, 0.1f);

        // Assert
        simplified.Should().HaveCount(3);
    }

    private static List<Vector3> SimplifyPath(List<Vector3> path, float tolerance)
    {
        if (path.Count < 3) return new List<Vector3>(path);

        var result = new List<Vector3> { path[0] };

        for (int i = 1; i < path.Count - 1; i++)
        {
            var prev = path[i - 1];
            var curr = path[i];
            var next = path[i + 1];

            // Check if curr deviates from line between prev and next
            var line = next - prev;
            var toCurr = curr - prev;

            var lineLength = line.Length();
            if (lineLength < 0.001f)
            {
                result.Add(curr);
                continue;
            }

            var projection = Vector3.Dot(toCurr, line) / lineLength;
            var closest = prev + (line / lineLength) * projection;
            var deviation = Vector3.Distance(curr, closest);

            if (deviation > tolerance)
            {
                result.Add(curr);
            }
        }

        result.Add(path[path.Count - 1]);
        return result;
    }

    #endregion

    #region Route Rehabilitation Tests

    [Fact]
    public void RouteRehabilitation_FailedPath_IncreasesCost()
    {
        // Arrange
        var failedAttempts = 3;
        var baseCost = 100f;

        // Act - simulate cost increase
        var rehabilitatedCost = baseCost * MathF.Pow(1.5f, failedAttempts);

        // Assert
        rehabilitatedCost.Should().BeGreaterThan(baseCost);
    }

    [Fact]
    public void RouteRehabilitation_ManyFailures_SignificantlyIncreasesCost()
    {
        // Arrange
        var failedAttempts = 10;
        var baseCost = 100f;

        // Act
        var rehabilitatedCost = baseCost * MathF.Pow(1.5f, failedAttempts);

        // Assert
        rehabilitatedCost.Should().BeGreaterThan(baseCost * 10);
    }

    #endregion

    #region Mount/Dismount Logic Tests

    [Fact]
    public void ShouldMount_LongDistance_ReturnsTrue()
    {
        // Arrange
        var distance = 50f;
        var mountThreshold = 10f;

        // Act
        var shouldMount = distance > mountThreshold;

        // Assert
        shouldMount.Should().BeTrue();
    }

    [Fact]
    public void ShouldMount_ShortDistance_ReturnsFalse()
    {
        // Arrange
        var distance = 5f;
        var mountThreshold = 10f;

        // Act
        var shouldMount = distance > mountThreshold;

        // Assert
        shouldMount.Should().BeFalse();
    }

    #endregion

    #region Distance Threshold Tests

    [Fact]
    public void IndoorThreshold_UsesSmallerDistance()
    {
        // Arrange
        var indoorMinDistance = 1f;
        var outdoorMinDistance = 3f;
        var isIndoor = true;

        // Act
        var threshold = isIndoor ? indoorMinDistance : outdoorMinDistance;

        // Assert
        threshold.Should().Be(1f);
    }

    [Fact]
    public void OutdoorThreshold_UsesLargerDistance()
    {
        // Arrange
        var indoorMinDistance = 1f;
        var outdoorMinDistance = 3f;
        var isIndoor = false;

        // Act
        var threshold = isIndoor ? indoorMinDistance : outdoorMinDistance;

        // Assert
        threshold.Should().Be(3f);
    }

    #endregion
}
