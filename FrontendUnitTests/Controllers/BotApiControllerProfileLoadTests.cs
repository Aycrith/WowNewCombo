using System;
using System.Text.Json;

using Core;

using Frontend.Controllers;
using Frontend.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class BotApiControllerProfileLoadTests
{
    [Fact]
    public void LoadProfile_WhenSuccessful_ReturnsRequestedAndAppliedProfile()
    {
        ProfileLoadTelemetryService telemetry = new();
        FakeBotController botController = new();
        BotApiController controller = CreateController(botController, telemetry);

        IActionResult result = controller.LoadProfile(new ProfileLoadRequest("BloodElf_Warlock_1-70_TBC.json"));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        JsonElement json = ToJson(ok.Value);
        Assert.True(GetPropertyIgnoreCase(json, "isLoaded").GetBoolean());
        Assert.Equal("BloodElf_Warlock_1-70_TBC.json", GetPropertyIgnoreCase(json, "requestedProfile").GetString());
        Assert.Equal("BloodElf_Warlock_1-70_TBC.json", GetPropertyIgnoreCase(json, "appliedProfile").GetString());

        ProfileLoadTelemetrySnapshot snapshot = telemetry.GetSnapshot();
        Assert.Equal("Succeeded", snapshot.Status);
        Assert.Equal("BloodElf_Warlock_1-70_TBC.json", snapshot.RequestedProfile);
        Assert.Equal("BloodElf_Warlock_1-70_TBC.json", snapshot.AppliedProfile);
    }

    [Fact]
    public void LoadProfile_WhenWrongProfileLoaded_ReturnsServerErrorAndTracksFailure()
    {
        ProfileLoadTelemetryService telemetry = new();
        FakeBotController botController = new()
        {
            LoadClassProfileAppliedSelection = "Undead_Rogue_1-70.json",
            LoadClassProfileAppliedConfig = new ClassConfiguration
            {
                FileName = "Undead_Rogue_1-70.json"
            }
        };

        BotApiController controller = CreateController(botController, telemetry);

        IActionResult result = controller.LoadProfile(new ProfileLoadRequest("BloodElf_Warlock_1-70_TBC.json"));

        ObjectResult failure = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, failure.StatusCode);
        JsonElement json = ToJson(failure.Value);
        Assert.Equal("WrongProfileLoaded", GetPropertyIgnoreCase(json, "failureKind").GetString());
        Assert.Equal("Undead_Rogue_1-70.json", GetPropertyIgnoreCase(json, "appliedProfile").GetString());

        ProfileLoadTelemetrySnapshot snapshot = telemetry.GetSnapshot();
        Assert.Equal("Failed", snapshot.Status);
        Assert.Equal("WrongProfileLoaded", snapshot.FailureKind);
    }

    [Fact]
    public void LoadProfile_WhenDisposedTimerFaultOccurs_ClassifiesStartupFailure()
    {
        ProfileLoadTelemetryService telemetry = new();
        FakeBotController botController = new()
        {
            LoadClassProfileException = new ObjectDisposedException("System.Timers.Timer")
        };

        BotApiController controller = CreateController(botController, telemetry);

        IActionResult result = controller.LoadProfile(new ProfileLoadRequest("BloodElf_Warlock_1-70_TBC.json"));

        ObjectResult failure = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, failure.StatusCode);
        JsonElement json = ToJson(failure.Value);
        Assert.Equal("ProfileLoadDisposedTimer", GetPropertyIgnoreCase(json, "failureKind").GetString());

        ProfileLoadTelemetrySnapshot snapshot = telemetry.GetSnapshot();
        Assert.Equal("Failed", snapshot.Status);
        Assert.Equal("ProfileLoadDisposedTimer", snapshot.FailureKind);
    }

    private static BotApiController CreateController(FakeBotController botController, ProfileLoadTelemetryService telemetry)
    {
        BotApiController controller = new(
            NullLogger<BotApiController>.Instance,
            botController,
            new FakeBotStartGuard(),
            telemetry);

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
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new InvalidOperationException($"Property '{propertyName}' was not found.");
    }
}
