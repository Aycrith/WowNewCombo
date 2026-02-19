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

    [Fact]
    public void SuggestAlternativePath_WhenManyNearbyEvents_LimitsDetourCount()
    {
        (HazardZoneStore store, FeatureFlagService flags) = CreateStore(enabled: true);
        RouteRehabilitator rehab = new(store, NullLogger<RouteRehabilitator>.Instance, flags);

        List<HazardEvent> events = [];
        DateTime now = DateTime.UtcNow;

        for (int i = 0; i < 8; i++)
        {
            events.Add(new HazardEvent
            {
                WorldPosition = new Vector3(50 + i, 1 + (i % 2), 0),
                MapId = 1,
                UIMapId = 100,
                Type = HazardEventType.Stuck,
                Timestamp = now.AddSeconds(-i)
            });
        }

        store.AddEvents(1, events);

        List<Vector3> alternatives = rehab.SuggestAlternativePath(
            start: new Vector3(0, 0, 0),
            end: new Vector3(100, 0, 0),
            mapId: 1,
            safetyMargin: 20f);

        Assert.NotEmpty(alternatives);
        Assert.InRange(alternatives.Count, 1, 3);
    }

    [Fact]
    public void SuggestAlternativePath_IgnoresOldFallbackEvents()
    {
        (HazardZoneStore store, FeatureFlagService flags) = CreateStore(enabled: true);
        RouteRehabilitator rehab = new(store, NullLogger<RouteRehabilitator>.Instance, flags);

        store.AddEvent(new HazardEvent
        {
            WorldPosition = new Vector3(50, 0, 0),
            MapId = 1,
            UIMapId = 100,
            Type = HazardEventType.Stuck,
            Timestamp = DateTime.UtcNow.AddHours(-5)
        });

        List<Vector3> alternatives = rehab.SuggestAlternativePath(
            start: new Vector3(0, 0, 0),
            end: new Vector3(100, 0, 0),
            mapId: 1,
            safetyMargin: 20f);

        Assert.Empty(alternatives);
    }

    [Fact]
    public void SuggestAlternativePath_IncludesRecentEventDetour_WhenClustersAlsoExist()
    {
        (HazardZoneStore store, FeatureFlagService flags) = CreateStore(enabled: true);
        RouteRehabilitator rehab = new(store, NullLogger<RouteRehabilitator>.Instance, flags);

        store.ReplaceClusters(1,
            [
                new HazardCluster
                {
                    Centroid = new Vector3(50, 0, 0),
                    Radius = 12f,
                    SeverityScore = 9f,
                    Events = new List<HazardEvent>()
                }
            ]);

        store.AddEvent(new HazardEvent
        {
            WorldPosition = new Vector3(80, 2, 0),
            MapId = 1,
            UIMapId = 100,
            Type = HazardEventType.Stuck,
            Timestamp = DateTime.UtcNow.AddMinutes(-1)
        });

        List<Vector3> alternatives = rehab.SuggestAlternativePath(
            start: new Vector3(0, 0, 0),
            end: new Vector3(120, 0, 0),
            mapId: 1,
            safetyMargin: 20f);

        Assert.True(alternatives.Count >= 2);
    }

    [Fact]
    public void SuggestAlternativePath_DetourIncreasesClearanceFromRecentHazardEvent()
    {
        (HazardZoneStore store, FeatureFlagService flags) = CreateStore(enabled: true);
        RouteRehabilitator rehab = new(store, NullLogger<RouteRehabilitator>.Instance, flags);

        Vector3 start = new(0, 0, 0);
        Vector3 end = new(100, 0, 0);
        Vector3 hazard = new(50, 0, 0);

        store.AddEvent(new HazardEvent
        {
            WorldPosition = hazard,
            MapId = 1,
            UIMapId = 100,
            Type = HazardEventType.Stuck,
            Timestamp = DateTime.UtcNow
        });

        List<Vector3> alternatives = rehab.SuggestAlternativePath(start, end, mapId: 1, safetyMargin: 20f);
        Assert.NotEmpty(alternatives);

        Vector3 detour = alternatives[0];

        float directClearance = MinPathClearance(hazard, [start, end]);
        float detourClearance = MinPathClearance(hazard, [start, detour, end]);

        Assert.True(detourClearance > directClearance + 10f);
    }

    [Fact]
    public void SuggestAlternativePath_DetourMaintainsClusterSafetyMargin()
    {
        (HazardZoneStore store, FeatureFlagService flags) = CreateStore(enabled: true);
        RouteRehabilitator rehab = new(store, NullLogger<RouteRehabilitator>.Instance, flags);

        Vector3 start = new(0, 0, 0);
        Vector3 end = new(100, 0, 0);
        Vector3 clusterCenter = new(50, 0, 0);

        HazardCluster cluster = new()
        {
            Centroid = clusterCenter,
            Radius = 12f,
            SeverityScore = 12f,
            Events = new List<HazardEvent>()
        };

        store.ReplaceClusters(1, [cluster]);

        const float safetyMargin = 20f;
        List<Vector3> alternatives = rehab.SuggestAlternativePath(start, end, mapId: 1, safetyMargin: safetyMargin);
        Assert.NotEmpty(alternatives);

        Vector3 detour = alternatives[0];
        float clearance = MinPathClearance(clusterCenter, [start, detour, end]);

        Assert.True(clearance >= (cluster.Radius + safetyMargin - 1f));
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

    private static float MinPathClearance(Vector3 point, Vector3[] path)
    {
        float min = float.MaxValue;

        for (int i = 1; i < path.Length; i++)
        {
            float clearance = DistancePointToSegment(point, path[i - 1], path[i]);
            if (clearance < min)
            {
                min = clearance;
            }
        }

        return min;
    }

    private static float DistancePointToSegment(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSquared = Vector3.Dot(segment, segment);
        if (lengthSquared < 0.0001f)
        {
            return Vector3.Distance(point, start);
        }

        float t = Vector3.Dot(point - start, segment) / lengthSquared;
        t = Math.Clamp(t, 0f, 1f);
        Vector3 projection = start + (segment * t);
        return Vector3.Distance(point, projection);
    }

}
