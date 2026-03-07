using Core;
using Core.Database;
using Core.Navigation;
using Core.Startup;

using Frontend.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SharedLib;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Numerics;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class DiagnosticsControllerRerouteTests
{
    [Fact]
    public void EnrichRerouteSnapshot_WhenNavigationProbeMissing_UsesRouteProviderFallback()
    {
        FakeAddonDataProvider addonData = new();
        addonData.Data[1] = 500000;
        addonData.Data[2] = 250000;

        AddonBits bits = new();
        PlayerReader playerReader = new(
            addonData,
            (WorldMapAreaDB)RuntimeHelpers.GetUninitializedObject(typeof(WorldMapAreaDB)),
            (AreaDB)RuntimeHelpers.GetUninitializedObject(typeof(AreaDB)),
            bits,
            new SpellInRange(),
            new Stance());
        playerReader.UIMapId.ForceUpdate(1942);
        SetWorldMapArea(playerReader, new WorldMapArea
        {
            AreaName = "Ghostlands",
            UIMapId = 1942,
            MapID = 1942,
            LocLeft = 0,
            LocRight = 100,
            LocTop = 0,
            LocBottom = 100
        });

        Core.Goals.Navigation navigation = CreateNavigationStub(playerReader);
        TestRouteProviderGoal routeGoal = new(
            "Follow 9-12_Ghostlands",
            [
                new Vector3(20, 25, 0),
                new Vector3(30, 25, 0),
                new Vector3(40, 25, 0)
            ],
            DateTime.UtcNow);
        Core.GOAP.GoapAgent goapAgent = TestGoapAgentFactory.Create(
            TestSessionStatFactory.Create(0, 0, TimeSpan.Zero),
            active: true,
            currentGoal: new TestGoal("Combat"),
            availableGoals: [routeGoal]);

        FakeBotController botController = new()
        {
            IsBotActive = true,
            GoapAgent = goapAgent
        };

        DiagnosticsController controller = new(
            NullLogger<DiagnosticsController>.Instance,
            keyBindingsReader: null!,
            slotValidator: null!,
            textureReader: null!,
            botController: botController,
            addonReader: null!,
            bagReader: null!,
            systemDiagnostics: null!,
            startupOptions: Options.Create(new StartupOptions()),
            navSoakMetricsService: null,
            featureFlagService: null,
            castingHandler: null,
            goapEventHistory: null,
            playerReader: playerReader);

        NavigationRerouteRuntimeSnapshot snapshot = new(
            RerouteTriggerCount: 0,
            RerouteApplyCount: 0,
            RerouteDropCount: 0,
            DetourOnlyCollapseCount: 0,
            LastRerouteDropReason: null,
            LastRerouteAnchorDistance: null,
            MapId: 1942,
            CurrentPosition: new Vector3(25, 50, 33),
            ProbeTarget: null,
            HasActiveReroute: false,
            ActiveRerouteId: null,
            ActiveRerouteStartedAt: null,
            ActiveRerouteOriginalTarget: null,
            ActiveRerouteWaypointCount: 0);

        MethodInfo? method = typeof(DiagnosticsController).GetMethod(
            "EnrichRerouteSnapshot",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        NavigationRerouteRuntimeSnapshot? response = (NavigationRerouteRuntimeSnapshot?)method!.Invoke(controller, [snapshot]);

        Assert.NotNull(response);
        Assert.Equal(1942, response!.MapId);
        Assert.NotNull(response.CurrentPosition);
        Assert.NotNull(response.ProbeTarget);
        Assert.True(response.ProbeTarget!.Value.Z > 0);
    }

    private static Core.Goals.Navigation CreateNavigationStub(PlayerReader playerReader)
    {
        Core.Goals.Navigation navigation = (Core.Goals.Navigation)RuntimeHelpers.GetUninitializedObject(typeof(Core.Goals.Navigation));
        SetField(navigation, "playerReader", playerReader);
        SetField(navigation, "routeToNextWaypoint", new Stack<Vector3>());
        SetField(navigation, "wayPoints", new Stack<Vector3>());
        return navigation;
    }

    private static void SetWorldMapArea(PlayerReader playerReader, WorldMapArea worldMapArea)
    {
        PropertyInfo? property = typeof(PlayerReader).GetProperty(nameof(PlayerReader.WorldMapArea));
        property!.SetValue(playerReader, worldMapArea);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        }

        field.SetValue(target, value);
    }

    private sealed class FakeAddonDataProvider : IAddonDataProvider
    {
        public int[] Data { get; } = new int[324];

        public void Dispose()
        {
        }

        public void InitFrames(DataFrame[] frames)
        {
        }

        public void UpdateData()
        {
        }
    }
}
