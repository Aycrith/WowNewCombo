using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

using Core;

using HeadlessServer;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class HeadlessRouteControlHostTests
{
    [Fact]
    public async Task GetRouteState_ReturnsConfiguredSlots()
    {
        FakeBotController botController = CreateBotController(active: false);
        await using HeadlessRouteControlHost host = CreateHost(botController);
        bool started = await host.StartAsync();

        Assert.True(started);
        Assert.NotNull(host.BaseAddress);

        using HttpClient client = new()
        {
            BaseAddress = host.BaseAddress
        };

        BotRouteControlState? state = await client.GetFromJsonAsync<BotRouteControlState>("api/bot/route/state");

        Assert.NotNull(state);
        Assert.True(state!.RouteControlAvailable);
        Assert.Equal(2, state.RouteSlots.Count);
        Assert.Equal("15-20_Ghostlands_Windrunner.json", state.RouteSlots[0].DefaultPathFileName);
    }

    [Fact]
    public async Task ApplyRoute_WhenResumeRequested_StopsSwitchesAndResumes()
    {
        FakeBotController botController = CreateBotController(active: true);
        await using HeadlessRouteControlHost host = CreateHost(botController);
        bool started = await host.StartAsync();

        Assert.True(started);
        Assert.NotNull(host.BaseAddress);

        using HttpClient client = new()
        {
            BaseAddress = host.BaseAddress
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("api/bot/route/apply", new BotRouteCommandRequest(
            TargetIndex: 0,
            FileName: "alternate-ghostlands.json",
            ClearOverride: false,
            StopBotFirst: true,
            ResumeBotAfterSwitch: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement payload = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.True(GetPropertyIgnoreCase(payload, "success").GetBoolean());
        Assert.True(GetPropertyIgnoreCase(payload, "botWasStopped").GetBoolean());
        Assert.True(GetPropertyIgnoreCase(payload, "resumeSucceeded").GetBoolean());

        JsonElement state = GetPropertyIgnoreCase(payload, "state");
        JsonElement routeSlots = GetPropertyIgnoreCase(state, "routeSlots");
        JsonElement firstSlot = routeSlots.EnumerateArray().First();
        Assert.Equal("alternate-ghostlands.json", GetPropertyIgnoreCase(firstSlot, "effectivePathFileName").GetString());
        Assert.True(GetPropertyIgnoreCase(state, "botActive").GetBoolean());
        Assert.True(botController.IsBotActive);
    }

    private static HeadlessRouteControlHost CreateHost(FakeBotController botController)
    {
        BotRouteControlService routeControl = new(
            NullLogger<BotRouteControlService>.Instance,
            botController,
            new FakeBotStartGuard());

        return new HeadlessRouteControlHost(
            NullLogger<HeadlessRouteControlHost>.Instance,
            routeControl,
            new HeadlessRouteControlOptions
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = 0
            });
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
