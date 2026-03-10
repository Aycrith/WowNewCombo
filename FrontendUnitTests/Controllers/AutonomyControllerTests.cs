using System;
using System.IO;
using System.Text.Json;

using Frontend.Controllers;
using Frontend.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace FrontendUnitTests.Controllers;

public sealed class AutonomyControllerTests
{
    [Fact]
    public void Control_EnableLiveWindow_ReturnsPersistedLiveWindowState()
    {
        using TempDirectory temp = new();
        AutonomyRuntimeService runtime = CreateRuntime(temp.Path);
        AutonomyController controller = CreateController(runtime);

        IActionResult result = controller.Control(new AutonomyControlRequest(
            "enablelivewindow",
            SupervisorId: "operator-gate",
            Reason: "Operator confirmed client readiness.",
            Source: "test"));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        JsonElement json = ToJson(ok.Value);
        JsonElement liveWindowState = GetPropertyIgnoreCase(json, "liveWindowState");
        Assert.True(GetPropertyIgnoreCase(liveWindowState, "enabled").GetBoolean());
        Assert.Equal("Operator confirmed client readiness.", GetPropertyIgnoreCase(liveWindowState, "reason").GetString());

        OkObjectResult statusResult = Assert.IsType<OkObjectResult>(controller.GetStatus("operator-gate"));
        JsonElement status = ToJson(statusResult.Value);
        JsonElement state = GetPropertyIgnoreCase(status, "state");
        JsonElement persistedLiveWindowState = GetPropertyIgnoreCase(state, "liveWindowState");
        Assert.True(GetPropertyIgnoreCase(persistedLiveWindowState, "enabled").GetBoolean());
    }

    [Fact]
    public void Control_DisableLiveWindow_ReturnsDisabledState()
    {
        using TempDirectory temp = new();
        AutonomyRuntimeService runtime = CreateRuntime(temp.Path);
        AutonomyController controller = CreateController(runtime);

        controller.Control(new AutonomyControlRequest("enablelivewindow", SupervisorId: "operator-gate"));

        IActionResult result = controller.Control(new AutonomyControlRequest(
            "disablelivewindow",
            SupervisorId: "operator-gate",
            Reason: "Guarded window closed.",
            Source: "test"));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        JsonElement json = ToJson(ok.Value);
        JsonElement liveWindowState = GetPropertyIgnoreCase(json, "liveWindowState");
        Assert.False(GetPropertyIgnoreCase(liveWindowState, "enabled").GetBoolean());
        Assert.Equal("Guarded window closed.", GetPropertyIgnoreCase(liveWindowState, "reason").GetString());
    }

    [Fact]
    public void Control_ResetPrep_ClearsStopPauseAndDisarmsWindow()
    {
        using TempDirectory temp = new();
        AutonomyRuntimeService runtime = CreateRuntime(temp.Path);
        AutonomyController controller = CreateController(runtime);

        controller.Control(new AutonomyControlRequest("pause", SupervisorId: "prep-lane"));
        controller.Control(new AutonomyControlRequest("stop", SupervisorId: "prep-lane"));
        controller.Control(new AutonomyControlRequest("enablekillswitch", SupervisorId: "prep-lane"));
        controller.Control(new AutonomyControlRequest("enablelivewindow", SupervisorId: "prep-lane"));

        IActionResult result = controller.Control(new AutonomyControlRequest(
            "resetprep",
            SupervisorId: "prep-lane",
            Reason: "Prep lane reset for guarded startup.",
            Source: "test"));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        JsonElement json = ToJson(ok.Value);
        Assert.False(GetPropertyIgnoreCase(json, "pauseRequested").GetBoolean());
        Assert.False(GetPropertyIgnoreCase(json, "stopRequested").GetBoolean());

        JsonElement killSwitchState = GetPropertyIgnoreCase(json, "killSwitchState");
        Assert.False(GetPropertyIgnoreCase(killSwitchState, "enabled").GetBoolean());

        JsonElement liveWindowState = GetPropertyIgnoreCase(json, "liveWindowState");
        Assert.False(GetPropertyIgnoreCase(liveWindowState, "enabled").GetBoolean());
        Assert.Equal("Prep lane reset for guarded startup.", GetPropertyIgnoreCase(liveWindowState, "reason").GetString());
    }

    private static AutonomyRuntimeService CreateRuntime(string contentRootPath)
    {
        FakeHostEnvironment env = new()
        {
            ContentRootPath = contentRootPath
        };

        return new AutonomyRuntimeService(NullLogger<AutonomyRuntimeService>.Instance, env);
    }

    private static AutonomyController CreateController(AutonomyRuntimeService runtime)
    {
        AutonomyController controller = new(runtime);
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

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutonomyControllerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
