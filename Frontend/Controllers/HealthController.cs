using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

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

    public HealthController(StartupState startupState, IOptions<StartupOptions> options)
    {
        this.startupState = startupState;
        this.options = options.Value;
    }

    [HttpGet]
    public IActionResult Get()
    {
        Assembly assembly = typeof(HealthController).Assembly;
        Version? version = assembly.GetName().Version;

        Process current = Process.GetCurrentProcess();
        TimeSpan uptime = DateTime.UtcNow - current.StartTime.ToUniversalTime();

        StartupStateSnapshot snapshot = startupState.GetSnapshot();

        return Ok(new
        {
            Status = "OK",
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
            Startup = snapshot,
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
