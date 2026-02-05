using Core.FeatureFlags;
using Core.Hazard;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Numerics;

using Xunit;

namespace CoreUnitTests.Hazard;

public sealed class HazardAnalyticsTests
{
    [Fact]
    public void TemporalWeight_CurrentEvent_IsNearOne()
    {
        float weight = HazardAnalytics.CalculateTemporalWeight(DateTime.UtcNow, halfLifeDays: 30);
        Assert.InRange(weight, 0.99f, 1.01f);
    }

    [Fact]
    public void TemporalWeight_30DaysOldEvent_IsNearHalf()
    {
        DateTime old = DateTime.UtcNow.AddDays(-30);
        float weight = HazardAnalytics.CalculateTemporalWeight(old, halfLifeDays: 30);
        Assert.InRange(weight, 0.48f, 0.52f);
    }

    [Fact]
    public void Dbscan_TwoClosePoints_FormCluster()
    {
        HazardEvent[] events =
        [
            new HazardEvent { WorldPosition = new Vector3(0, 0, 0), MapId = 0, UIMapId = 0, Type = HazardEventType.Stuck },
            new HazardEvent { WorldPosition = new Vector3(5, 0, 0), MapId = 0, UIMapId = 0, Type = HazardEventType.Stuck }
        ];

        HazardClusterAnalyzer analyzer = new(NullLogger<HazardClusterAnalyzer>.Instance);
        List<HazardCluster> clusters = analyzer.RunDBSCAN(events, epsilon: 15f, minPoints: 2, halfLifeDays: 30);

        Assert.Single(clusters);
        Assert.Equal(2, clusters[0].EventCount);
    }

    [Fact]
    public void Dbscan_TwoDistantPoints_AreNoise()
    {
        HazardEvent[] events =
        [
            new HazardEvent { WorldPosition = new Vector3(0, 0, 0), MapId = 0, UIMapId = 0, Type = HazardEventType.Stuck },
            new HazardEvent { WorldPosition = new Vector3(100, 0, 0), MapId = 0, UIMapId = 0, Type = HazardEventType.Stuck }
        ];

        HazardClusterAnalyzer analyzer = new(NullLogger<HazardClusterAnalyzer>.Instance);
        List<HazardCluster> clusters = analyzer.RunDBSCAN(events, epsilon: 15f, minPoints: 2, halfLifeDays: 30);

        Assert.Empty(clusters);
    }

    [Fact]
    public void HazardStore_CostIncludesMultiplier()
    {
        FeatureFlagsOptions flags = new()
        {
            HazardAvoidance = new HazardAvoidanceOptions
            {
                Enabled = true,
                HazardCostMultiplier = 10.0f
            }
        };

        IOptionsMonitor<FeatureFlagsOptions> monitor = CreateFixedMonitor(flags);
        FeatureFlagServiceOptions serviceOptions = new() { ConfigFilePath = "runtime_feature_flags.json" };
        FeatureFlagService featureFlagService = new(
            NullLogger<FeatureFlagService>.Instance,
            monitor,
            Options.Create(serviceOptions));

        using HazardZoneStore store = new(NullLogger<HazardZoneStore>.Instance, featureFlagService);

        HazardCluster cluster = new()
        {
            Centroid = Vector3.Zero,
            Radius = 10f,
            Events = new List<HazardEvent>
            {
                new HazardEvent { WorldPosition = Vector3.Zero, MapId = 0, UIMapId = 0, Type = HazardEventType.Death }
            },
            SeverityScore = 5f
        };

        store.ReplaceClusters(0, new List<HazardCluster> { cluster });

        float cost = store.GetHazardCost(new Vector3(0, 0, 0), mapId: 0);
        Assert.InRange(cost, 40f, 60f);
    }

    private static IOptionsMonitor<FeatureFlagsOptions> CreateFixedMonitor(FeatureFlagsOptions options)
    {
        IConfigureOptions<FeatureFlagsOptions>[] configures =
        [
            new ConfigureOptions<FeatureFlagsOptions>(_ => { })
        ];

        IPostConfigureOptions<FeatureFlagsOptions>[] postConfigures = [];
        OptionsFactory<FeatureFlagsOptions> factory = new(configures, postConfigures);
        OptionsCache<FeatureFlagsOptions> cache = new();
        IOptionsChangeTokenSource<FeatureFlagsOptions>[] sources = [];

        cache.TryAdd(Options.DefaultName, options);

        return new OptionsMonitor<FeatureFlagsOptions>(factory, sources, cache);
    }
}

