using System;
using System.Linq;

using Core.Autonomy;

using Frontend.Services;

using Microsoft.AspNetCore.Mvc;

namespace Frontend.Controllers;

public sealed record AutonomyControlRequest(
    string Command,
    string SupervisorId = "default",
    string? Reason = null,
    string? Source = null);

[ApiController]
[Route("api/autonomy")]
public sealed class AutonomyController : ControllerBase
{
    private readonly AutonomyRuntimeService autonomyRuntime;

    public AutonomyController(AutonomyRuntimeService autonomyRuntime)
    {
        this.autonomyRuntime = autonomyRuntime;
    }

    [HttpGet("status")]
    public IActionResult GetStatus([FromQuery] string supervisorId = "default")
    {
        string correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
        if (HttpContext != null)
        {
            Response.Headers["X-Correlation-ID"] = correlationId;
        }

        AutonomyRunState state = autonomyRuntime.GetRunState(supervisorId);
        return Ok(new
        {
            CorrelationId = correlationId,
            State = state,
            LatestStatus = autonomyRuntime.GetLatestStatus(supervisorId),
            Incidents = autonomyRuntime.GetIncidents(supervisorId).Take(10).ToArray(),
            Runs = autonomyRuntime.GetRuns(supervisorId, 5).ToArray(),
            SupervisorRoot = autonomyRuntime.GetSupervisorRoot(supervisorId),
            ControlRoot = autonomyRuntime.GetControlRoot(supervisorId)
        });
    }

    [HttpGet("incidents")]
    public IActionResult GetIncidents([FromQuery] string supervisorId = "default")
    {
        string correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
        if (HttpContext != null)
        {
            Response.Headers["X-Correlation-ID"] = correlationId;
        }
        return Ok(new
        {
            CorrelationId = correlationId,
            Incidents = autonomyRuntime.GetIncidents(supervisorId)
        });
    }

    [HttpGet("runs")]
    public IActionResult GetRuns([FromQuery] string supervisorId = "default", [FromQuery] int limit = 10)
    {
        string correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
        if (HttpContext != null)
        {
            Response.Headers["X-Correlation-ID"] = correlationId;
        }
        return Ok(new
        {
            CorrelationId = correlationId,
            Runs = autonomyRuntime.GetRuns(supervisorId, limit)
        });
    }

    [HttpPost("control")]
    public IActionResult Control([FromBody] AutonomyControlRequest request)
    {
        string correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
        if (HttpContext != null)
        {
            Response.Headers["X-Correlation-ID"] = correlationId;
        }

        (bool pauseRequested, bool stopRequested, KillSwitchState killSwitchState) = autonomyRuntime.ApplyControl(
            request.SupervisorId,
            request.Command,
            request.Reason,
            request.Source);

        return Ok(new
        {
            CorrelationId = correlationId,
            request.Command,
            request.SupervisorId,
            PauseRequested = pauseRequested,
            StopRequested = stopRequested,
            KillSwitchState = killSwitchState
        });
    }

    [HttpGet("incidents/{incidentId}/artifacts")]
    public IActionResult GetIncidentArtifacts(string incidentId, [FromQuery] string supervisorId = "default")
    {
        string correlationId = HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
        if (HttpContext != null)
        {
            Response.Headers["X-Correlation-ID"] = correlationId;
        }
        return Ok(new
        {
            CorrelationId = correlationId,
            IncidentId = incidentId,
            Artifacts = autonomyRuntime.GetArtifacts(supervisorId, incidentId)
        });
    }
}
