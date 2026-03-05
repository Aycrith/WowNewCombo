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

    [Fact]
    public void GetInitialUnstuckState_RepeatedHotspot_WhenBreadcrumbTemporarilyDisabled_UsesPathClear()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: true);
        BreadcrumbTracker tracker = new();
        StuckDetector detector = CreateDetector(tracker, featureFlags);
        SetField(detector, "breadcrumbBacktrackDisabledUntilUtc", DateTime.UtcNow.AddSeconds(30));

        Vector3 hotspot = new(75f, 75f, 0f);

        UnstuckState first = InvokeInitialState(detector, hotspot);
        UnstuckState second = InvokeInitialState(detector, hotspot);
        UnstuckState third = InvokeInitialState(detector, hotspot);

        Assert.Equal(UnstuckState.InitialAttempt, first);
        Assert.Equal(UnstuckState.StrafeAttempt, second);
        Assert.Equal(UnstuckState.PathClearAttempt, third);
    }

    [Fact]
    public void TrySelectBacktrackTarget_FallsBackToFarthestTrailPoint_WhenStepTargetsUnavailable()
    {
        BreadcrumbEntry[] trail =
        [
            new BreadcrumbEntry(new Vector3(2f, 0f, 0f), 1, DateTime.UtcNow),
            new BreadcrumbEntry(new Vector3(6f, 0f, 0f), 1, DateTime.UtcNow),
            new BreadcrumbEntry(new Vector3(14f, 0f, 0f), 1, DateTime.UtcNow)
        ];

        bool selected = StuckDetector.TrySelectBacktrackTarget(
            trail,
            currentPosition: new Vector3(0f, 0f, 0f),
            preferredSteps: 8,
            alternateSteps: 10,
            minDistance: 1f,
            out Vector3 target);

        Assert.True(selected);
        Assert.Equal(new Vector3(14f, 0f, 0f), target);
    }

    [Fact]
    public void TrySelectBacktrackTarget_NoPointBeyondMinDistance_ReturnsFalse()
    {
        BreadcrumbEntry[] trail =
        [
            new BreadcrumbEntry(new Vector3(0.2f, 0f, 0f), 1, DateTime.UtcNow),
            new BreadcrumbEntry(new Vector3(0.4f, 0f, 0f), 1, DateTime.UtcNow)
        ];

        bool selected = StuckDetector.TrySelectBacktrackTarget(
            trail,
            currentPosition: Vector3.Zero,
            preferredSteps: 1,
            alternateSteps: 2,
            minDistance: 1f,
            out _);

        Assert.False(selected);
    }

    [Fact]
    public void ResolveReverseEscalationState_WhenCooldownActive_ReturnsPathClearAttempt()
    {
        DateTime nowUtc = DateTime.UtcNow;

        UnstuckState next = StuckDetector.ResolveReverseEscalationState(
            enhancedRecoveryAvailable: true,
            nowUtc: nowUtc,
            breadcrumbBacktrackDisabledUntilUtc: nowUtc.AddSeconds(30));

        Assert.Equal(UnstuckState.PathClearAttempt, next);
    }

    [Fact]
    public void ResolveReverseEscalationState_WhenCooldownElapsed_ReturnsBreadcrumbBacktrack()
    {
        DateTime nowUtc = DateTime.UtcNow;

        UnstuckState next = StuckDetector.ResolveReverseEscalationState(
            enhancedRecoveryAvailable: true,
            nowUtc: nowUtc,
            breadcrumbBacktrackDisabledUntilUtc: nowUtc.AddSeconds(-1));

        Assert.Equal(UnstuckState.BreadcrumbBacktrack, next);
    }

    [Fact]
    public void ResolveReverseEscalationState_WhenEnhancedRecoveryDisabled_ReturnsPathClearAttempt()
    {
        DateTime nowUtc = DateTime.UtcNow;

        UnstuckState next = StuckDetector.ResolveReverseEscalationState(
            enhancedRecoveryAvailable: false,
            nowUtc: nowUtc,
            breadcrumbBacktrackDisabledUntilUtc: nowUtc.AddSeconds(-1));

        Assert.Equal(UnstuckState.PathClearAttempt, next);
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
