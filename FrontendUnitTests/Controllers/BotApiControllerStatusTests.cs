using Core;
using Core.Goals;

using Frontend.Controllers;
using Frontend.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using System;
using System.Threading;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class BotApiControllerStatusTests
{
    [Fact]
    public void GetStatus_WhenConfigMode_ReturnsConfigurationRuntimeMode()
    {
        using CancellationTokenSource cts = new();
        using ConfigBotController botController = TestConfigBotControllerFactory.Create(cts);
        BotApiController controller = new(
            NullLogger<BotApiController>.Instance,
            botController,
            new FakeBotStartGuard(),
            new ProfileLoadTelemetryService(),
            new BotRouteControlService(
                NullLogger<BotRouteControlService>.Instance,
                botController,
                new FakeBotStartGuard()));

        IActionResult result = controller.GetStatus();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        BotStatus status = Assert.IsType<BotStatus>(ok.Value);
        Assert.Equal("configuration", status.RuntimeMode);
        Assert.False(status.AgentAvailable);
        Assert.False(status.IsActive);
    }

    [Fact]
    public void GetStatus_WhenLiveAgentExists_ReturnsLiveRuntimeModeAndAgentAvailability()
    {
        SessionStat stats = TestSessionStatFactory.Create(kills: 2, deaths: 0, uptime: TimeSpan.FromMinutes(20));
        GoapGoal goal = new TestGoal(nameof(TestGoal));

        FakeBotController botController = new()
        {
            IsBotActive = true,
            GoapAgent = TestGoapAgentFactory.Create(
                sessionStat: stats,
                active: true,
                currentGoal: goal,
                availableGoals: [goal]),
            SelectedClassFilename = "BloodElf_Warlock_1-70_TBC.json",
            AvgScreenLatency = 3.5,
            AvgNPCLatency = 1.2
        };

        BotApiController controller = new(
            NullLogger<BotApiController>.Instance,
            botController,
            new FakeBotStartGuard(),
            new ProfileLoadTelemetryService(),
            new BotRouteControlService(
                NullLogger<BotRouteControlService>.Instance,
                botController,
                new FakeBotStartGuard()));

        IActionResult result = controller.GetStatus();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        BotStatus status = Assert.IsType<BotStatus>(ok.Value);
        Assert.Equal("live", status.RuntimeMode);
        Assert.True(status.AgentAvailable);
        Assert.True(status.IsActive);
        Assert.Equal("BloodElf_Warlock_1-70_TBC.json", status.ProfileName);
    }

    [Fact]
    public void GetStatus_WhenInactiveAgentExists_ReturnsLiveRuntimeModeButNoAgentAvailability()
    {
        SessionStat stats = TestSessionStatFactory.Create(kills: 6, deaths: 1, uptime: TimeSpan.FromMinutes(35));
        GoapGoal goal = new TestGoal(nameof(TestGoal));

        FakeBotController botController = new()
        {
            IsBotActive = false,
            GoapAgent = TestGoapAgentFactory.Create(
                sessionStat: stats,
                active: false,
                currentGoal: goal,
                availableGoals: [goal]),
            SelectedClassFilename = "BloodElf_Warlock_1-70_TBC.json"
        };

        BotApiController controller = new(
            NullLogger<BotApiController>.Instance,
            botController,
            new FakeBotStartGuard(),
            new ProfileLoadTelemetryService(),
            new BotRouteControlService(
                NullLogger<BotRouteControlService>.Instance,
                botController,
                new FakeBotStartGuard()));

        IActionResult result = controller.GetStatus();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        BotStatus status = Assert.IsType<BotStatus>(ok.Value);
        Assert.Equal("live", status.RuntimeMode);
        Assert.False(status.AgentAvailable);
        Assert.False(status.IsActive);
        Assert.Equal("BloodElf_Warlock_1-70_TBC.json", status.ProfileName);
    }
}
