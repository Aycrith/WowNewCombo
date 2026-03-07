using Core;
using Core.GOAP;
using Core.Goals;

using Frontend.Controllers;

using Microsoft.AspNetCore.Mvc;

using System;
using System.Collections.Specialized;
using System.Text.Json;
using System.Threading;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class SessionControllerTests
{
    [Fact]
    public void GetStats_WhenLiveAgentAvailable_ReturnsLivePayloadAndRefreshesCache()
    {
        SessionStat stats = TestSessionStatFactory.Create(kills: 12, deaths: 1, uptime: TimeSpan.FromHours(2));
        GoapGoal currentGoal = new TestGoal(nameof(TestGoal));
        GoapAgent agent = TestGoapAgentFactory.Create(
            sessionStat: stats,
            active: true,
            currentGoal: currentGoal,
            availableGoals: [currentGoal]);

        FakeBotController botController = new()
        {
            IsBotActive = true,
            GoapAgent = agent
        };

        SessionStatsCache cache = new();
        SessionController controller = new(botController, cache);

        IActionResult result = controller.GetStats();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        SessionStatsResponse response = Assert.IsType<SessionStatsResponse>(ok.Value);
        Assert.Equal(12, response.Kills);
        Assert.Equal("live", response.StatsSource);
        Assert.Equal("live", response.RuntimeMode);
        Assert.False(response.IsStale);
        Assert.True(response.BotActive);
        Assert.Equal(nameof(TestGoal), response.CurrentGoal);
        Assert.NotNull(cache.GetSnapshot());
    }

    [Fact]
    public void GetStats_WhenOnlyCacheAvailable_ReturnsCachedPayload()
    {
        SessionStatsCache cache = new();
        cache.Capture(
            TestSessionStatFactory.Create(kills: 5, deaths: 0, uptime: TimeSpan.FromMinutes(90)),
            BotRuntimeModeHelper.Live,
            "CombatGoal",
            botActive: false);

        SessionController controller = new(new FakeBotController(), cache);

        IActionResult result = controller.GetStats();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        SessionStatsResponse response = Assert.IsType<SessionStatsResponse>(ok.Value);
        Assert.Equal(5, response.Kills);
        Assert.Equal("cached", response.StatsSource);
        Assert.True(response.IsStale);
        Assert.Equal("CombatGoal", response.CurrentGoal);
    }

    [Fact]
    public void GetStats_WhenInactiveAgentExists_ReturnsCachedPayload()
    {
        SessionStat stats = TestSessionStatFactory.Create(kills: 9, deaths: 2, uptime: TimeSpan.FromMinutes(45));
        GoapGoal currentGoal = new TestGoal(nameof(TestGoal));

        FakeBotController botController = new()
        {
            IsBotActive = false,
            GoapAgent = TestGoapAgentFactory.Create(
                sessionStat: stats,
                active: false,
                currentGoal: currentGoal,
                availableGoals: [currentGoal])
        };

        SessionController controller = new(botController, new SessionStatsCache());

        IActionResult result = controller.GetStats();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        SessionStatsResponse response = Assert.IsType<SessionStatsResponse>(ok.Value);
        Assert.Equal(9, response.Kills);
        Assert.Equal(2, response.Deaths);
        Assert.Equal("cached", response.StatsSource);
        Assert.True(response.IsStale);
        Assert.False(response.BotActive);
        Assert.Equal(nameof(TestGoal), response.CurrentGoal);
        Assert.Equal("live", response.RuntimeMode);
    }

    [Fact]
    public void Get_WhenOnlyCacheAvailable_ReturnsPartialSummary()
    {
        SessionStatsCache cache = new();
        cache.Capture(
            TestSessionStatFactory.Create(kills: 3, deaths: 1, uptime: TimeSpan.FromMinutes(30)),
            BotRuntimeModeHelper.Live,
            "PullTargetGoal",
            botActive: false);

        SessionController controller = new(new FakeBotController(), cache);

        IActionResult result = controller.Get();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        SessionSummaryResponse response = Assert.IsType<SessionSummaryResponse>(ok.Value);
        Assert.False(response.WorldStateAvailable);
        Assert.Null(response.WorldState);
        Assert.Empty(response.AvailableGoals);
        Assert.Equal("cached", response.StatsSource);
        Assert.True(response.IsStale);
        Assert.Equal("PullTargetGoal", response.CurrentGoal);
    }

    [Fact]
    public void Get_WhenInactiveAgentExists_ReturnsCachedPartialSummary()
    {
        SessionStat stats = TestSessionStatFactory.Create(kills: 4, deaths: 1, uptime: TimeSpan.FromMinutes(25));
        GoapGoal currentGoal = new TestGoal(nameof(TestGoal));
        BitVector32 worldState = new();
        worldState[1 << (int)GoapKey.hastarget] = true;

        FakeBotController botController = new()
        {
            IsBotActive = false,
            GoapAgent = TestGoapAgentFactory.Create(
                sessionStat: stats,
                active: false,
                currentGoal: currentGoal,
                availableGoals: [currentGoal],
                worldState: worldState)
        };

        SessionController controller = new(botController, new SessionStatsCache());

        IActionResult result = controller.Get();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        SessionSummaryResponse response = Assert.IsType<SessionSummaryResponse>(ok.Value);
        Assert.False(response.Active);
        Assert.False(response.WorldStateAvailable);
        Assert.Null(response.WorldState);
        Assert.Empty(response.AvailableGoals);
        Assert.Equal("cached", response.StatsSource);
        Assert.True(response.IsStale);
        Assert.Equal(nameof(TestGoal), response.CurrentGoal);
    }

    [Fact]
    public void GetWorldState_WhenInactiveAgentExists_Returns503()
    {
        SessionStat stats = TestSessionStatFactory.Create(kills: 1, deaths: 0, uptime: TimeSpan.FromMinutes(5));
        FakeBotController botController = new()
        {
            IsBotActive = false,
            GoapAgent = TestGoapAgentFactory.Create(
                sessionStat: stats,
                active: false,
                currentGoal: new TestGoal(nameof(TestGoal)))
        };

        SessionController controller = new(botController, new SessionStatsCache());

        IActionResult result = controller.GetWorldState();

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);

        JsonElement json = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(obj.Value));
        Assert.Equal("live", GetPropertyIgnoreCase(json, "runtimeMode").GetString());
        Assert.Equal("unavailable", GetPropertyIgnoreCase(json, "statsSource").GetString());
    }

    [Fact]
    public void GetStats_WhenConfigModeAndNoCache_Returns503WithRuntimeMetadata()
    {
        using CancellationTokenSource cts = new();
        using ConfigBotController botController = TestConfigBotControllerFactory.Create(cts);
        SessionController controller = new(botController, new SessionStatsCache());

        IActionResult result = controller.GetStats();

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, obj.StatusCode);

        JsonElement json = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(obj.Value));
        Assert.Equal("configuration", GetPropertyIgnoreCase(json, "runtimeMode").GetString());
        Assert.Equal("unavailable", GetPropertyIgnoreCase(json, "statsSource").GetString());
    }

    private static JsonElement GetPropertyIgnoreCase(JsonElement element, string propertyName)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new InvalidOperationException($"Property '{propertyName}' was not found.");
    }
}
