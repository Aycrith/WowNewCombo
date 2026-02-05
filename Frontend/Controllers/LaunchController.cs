using System;
using System.Diagnostics;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Core;
using Core.Launch;

namespace Frontend.Controllers;

[ApiController]
[Route("api/launch")]
public sealed class LaunchController : ControllerBase
{
    private readonly ILogger<LaunchController> logger;
    private readonly LaunchReadinessService readiness;
    private readonly LaunchOverrideState overrides;
    private readonly IBotController botController;

    public LaunchController(
        ILogger<LaunchController> logger,
        LaunchReadinessService readiness,
        LaunchOverrideState overrides,
        IBotController botController)
    {
        this.logger = logger;
        this.readiness = readiness;
        this.overrides = overrides;
        this.botController = botController;
    }

    [HttpGet("status")]
    public ActionResult<LaunchReadinessSnapshot> GetStatus()
    {
        Stopwatch sw = Stopwatch.StartNew();
        string trace = HttpContext?.TraceIdentifier ?? string.Empty;
        logger.LogDebug("[LaunchController] /api/launch/status start (trace={Trace})", trace);

        ClassConfiguration? classConfig = botController.ClassConfig;
        RouteInfo? routeInfo = botController is BotController full ? full.RouteInfo : null;

        LaunchReadinessSnapshot snapshot = readiness.Evaluate(classConfig, routeInfo);

        sw.Stop();
        logger.LogDebug("[LaunchController] /api/launch/status end (trace={Trace}) in {Elapsed}ms", trace, sw.ElapsedMilliseconds);
        return Ok(snapshot);
    }

    public sealed record LaunchOverrideRequest(
        bool AllowStartWithWarnings,
        bool SkipNavigationChecks,
        bool SkipKeybindingChecks,
        bool SkipActionBarChecks);

    [HttpPost("overrides")]
    public IActionResult SetOverrides([FromBody] LaunchOverrideRequest request)
    {
        overrides.SetAllowStartWithWarnings(request.AllowStartWithWarnings);
        overrides.SetSkipNavigationChecks(request.SkipNavigationChecks);
        overrides.SetSkipKeybindingChecks(request.SkipKeybindingChecks);
        overrides.SetSkipActionBarChecks(request.SkipActionBarChecks);

        return Ok(new { Success = true });
    }

    [HttpPost("overrides/reset")]
    public IActionResult ResetOverrides()
    {
        overrides.Reset();
        return Ok(new { Success = true });
    }
}
