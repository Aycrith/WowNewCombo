using Core;
using Core.Navigation;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

using Xunit;

namespace CoreUnitTests.Navigation;

public class NavSoakMetricsServiceTests
{
    private static NavSoakMetricsService CreateService()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"navsoak-tests-{Guid.NewGuid():N}");
        return new NavSoakMetricsService(
            NullLogger<NavSoakMetricsService>.Instance,
            outputDir: outputDir);
    }

    private static Core.Goals.Navigation CreateNavigationStub()
    {
        return (Core.Goals.Navigation)RuntimeHelpers.GetUninitializedObject(typeof(Core.Goals.Navigation));
    }

    private static StuckDetector CreateStuckDetectorStub()
    {
        return (StuckDetector)RuntimeHelpers.GetUninitializedObject(typeof(StuckDetector));
    }

    private static void RaiseNavigationEvent(Core.Goals.Navigation navigation, string eventFieldName)
    {
        FieldInfo? field = typeof(Core.Goals.Navigation).GetField(
            eventFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Delegate? handler = field?.GetValue(navigation) as Delegate;
        handler?.DynamicInvoke();
    }

    private static void RaiseNavigationEvent(Core.Goals.Navigation navigation, string eventFieldName, float value)
    {
        FieldInfo? field = typeof(Core.Goals.Navigation).GetField(
            eventFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Delegate? handler = field?.GetValue(navigation) as Delegate;
        handler?.DynamicInvoke(value);
    }

    private static void RaiseNavigationEvent(Core.Goals.Navigation navigation, string eventFieldName, string value)
    {
        FieldInfo? field = typeof(Core.Goals.Navigation).GetField(
            eventFieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Delegate? handler = field?.GetValue(navigation) as Delegate;
        handler?.DynamicInvoke(value);
    }

    [Fact]
    public void AttachRuntimeSources_NavigationEvents_IncrementWindowCounters()
    {
        using NavSoakMetricsService service = CreateService();
        StuckDetector stuckDetector = CreateStuckDetectorStub();
        Core.Goals.Navigation navigation = CreateNavigationStub();

        service.AttachRuntimeSources(stuckDetector, navigation);

        RaiseNavigationEvent(navigation, "OnDynamicDetourApplied");
        RaiseNavigationEvent(navigation, "OnSuccessfulReconnect");
        RaiseNavigationEvent(navigation, "OnSuccessfulReconnect");
        RaiseNavigationEvent(navigation, "OnRerouteTriggered");
        RaiseNavigationEvent(navigation, "OnRerouteApplied");
        RaiseNavigationEvent(navigation, "OnRerouteDropped", "TargetMismatch");
        RaiseNavigationEvent(navigation, "OnDetourOnlyCollapseDetected");
        RaiseNavigationEvent(navigation, "OnDeviationSample", 3.5f);
        RaiseNavigationEvent(navigation, "OnDeviationSample", 6.5f);

        NavSoakMetricsSnapshot snapshot = service.GetSnapshot();
        snapshot.CurrentWindowFrontBypassActivations.Should().Be(1);
        snapshot.CurrentWindowSuccessfulReconnects.Should().Be(2);
        snapshot.CurrentWindowRerouteTriggerCount.Should().Be(1);
        snapshot.CurrentWindowRerouteApplyCount.Should().Be(1);
        snapshot.CurrentWindowRerouteDropCount.Should().Be(1);
        snapshot.CurrentWindowDetourOnlyCollapseCount.Should().Be(1);
        snapshot.CurrentRouteDeviation.Should().Be(6.5f);
        snapshot.CurrentWindowMaxRouteDeviation.Should().Be(6.5f);
        snapshot.CurrentWindowAvgRouteDeviation.Should().BeApproximately(5.0f, 0.001f);
    }

    [Fact]
    public void Dispose_DetachesNavigationHandlers_NoFurtherCounterChanges()
    {
        NavSoakMetricsService service = CreateService();
        StuckDetector stuckDetector = CreateStuckDetectorStub();
        Core.Goals.Navigation navigation = CreateNavigationStub();

        service.AttachRuntimeSources(stuckDetector, navigation);

        RaiseNavigationEvent(navigation, "OnDynamicDetourApplied");
        RaiseNavigationEvent(navigation, "OnSuccessfulReconnect");
        RaiseNavigationEvent(navigation, "OnRerouteTriggered");
        RaiseNavigationEvent(navigation, "OnRerouteApplied");
        RaiseNavigationEvent(navigation, "OnRerouteDropped", "RerouteExpired");
        RaiseNavigationEvent(navigation, "OnDetourOnlyCollapseDetected");

        NavSoakMetricsSnapshot beforeDispose = service.GetSnapshot();
        beforeDispose.CurrentWindowFrontBypassActivations.Should().Be(1);
        beforeDispose.CurrentWindowSuccessfulReconnects.Should().Be(1);
        beforeDispose.CurrentWindowRerouteTriggerCount.Should().Be(1);
        beforeDispose.CurrentWindowRerouteApplyCount.Should().Be(1);
        beforeDispose.CurrentWindowRerouteDropCount.Should().Be(1);
        beforeDispose.CurrentWindowDetourOnlyCollapseCount.Should().Be(1);

        service.Dispose();

        RaiseNavigationEvent(navigation, "OnDynamicDetourApplied");
        RaiseNavigationEvent(navigation, "OnSuccessfulReconnect");
        RaiseNavigationEvent(navigation, "OnRerouteTriggered");
        RaiseNavigationEvent(navigation, "OnRerouteApplied");
        RaiseNavigationEvent(navigation, "OnRerouteDropped", "TargetMismatch");
        RaiseNavigationEvent(navigation, "OnDetourOnlyCollapseDetected");
        RaiseNavigationEvent(navigation, "OnDeviationSample", 10.0f);

        NavSoakMetricsSnapshot afterDispose = service.GetSnapshot();
        afterDispose.CurrentWindowFrontBypassActivations.Should().Be(1);
        afterDispose.CurrentWindowSuccessfulReconnects.Should().Be(1);
        afterDispose.CurrentWindowRerouteTriggerCount.Should().Be(1);
        afterDispose.CurrentWindowRerouteApplyCount.Should().Be(1);
        afterDispose.CurrentWindowRerouteDropCount.Should().Be(1);
        afterDispose.CurrentWindowDetourOnlyCollapseCount.Should().Be(1);
        afterDispose.CurrentRouteDeviation.Should().Be(0f);
    }

    [Fact]
    public void SuccessfulReconnectEvent_FromNoPathRecovery_IncrementsCounter()
    {
        using NavSoakMetricsService service = CreateService();
        StuckDetector stuckDetector = CreateStuckDetectorStub();
        Core.Goals.Navigation navigation = CreateNavigationStub();

        service.AttachRuntimeSources(stuckDetector, navigation);

        // Navigation emits OnSuccessfulReconnect after a path succeeds following one-or-more no-path failures.
        RaiseNavigationEvent(navigation, "OnSuccessfulReconnect");

        NavSoakMetricsSnapshot snapshot = service.GetSnapshot();
        snapshot.CurrentWindowSuccessfulReconnects.Should().Be(1);
    }

    [Fact]
    public void NavSoakWindow_RepeatStuckRate_CalculatesCorrectly()
    {
        NavSoakWindow window = new()
        {
            StuckEvents = 10,
            RepeatStuckCount = 3
        };

        window.RepeatStuckRate.Should().Be(0.3, "3 of 10 stuck events were repeats");
    }

    [Fact]
    public void NavSoakWindow_RepeatStuckRate_RoundedToFourDecimalPlaces()
    {
        NavSoakWindow window = new()
        {
            StuckEvents = 3,
            RepeatStuckCount = 1
        };

        // 1/3 = 0.333333... should round to 0.3333
        window.RepeatStuckRate.Should().Be(0.3333, "rate is rounded to 4 decimal places");
    }

    [Fact]
    public void NavSoakWindow_RepeatStuckRate_ZeroWhenNoStuck()
    {
        NavSoakWindow window = new()
        {
            StuckEvents = 0,
            RepeatStuckCount = 0
        };

        window.RepeatStuckRate.Should().Be(0.0, "no division by zero when StuckEvents = 0");
    }

    [Fact]
    public void NavSoakWindow_JsonRoundTrip_PreservesAllFields()
    {
        DateTime start = new(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        DateTime end   = new(2026, 3, 1, 10, 10, 0, DateTimeKind.Utc);

        NavSoakWindow original = new()
        {
            WindowStartUtc         = start,
            WindowEndUtc           = end,
            FrontBypassActivations = 5,
            SuccessfulReconnects   = 2,
            StuckEvents            = 4,
            RepeatStuckCount       = 1,
            TailRecalcFailures     = 3,
            MaxRouteDeviation      = 12.5f,
            AvgRouteDeviation      = 7.25f,
            RerouteTriggerCount    = 4,
            RerouteApplyCount      = 3,
            RerouteDropCount       = 1,
            DetourOnlyCollapseCount = 1
        };

        JsonSerializerOptions opts = new() { WriteIndented = false };
        string json = JsonSerializer.Serialize(original, opts);
        NavSoakWindow? restored = JsonSerializer.Deserialize<NavSoakWindow>(json, opts);

        restored.Should().NotBeNull();
        restored!.WindowStartUtc.Should().Be(start);
        restored.WindowEndUtc.Should().Be(end);
        restored.FrontBypassActivations.Should().Be(5);
        restored.SuccessfulReconnects.Should().Be(2);
        restored.StuckEvents.Should().Be(4);
        restored.RepeatStuckCount.Should().Be(1);
        restored.TailRecalcFailures.Should().Be(3);
        restored.MaxRouteDeviation.Should().BeApproximately(12.5f, 0.001f);
        restored.AvgRouteDeviation.Should().BeApproximately(7.25f, 0.001f);
        restored.RerouteTriggerCount.Should().Be(4);
        restored.RerouteApplyCount.Should().Be(3);
        restored.RerouteDropCount.Should().Be(1);
        restored.DetourOnlyCollapseCount.Should().Be(1);
    }

    [Fact]
    public void NavSoakWindow_DefaultValues_AreZeroOrMinDateTime()
    {
        NavSoakWindow window = new();

        window.FrontBypassActivations.Should().Be(0);
        window.SuccessfulReconnects.Should().Be(0);
        window.StuckEvents.Should().Be(0);
        window.RepeatStuckCount.Should().Be(0);
        window.TailRecalcFailures.Should().Be(0);
        window.MaxRouteDeviation.Should().Be(0f);
        window.AvgRouteDeviation.Should().Be(0f);
        window.RerouteTriggerCount.Should().Be(0);
        window.RerouteApplyCount.Should().Be(0);
        window.RerouteDropCount.Should().Be(0);
        window.DetourOnlyCollapseCount.Should().Be(0);
        window.RepeatStuckRate.Should().Be(0.0);
    }

    [Fact]
    public async Task FlushAsync_WithNonFiniteDeviation_SerializesArtifactSuccessfully()
    {
        using NavSoakMetricsService service = CreateService();
        StuckDetector stuckDetector = CreateStuckDetectorStub();
        Core.Goals.Navigation navigation = CreateNavigationStub();

        service.AttachRuntimeSources(stuckDetector, navigation);

        RaiseNavigationEvent(navigation, "OnSuccessfulReconnect");
        RaiseNavigationEvent(navigation, "OnDeviationSample", float.PositiveInfinity);
        RaiseNavigationEvent(navigation, "OnDeviationSample", float.NaN);
        RaiseNavigationEvent(navigation, "OnDeviationSample", 4.25f);

        await service.FlushAsync();

        NavSoakMetricsSnapshot snapshot = service.GetSnapshot();
        File.Exists(snapshot.ArtifactPath).Should().BeTrue("artifact should still be written for the window");

        string json = File.ReadAllText(snapshot.ArtifactPath);
        json.Should().Contain("4.25");
        json.Should().NotContain("Infinity");
        json.Should().NotContain("NaN");
    }
}
