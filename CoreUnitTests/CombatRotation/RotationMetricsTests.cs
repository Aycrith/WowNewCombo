using Core.CombatRotation;

using System.Collections.Generic;
using System.Threading.Tasks;

using Xunit;

namespace CoreUnitTests.CombatRotation;

public sealed class RotationMetricsTests
{
    [Fact]
    public void RotationSessionMetrics_TracksTicks()
    {
        RotationSessionMetrics metrics = new();

        metrics.TotalTicks = 10;
        metrics.OptimizedTicks = 7;
        metrics.FallbackTicks = 3;

        Assert.Equal(10, metrics.TotalTicks);
        Assert.Equal(7, metrics.OptimizedTicks);
        Assert.Equal(3, metrics.FallbackTicks);
    }

    [Fact]
    public void RecordAttempt_CreatesStatEntry()
    {
        RotationSessionMetrics metrics = new();

        metrics.RecordAttempt("Fireball", score: 5.0f, success: true);

        Assert.True(metrics.AbilityStats.ContainsKey("Fireball"));
        AbilityUsageStat stat = metrics.AbilityStats["Fireball"];
        Assert.Equal("Fireball", stat.Name);
        Assert.Equal(1, stat.AttemptCount);
        Assert.Equal(1, stat.SuccessCount);
        Assert.Equal(5.0f, stat.AverageScore, precision: 1);
    }

    [Fact]
    public void RecordAttempt_AccumulatesMultipleAttempts()
    {
        RotationSessionMetrics metrics = new();

        metrics.RecordAttempt("Frostbolt", score: 4.0f, success: true);
        metrics.RecordAttempt("Frostbolt", score: 2.0f, success: false);

        AbilityUsageStat stat = metrics.AbilityStats["Frostbolt"];
        Assert.Equal(2, stat.AttemptCount);
        Assert.Equal(1, stat.SuccessCount);
        Assert.Equal(3.0f, stat.AverageScore, precision: 1);
    }

    [Fact]
    public void SuccessRate_CalculatesCorrectly()
    {
        RotationSessionMetrics metrics = new();

        metrics.RecordAttempt("Execute", score: 10.0f, success: true);
        metrics.RecordAttempt("Execute", score: 8.0f, success: true);
        metrics.RecordAttempt("Execute", score: 6.0f, success: false);
        metrics.RecordAttempt("Execute", score: 4.0f, success: false);

        AbilityUsageStat stat = metrics.AbilityStats["Execute"];
        Assert.Equal(0.5f, stat.SuccessRate, precision: 2);
    }

    [Fact]
    public void SuccessRate_ZeroAttempts_ReturnsZero()
    {
        AbilityUsageStat stat = new() { Name = "EmptyAbility" };
        Assert.Equal(0f, stat.SuccessRate);
    }

    [Fact]
    public void GetOrderedStats_OrdersByAttemptCountDescending()
    {
        RotationSessionMetrics metrics = new();

        metrics.RecordAttempt("LowUse", score: 1.0f, success: true);
        metrics.RecordAttempt("HighUse", score: 1.0f, success: true);
        metrics.RecordAttempt("HighUse", score: 1.0f, success: true);
        metrics.RecordAttempt("HighUse", score: 1.0f, success: true);
        metrics.RecordAttempt("MidUse", score: 1.0f, success: true);
        metrics.RecordAttempt("MidUse", score: 1.0f, success: true);

        List<AbilityUsageStat> ordered = new(metrics.GetOrderedStats());

        Assert.Equal("HighUse", ordered[0].Name);
        Assert.Equal("MidUse", ordered[1].Name);
        Assert.Equal("LowUse", ordered[2].Name);
    }

    [Fact]
    public void RecordAttempt_ConcurrentSafety_SameKey()
    {
        RotationSessionMetrics metrics = new();

        Parallel.For(0, 100, _ =>
        {
            metrics.RecordAttempt("ConcurrentAbility", score: 1.0f, success: true);
        });

        AbilityUsageStat stat = metrics.AbilityStats["ConcurrentAbility"];
        Assert.Equal(100, stat.AttemptCount);
    }
}
