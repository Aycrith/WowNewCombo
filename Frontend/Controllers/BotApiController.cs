using Core;
using Core.Launch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Frontend.Controllers;

#region Response DTOs

public record BotStatus(
    bool IsActive,
    string? ProfileName,
    string? CurrentGoal,
    int? GoalStackDepth,
    double AvgScreenLatency,
    double AvgNpcLatency);

public record ProfileListItem(
    string FileName,
    bool IsCurrentlyLoaded);

public record ProfileLoadRequest(
    string FileName);

#endregion

/// <summary>
/// API controller for bot control operations
/// </summary>
[ApiController]
[Route("api/bot")]
[Route("api/[controller]")]
public class BotApiController : ControllerBase
{
    private readonly ILogger<BotApiController> logger;
    private readonly IBotController botController;
    private readonly IBotStartGuard botStartGuard;

    public BotApiController(
        ILogger<BotApiController> logger,
        IBotController botController,
        IBotStartGuard botStartGuard)
    {
        this.logger = logger;
        this.botController = botController;
        this.botStartGuard = botStartGuard;
    }

    /// <summary>
    /// GET /api/bot/status
    /// Returns current bot status and runtime metrics
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            string? profileName = null;
            try { profileName = botController.SelectedClassFilename; }
            catch (NotImplementedException ex) { logger.LogWarning(ex, "SelectedClassFilename not implemented"); }

            Core.GOAP.GoapAgent? agent = null;
            try { agent = botController.GoapAgent; }
            catch (NotImplementedException ex) { logger.LogWarning(ex, "GoapAgent access not implemented"); }

            double avgScreen = 0;
            double avgNpc = 0;
            try { avgScreen = botController.AvgScreenLatency; }
            catch (NotImplementedException ex) { logger.LogWarning(ex, "AvgScreenLatency not implemented"); }
            try { avgNpc = botController.AvgNPCLatency; }
            catch (NotImplementedException ex) { logger.LogWarning(ex, "AvgNPCLatency not implemented"); }

            BotStatus status = new(
                botController.IsBotActive,
                profileName,
                agent?.CurrentGoal?.Name,
                agent?.Plan?.Count,
                avgScreen,
                avgNpc);

            sw.Stop();
            logger.LogDebug("Bot status: active={IsActive}, profile={ProfileName} ({ElapsedMs}ms)",
                status.IsActive, status.ProfileName, sw.ElapsedMilliseconds);

            return Ok(status);
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex, "Get bot status failed");
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/bot/start
    /// Starts the bot if not already running
    /// </summary>
    [HttpPost("start")]
    public IActionResult Start()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            if (botController.IsBotActive)
            {
                logger.LogInformation("Bot already active - ignoring start request");
                return Ok(new { Message = "Bot already running", IsActive = true });
            }

            LaunchReadinessSnapshot readiness = EvaluateReadiness();
            if (!readiness.CanStartBot)
            {
                sw.Stop();
                return Conflict(new
                {
                    Message = "Bot start blocked by launch readiness. Open /launch for details.",
                    IsActive = false,
                    Readiness = readiness
                });
            }

            logger.LogInformation("Starting bot");
            botController.ToggleBotStatus();

            sw.Stop();
            if (!botController.IsBotActive)
            {
                return Conflict(new
                {
                    Message = "Bot start attempted but was blocked. Open /launch for details.",
                    IsActive = false,
                    Readiness = EvaluateReadiness()
                });
            }

            return Ok(new { Message = "Bot started", IsActive = true });
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex, "Start bot failed");
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/bot/stop
    /// Stops the bot if running
    /// </summary>
    [HttpPost("stop")]
    public IActionResult Stop()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            if (!botController.IsBotActive)
            {
                logger.LogInformation("Bot already stopped - ignoring stop request");
                return Ok(new { Message = "Bot already stopped", IsActive = false });
            }

            logger.LogInformation("Stopping bot");
            botController.ToggleBotStatus();

            sw.Stop();
            return Ok(new { Message = "Bot stopped", IsActive = botController.IsBotActive });
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex, "Stop bot failed");
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/bot/toggle
    /// Toggles bot on/off
    /// </summary>
    [HttpPost("toggle")]
    public IActionResult Toggle()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            bool wasBotActive = botController.IsBotActive;
            logger.LogInformation("Toggling bot: {PreviousState} -> {NewState}",
                wasBotActive ? "active" : "stopped",
                !wasBotActive ? "active" : "stopped");

            if (!wasBotActive)
            {
                LaunchReadinessSnapshot readiness = EvaluateReadiness();
                if (!readiness.CanStartBot)
                {
                    sw.Stop();
                    return Conflict(new
                    {
                        Message = "Bot start blocked by launch readiness. Open /launch for details.",
                        IsActive = false,
                        Readiness = readiness
                    });
                }
            }

            botController.ToggleBotStatus();

            sw.Stop();
            return Ok(new
            {
                Message = botController.IsBotActive ? "Bot started" : "Bot stopped",
                IsActive = botController.IsBotActive
            });
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex, "Toggle bot failed");
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    private LaunchReadinessSnapshot EvaluateReadiness()
    {
        ClassConfiguration? classConfig = botController.ClassConfig;
        RouteInfo? routeInfo = botController is BotController full ? full.RouteInfo : null;
        return botStartGuard.Evaluate(classConfig, routeInfo);
    }

    /// <summary>
    /// GET /api/bot/profile/list
    /// Returns list of available class profiles
    /// </summary>
    [HttpGet("profile/list")]
    public IActionResult ListProfiles()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            System.Collections.Generic.IEnumerable<string> files = botController.ClassFiles();
            List<ProfileListItem> profiles = files.Select(f => new ProfileListItem(
                f,
                f == botController.SelectedClassFilename)).ToList();

            sw.Stop();
            logger.LogDebug("Listed {Count} profiles ({ElapsedMs}ms)", profiles.Count, sw.ElapsedMilliseconds);

            return Ok(profiles);
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex, "List profiles failed");
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/bot/profile/current
    /// Returns current loaded profile details
    /// </summary>
    [HttpGet("profile/current")]
    public IActionResult GetCurrentProfile()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            ClassConfiguration? classConfig = botController.ClassConfig;
            if (classConfig == null)
            {
                return Ok(new { FileName = (string?)null, IsLoaded = false });
            }

            object result = new
            {
                FileName = botController.SelectedClassFilename,
                IsLoaded = true,
                Mode = classConfig.Mode.ToString(),
                Mail = classConfig.Mail,
                UseMount = classConfig.UseMount,
                Loot = classConfig.Loot,
                Skin = classConfig.Skin,
                Herb = classConfig.Herb,
                Mine = classConfig.Mine,
                PullSequenceCount = classConfig.Pull.Sequence.Length,
                CombatSequenceCount = classConfig.Combat.Sequence.Length,
                AdhocSequenceCount = classConfig.Adhoc.Sequence.Length
            };

            sw.Stop();
            logger.LogDebug("Current profile: {FileName} ({ElapsedMs}ms)",
                botController.SelectedClassFilename, sw.ElapsedMilliseconds);

            return Ok(result);
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex, "Get current profile failed");
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/bot/profile/load
    /// Loads a specific class profile
    /// Body: { "fileName": "BloodElf_Rogue_Starter_Test.json" }
    /// </summary>
    [HttpPost("profile/load")]
    public IActionResult LoadProfile([FromBody] ProfileLoadRequest request)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(request.FileName))
            {
                return BadRequest(new { Error = "FileName is required" });
            }

            logger.LogInformation("Loading profile: {FileName}", request.FileName);
            botController.LoadClassProfile(request.FileName);

            sw.Stop();
            return Ok(new
            {
                Message = $"Profile '{request.FileName}' loaded successfully",
                FileName = request.FileName,
                IsLoaded = botController.ClassConfig != null
            });
        }
        catch (System.Exception ex)
        {
            logger.LogError(ex, "Load profile failed: {FileName}", request.FileName);
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}
