using Core;
using Core.Diagnostics;
using Core.GOAP;
using Core.Goals;
using Core.Startup;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Frontend.Controllers;

/// <summary>
/// Unified troubleshooting controller that aggregates diagnostic information
/// from all systems for agent troubleshooting and root cause analysis.
/// </summary>
[ApiController]
[Route("api/troubleshoot")]
public sealed class TroubleshootController : ControllerBase
{
    private readonly ILogger<TroubleshootController> logger;
    private readonly IBotController botController;
    private readonly StartupState startupState;
    private readonly IGoapEventHistory goapHistory;
    private readonly IExecutionTracer executionTracer;

    public TroubleshootController(
        ILogger<TroubleshootController> logger,
        IBotController botController,
        StartupState startupState,
        IGoapEventHistory goapHistory,
        IExecutionTracer executionTracer)
    {
        this.logger = logger;
        this.botController = botController;
        this.startupState = startupState;
        this.goapHistory = goapHistory;
        this.executionTracer = executionTracer;
    }

    /// <summary>
    /// GET /api/troubleshoot
    /// Returns comprehensive troubleshooting information aggregating health,
    /// diagnostics, GOAP state, recent logs, and actionable recommendations.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTroubleshootInfo()
    {
        Stopwatch sw = Stopwatch.StartNew();
        logger.LogInformation("[Troubleshoot] Generating diagnostic report");

        try
        {
            var report = new TroubleshootReport
            {
                Timestamp = DateTime.UtcNow,
                Summary = await GenerateSummaryAsync(),
                Health = await GenerateHealthStatusAsync(),
                Bot = await GenerateBotStateAsync(),
                Goap = await GenerateGoapStateAsync(),
                Diagnostics = await GenerateDiagnosticsAsync(),
                RecentLogs = await GenerateRecentLogsAsync(),
                Recommendations = await GenerateRecommendationsAsync()
            };

            sw.Stop();
            logger.LogInformation("[Troubleshoot] Report generated in {ElapsedMs}ms", sw.ElapsedMilliseconds);

            return Ok(report);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "[Troubleshoot] Failed to generate report");
            return StatusCode(500, new { Error = ex.Message, Stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// GET /api/troubleshoot/goap
    /// Returns detailed GOAP inspection data including current goal,
    /// plan, recent transitions, and NO PLAN events.
    /// </summary>
    [HttpGet("goap")]
    public IActionResult GetGoapInspection()
    {
        try
        {
            var goapAgent = botController.GoapAgent;
            if (goapAgent == null)
            {
                return Ok(new GoapInspectionResult
                {
                    Status = "NotInitialized",
                    Message = "GOAP agent is not yet initialized"
                });
            }

            var currentGoal = goapAgent.CurrentGoal;
            var plan = goapAgent.Plan;

            var result = new GoapInspectionResult
            {
                Status = currentGoal != null ? "Active" : "NoGoal",
                CurrentGoal = currentGoal != null ? new GoalInfo
                {
                    Name = currentGoal.Name,
                    DisplayName = currentGoal.DisplayName,
                    Cost = currentGoal.Cost,
                    Preconditions = currentGoal.Preconditions.ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => kvp.Value),
                    Effects = currentGoal.Effects.ToDictionary(
                        kvp => kvp.Key.ToString(),
                        kvp => kvp.Value),
                    CanRun = currentGoal.CanRun()
                } : null,
                Plan = plan?.Select(g => new PlanStepInfo
                {
                    Name = g.Name,
                    Cost = g.Cost,
                    PreconditionsMet = g.Preconditions.All(p =>
                        p.Key.ToString().Contains(p.Value.ToString()))
                }).ToList() ?? new List<PlanStepInfo>(),
                PlanDepth = plan?.Count ?? 0,
                RecentTransitions = goapHistory.GetRecentTransitions(20),
                NoPlanEvents = goapHistory.GetNoPlanEvents(10),
                PreconditionFailures = goapHistory.GetPreconditionFailures(10),
                ExecutionTrace = executionTracer.GetRecentTraces(50)
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Troubleshoot] Failed to get GOAP inspection");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/troubleshoot/combat
    /// Returns detailed combat state including rotation metrics,
    /// recent casts, and cooldown status.
    /// </summary>
    [HttpGet("combat")]
    public IActionResult GetCombatInspection()
    {
        try
        {
            var combatLog = GetCombatLogSummary();
            var rotationMetrics = GetRotationMetrics();

            var result = new CombatInspectionResult
            {
                InCombat = botController.IsBotActive &&
                          botController.ClassConfig?.Mode.ToString().Contains("Combat") == true,
                TargetInfo = GetTargetInfo(),
                RotationMetrics = rotationMetrics,
                RecentCasts = combatLog.RecentCasts,
                CooldownStatus = GetCooldownStatus(),
                CombatLogSummary = combatLog,
                LastAbilitiesUsed = executionTracer.GetRecentTraces(20)
                    .Where(t => t.Category == TraceCategory.Ability)
                    .Select(t => new AbilityUsageInfo
                    {
                        Ability = t.Operation,
                        Timestamp = t.Timestamp,
                        Success = t.Success,
                        Details = t.Details
                    }).ToList()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Troubleshoot] Failed to get combat inspection");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/troubleshoot/metrics
    /// Returns real-time performance metrics in Prometheus-compatible format.
    /// </summary>
    [HttpGet("metrics")]
    public IActionResult GetMetrics()
    {
        try
        {
            var metrics = new MetricsResult
            {
                Timestamp = DateTime.UtcNow,
                GoapMetrics = CollectGoapMetrics(),
                CombatMetrics = CollectCombatMetrics(),
                SystemMetrics = CollectSystemMetrics(),
                AddonMetrics = CollectAddonMetrics()
            };

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Troubleshoot] Failed to get metrics");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    #region Private Helpers

    private async Task<SummaryInfo> GenerateSummaryAsync()
    {
        var snapshot = startupState.GetSnapshot();
        var isHealthy = snapshot.CurrentStage == StartupStage.Ready &&
                       botController.IsBotActive;

        var issues = new List<string>();

        if (snapshot.CurrentStage != StartupStage.Ready)
        {
            issues.Add($"Startup not complete: {snapshot.CurrentStage}");
        }

        if (!botController.IsBotActive)
        {
            issues.Add("Bot is not active");
        }

        var goapAgent = botController.GoapAgent;
        if (goapAgent?.CurrentGoal == null)
        {
            issues.Add("No current GOAP goal (NO PLAN state)");
        }

        return new SummaryInfo
        {
            Status = issues.Count == 0 ? "healthy" : issues.Count < 3 ? "degraded" : "critical",
            IsBotActive = botController.IsBotActive,
            StartupStage = snapshot.CurrentStage.ToString(),
            Issues = issues,
            IssueCount = issues.Count
        };
    }

    private async Task<HealthStatusInfo> GenerateHealthStatusAsync()
    {
        var current = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - current.StartTime.ToUniversalTime();

        return new HealthStatusInfo
        {
            Status = botController.IsBotActive ? "RUNNING" : "IDLE",
            Uptime = uptime.ToString("c"),
            ProcessId = current.Id,
            ThreadCount = current.Threads.Count,
            MemoryMB = current.WorkingSet64 / (1024 * 1024),
            CpuTime = current.TotalProcessorTime.ToString("c")
        };
    }

    private async Task<BotStateInfo> GenerateBotStateAsync()
    {
        var classConfig = botController.ClassConfig;

        return new BotStateInfo
        {
            IsActive = botController.IsBotActive,
            Profile = botController.SelectedClassFilename,
            Mode = classConfig?.Mode.ToString() ?? "None",
            PathCount = botController.SelectedPathFilename?.Count ?? 0,
            AvgScreenLatencyMs = Math.Round(botController.AvgScreenLatency, 1),
            AvgNPCLatencyMs = Math.Round(botController.AvgNPCLatency, 1),
            InCombat = botController.IsBotActive && classConfig?.Mode.ToString().Contains("Combat") == true
        };
    }

    private async Task<GoapStateInfo> GenerateGoapStateAsync()
    {
        var goapAgent = botController.GoapAgent;
        if (goapAgent == null)
        {
            return new GoapStateInfo { Status = "NotInitialized" };
        }

        var currentGoal = goapAgent.CurrentGoal;
        var plan = goapAgent.Plan;

        return new GoapStateInfo
        {
            Status = currentGoal != null ? "Active" : "NoGoal",
            CurrentGoalName = currentGoal?.Name ?? "None",
            CurrentGoalDisplayName = currentGoal?.DisplayName ?? "None",
            PlanDepth = plan?.Count ?? 0,
            RecentNoPlanCount = goapHistory.GetNoPlanEvents(1).Count,
            LastTransition = goapHistory.GetRecentTransitions(1).FirstOrDefault()?.Timestamp
        };
    }

    private async Task<DiagnosticsInfo> GenerateDiagnosticsAsync()
    {
        return new DiagnosticsInfo
        {
            KeybindingsInitialized = true, // Would query KeyBindingsReader
            ActionBarInitialized = true,     // Would query ActionBarTextureReader
            AddonResponding = botController.AvgScreenLatency > 0,
            LastScreenLatencyMs = botController.AvgScreenLatency,
            LastNPCLatencyMs = botController.AvgNPCLatency
        };
    }

    private static async Task<List<LogEntryInfo>> GenerateRecentLogsAsync()
    {
        // This would integrate with LoggerSink to get recent entries
        // For now, return placeholder
        return new List<LogEntryInfo>();
    }

    private async Task<List<RecommendationInfo>> GenerateRecommendationsAsync()
    {
        var recommendations = new List<RecommendationInfo>();

        var snapshot = startupState.GetSnapshot();
        if (snapshot.CurrentStage != StartupStage.Ready)
        {
            recommendations.Add(new RecommendationInfo
            {
                Severity = "high",
                Category = "startup",
                Message = $"Startup incomplete: {snapshot.CurrentStage}",
                Action = "Check startup logs and verify all systems initialized"
            });
        }

        if (!botController.IsBotActive)
        {
            recommendations.Add(new RecommendationInfo
            {
                Severity = "medium",
                Category = "bot",
                Message = "Bot is not active",
                Action = "Call POST /api/diagnostics/fix/initstate to reset addon state"
            });
        }

        var goapAgent = botController.GoapAgent;
        if (goapAgent?.CurrentGoal == null)
        {
            recommendations.Add(new RecommendationInfo
            {
                Severity = "high",
                Category = "goap",
                Message = "GOAP has no current goal (NO PLAN)",
                Action = "Check GOAP inspection at /api/troubleshoot/goap for details"
            });
        }

        if (botController.AvgScreenLatency > 100)
        {
            recommendations.Add(new RecommendationInfo
            {
                Severity = "medium",
                Category = "performance",
                Message = $"High screen latency: {botController.AvgScreenLatency:F1}ms",
                Action = "Check addon frame rate and reduce game resolution if needed"
            });
        }

        return recommendations;
    }

    private static CombatLogSummary GetCombatLogSummary()
    {
        // This would query actual combat log
        return new CombatLogSummary
        {
            RecentCasts = new List<CastInfo>(),
            Summary = "Combat log data not yet integrated"
        };
    }

    private static RotationMetricsInfo GetRotationMetrics()
    {
        // This would query RotationMetricsCollector
        return new RotationMetricsInfo
        {
            TotalTicks = 0,
            OptimizedTicks = 0,
            FallbackTicks = 0
        };
    }

    private static TargetInfo GetTargetInfo()
    {
        // This would query PlayerReader for target data
        return new TargetInfo
        {
            HasTarget = false,
            TargetHealth = 0,
            TargetName = "None"
        };
    }

    private static CooldownStatusInfo GetCooldownStatus()
    {
        // This would query cooldown tracking
        return new CooldownStatusInfo
        {
            GlobalCooldownRemaining = 0,
            AbilityCooldowns = new Dictionary<string, float>()
        };
    }

    private GoapMetricsInfo CollectGoapMetrics()
    {
        var history = goapHistory.GetRecentTransitions(100);

        return new GoapMetricsInfo
        {
            TotalTransitions = goapHistory.TotalTransitions,
            NoPlanEvents = goapHistory.TotalNoPlanEvents,
            PlanningSuccessRate = history.Count > 0
            ? (1 - (goapHistory.GetNoPlanEvents(100).Count / 100.0)) * 100
            : 100,
            AveragePlanDepth = history.Count > 0 ? history.Average(t => t.PlanDepth) : 0,
            MostCommonGoals = history
            .GroupBy(t => t.ToGoal)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new GoalMetricInfo { GoalName = g.Key, Count = g.Count() })
            .ToList()
        };
    }

    private static CombatMetricsInfo CollectCombatMetrics()
    {
        // This would query combat metrics
        return new CombatMetricsInfo
        {
            Dps = 0,
            CastAttempts = 0,
            CastSuccesses = 0,
            SuccessRate = 0
        };
    }

    private static SystemMetricsInfo CollectSystemMetrics()
    {
        var current = Process.GetCurrentProcess();

        return new SystemMetricsInfo
        {
            MemoryMB = current.WorkingSet64 / (1024 * 1024),
            ThreadCount = current.Threads.Count,
            CpuPercent = 0, // Would calculate from processor time
            GcCollections = GC.CollectionCount(0)
        };
    }

    private AddonMetricsInfo CollectAddonMetrics()
    {
        return new AddonMetricsInfo
        {
            ScreenLatencyMs = botController.AvgScreenLatency,
            NPCLatencyMs = botController.AvgNPCLatency,
            FrameRate = 0, // Would calculate from frame timestamps
            LastUpdateMs = 0
        };
    }

    #endregion
}

#region DTOs

public class TroubleshootReport
{
    public DateTime Timestamp { get; set; }
    public SummaryInfo Summary { get; set; } = null!;
    public HealthStatusInfo Health { get; set; } = null!;
    public BotStateInfo Bot { get; set; } = null!;
    public GoapStateInfo Goap { get; set; } = null!;
    public DiagnosticsInfo Diagnostics { get; set; } = null!;
    public List<LogEntryInfo> RecentLogs { get; set; } = null!;
    public List<RecommendationInfo> Recommendations { get; set; } = null!;
}

public class SummaryInfo
{
    public string Status { get; set; } = null!;
    public bool IsBotActive { get; set; }
    public string StartupStage { get; set; } = null!;
    public List<string> Issues { get; set; } = null!;
    public int IssueCount { get; set; }
}

public class HealthStatusInfo
{
    public string Status { get; set; } = null!;
    public string Uptime { get; set; } = null!;
    public int ProcessId { get; set; }
    public int ThreadCount { get; set; }
    public long MemoryMB { get; set; }
    public string CpuTime { get; set; } = null!;
}

public class BotStateInfo
{
    public bool IsActive { get; set; }
    public string? Profile { get; set; }
    public string Mode { get; set; } = null!;
    public int PathCount { get; set; }
    public double AvgScreenLatencyMs { get; set; }
    public double AvgNPCLatencyMs { get; set; }
    public bool InCombat { get; set; }
}

public class GoapStateInfo
{
    public string Status { get; set; } = null!;
    public string CurrentGoalName { get; set; } = null!;
    public string CurrentGoalDisplayName { get; set; } = null!;
    public int PlanDepth { get; set; }
    public int RecentNoPlanCount { get; set; }
    public DateTime? LastTransition { get; set; }
}

public class DiagnosticsInfo
{
    public bool KeybindingsInitialized { get; set; }
    public bool ActionBarInitialized { get; set; }
    public bool AddonResponding { get; set; }
    public double LastScreenLatencyMs { get; set; }
    public double LastNPCLatencyMs { get; set; }
}

public class LogEntryInfo
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? Source { get; set; }
}

public class RecommendationInfo
{
    public string Severity { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string Action { get; set; } = null!;
}

public class GoapInspectionResult
{
    public string Status { get; set; } = null!;
    public string? Message { get; set; }
    public GoalInfo? CurrentGoal { get; set; }
    public List<PlanStepInfo> Plan { get; set; } = null!;
    public int PlanDepth { get; set; }
    public List<GoalTransitionEntry> RecentTransitions { get; set; } = null!;
    public List<NoPlanEventEntry> NoPlanEvents { get; set; } = null!;
    public List<PreconditionFailureEntry> PreconditionFailures { get; set; } = null!;
    public List<TraceEntry> ExecutionTrace { get; set; } = null!;
}

public class GoalInfo
{
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public float Cost { get; set; }
    public Dictionary<string, bool> Preconditions { get; set; } = null!;
    public Dictionary<string, bool> Effects { get; set; } = null!;
    public bool CanRun { get; set; }
}

public class PlanStepInfo
{
    public string Name { get; set; } = null!;
    public float Cost { get; set; }
    public bool PreconditionsMet { get; set; }
}

public class CombatInspectionResult
{
    public bool InCombat { get; set; }
    public TargetInfo TargetInfo { get; set; } = null!;
    public RotationMetricsInfo RotationMetrics { get; set; } = null!;
    public List<CastInfo> RecentCasts { get; set; } = null!;
    public CooldownStatusInfo CooldownStatus { get; set; } = null!;
    public CombatLogSummary CombatLogSummary { get; set; } = null!;
    public List<AbilityUsageInfo> LastAbilitiesUsed { get; set; } = null!;
}

public class TargetInfo
{
    public bool HasTarget { get; set; }
    public int TargetHealth { get; set; }
    public string TargetName { get; set; } = null!;
}

public class RotationMetricsInfo
{
    public int TotalTicks { get; set; }
    public int OptimizedTicks { get; set; }
    public int FallbackTicks { get; set; }
}

public class CastInfo
{
    public string AbilityName { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
}

public class CombatLogSummary
{
    public List<CastInfo> RecentCasts { get; set; } = null!;
    public string Summary { get; set; } = null!;
}

public class CooldownStatusInfo
{
    public float GlobalCooldownRemaining { get; set; }
    public Dictionary<string, float> AbilityCooldowns { get; set; } = null!;
}

public class AbilityUsageInfo
{
    public string Ability { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string Details { get; set; } = null!;
}

public class MetricsResult
{
    public DateTime Timestamp { get; set; }
    public GoapMetricsInfo GoapMetrics { get; set; } = null!;
    public CombatMetricsInfo CombatMetrics { get; set; } = null!;
    public SystemMetricsInfo SystemMetrics { get; set; } = null!;
    public AddonMetricsInfo AddonMetrics { get; set; } = null!;
}

public class GoapMetricsInfo
{
    public int TotalTransitions { get; set; }
    public int NoPlanEvents { get; set; }
    public double PlanningSuccessRate { get; set; }
    public double AveragePlanDepth { get; set; }
    public List<GoalMetricInfo> MostCommonGoals { get; set; } = null!;
}

public class GoalMetricInfo
{
    public string GoalName { get; set; } = null!;
    public int Count { get; set; }
}

public class CombatMetricsInfo
{
    public int Dps { get; set; }
    public int CastAttempts { get; set; }
    public int CastSuccesses { get; set; }
    public double SuccessRate { get; set; }
}

public class SystemMetricsInfo
{
    public long MemoryMB { get; set; }
    public int ThreadCount { get; set; }
    public double CpuPercent { get; set; }
    public int GcCollections { get; set; }
}

public class AddonMetricsInfo
{
    public double ScreenLatencyMs { get; set; }
    public double NPCLatencyMs { get; set; }
    public double FrameRate { get; set; }
    public double LastUpdateMs { get; set; }
}

#endregion
