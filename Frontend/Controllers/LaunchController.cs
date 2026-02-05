using System;

using Microsoft.AspNetCore.Mvc;

using Core;
using Core.Launch;

namespace Frontend.Controllers;

[ApiController]
[Route("api/launch")]
public sealed class LaunchController : ControllerBase
{
    private readonly LaunchReadinessService readiness;
    private readonly LaunchOverrideState overrides;
    private readonly IBotController botController;

    public LaunchController(
        LaunchReadinessService readiness,
        LaunchOverrideState overrides,
        IBotController botController)
    {
        this.readiness = readiness;
        this.overrides = overrides;
        this.botController = botController;
    }

    [HttpGet("status")]
    public ActionResult<LaunchReadinessSnapshot> GetStatus()
    {
        ClassConfiguration? classConfig = botController.ClassConfig;
        RouteInfo? routeInfo = botController is BotController full ? full.RouteInfo : null;

        LaunchReadinessSnapshot snapshot = readiness.Evaluate(classConfig, routeInfo);
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
