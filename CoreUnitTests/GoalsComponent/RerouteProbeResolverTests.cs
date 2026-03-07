using Core;

using FluentAssertions;

using SharedLib;

using System.Numerics;

using Xunit;

namespace CoreUnitTests.GoalsComponent;

public sealed class RerouteProbeResolverTests
{
    [Fact]
    public void TryResolveFromMapRoute_WhenPlayerNearRoute_ReturnsLookaheadAnchor()
    {
        WorldMapArea worldMapArea = CreateWorldMapArea();
        Vector3[] mapRoute =
        [
            new Vector3(10, 10, 0),
            new Vector3(20, 10, 0),
            new Vector3(30, 10, 0),
            new Vector3(40, 10, 0)
        ];

        Vector3? probe = RerouteProbeResolver.TryResolveFromMapRoute(
            mapRoute,
            new Vector3(15, 10, 0),
            new Vector3(10, 15, 42),
            worldMapArea,
            lookaheadPoints: 4,
            minAnchorDistance: 8.0f);

        probe.Should().Be(new Vector3(10, 30, 42));
    }

    [Fact]
    public void TryResolveFromMapRoute_WhenRouteEmpty_ReturnsNull()
    {
        Vector3? probe = RerouteProbeResolver.TryResolveFromMapRoute(
            [],
            new Vector3(15, 10, 0),
            new Vector3(10, 15, 42),
            CreateWorldMapArea());

        probe.Should().BeNull();
    }

    [Fact]
    public void TryResolveFromMapRoute_WhenPlayerAlreadyAtOnlyPoint_ReturnsNull()
    {
        WorldMapArea worldMapArea = CreateWorldMapArea();
        Vector3? probe = RerouteProbeResolver.TryResolveFromMapRoute(
            [new Vector3(15, 10, 0)],
            new Vector3(15, 10, 0),
            new Vector3(10, 15, 42),
            worldMapArea);

        probe.Should().BeNull();
    }

    [Fact]
    public void TryResolveFromMapRoute_WhenMapRouteHasZeroZ_UsesPlayerWorldZForProbe()
    {
        WorldMapArea worldMapArea = CreateWorldMapArea();
        Vector3[] mapRoute =
        [
            new Vector3(10, 10, 0),
            new Vector3(20, 10, 0),
            new Vector3(30, 10, 0)
        ];

        Vector3? probe = RerouteProbeResolver.TryResolveFromMapRoute(
            mapRoute,
            new Vector3(10, 10, 0),
            new Vector3(10, 10, 66.5f),
            worldMapArea,
            lookaheadPoints: 3,
            minAnchorDistance: 8.0f);

        probe.Should().NotBeNull();
        probe!.Value.Z.Should().Be(66.5f);
    }

    private static WorldMapArea CreateWorldMapArea()
    {
        return new WorldMapArea
        {
            AreaName = "Test Zone",
            UIMapId = 1942,
            MapID = 1942,
            LocLeft = 0,
            LocRight = 100,
            LocTop = 0,
            LocBottom = 100
        };
    }
}
