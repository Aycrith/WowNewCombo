using Core.FeatureFlags;
using Core.Hazard;

using Frontend.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Numerics;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class HazardDebugControllerTests
{
    [Fact]
    public void GetKnownMaps_ReturnsCounts()
    {
        (HazardZoneStore store, FeatureFlagService flags, HazardClusterAnalyzer analyzer) = CreateStore(hazardEnabled: true, debugMode: false);

        store.AddEvent(new HazardEvent
        {
            MapId = 1,
            UIMapId = 100,
            Type = HazardEventType.Stuck,
            WorldPosition = new Vector3(10f, 20f, 1f),
            MapX = 0.4f,
            MapY = 0.6f,
            Timestamp = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc),
            Zone = "TestZone"
        });

        HazardDebugController controller = new(
            NullLogger<HazardDebugController>.Instance,
            store,
            flags,
            analyzer);

        ActionResult<IReadOnlyList<HazardMapSummary>> action = controller.GetKnownMaps();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(action.Result);
        IReadOnlyList<HazardMapSummary> payload = Assert.IsAssignableFrom<IReadOnlyList<HazardMapSummary>>(ok.Value);

        Assert.Single(payload);
        Assert.Equal(1, payload[0].MapId);
        Assert.Equal(1, payload[0].EventCount);
    }

    [Fact]
    public void GetSnapshot_RespectsMaxEventsAndOrdering()
    {
        (HazardZoneStore store, FeatureFlagService flags, HazardClusterAnalyzer analyzer) = CreateStore(hazardEnabled: true, debugMode: false);

        DateTime older = new DateTime(2026, 2, 4, 0, 0, 0, DateTimeKind.Utc);
        DateTime newer = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc);

        HazardEvent e1 = new()
        {
            MapId = 1,
            UIMapId = 100,
            Type = HazardEventType.Stuck,
            WorldPosition = new Vector3(10f, 20f, 1f),
            MapX = 0.1f,
            MapY = 0.2f,
            Timestamp = older,
            Zone = "TestZone"
        };

        HazardEvent e2 = new()
        {
            MapId = 1,
            UIMapId = 100,
            Type = HazardEventType.Death,
            WorldPosition = new Vector3(11f, 21f, 1f),
            MapX = 0.3f,
            MapY = 0.4f,
            Timestamp = newer,
            Zone = "TestZone"
        };

        store.AddEvent(e1);
        store.AddEvent(e2);

        HazardCluster cluster = new()
        {
            Centroid = new Vector3(10.5f, 20.5f, 1f),
            Radius = 12f,
            SeverityScore = 42f,
            Events = new List<HazardEvent> { e1, e2 }
        };

        store.ReplaceClusters(1, new List<HazardCluster> { cluster });

        HazardDebugController controller = new(
            NullLogger<HazardDebugController>.Instance,
            store,
            flags,
            analyzer);

        ActionResult<HazardDebugSnapshotResponse> action = controller.GetSnapshot(
            mapId: 1,
            includeEvents: true,
            includeClusters: true,
            maxEvents: 1,
            maxClusters: 10,
            maxAgeMinutes: null,
            mostRecentFirst: true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(action.Result);
        HazardDebugSnapshotResponse payload = Assert.IsType<HazardDebugSnapshotResponse>(ok.Value);

        Assert.True(payload.HazardAvoidanceEnabled);
        Assert.Equal(2, payload.TotalEventCount);
        Assert.Equal(1, payload.Events.Count);
        Assert.Equal(newer, payload.Events[0].Timestamp);

        Assert.Single(payload.Clusters);
        Assert.Equal(42f, payload.Clusters[0].SeverityScore);
    }

    [Fact]
    public void GetSnapshot_RespectsMaxAgeFilter()
    {
        (HazardZoneStore store, FeatureFlagService flags, HazardClusterAnalyzer analyzer) = CreateStore(hazardEnabled: true, debugMode: false);

        DateTime oldEvent = DateTime.UtcNow - TimeSpan.FromHours(2);
        DateTime recentEvent = DateTime.UtcNow - TimeSpan.FromMinutes(1);

        store.AddEvent(new HazardEvent
        {
            MapId = 1,
            UIMapId = 100,
            Type = HazardEventType.Stuck,
            WorldPosition = new Vector3(10f, 20f, 1f),
            MapX = 0.1f,
            MapY = 0.2f,
            Timestamp = oldEvent,
            Zone = "TestZone"
        });

        store.AddEvent(new HazardEvent
        {
            MapId = 1,
            UIMapId = 100,
            Type = HazardEventType.Death,
            WorldPosition = new Vector3(11f, 21f, 1f),
            MapX = 0.3f,
            MapY = 0.4f,
            Timestamp = recentEvent,
            Zone = "TestZone"
        });

        HazardDebugController controller = new(
            NullLogger<HazardDebugController>.Instance,
            store,
            flags,
            analyzer);

        ActionResult<HazardDebugSnapshotResponse> action = controller.GetSnapshot(
            mapId: 1,
            includeEvents: true,
            includeClusters: false,
            maxEvents: 100,
            maxClusters: 0,
            maxAgeMinutes: 10,
            mostRecentFirst: true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(action.Result);
        HazardDebugSnapshotResponse payload = Assert.IsType<HazardDebugSnapshotResponse>(ok.Value);

        Assert.Equal(2, payload.TotalEventCount);
        Assert.Single(payload.Events);
        Assert.Equal(recentEvent, payload.Events[0].Timestamp);
    }

    [Fact]
    public void InjectEvents_WhenDebugModeDisabled_ReturnsForbid()
    {
        (HazardZoneStore store, FeatureFlagService flags, HazardClusterAnalyzer analyzer) = CreateStore(hazardEnabled: true, debugMode: false);

        HazardDebugController controller = new(
            NullLogger<HazardDebugController>.Instance,
            store,
            flags,
            analyzer);

        HazardInjectRequest request = new(
            X: 0,
            Y: 0,
            Z: 0,
            UIMapId: 0,
            Type: HazardEventType.ManualMarker,
            Count: 2);

        ActionResult<HazardInjectResponse> action = controller.InjectEvents(mapId: 1, request);

        ObjectResult forbidden = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(403, forbidden.StatusCode);
    }

    [Fact]
    public void InjectAndCluster_WhenDebugModeEnabled_CreatesCluster()
    {
        (HazardZoneStore store, FeatureFlagService flags, HazardClusterAnalyzer analyzer) = CreateStore(hazardEnabled: true, debugMode: true);

        HazardDebugController controller = new(
            NullLogger<HazardDebugController>.Instance,
            store,
            flags,
            analyzer);

        HazardInjectRequest request = new(
            X: 0,
            Y: 0,
            Z: 0,
            UIMapId: 0,
            Type: HazardEventType.ManualMarker,
            Count: 3);

        OkObjectResult injectedOk = Assert.IsType<OkObjectResult>(controller.InjectEvents(mapId: 1, request).Result);
        HazardInjectResponse injected = Assert.IsType<HazardInjectResponse>(injectedOk.Value);
        Assert.Equal(3, injected.AddedEvents);
        Assert.Equal(3, injected.TotalEvents);

        OkObjectResult clusteredOk = Assert.IsType<OkObjectResult>(controller.ClusterNow(mapId: 1).Result);
        HazardClusterNowResponse clustered = Assert.IsType<HazardClusterNowResponse>(clusteredOk.Value);

        Assert.Equal(1, clustered.MapId);
        Assert.Equal(3, clustered.TotalEvents);
        Assert.True(clustered.ClusterCount >= 1);
    }

    private static (HazardZoneStore Store, FeatureFlagService Flags, HazardClusterAnalyzer Analyzer) CreateStore(bool hazardEnabled, bool debugMode)
    {
        FeatureFlagsOptions options = new()
        {
            DebugMode = debugMode,
            HazardAvoidance = new HazardAvoidanceOptions
            {
                Enabled = hazardEnabled,
                MaxEventsBeforePrune = 10000,
                HazardCostMultiplier = 1.0f,
                DBSCANEpsilon = 15.0f,
                DBSCANMinPoints = 2,
                DecayHalfLifeDays = 30
            }
        };

        TestOptionsMonitor<FeatureFlagsOptions> monitor = new(options);

        FeatureFlagService flags = new(
            NullLogger<FeatureFlagService>.Instance,
            monitor,
            Options.Create(new FeatureFlagServiceOptions()));

        HazardZoneStore store = new(
            NullLogger<HazardZoneStore>.Instance,
            flags);

        HazardClusterAnalyzer analyzer = new(NullLogger<HazardClusterAnalyzer>.Instance);

        return (store, flags, analyzer);
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        private readonly T value = value;

        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
