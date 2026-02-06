using Core.FeatureFlags;
using Core.Hazard;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace CoreUnitTests.Hazard;

public sealed class HazardPipelineIntegrationTests
{
    [Fact]
    public void StoreClusterCostPipeline_ProducesHighCostNearCentroid_ZeroFarAway()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: true);
        using HazardZoneStore store = new(NullLogger<HazardZoneStore>.Instance, featureFlags);
        HazardClusterAnalyzer analyzer = new(NullLogger<HazardClusterAnalyzer>.Instance);

        HazardEvent[] events =
        [
            CreateEvent(new Vector3(100f, 100f, 0f), mapId: 1, type: HazardEventType.Death),
            CreateEvent(new Vector3(106f, 98f, 0f), mapId: 1, type: HazardEventType.Stuck),
            CreateEvent(new Vector3(102f, 104f, 0f), mapId: 1, type: HazardEventType.TargetEvade)
        ];

        store.AddEvents(mapId: 1, events);
        List<HazardCluster> clusters = analyzer.RunDBSCAN(store.GetEventsSnapshot(1), epsilon: 15f, minPoints: 2, halfLifeDays: 30);
        Assert.NotEmpty(clusters);

        store.ReplaceClusters(1, clusters);

        float near = store.GetHazardCost(new Vector3(103f, 101f, 0f), mapId: 1);
        float far = store.GetHazardCost(new Vector3(1000f, 1000f, 0f), mapId: 1);

        Assert.True(near > 0f);
        Assert.Equal(0f, far);
    }

    [Fact]
    public async Task PersistenceRoundTrip_LoadRecluster_PreservesCostBehavior()
    {
        string root = Path.Combine(Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            DataConfig dataConfig = new()
            {
                Root = root,
                Exp = "wrath"
            };

            LocalHazardDAO dao = new(dataConfig, NullLogger<LocalHazardDAO>.Instance);

            HazardEvent[] events =
            [
                CreateEvent(new Vector3(50f, 50f, 0f), mapId: 2, type: HazardEventType.Stuck),
                CreateEvent(new Vector3(55f, 52f, 0f), mapId: 2, type: HazardEventType.Stuck),
                CreateEvent(new Vector3(52f, 57f, 0f), mapId: 2, type: HazardEventType.Death)
            ];

            await dao.SaveAsync("wrath", mapId: 2, events, CancellationToken.None);
            IReadOnlyList<HazardEvent> loaded = await dao.LoadAsync("wrath", mapId: 2, CancellationToken.None);

            FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: true);
            using HazardZoneStore store = new(NullLogger<HazardZoneStore>.Instance, featureFlags);
            HazardClusterAnalyzer analyzer = new(NullLogger<HazardClusterAnalyzer>.Instance);

            store.AddEvents(mapId: 2, loaded);
            List<HazardCluster> clusters = analyzer.RunDBSCAN(store.GetEventsSnapshot(2), epsilon: 12f, minPoints: 2, halfLifeDays: 30);
            store.ReplaceClusters(2, clusters);

            float near = store.GetHazardCost(new Vector3(52f, 53f, 0f), mapId: 2);
            float far = store.GetHazardCost(new Vector3(-500f, -500f, 0f), mapId: 2);

            Assert.True(near > 0f);
            Assert.Equal(0f, far);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void FeatureDisabled_ReturnsZeroCost_EvenWithClustersPresent()
    {
        FeatureFlagService featureFlags = CreateFeatureFlagService(enabled: false);
        using HazardZoneStore store = new(NullLogger<HazardZoneStore>.Instance, featureFlags);

        HazardCluster cluster = new()
        {
            Centroid = new Vector3(5f, 5f, 0f),
            Radius = 10f,
            SeverityScore = 4f,
            Events = new List<HazardEvent> { CreateEvent(new Vector3(5f, 5f, 0f), mapId: 9, type: HazardEventType.Death) }
        };

        store.ReplaceClusters(9, new List<HazardCluster> { cluster });
        float cost = store.GetHazardCost(new Vector3(5f, 5f, 0f), mapId: 9);

        Assert.Equal(0f, cost);
    }

    private static HazardEvent CreateEvent(Vector3 position, int mapId, HazardEventType type)
    {
        return new HazardEvent
        {
            WorldPosition = position,
            MapX = position.X,
            MapY = position.Y,
            MapId = mapId,
            UIMapId = mapId,
            Type = type,
            Zone = "Test"
        };
    }

    private static FeatureFlagService CreateFeatureFlagService(bool enabled)
    {
        FeatureFlagsOptions options = new()
        {
            HazardAvoidance = new HazardAvoidanceOptions
            {
                Enabled = enabled,
                HazardCostMultiplier = 10f,
                DBSCANEpsilon = 20f,
                DBSCANMinPoints = 2,
                MaxEventsBeforePrune = 1000
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

    private sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
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
