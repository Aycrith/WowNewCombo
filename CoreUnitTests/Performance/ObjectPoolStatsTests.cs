using Core.Performance;

using FluentAssertions;

using Xunit;

namespace CoreUnitTests.Performance;

public class ObjectPoolStatsTests
{
    [Fact]
    public void ObjectPoolStats_CreateWithValues_StoresCorrectly()
    {
        // Arrange
        const int currentSize = 50;
        const int maxSize = 100;
        const long rentCount = 1000;
        const long returnCount = 950;
        const long createCount = 100;
        const long discardCount = 50;
        const double hitRate = 0.9;

        // Act
        var stats = new ObjectPoolStats(currentSize, maxSize, rentCount, returnCount, createCount, discardCount, hitRate);

        // Assert
        stats.CurrentSize.Should().Be(currentSize);
        stats.MaxSize.Should().Be(maxSize);
        stats.RentCount.Should().Be(rentCount);
        stats.ReturnCount.Should().Be(returnCount);
        stats.CreateCount.Should().Be(createCount);
        stats.DiscardCount.Should().Be(discardCount);
        stats.HitRate.Should().Be(hitRate);
    }

    [Fact]
    public void ObjectPoolStats_CreateWithZeroValues_StoresCorrectly()
    {
        // Act
        var stats = new ObjectPoolStats(0, 0, 0, 0, 0, 0, 0.0);

        // Assert
        stats.CurrentSize.Should().Be(0);
        stats.HitRate.Should().Be(0.0);
    }

    [Fact]
    public void ObjectPoolStats_With_ModifiesSingleProperty()
    {
        // Arrange
        var original = new ObjectPoolStats(50, 100, 1000, 900, 100, 50, 0.9);

        // Act
        var modified = original with { CurrentSize = 75 };

        // Assert
        modified.CurrentSize.Should().Be(75);
        modified.MaxSize.Should().Be(original.MaxSize);
        modified.RentCount.Should().Be(original.RentCount);
    }

    [Fact]
    public void ObjectPoolStats_Equality_SameValues_AreEqual()
    {
        // Arrange
        var stats1 = new ObjectPoolStats(50, 100, 1000, 900, 100, 50, 0.9);
        var stats2 = new ObjectPoolStats(50, 100, 1000, 900, 100, 50, 0.9);

        // Assert
        stats1.Should().Be(stats2);
        (stats1 == stats2).Should().BeTrue();
        stats1.GetHashCode().Should().Be(stats2.GetHashCode());
    }

    [Fact]
    public void ObjectPoolStats_Equality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var stats1 = new ObjectPoolStats(50, 100, 1000, 900, 100, 50, 0.9);
        var stats2 = new ObjectPoolStats(51, 100, 1000, 900, 100, 50, 0.9);

        // Assert
        stats1.Should().NotBe(stats2);
        (stats1 != stats2).Should().BeTrue();
    }

    [Fact]
    public void ObjectPoolStats_Deconstruct_ReturnsCorrectValues()
    {
        // Arrange
        const int currentSize = 75;
        const int maxSize = 150;
        const long rentCount = 2000;
        const long returnCount = 1900;
        const long createCount = 200;
        const long discardCount = 100;
        const double hitRate = 0.95;

        var stats = new ObjectPoolStats(currentSize, maxSize, rentCount, returnCount, createCount, discardCount, hitRate);

        // Act
        var (resultCurrentSize, resultMaxSize, resultRentCount, resultReturnCount, resultCreateCount, resultDiscardCount, resultHitRate) = stats;

        // Assert
        resultCurrentSize.Should().Be(currentSize);
        resultMaxSize.Should().Be(maxSize);
        resultRentCount.Should().Be(rentCount);
        resultReturnCount.Should().Be(returnCount);
        resultCreateCount.Should().Be(createCount);
        resultDiscardCount.Should().Be(discardCount);
        resultHitRate.Should().Be(hitRate);
    }

    [Fact]
    public void ObjectPoolStats_PerfectHitRate_StoresCorrectly()
    {
        // Act
        var stats = new ObjectPoolStats(100, 100, 1000, 1000, 0, 0, 1.0);

        // Assert
        stats.HitRate.Should().Be(1.0);
    }

    [Fact]
    public void ObjectPoolStats_ZeroHitRate_StoresCorrectly()
    {
        // Act
        var stats = new ObjectPoolStats(0, 100, 1000, 0, 1000, 1000, 0.0);

        // Assert
        stats.HitRate.Should().Be(0.0);
    }

    [Fact]
    public void ObjectPoolStats_HighCounts_StoresCorrectly()
    {
        // Act
        var stats = new ObjectPoolStats(int.MaxValue, int.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, 0.999);

        // Assert
        stats.RentCount.Should().Be(long.MaxValue);
        stats.CreateCount.Should().Be(long.MaxValue);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void ObjectPoolStats_VariousHitRates_Accepted(double hitRate)
    {
        // Arrange & Act
        var stats = new ObjectPoolStats(50, 100, 100, 100, 0, 0, hitRate);

        // Assert
        stats.HitRate.Should().Be(hitRate);
    }

    [Fact]
    public void ObjectPoolStats_ToString_ContainsCurrentSize()
    {
        // Arrange
        var stats = new ObjectPoolStats(42, 100, 1000, 900, 100, 50, 0.9);

        // Act
        string result = stats.ToString();

        // Assert
        result.Should().Contain("42");
    }

    [Fact]
    public void ObjectPoolStats_PoolEfficiency_CanBeCalculated()
    {
        // Arrange
        var stats = new ObjectPoolStats(80, 100, 1000, 950, 50, 30, 0.95);

        // Act - Calculate utilization percentage
        double utilization = (double)stats.CurrentSize / stats.MaxSize;

        // Assert
        utilization.Should().Be(0.8);
    }
}
