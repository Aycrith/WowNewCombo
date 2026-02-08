using System;

using Core.GoalsComponent;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.GoalsComponent;

public class BreadcrumbStatsTests
{
    [Fact]
    public void BreadcrumbStats_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        const int count = 50;
        const int maxSize = 100;
        const long totalRecorded = 200;
        const long totalSkipped = 50;
        const float totalDistance = 1250.5f;
        DateTime? oldest = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime? newest = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var stats = new BreadcrumbStats(count, maxSize, totalRecorded, totalSkipped, totalDistance, oldest, newest);

        // Assert
        stats.Count.Should().Be(count);
        stats.MaxSize.Should().Be(maxSize);
        stats.TotalRecorded.Should().Be(totalRecorded);
        stats.TotalSkipped.Should().Be(totalSkipped);
        stats.TotalDistance.Should().Be(totalDistance);
        stats.OldestTimestamp.Should().Be(oldest);
        stats.NewestTimestamp.Should().Be(newest);
    }

    [Fact]
    public void BreadcrumbStats_CreateWithZeroValues_StoresCorrectly()
    {
        // Act
        var stats = new BreadcrumbStats(0, 0, 0, 0, 0f, null, null);

        // Assert
        stats.Count.Should().Be(0);
        stats.TotalDistance.Should().Be(0f);
        stats.OldestTimestamp.Should().BeNull();
        stats.NewestTimestamp.Should().BeNull();
    }

    [Fact]
    public void BreadcrumbStats_CreateWithNullTimestamps_StoresCorrectly()
    {
        // Act
        var stats = new BreadcrumbStats(0, 100, 0, 0, 0f, null, null);

        // Assert
        stats.OldestTimestamp.Should().BeNull();
        stats.NewestTimestamp.Should().BeNull();
    }

    [Fact]
    public void BreadcrumbStats_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new BreadcrumbStats(10, 100, 50, 5, 500f, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        // Act
        var modified = original with { Count = 20 };

        // Assert
        modified.Count.Should().Be(20);
        modified.MaxSize.Should().Be(original.MaxSize);
        modified.TotalRecorded.Should().Be(original.TotalRecorded);
    }

    [Fact]
    public void BreadcrumbStats_Equality_SameValues_AreEqual()
    {
        // Arrange
        DateTime oldest = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime newest = new(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        var stats1 = new BreadcrumbStats(50, 100, 200, 20, 1000f, oldest, newest);
        var stats2 = new BreadcrumbStats(50, 100, 200, 20, 1000f, oldest, newest);

        // Assert
        stats1.Should().Be(stats2);
        (stats1 == stats2).Should().BeTrue();
        stats1.GetHashCode().Should().Be(stats2.GetHashCode());
    }

    [Fact]
    public void BreadcrumbStats_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var stats1 = new BreadcrumbStats(50, 100, 200, 20, 1000f, DateTime.UtcNow, DateTime.UtcNow);
        var stats2 = new BreadcrumbStats(51, 100, 200, 20, 1000f, DateTime.UtcNow, DateTime.UtcNow);

        // Assert
        stats1.Should().NotBe(stats2);
        (stats1 != stats2).Should().BeTrue();
    }

    [Fact]
    public void BreadcrumbStats_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        const int count = 75;
        const int maxSize = 150;
        const long totalRecorded = 300;
        const long totalSkipped = 25;
        const float totalDistance = 2000f;
        DateTime oldest = new(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime newest = new(2024, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        var stats = new BreadcrumbStats(count, maxSize, totalRecorded, totalSkipped, totalDistance, oldest, newest);

        // Act
        var (resultCount, resultMaxSize, resultTotalRecorded, resultTotalSkipped, resultTotalDistance, resultOldest, resultNewest) = stats;

        // Assert
        resultCount.Should().Be(count);
        resultMaxSize.Should().Be(maxSize);
        resultTotalRecorded.Should().Be(totalRecorded);
        resultTotalSkipped.Should().Be(totalSkipped);
        resultTotalDistance.Should().Be(totalDistance);
        resultOldest.Should().Be(oldest);
        resultNewest.Should().Be(newest);
    }

    [Fact]
    public void BreadcrumbStats_ToString_ContainsCount()
    {
        // Arrange
        var stats = new BreadcrumbStats(42, 100, 200, 20, 1000f, null, null);

        // Act
        string result = stats.ToString();

        // Assert
        result.Should().Contain("42");
    }

    [Fact]
    public void BreadcrumbStats_LargeValues_StoresCorrectly()
    {
        // Act
        var stats = new BreadcrumbStats(int.MaxValue, int.MaxValue, long.MaxValue, long.MaxValue, float.MaxValue, DateTime.MinValue, DateTime.MaxValue);

        // Assert
        stats.Count.Should().Be(int.MaxValue);
        stats.TotalRecorded.Should().Be(long.MaxValue);
        stats.TotalDistance.Should().Be(float.MaxValue);
    }

    [Fact]
    public void BreadcrumbStats_CalculateFillPercentage_ReturnsExpected()
    {
        // Arrange
        var stats = new BreadcrumbStats(75, 100, 300, 50, 1500f, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

        // Act - Calculate manually since there's no built-in property
        double fillPercentage = (double)stats.Count / stats.MaxSize * 100;

        // Assert
        fillPercentage.Should().Be(75.0);
    }

    [Fact]
    public void BreadcrumbStats_CountExceedsMaxSize_IsPossible()
    {
        // This tests that the record allows count > maxSize (implementation detail)
        // Act
        var stats = new BreadcrumbStats(150, 100, 300, 0, 0f, null, null);

        // Assert
        stats.Count.Should().Be(150);
        stats.MaxSize.Should().Be(100);
    }

    [Fact]
    public void BreadcrumbStats_NegativeSkipCount_StoresCorrectly()
    {
        // Act
        var stats = new BreadcrumbStats(50, 100, 200, -10, 1000f, null, null);

        // Assert
        stats.TotalSkipped.Should().Be(-10);
    }
}
