using System;
using System.Collections.Generic;
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
    private readonly AddonValidator addonValidator;
    private readonly ILaunchReadinessCacheInvalidator cacheInvalidator;

    public LaunchController(
        ILogger<LaunchController> logger,
        LaunchReadinessService readiness,
        LaunchOverrideState overrides,
        IBotController botController,
        AddonValidator addonValidator,
        ILaunchReadinessCacheInvalidator cacheInvalidator)
    {
        this.logger = logger;
        this.readiness = readiness;
        this.overrides = overrides;
        this.botController = botController;
        this.addonValidator = addonValidator;
        this.cacheInvalidator = cacheInvalidator;
    }

    [HttpGet("status")]
    public ActionResult<LaunchReadinessSnapshot> GetStatus()
    {
        Stopwatch sw = Stopwatch.StartNew();
        string trace = HttpContext?.TraceIdentifier ?? string.Empty;
        if (HttpContext != null)
        {
            Response.Headers["X-Correlation-ID"] = trace;
        }
        logger.LogDebug("[LaunchController] /api/launch/status start (trace={Trace})", trace);

        ClassConfiguration? classConfig = botController.ClassConfig;
        RouteInfo? routeInfo = botController is BotController full ? full.RouteInfo : null;

        LaunchReadinessSnapshot snapshot;
        try
        {
            snapshot = readiness.Evaluate(classConfig, routeInfo);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[LaunchController] /api/launch/status failed");
            snapshot = new LaunchReadinessSnapshot(
                IsLaunchReady: false,
                CanStartBot: false,
                TimestampUtc: DateTimeOffset.UtcNow,
                Checks: new[]
                {
                    new LaunchSubsystemCheck(
                        LaunchSubsystem.Addons,
                        LaunchStatus.Error,
                        "Launch Status",
                        $"Readiness evaluation failed: {ex.Message}",
                        IsRequired: true,
                        IsBlocking: true,
                        TimestampUtc: DateTimeOffset.UtcNow,
                        FixHint: "Open logs and consider Restart Server",
                        NavigateTo: "/Log")
                },
                Overrides: overrides.Snapshot());
        }

        sw.Stop();
        logger.LogDebug("[LaunchController] /api/launch/status end (trace={Trace}) in {Elapsed}ms", trace, sw.ElapsedMilliseconds);
        return Ok(snapshot);
    }

    public sealed record LaunchOverrideRequest(
        bool AllowStartWithWarnings,
        bool EmergencyBypassAll,
        Dictionary<string, bool>? Bypass,
        string? Reason,
        string? Source);

    [HttpPost("overrides")]
    public IActionResult SetOverrides([FromBody] LaunchOverrideRequest request)
    {
        string reason = string.IsNullOrWhiteSpace(request.Reason) ? "API override" : request.Reason;
        string source = string.IsNullOrWhiteSpace(request.Source) ? "API" : request.Source;

        overrides.SetAllowStartWithWarnings(request.AllowStartWithWarnings, reason, source);
        overrides.SetEmergencyBypassAll(request.EmergencyBypassAll, reason, source);

        if (request.Bypass != null)
        {
            foreach (var kvp in request.Bypass)
            {
                if (TryParseLaunchSubsystemKey(kvp.Key, out LaunchSubsystem subsystem))
                {
                    overrides.SetBypass(subsystem, kvp.Value, reason, source);
                }
                else
                {
                    logger.LogWarning("[LaunchController] Ignoring unknown launch bypass key '{Key}'", kvp.Key);
                }
            }
        }

        return Ok(new { Success = true });
    }

    private static bool TryParseLaunchSubsystemKey(string key, out LaunchSubsystem subsystem)
    {
        if (Enum.TryParse(key, ignoreCase: true, out subsystem))
        {
            return true;
        }

        if (int.TryParse(key, out int numeric) &&
            Enum.IsDefined(typeof(LaunchSubsystem), numeric))
        {
            subsystem = (LaunchSubsystem)numeric;
            return true;
        }

        subsystem = default;
        return false;
    }

    [HttpPost("overrides/reset")]
    public IActionResult ResetOverrides()
    {
        overrides.Reset();
        return Ok(new { Success = true });
    }

    [HttpGet("overrides/audit")]
    public IActionResult GetOverrideAudit()
    {
        return Ok(new
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Entries = overrides.GetAudit()
        });
    }

    [HttpPost("refresh/addons")]
    public IActionResult RefreshAddons()
    {
        cacheInvalidator.InvalidateAddonValidation();
        AddonValidationResult result = addonValidator.Validate();
        cacheInvalidator.PrimeAddonValidation(result, "ApiRefresh");

        return Ok(new
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            IsValid = result.IsValid,
            Summary = result.GetSummary()
        });
    }
}
