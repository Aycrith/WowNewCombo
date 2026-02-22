using Core;
using Core.FeatureFlags;
using Core.GoalsComponent;
using CoreUnitTests.TestHelpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

using Xunit;

namespace CoreUnitTests.GoalsComponent;

public sealed class StuckDetectorBreadcrumbTests
{
    [Fact]
    public void IsEnhancedRecoveryAvailable_True_WhenTrackerPresentAndFlagEnabled()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: true);
        BreadcrumbTracker tracker = new();

        StuckDetector detector = CreateDetector(tracker, featureFlags);

        Assert.True(detector.IsEnhancedRecoveryAvailable);
    }

    [Fact]
    public void IsEnhancedRecoveryAvailable_False_WhenTrackerMissing()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: true);

        StuckDetector detector = CreateDetector(null, featureFlags);

        Assert.False(detector.IsEnhancedRecoveryAvailable);
    }

    [Fact]
    public void IsEnhancedRecoveryAvailable_False_WhenFeatureFlagDisabled()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: false);
        BreadcrumbTracker tracker = new();

        StuckDetector detector = CreateDetector(tracker, featureFlags);

        Assert.False(detector.IsEnhancedRecoveryAvailable);
    }

    [Fact]
    public void GetInitialUnstuckState_RepeatedHotspot_EscalatesToBreadcrumbBacktrack()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: true);
        BreadcrumbTracker tracker = new();
        StuckDetector detector = CreateDetector(tracker, featureFlags);

        Vector3 hotspot = new(100f, 200f, 5f);

        UnstuckState first = InvokeInitialState(detector, hotspot);
        UnstuckState second = InvokeInitialState(detector, hotspot);
        UnstuckState third = InvokeInitialState(detector, hotspot);

        Assert.Equal(UnstuckState.InitialAttempt, first);
        Assert.Equal(UnstuckState.StrafeAttempt, second);
        Assert.Equal(UnstuckState.BreadcrumbBacktrack, third);
    }

    [Fact]
    public void GetInitialUnstuckState_RepeatedHotspot_WithoutBreadcrumbs_StaysAtStrafe()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: true);
        StuckDetector detector = CreateDetector(null, featureFlags);

        Vector3 hotspot = new(50f, 50f, 0f);

        UnstuckState first = InvokeInitialState(detector, hotspot);
        UnstuckState second = InvokeInitialState(detector, hotspot);
        UnstuckState third = InvokeInitialState(detector, hotspot);

        Assert.Equal(UnstuckState.InitialAttempt, first);
        Assert.Equal(UnstuckState.StrafeAttempt, second);
        Assert.Equal(UnstuckState.StrafeAttempt, third);
    }

    private static StuckDetector CreateDetector(BreadcrumbTracker? tracker, FeatureFlagService? featureFlags)
    {
        // Constructor bypass keeps this focused on feature-flag wiring; private field names are intentionally asserted below.
        StuckDetector detector = (StuckDetector)RuntimeHelpers.GetUninitializedObject(typeof(StuckDetector));
        SetField(detector, "breadcrumbTracker", tracker);
        SetField(detector, "featureFlagService", featureFlags);
        return detector;
    }

    private static void SetField<T>(object target, string fieldName, T value)
    {
        FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }

    private static FeatureFlagService CreateFeatureFlagService(bool enabled)
    {
        FeatureFlagsOptions options = new()
        {
            StuckRecoveryV2 = new StuckRecoveryV2Options
            {
                Enabled = enabled
            }
        };

        IOptionsMonitor<FeatureFlagsOptions> monitor = new FixedOptionsMonitor<FeatureFlagsOptions>(options);
        FeatureFlagServiceOptions serviceOptions = new()
        {
            ConfigFilePath = Path.Combine(Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"), "runtime_feature_flags.json")
        };

        return new FeatureFlagService(
            NullLogger<FeatureFlagService>.Instance,
            monitor,
            Options.Create(serviceOptions));
    }

    private static UnstuckState InvokeInitialState(StuckDetector detector, Vector3 position)
    {
        MethodInfo? method = typeof(StuckDetector).GetMethod(
            "GetInitialUnstuckState",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        object? result = method.Invoke(detector, new object[] { position });
        Assert.NotNull(result);
        return Assert.IsType<UnstuckState>(result);
    }

}
