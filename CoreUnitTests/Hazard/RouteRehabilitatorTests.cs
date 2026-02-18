using Core.FeatureFlags;
using Core.Hazard;
using CoreUnitTests.TestHelpers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Numerics;

using Xunit;

namespace CoreUnitTests.Hazard;

public sealed class RouteRehabilitatorTests
{
    [Fact]
    public void ReportSuccessfulTraversal_WhenEnabled_ReducesSeverity()
    {
        (HazardZoneStore store, FeatureFlagService flags) = CreateStore(enabled: true);

        HazardEvent e = new()
        {
            WorldPosition = Vector3.Zero,
            MapId = 1,
            UIMapId = 100,
            Type = HazardEventType.Stuck,
            Timestamp = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc)
        };

        HazardCluster cluster = new()
        {
            Centroid = Vector3.Zero,
            Radius = 10f,
            SeverityScore = 100f,
            Events = new List<HazardEvent> { e }
        };

        store.ReplaceClusters(1, new List<HazardCluster> { cluster });

        RouteRehabilitator rehab = new(store, NullLogger<RouteRehabilitator>.Instance, flags);

        rehab.ReportSuccessfulTraversal(new Vector3(0, 0, 0), mapId: 1, radius: 0f, severityFactor: 0.9f);

        Assert.InRange(cluster.SeverityScore, 89f, 91f);
    }

    [Fact]
    public void ReportSuccessfulTraversal_WhenDisabled_DoesNothing()
    {
        (HazardZoneStore store, FeatureFlagService flags) = CreateStore(enabled: false);

        HazardEvent e = new()
        {
            WorldPosition = Vector3.Zero,
            MapId = 1,
            UIMapId = 100,
            Type = HazardEventType.Death,
            Timestamp = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc)
        };

        HazardCluster cluster = new()
        {
            Centroid = Vector3.Zero,
            Radius = 10f,
            SeverityScore = 100f,
            Events = new List<HazardEvent> { e }
        };

        store.ReplaceClusters(1, new List<HazardCluster> { cluster });

        RouteRehabilitator rehab = new(store, NullLogger<RouteRehabilitator>.Instance, flags);

        rehab.ReportSuccessfulTraversal(new Vector3(0, 0, 0), mapId: 1, radius: 0f, severityFactor: 0.5f);

        Assert.Equal(100f, cluster.SeverityScore);
    }

    [Fact]
    public void ReportSuccessfulTraversal_OutsideCluster_DoesNothing()
    {
        (HazardZoneStore store, FeatureFlagService flags) = CreateStore(enabled: true);

        HazardEvent e = new()
        {
            WorldPosition = Vector3.Zero,
            MapId = 1,
            UIMapId = 100,
            Type = HazardEventType.Stuck,
            Timestamp = new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc)
        };

        HazardCluster cluster = new()
        {
            Centroid = Vector3.Zero,
            Radius = 10f,
            SeverityScore = 100f,
            Events = new List<HazardEvent> { e }
        };

        store.ReplaceClusters(1, new List<HazardCluster> { cluster });

        RouteRehabilitator rehab = new(store, NullLogger<RouteRehabilitator>.Instance, flags);

        rehab.ReportSuccessfulTraversal(new Vector3(200, 0, 0), mapId: 1, radius: 0f, severityFactor: 0.5f);

        Assert.Equal(100f, cluster.SeverityScore);
    }

    private static (HazardZoneStore Store, FeatureFlagService Flags) CreateStore(bool enabled)
    {
        FeatureFlagsOptions flags = new()
        {
            HazardAvoidance = new HazardAvoidanceOptions
            {
                Enabled = enabled,
                HazardCostMultiplier = 1.0f,
                MaxEventsBeforePrune = 1000
            }
        };

        FixedOptionsMonitor<FeatureFlagsOptions> monitor = new(flags);

        FeatureFlagService featureFlagService = new(
            NullLogger<FeatureFlagService>.Instance,
            monitor,
            Options.Create(new FeatureFlagServiceOptions()));

        HazardZoneStore store = new(NullLogger<HazardZoneStore>.Instance, featureFlagService);
        return (store, featureFlagService);
    }

}
