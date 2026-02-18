using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

using Core;
using Core.FeatureFlags;
using Core.Startup;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Frontend.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly StartupState startupState;
    private readonly StartupOptions options;
    private readonly FeatureFlagService featureFlagService;
    private readonly IBotController botController;

    public HealthController(
        StartupState startupState,
        IOptions<StartupOptions> options,
        FeatureFlagService featureFlagService,
        IBotController botController)
    {
        this.startupState = startupState;
        this.options = options.Value;
        this.featureFlagService = featureFlagService;
        this.botController = botController;
    }

    [HttpGet]
    public IActionResult Get()
    {
        Assembly assembly = typeof(HealthController).Assembly;
        Version? version = assembly.GetName().Version;

        Process current = Process.GetCurrentProcess();
        TimeSpan uptime = DateTime.UtcNow - current.StartTime.ToUniversalTime();

        StartupStateSnapshot snapshot = startupState.GetSnapshot();

        string status = snapshot.CurrentStage switch
        {
            StartupStage.Failed => "FAILED",
            StartupStage.Ready => botController.IsBotActive ? "RUNNING" : "IDLE",
            _ => "STARTING"
        };

        return Ok(new
        {
            Status = status,
            TimestampUtc = DateTime.UtcNow,
            App = new
            {
                Name = assembly.GetName().Name,
                Version = version?.ToString() ?? "unknown",
                ProcessId = current.Id,
                Uptime = uptime.ToString("c"),
                OS = RuntimeInformation.OSDescription,
                Arch = RuntimeInformation.OSArchitecture.ToString(),
                ThreadCount = current.Threads.Count
            },
            Bot = new
            {
                Active = botController.IsBotActive,
                Profile = botController.SelectedClassFilename,
                AvgScreenLatencyMs = Math.Round(botController.AvgScreenLatency, 1),
                AvgNPCLatencyMs = Math.Round(botController.AvgNPCLatency, 1)
            },
            Startup = snapshot,
            Features = featureFlagService.GetAllFeatureStates(),
            Options = new
            {
                options.AutoLaunchWoW,
                options.AutoStartNavigationServer,
                options.AutoConfigureFrames,
                options.EnableHealthMonitoring,
                options.WebUIPort,
                options.NavigationServerPort
            }
        });
    }

    [HttpGet("startup")]
    public IActionResult GetStartup()
    {
        return Ok(startupState.GetSnapshot());
    }
}
