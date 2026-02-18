using Core.CombatRotation;
using Core.FeatureFlags;

using System.Collections.Generic;

using Xunit;

namespace CoreUnitTests.CombatRotation;

public sealed class FeatureFlagIntegrationTests
{
    [Fact]
    public void FeatureFlagsOptions_CombatRotationOptimizer_DefaultsToDisabled()
    {
        FeatureFlagsOptions options = new();
        Assert.False(options.CombatRotationOptimizer.Enabled);
    }

    [Fact]
    public void FeatureFlagsOptions_IsEnabled_ReturnsFalseForCombatRotation()
    {
        FeatureFlagsOptions options = new();
        Assert.False(options.IsEnabled("CombatRotationOptimizer"));
    }

    [Fact]
    public void FeatureFlagsOptions_IsEnabled_ReturnsTrueWhenEnabled()
    {
        FeatureFlagsOptions options = new();
        options.CombatRotationOptimizer = new CombatRotationOptimizerOptions
        {
            Enabled = true
        };

        Assert.True(options.IsEnabled("CombatRotationOptimizer"));
    }

    [Fact]
    public void FeatureFlagsOptions_GetEnabledFeatures_IncludesCombatRotation()
    {
        FeatureFlagsOptions options = new();
        options.CombatRotationOptimizer = new CombatRotationOptimizerOptions
        {
            Enabled = true
        };

        List<string> enabled = new(options.GetEnabledFeatures());
        Assert.Contains("CombatRotationOptimizer", enabled);
    }

    [Fact]
    public void FeatureFlagsOptions_GetEnabledFeatures_ExcludesWhenDisabled()
    {
        FeatureFlagsOptions options = new();

        List<string> enabled = new(options.GetEnabledFeatures());
        Assert.DoesNotContain("CombatRotationOptimizer", enabled);
    }
}
