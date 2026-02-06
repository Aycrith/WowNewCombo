using Core.CombatRotation;

using Xunit;

namespace CoreUnitTests.CombatRotation;

public sealed class CombatRotationOptimizerOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        CombatRotationOptimizerOptions options = new();

        Assert.False(options.Enabled);
        Assert.True(options.FallbackToStaticPriority);
        Assert.Equal(1.0f, options.BaseWeightMultiplier);
        Assert.True(options.EnableMetrics);
        Assert.True(options.EnableResourceForecasting);
        Assert.False(options.EnableSwingTimerAlignment);
        Assert.Equal(30, options.MetricsFlushIntervalSeconds);
        Assert.Equal("logs/rotation_metrics.json", options.MetricsOutputPath);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        CombatRotationOptimizerOptions options = new()
        {
            Enabled = true,
            FallbackToStaticPriority = false,
            BaseWeightMultiplier = 2.5f,
            EnableMetrics = false,
            EnableResourceForecasting = false,
            EnableSwingTimerAlignment = true,
            MetricsFlushIntervalSeconds = 60,
            MetricsOutputPath = "custom/path.json"
        };

        Assert.True(options.Enabled);
        Assert.False(options.FallbackToStaticPriority);
        Assert.Equal(2.5f, options.BaseWeightMultiplier);
        Assert.False(options.EnableMetrics);
        Assert.False(options.EnableResourceForecasting);
        Assert.True(options.EnableSwingTimerAlignment);
        Assert.Equal(60, options.MetricsFlushIntervalSeconds);
        Assert.Equal("custom/path.json", options.MetricsOutputPath);
    }
}
