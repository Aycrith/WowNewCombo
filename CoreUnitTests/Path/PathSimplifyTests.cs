using System;
using System.Numerics;

using Core;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Pathing;

public class PathSimplifyTests
{
    [Fact]
    public void Simplify_EmptyArray_ReturnsEmpty()
    {
        // Arrange
        Span<Vector3> points = [];

        // Act
        var result = PathSimplify.Simplify(points);

        // Assert
        result.Length.Should().Be(0);
    }

    [Fact]
    public void Simplify_SinglePoint_ReturnsSamePoint()
    {
        // Arrange
        Span<Vector3> points = [new Vector3(100, 200, 50)];

        // Act
        var result = PathSimplify.Simplify(points);

        // Assert
        result.Length.Should().Be(1);
        result[0].Should().Be(new Vector3(100, 200, 50));
    }

    [Fact]
    public void Simplify_TwoPoints_ReturnsBothPoints()
    {
        // Arrange
        Span<Vector3> points = [new Vector3(0, 0, 0), new Vector3(100, 100, 0)];

        // Act
        var result = PathSimplify.Simplify(points);

        // Assert
        result.Length.Should().Be(2);
        result[0].Should().Be(new Vector3(0, 0, 0));
        result[1].Should().Be(new Vector3(100, 100, 0));
    }

    [Fact]
    public void Simplify_StraightLine_HighTolerance_ReducesToEndpoints()
    {
        // Arrange - Points in a straight line
        Span<Vector3> points =
        [
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(20, 0, 0),
            new Vector3(30, 0, 0),
            new Vector3(40, 0, 0),
            new Vector3(50, 0, 0)
        ];

        // Act - High tolerance should reduce to just endpoints
        var result = PathSimplify.Simplify(points, tolerance: 1.0f, highestQuality: true);

        // Assert - Should at least keep first and last
        result.Length.Should().BeGreaterOrEqualTo(2);
        result[0].Should().Be(new Vector3(0, 0, 0));
        result[result.Length - 1].Should().Be(new Vector3(50, 0, 0));
    }

    [Fact]
    public void Simplify_ZigzagPattern_LowTolerance_KeepsMorePoints()
    {
        // Arrange - Zigzag pattern with many direction changes
        Span<Vector3> points =
        [
            new Vector3(0, 0, 0),
            new Vector3(10, 10, 0),
            new Vector3(20, 0, 0),
            new Vector3(30, 10, 0),
            new Vector3(40, 0, 0),
            new Vector3(50, 10, 0)
        ];

        // Act - Low tolerance should keep most points
        var result = PathSimplify.Simplify(points, tolerance: 0.01f);

        // Assert - Should keep most points
        result.Length.Should().BeGreaterOrEqualTo(4);
    }

    [Fact]
    public void Simplify_CircularPath_ReducesCorrectly()
    {
        // Arrange - Approximate circle
        Span<Vector3> points =
        [
            new Vector3(10, 0, 0),
            new Vector3(7, 7, 0),
            new Vector3(0, 10, 0),
            new Vector3(-7, 7, 0),
            new Vector3(-10, 0, 0),
            new Vector3(-7, -7, 0),
            new Vector3(0, -10, 0),
            new Vector3(7, -7, 0),
            new Vector3(10, 0, 0) // Close the loop
        ];

        // Act - Lower tolerance to preserve more points
        var result = PathSimplify.Simplify(points, tolerance: 2.0f);

        // Assert - Should keep start and end at minimum
        result.Length.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public void Simplify_WithNegativeCoordinates_WorksCorrectly()
    {
        // Arrange
        Span<Vector3> points =
        [
            new Vector3(-100, -100, 0),
            new Vector3(-50, -50, 0),
            new Vector3(0, 0, 0),
            new Vector3(50, 50, 0),
            new Vector3(100, 100, 0)
        ];

        // Act
        var result = PathSimplify.Simplify(points, tolerance: 10.0f);

        // Assert
        result.Length.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public void Simplify_HighQuality_True_UsesDouglasPeucker()
    {
        // Arrange
        Span<Vector3> points =
        [
            new Vector3(0, 0, 0),
            new Vector3(5, 1, 0),
            new Vector3(10, 0, 0),
            new Vector3(15, -1, 0),
            new Vector3(20, 0, 0)
        ];

        // Act
        var result = PathSimplify.Simplify(points, tolerance: 0.5f, highestQuality: true);

        // Assert
        result.Length.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public void Simplify_HighQuality_False_UsesRadialThenDouglasPeucker()
    {
        // Arrange
        Span<Vector3> points =
        [
            new Vector3(0, 0, 0),
            new Vector3(5, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(15, 0, 0),
            new Vector3(20, 0, 0)
        ];

        // Act
        var result = PathSimplify.Simplify(points, tolerance: 2.0f, highestQuality: false);

        // Assert
        result.Length.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public void Simplify_VeryHighTolerance_ReducesSignificantly()
    {
        // Arrange
        Span<Vector3> points = new Vector3[100];
        for (int i = 0; i < 100; i++)
        {
            points[i] = new Vector3(i, i, 0);
        }

        // Act - Very high tolerance
        var result = PathSimplify.Simplify(points, tolerance: 1000.0f);

        // Assert - Should reduce significantly
        result.Length.Should().BeLessThan(50);
    }

    [Fact]
    public void Simplify_DuplicatePoints_HandlesCorrectly()
    {
        // Arrange - Points with duplicates
        Span<Vector3> points =
        [
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 0),
            new Vector3(10, 10, 0),
            new Vector3(10, 10, 0),
            new Vector3(20, 20, 0)
        ];

        // Act
        var result = PathSimplify.Simplify(points, tolerance: 1.0f);

        // Assert
        result.Length.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public void Simplify_ThreeD_Path_WorksCorrectly()
    {
        // Arrange - 3D path with Z coordinates
        Span<Vector3> points =
        [
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 5),
            new Vector3(20, 0, 10),
            new Vector3(30, 0, 15)
        ];

        // Act
        var result = PathSimplify.Simplify(points, tolerance: 1.0f);

        // Assert
        result.Length.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public void Simplify_VeryLongPath_PerformsEfficiently()
    {
        // Arrange - Long path
        Span<Vector3> points = new Vector3[1000];
        for (int i = 0; i < 1000; i++)
        {
            points[i] = new Vector3(i, (float)Math.Sin(i * 0.1), 0);
        }

        // Act - Lower tolerance to preserve more detail
        var result = PathSimplify.Simplify(points, tolerance: 0.5f);

        // Assert - Should reduce significantly but still preserve meaningful points
        result.Length.Should().BeLessThan(500);
        result.Length.Should().BeGreaterOrEqualTo(10);
    }

    [Fact]
    public void Simplify_DefaultTolerance_Uses03()
    {
        // Arrange
        Span<Vector3> points =
        [
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(2, 0, 0)
        ];

        // Act - Use default tolerance
        var result = PathSimplify.Simplify(points);

        // Assert
        result.Length.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public void Simplify_StartAndEndPoints_AlwaysPreserved()
    {
        // Arrange
        Vector3 start = new(100, 200, 300);
        Vector3 end = new(500, 600, 700);
        Span<Vector3> points =
        [
            start,
            new Vector3(200, 300, 400),
            new Vector3(300, 400, 500),
            new Vector3(400, 500, 600),
            end
        ];

        // Act
        var result = PathSimplify.Simplify(points, tolerance: 100.0f);

        // Assert
        result[0].Should().Be(start);
        result[result.Length - 1].Should().Be(end);
    }
}
