using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

using Core;
using Core.GOAP;
using Core.Goals;

using Microsoft.AspNetCore.Mvc;

namespace Frontend.Controllers;

#region Response DTOs

public sealed record SessionUptimeResponse(
    int Seconds,
    int Minutes,
    int Hours);

public sealed record SessionStatsResponse(
    int Kills,
    int Deaths,
    SessionUptimeResponse Uptime,
    double KillsPerHour,
    double DeathsPerHour,
    bool BotActive,
    string RuntimeMode,
    string StatsSource,
    string? CurrentGoal,
    System.DateTime? LastUpdatedUtc,
    bool IsStale);

public sealed record SessionSummaryResponse(
    bool Active,
    string? CurrentGoal,
    SessionStatsResponse SessionStats,
    object? WorldState,
    string[] AvailableGoals,
    bool WorldStateAvailable,
    string RuntimeMode,
    string StatsSource,
    System.DateTime? LastUpdatedUtc,
    bool IsStale);

#endregion

/// <summary>
/// API controller for querying current bot session state.
/// Provides read-only access to GOAP agent state, session stats, and world state.
/// </summary>
[ApiController]
[Route("api/session")]
public sealed class SessionController : ControllerBase
{
    private readonly IBotController botController;
    private readonly SessionStatsCache sessionStatsCache;

    public SessionController(IBotController botController, SessionStatsCache sessionStatsCache)
    {
        this.botController = botController;
        this.sessionStatsCache = sessionStatsCache;
    }

    /// <summary>
    /// GET /api/session — Returns current session stats, goal, and world state summary.
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        if (!TryResolveSessionStats(out SessionStatsResponse? statsResponse, out GoapAgent? goapAgent, out bool liveAgentAvailable))
        {
            return ServiceUnavailable("GOAP agent not initialized");
        }

        string[] availableGoals = liveAgentAvailable
            ? goapAgent!.AvailableGoals.Select(g => g.GetType().Name).ToArray()
            : [];
        object? worldState = liveAgentAvailable ? GetWorldStateSummary(goapAgent!.WorldState) : null;

        return Ok(new SessionSummaryResponse(
            Active: botController.IsBotActive,
            CurrentGoal: liveAgentAvailable ? goapAgent!.CurrentGoal?.GetType().Name : statsResponse!.CurrentGoal,
            SessionStats: statsResponse!,
            WorldState: worldState,
            AvailableGoals: availableGoals,
            WorldStateAvailable: liveAgentAvailable,
            RuntimeMode: BotRuntimeModeHelper.GetRuntimeMode(botController),
            StatsSource: statsResponse!.StatsSource,
            LastUpdatedUtc: statsResponse!.LastUpdatedUtc,
            IsStale: statsResponse!.IsStale));
    }

    /// <summary>
    /// GET /api/session/worldstate — Returns the full world state as named flags.
    /// </summary>
    [HttpGet("worldstate")]
    public IActionResult GetWorldState()
    {
        GoapAgent? goapAgent = botController.GoapAgent;
        if (goapAgent == null || !goapAgent.Active)
        {
            return ServiceUnavailable("GOAP agent not active");
        }

        return Ok(GetWorldStateFlags(goapAgent.WorldState));
    }

    /// <summary>
    /// GET /api/session/stats — Returns just the session statistics.
    /// </summary>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        if (!TryResolveSessionStats(out SessionStatsResponse? response, out _, out _))
        {
            return ServiceUnavailable("GOAP agent not initialized");
        }

        return Ok(response);
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

    private bool TryResolveSessionStats(out SessionStatsResponse? response, out GoapAgent? goapAgent, out bool liveAgentAvailable)
    {
        goapAgent = botController.GoapAgent;
        liveAgentAvailable = goapAgent is { Active: true };

        if (liveAgentAvailable)
        {
            sessionStatsCache.Capture(goapAgent!, BotRuntimeModeHelper.GetRuntimeMode(botController));
            response = CreateLiveStatsResponse(goapAgent!);
            return true;
        }

        if (goapAgent != null)
        {
            sessionStatsCache.Capture(goapAgent, BotRuntimeModeHelper.GetRuntimeMode(botController));
        }

        CachedSessionStats? cached = sessionStatsCache.GetSnapshot();
        if (cached != null)
        {
            response = CreateCachedStatsResponse(cached);
            return true;
        }

        response = null;
        return false;
    }

    private SessionStatsResponse CreateLiveStatsResponse(GoapAgent goapAgent)
    {
        SessionStat stats = goapAgent.SessionStat;
        return CreateStatsResponse(
            stats.Kills,
            stats.Deaths,
            stats.Seconds,
            stats.Minutes,
            stats.Hours,
            goapAgent.Active,
            BotRuntimeModeHelper.GetRuntimeMode(botController),
            "live",
            goapAgent.CurrentGoal?.GetType().Name,
            System.DateTime.UtcNow,
            isStale: false);
    }

    private static SessionStatsResponse CreateCachedStatsResponse(CachedSessionStats cached)
    {
        return CreateStatsResponse(
            cached.Kills,
            cached.Deaths,
            cached.Seconds,
            cached.Minutes,
            cached.Hours,
            cached.BotActive,
            cached.RuntimeMode,
            "cached",
            cached.CurrentGoal,
            cached.LastUpdatedUtc,
            isStale: true);
    }

    private static SessionStatsResponse CreateStatsResponse(
        int kills,
        int deaths,
        int seconds,
        int minutes,
        int hours,
        bool botActive,
        string runtimeMode,
        string statsSource,
        string? currentGoal,
        System.DateTime? lastUpdatedUtc,
        bool isStale)
    {
        return new SessionStatsResponse(
            Kills: kills,
            Deaths: deaths,
            Uptime: new SessionUptimeResponse(seconds, minutes, hours),
            KillsPerHour: hours > 0 ? kills / (double)hours : 0,
            DeathsPerHour: hours > 0 ? deaths / (double)hours : 0,
            BotActive: botActive,
            RuntimeMode: runtimeMode,
            StatsSource: statsSource,
            CurrentGoal: currentGoal,
            LastUpdatedUtc: lastUpdatedUtc,
            IsStale: isStale);
    }

    private ObjectResult ServiceUnavailable(string message)
    {
        return StatusCode(503, new
        {
            Error = message,
            RuntimeMode = BotRuntimeModeHelper.GetRuntimeMode(botController),
            StatsSource = "unavailable"
        });
    }
}
