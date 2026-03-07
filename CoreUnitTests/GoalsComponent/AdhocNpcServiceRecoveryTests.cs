using Core;
using Core.Database;
using Core.Goals;

using FluentAssertions;

using SharedLib;
using SharedLib.Data;

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using WowheadDB;

using Xunit;

namespace CoreUnitTests.GoalsComponent;

public sealed class AdhocNpcServiceRecoveryTests
{
    private static readonly WorldMapArea TestWorldMapArea = new()
    {
        MapID = 530,
        AreaID = 3433,
        AreaName = "Ghostlands",
        LocLeft = 0,
        LocRight = 100,
        LocTop = 100,
        LocBottom = 0,
        UIMapId = 1942,
        Continent = "Expansion01",
        ParentAreaId = 0,
        ExplorationLevel = 1
    };

    [Theory]
    [InlineData("Sell", NpcServiceKind.Vendor)]
    [InlineData("Repair", NpcServiceKind.Repair)]
    [InlineData("Trainer Warlock", NpcServiceKind.Trainer)]
    [InlineData("Innkeeper", NpcServiceKind.Innkeeper)]
    [InlineData("Flightmaster Silvermoon", NpcServiceKind.FlightMaster)]
    public void GetRequestedServiceKind_ShouldMapKnownServiceActions(string actionName, NpcServiceKind expected)
    {
        NpcServiceKind result = AdhocNPCGoal.GetRequestedServiceKind(actionName);

        result.Should().Be(expected);
    }

    [Fact]
    public void BuildRejectedServiceCandidateKey_ShouldIncludeServiceEntryAndName()
    {
        string result = AdhocNPCGoal.BuildRejectedServiceCandidateKey(
            NpcServiceKind.Vendor,
            entry: 16224,
            candidateName: "Rathis Tomber");

        result.Should().Be("1:16224:Rathis Tomber");
    }

    [Fact]
    public void ShouldAttemptSingleOptionVendorFallback_ShouldOnlyAllowVendorAndRepair()
    {
        AdhocNPCGoal.ShouldAttemptSingleOptionVendorFallback(NpcServiceKind.Vendor, gossipCount: 1, hasVendorOption: false).Should().BeTrue();
        AdhocNPCGoal.ShouldAttemptSingleOptionVendorFallback(NpcServiceKind.Repair, gossipCount: 1, hasVendorOption: false).Should().BeTrue();
        AdhocNPCGoal.ShouldAttemptSingleOptionVendorFallback(NpcServiceKind.Trainer, gossipCount: 1, hasVendorOption: false).Should().BeFalse();
        AdhocNPCGoal.ShouldAttemptSingleOptionVendorFallback(NpcServiceKind.Vendor, gossipCount: 2, hasVendorOption: false).Should().BeFalse();
        AdhocNPCGoal.ShouldAttemptSingleOptionVendorFallback(NpcServiceKind.Vendor, gossipCount: 1, hasVendorOption: true).Should().BeFalse();
    }

    [Fact]
    public void HasExpectedServiceGossip_ShouldRecognizeServiceSpecificSignals()
    {
        Dictionary<Gossip, int> vendorGossip = new()
        {
            [Gossip.Vendor] = 1
        };
        Dictionary<Gossip, int> trainerGossip = new()
        {
            [Gossip.Trainer] = 2
        };
        Dictionary<Gossip, int> taxiGossip = new()
        {
            [Gossip.Taxi] = 1
        };
        Dictionary<Gossip, int> innkeeperGossip = new()
        {
            [Gossip.Binder] = 1
        };

        AdhocNPCGoal.HasExpectedServiceGossip(NpcServiceKind.Vendor, vendorGossip, merchantWindowOpened: false).Should().BeTrue();
        AdhocNPCGoal.HasExpectedServiceGossip(NpcServiceKind.Repair, new Dictionary<Gossip, int>(), merchantWindowOpened: true).Should().BeTrue();
        AdhocNPCGoal.HasExpectedServiceGossip(NpcServiceKind.Trainer, trainerGossip, merchantWindowOpened: false).Should().BeTrue();
        AdhocNPCGoal.HasExpectedServiceGossip(NpcServiceKind.FlightMaster, taxiGossip, merchantWindowOpened: false).Should().BeTrue();
        AdhocNPCGoal.HasExpectedServiceGossip(NpcServiceKind.Innkeeper, innkeeperGossip, merchantWindowOpened: false).Should().BeTrue();
        AdhocNPCGoal.HasExpectedServiceGossip(NpcServiceKind.Trainer, vendorGossip, merchantWindowOpened: false).Should().BeFalse();
    }

    [Fact]
    public void CollectCuratedServiceCandidates_ShouldPreferNearestFriendlyCandidateAndSkipTrainerTaggedVendor()
    {
        Area area = CreateArea(vendor:
        [
            CreateNpc(id: 16224, name: "Rathis Tomber", description: "General Goods", reactHorde: 1, reactAlliance: 0, new Vector3(20, 20, 0)),
            CreateNpc(id: 16268, name: "Eralan", description: "Trade Supplies", reactHorde: 1, reactAlliance: 0, new Vector3(70, 70, 0)),
            CreateNpc(id: 99999, name: "Warlock Instructor", description: "Warlock Trainer", reactHorde: 1, reactAlliance: 0, new Vector3(10, 10, 0)),
            CreateNpc(id: 88888, name: "Alliance Vendor", description: "General Supplies", reactHorde: 0, reactAlliance: 1, new Vector3(15, 15, 0))
        ]);

        FrozenDictionary<int, Vector3[]> npcWorldLocations = new Dictionary<int, Vector3[]>
        {
            [16224] = [new Vector3(12, 10, 35)],
            [16268] = [new Vector3(35, 35, 35)],
            [99999] = [new Vector3(8, 8, 35)],
            [88888] = [new Vector3(9, 9, 35)]
        }.ToFrozenDictionary();

        NpcServiceCandidate[] destination = new NpcServiceCandidate[4];
        int found = AreaDB.CollectCuratedServiceCandidates(
            area,
            TestWorldMapArea,
            npcWorldLocations,
            PlayerFaction.Horde,
            NpcServiceKind.Vendor,
            new Vector3(11, 10, 35),
            [],
            destination,
            out int written);

        found.Should().Be(2);
        written.Should().Be(2);
        destination[0].Name.Should().Be("Rathis Tomber");
        destination[0].Source.Should().Be(NpcServiceCandidateSource.AreaCurated);
        destination[1].Name.Should().Be("Eralan");
    }

    [Fact]
    public void CollectCuratedServiceCandidates_ShouldRespectAllowedNamesFilter()
    {
        Area area = CreateArea(vendor:
        [
            CreateNpc(id: 16224, name: "Rathis Tomber", description: "General Goods", reactHorde: 1, reactAlliance: 0, new Vector3(20, 20, 0)),
            CreateNpc(id: 16268, name: "Eralan", description: "Trade Supplies", reactHorde: 1, reactAlliance: 0, new Vector3(70, 70, 0))
        ]);

        FrozenDictionary<int, Vector3[]> npcWorldLocations = new Dictionary<int, Vector3[]>
        {
            [16224] = [new Vector3(15, 10, 35)],
            [16268] = [new Vector3(30, 30, 35)]
        }.ToFrozenDictionary();

        NpcServiceCandidate[] destination = new NpcServiceCandidate[2];
        int found = AreaDB.CollectCuratedServiceCandidates(
            area,
            TestWorldMapArea,
            npcWorldLocations,
            PlayerFaction.Horde,
            NpcServiceKind.Vendor,
            new Vector3(11, 10, 35),
            ["Eralan"],
            destination,
            out int written);

        found.Should().Be(1);
        written.Should().Be(1);
        destination[0].Name.Should().Be("Eralan");
    }

    [Fact]
    public void TryResolveCuratedServiceWorldPosition_ShouldPreferNearestSpawnLocation()
    {
        NPC npc = CreateNpc(
            id: 16224,
            name: "Rathis Tomber",
            description: "General Goods",
            reactHorde: 1,
            reactAlliance: 0,
            new Vector3(20, 20, 40));

        FrozenDictionary<int, Vector3[]> npcWorldLocations = new Dictionary<int, Vector3[]>
        {
            [16224] =
            [
                new Vector3(80, 80, 35),
                new Vector3(14, 12, 35)
            ]
        }.ToFrozenDictionary();

        bool result = AreaDB.TryResolveCuratedServiceWorldPosition(
            npc,
            npcWorldLocations,
            new Vector3(12, 10, 35),
            TestWorldMapArea,
            out Vector3 worldPosition,
            out Vector3 mapPosition);

        result.Should().BeTrue();
        worldPosition.Should().Be(new Vector3(14, 12, 35));
        mapPosition.Should().Be(WorldMapAreaDB.ToMap_FlipXY(new Vector3(14, 12, 35), TestWorldMapArea));
    }

    [Fact]
    public void TryResolveCuratedServiceWorldPosition_ShouldFallbackToMapCoordinatesWhenSpawnMissing()
    {
        NPC npc = CreateNpc(
            id: 16268,
            name: "Eralan",
            description: "Trade Supplies",
            reactHorde: 1,
            reactAlliance: 0,
            new Vector3(30, 60, 42));

        bool result = AreaDB.TryResolveCuratedServiceWorldPosition(
            npc,
            FrozenDictionary<int, Vector3[]>.Empty,
            new Vector3(10, 10, 35),
            TestWorldMapArea,
            out Vector3 worldPosition,
            out Vector3 mapPosition);

        result.Should().BeTrue();
        Vector3 expectedMapPosition = npc.MapCoords[0];
        mapPosition.Should().Be(expectedMapPosition);
        worldPosition.Should().Be(WorldMapAreaDB.ToWorld_FlipXY(expectedMapPosition, TestWorldMapArea));
    }

    private static Area CreateArea(
        List<NPC>? vendor = null,
        List<NPC>? repair = null,
        List<NPC>? innkeeper = null,
        List<NPC>? trainer = null,
        List<NPC>? flightmaster = null)
    {
        return new Area
        {
            vendor = vendor ?? [],
            repair = repair ?? [],
            innkeeper = innkeeper ?? [],
            trainer = trainer ?? [],
            flightmaster = flightmaster ?? [],
            herb = new Dictionary<string, List<Node>>(),
            vein = new Dictionary<string, List<Node>>(),
            skinnable = [],
            gatherable = [],
            minable = [],
            salvegable = []
        };
    }

    private static NPC CreateNpc(
        int id,
        string name,
        string description,
        int reactHorde,
        int reactAlliance,
        params Vector3[] mapCoords)
    {
        List<List<float>> coords = mapCoords
            .Select(static mapCoord => new List<float> { mapCoord.X, mapCoord.Y, mapCoord.Z })
            .ToList();

        return new NPC
        {
            id = id,
            name = name,
            description = description,
            reacthorde = reactHorde,
            reactalliance = reactAlliance,
            coords = coords,
            level = 1,
            type = 1
        };
    }
}
