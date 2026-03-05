using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

using Core;
using Core.GOAP;
using Core.Goals;

using Microsoft.AspNetCore.Mvc;

namespace Frontend.Controllers;

/// <summary>
/// API controller for querying current bot session state.
/// Provides read-only access to GOAP agent state, session stats, and world state.
/// </summary>
[ApiController]
[Route("api/session")]
public sealed class SessionController : ControllerBase
{
    private readonly IBotController botController;

    public SessionController(IBotController botController)
    {
        this.botController = botController;
    }

    /// <summary>
    /// GET /api/session — Returns current session stats, goal, and world state summary.
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        GoapAgent? goapAgent = botController.GoapAgent;
        if (goapAgent == null)
        {
            return ServiceUnavailable("GOAP agent not initialized");
        }

        SessionStat stats = goapAgent.SessionStat;
        GoapGoal? currentGoal = goapAgent.CurrentGoal;

        return Ok(new
        {
            Active = goapAgent.Active,
            CurrentGoal = currentGoal?.GetType().Name,
            SessionStats = new
            {
                stats.Kills,
                stats.Deaths,
                Uptime = new
                {
                    stats.Seconds,
                    stats.Minutes,
                    stats.Hours
                }
            },
            WorldState = GetWorldStateSummary(goapAgent.WorldState),
            AvailableGoals = goapAgent.AvailableGoals.Select(g => g.GetType().Name).ToArray()
        });
    }

    /// <summary>
    /// GET /api/session/worldstate — Returns the full world state as named flags.
    /// </summary>
    [HttpGet("worldstate")]
    public IActionResult GetWorldState()
    {
        GoapAgent? goapAgent = botController.GoapAgent;
        if (goapAgent == null)
        {
            return ServiceUnavailable("GOAP agent not initialized");
        }

        return Ok(GetWorldStateFlags(goapAgent.WorldState));
    }

    /// <summary>
    /// GET /api/session/stats — Returns just the session statistics.
    /// </summary>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        GoapAgent? goapAgent = botController.GoapAgent;
        if (goapAgent == null)
        {
            return ServiceUnavailable("GOAP agent not initialized");
        }

        SessionStat stats = goapAgent.SessionStat;

        return Ok(new
        {
            stats.Kills,
            stats.Deaths,
            Uptime = new
            {
                stats.Seconds,
                stats.Minutes,
                stats.Hours
            },
            KillsPerHour = stats.Hours > 0 ? stats.Kills / (double)stats.Hours : 0,
            DeathsPerHour = stats.Hours > 0 ? stats.Deaths / (double)stats.Hours : 0
        });
    }

    private static Dictionary<string, bool> GetWorldStateFlags(BitVector32 worldState)
    {
        Dictionary<string, bool> flags = new();

        foreach (GoapKey key in System.Enum.GetValues<GoapKey>())
        {
            flags[key.ToString()] = worldState[1 << (int)key];
        }

        return flags;
    }

    private static object GetWorldStateSummary(BitVector32 worldState)
    {
        // Return a subset of important flags for the summary view
        return new
        {
            HasTarget = worldState[1 << (int)GoapKey.hastarget],
            InCombat = worldState[1 << (int)GoapKey.incombat],
            IsDead = worldState[1 << (int)GoapKey.isdead],
            IsMounted = worldState[1 << (int)GoapKey.ismounted],
            ShouldLoot = worldState[1 << (int)GoapKey.shouldloot],
            WithinPullRange = worldState[1 << (int)GoapKey.withinpullrange],
            InCombatRange = worldState[1 << (int)GoapKey.incombatrange],
            TargetHostile = worldState[1 << (int)GoapKey.targethostile]
        };
    }

    private ObjectResult ServiceUnavailable(string message)
    {
        return StatusCode(503, new { Error = message });
    }
}
