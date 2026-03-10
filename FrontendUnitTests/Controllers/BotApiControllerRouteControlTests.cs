using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Core;

using Frontend.Controllers;
using Frontend.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class BotApiControllerRouteControlTests
{
    [Fact]
    public void GetRouteState_WhenProfileLoaded_ReturnsConfiguredRouteSlots()
    {
        FakeBotController botController = CreateBotController(active: false);
        BotApiController controller = CreateController(botController);

        ActionResult<BotRouteControlState> response = controller.GetRouteState();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(response.Result);
        BotRouteControlState state = Assert.IsType<BotRouteControlState>(ok.Value);
        Assert.True(state.RouteControlAvailable);
        Assert.Equal(2, state.RouteSlots.Count);
        Assert.Equal("15-20_Ghostlands_Windrunner.json", state.RouteSlots[0].DefaultPathFileName);
        Assert.Equal("custom-ghostlands.json", state.RouteSlots[1].EffectivePathFileName);
    }

    [Fact]
    public void ApplyRoute_WhenBotIsActiveAndStopBotFirstTrue_SwitchesRouteAndStopsBot()
    {
        FakeBotController botController = CreateBotController(active: true);
        BotApiController controller = CreateController(botController);

        IActionResult result = controller.ApplyRoute(new BotRouteCommandRequest(
            TargetIndex: 0,
            FileName: "alternate-ghostlands.json",
            ClearOverride: false,
            StopBotFirst: true,
            ResumeBotAfterSwitch: false));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        JsonElement json = ToJson(ok.Value);
        Assert.True(GetPropertyIgnoreCase(json, "success").GetBoolean());
        Assert.True(GetPropertyIgnoreCase(json, "botWasStopped").GetBoolean());

        JsonElement state = GetPropertyIgnoreCase(json, "state");
        JsonElement routeSlots = GetPropertyIgnoreCase(state, "routeSlots");
        JsonElement firstSlot = routeSlots.EnumerateArray().First();
        Assert.Equal("alternate-ghostlands.json", GetPropertyIgnoreCase(firstSlot, "effectivePathFileName").GetString());
        Assert.False(botController.IsBotActive);
    }

    [Fact]
    public void ApplyRoute_WhenBotIsActiveAndStopBotFirstFalse_ReturnsConflict()
    {
        FakeBotController botController = CreateBotController(active: true);
        BotApiController controller = CreateController(botController);

        IActionResult result = controller.ApplyRoute(new BotRouteCommandRequest(
            TargetIndex: 0,
            FileName: "alternate-ghostlands.json",
            ClearOverride: false,
            StopBotFirst: false,
            ResumeBotAfterSwitch: false));

        ConflictObjectResult conflict = Assert.IsType<ConflictObjectResult>(result);
        JsonElement json = ToJson(conflict.Value);
        Assert.False(GetPropertyIgnoreCase(json, "success").GetBoolean());
    }

    private static FakeBotController CreateBotController(bool active)
    {
        ClassConfiguration classConfiguration = new()
        {
            FileName = "BloodElf_Warlock_1-70_TBC.json",
            Paths =
            [
                new PathSettings
                {
                    Id = 10,
                    PathFilename = "15-20_Ghostlands_Windrunner.json",
                    PathThereAndBack = true
                },
                new PathSettings
                {
                    Id = 20,
                    PathFilename = "18-22_Ghostlands_Deatholme_Approach.json",
                    OverridePathFilename = "custom-ghostlands.json",
                    PathThereAndBack = false
                }
            ]
        };

        return new FakeBotController
        {
            IsBotActive = active,
            SelectedClassFilename = "BloodElf_Warlock_1-70_TBC.json",
            ClassConfig = classConfiguration,
            SelectedPathFilename = new Dictionary<int, string>
            {
                [1] = "custom-ghostlands.json"
            },
            PathFileList =
            [
                "15-20_Ghostlands_Windrunner.json",
                "18-22_Ghostlands_Deatholme_Approach.json",
                "custom-ghostlands.json",
                "alternate-ghostlands.json"
            ]
        };
    }

    private static BotApiController CreateController(FakeBotController botController)
    {
        FakeBotStartGuard guard = new();
        BotRouteControlService routeControl = new(
            NullLogger<BotRouteControlService>.Instance,
            botController,
            guard);
        BotApiController controller = new(
            NullLogger<BotApiController>.Instance,
            botController,
            guard,
            new ProfileLoadTelemetryService(),
            routeControl);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static JsonElement ToJson(object? value)
    {
        return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));
    }

    private static JsonElement GetPropertyIgnoreCase(JsonElement element, string propertyName)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, System.StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new System.InvalidOperationException($"Property '{propertyName}' was not found.");
    }
}
