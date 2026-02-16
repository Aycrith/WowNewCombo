using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Core.FeatureFlags;
using Core.GOAP;
using Core.Resilience;
using Core.Session;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.LLM;

/// <summary>
/// Hybrid GOAP+LLM decision service that monitors GOAP events and queries an LLM
/// when GOAP planning fails or encounters unexpected states.
/// Uses circuit breaker for resilience and respects feature flag configuration.
/// </summary>
public sealed class HybridLLMDecisionService : BackgroundService, IGoapEventListener
{
    private readonly ILogger<HybridLLMDecisionService> logger;
    private readonly ILLMClient llmClient;
    private readonly IOptionsMonitor<FeatureFlagsOptions> featureFlags;
    private readonly GoapAgent? goapAgent;
    private readonly CircuitBreaker<LLMDecision> circuitBreaker;

    // Simple decision cache to prevent redundant LLM queries
    private readonly Dictionary<string, (LLMDecision Decision, DateTime Expiry)> decisionCache = new();
    private readonly object cacheLock = new();

    public HybridLLMDecisionService(
        ILogger<HybridLLMDecisionService> logger,
        ILLMClient llmClient,
        IOptionsMonitor<FeatureFlagsOptions> featureFlags,
        GoapAgent? goapAgent = null)
    {
        this.logger = logger;
        this.llmClient = llmClient;
        this.featureFlags = featureFlags;
        this.goapAgent = goapAgent;

        CircuitBreakerOptions cbOptions = featureFlags.CurrentValue.CircuitBreaker;
        circuitBreaker = new CircuitBreaker<LLMDecision>(
            logger,
            "LLM",
            cbOptions.LLMThreshold,
            TimeSpan.FromSeconds(cbOptions.LLMCooldownSeconds),
            () => new LLMDecision("NoAction", "Circuit breaker open", 0.0f)
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        HybridLLMDecisionOptions options = featureFlags.CurrentValue.HybridLLMDecision;

        if (!options.Enabled)
        {
            logger.LogInformation("[HybridLLM       ] Service disabled by feature flag");
            return;
        }

        if (goapAgent == null)
        {
            logger.LogWarning("[HybridLLM       ] No GoapAgent available (configuration mode), service inactive");
            return;
        }

        logger.LogInformation("[HybridLLM       ] Service started (confidence threshold: {Threshold})", options.ConfidenceThreshold);

        // Keep service alive until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        HybridLLMDecisionOptions options = featureFlags.CurrentValue.HybridLLMDecision;

        if (!options.Enabled || goapAgent == null)
            return;

        // Only respond to "NO PLAN" events (when GOAP fails to find a solution)
        if (!IsNoPlanEvent(e))
            return;

        // Fire-and-forget async query (don't block GOAP event bus)
        _ = Task.Run(async () =>
        {
            try
            {
                LLMDecision decision = await QueryLLMWithCircuitBreaker(options);
                HandleLLMDecision(decision, options);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[HybridLLM       ] Error processing GOAP event");
            }
        });
    }

    private async Task<LLMDecision> QueryLLMWithCircuitBreaker(HybridLLMDecisionOptions options)
    {
        if (goapAgent == null)
        {
            return new LLMDecision("NoAction", "No GoapAgent available", 0.0f);
        }

        string context = BuildGameStateContext(goapAgent);

        // Check cache first
        lock (cacheLock)
        {
            if (decisionCache.TryGetValue(context, out (LLMDecision Decision, DateTime Expiry) cached))
            {
                if (cached.Expiry > DateTime.UtcNow)
                {
                    logger.LogDebug("[HybridLLM       ] Returning cached decision");
                    return cached.Decision;
                }
                decisionCache.Remove(context);
            }
        }

        // Use circuit breaker for LLM queries
        LLMDecision decision = await circuitBreaker.ExecuteAsync(async () =>
        {
            using CancellationTokenSource cts = new(options.MaxLatencyMs);
            return await llmClient.QueryAsync(context, cts.Token);
        });

        // Cache the decision
        if (decision.Confidence >= options.ConfidenceThreshold)
        {
            lock (cacheLock)
            {
                DateTime expiry = DateTime.UtcNow.AddSeconds(options.CacheDecisionsSeconds);
                decisionCache[context] = (decision, expiry);
            }
        }

        return decision;
    }

    private void HandleLLMDecision(LLMDecision decision, HybridLLMDecisionOptions options)
    {
        if (decision.Confidence < options.ConfidenceThreshold)
        {
            logger.LogWarning("[HybridLLM       ] Low confidence decision ({Confidence:F2}): {Action}",
                decision.Confidence, decision.SuggestedAction);
            return;
        }

        logger.LogInformation("[HybridLLM       ] Decision: {Action} (confidence: {Confidence:F2}, reasoning: {Reasoning})",
            decision.SuggestedAction, decision.Confidence, decision.Reasoning);

            // Feature: LLM decision integration with GOAP state
            // Currently logs decisions only. Future enhancements:
            // - Set world state flags to unblock GOAP planning
            // - Trigger specific goals based on LLM recommendations
            // - Send executive commands to the bot controller
    }

    private static bool IsNoPlanEvent(GoapEventArgs e)
    {
        // Check if this is a "NO PLAN" event (GOAP planner failed)
        // The GoapAgent logs "NO PLAN" with EventId 0053 at Warning level
        // We can detect this via event type or by checking for AbortEvent
        return e is AbortEvent;
    }

    private static string BuildGameStateContext(GoapAgent agent)
    {
        // Build a JSON context of current game state for LLM
        StringBuilder sb = new();
        sb.AppendLine("{");
        sb.AppendLine($"  \"CurrentGoal\": \"{agent.CurrentGoal?.Name ?? "None"}\",");
        sb.AppendLine($"  \"WorldState\": {{");

        // Serialize world state bits
        List<string> stateFlags = new();
        foreach (GoapKey key in Enum.GetValues<GoapKey>())
        {
            bool value = agent.WorldState[(int)key];
            stateFlags.Add($"    \"{key}\": {value.ToString().ToLowerInvariant()}");
        }
        sb.AppendLine(string.Join(",\n", stateFlags));

        sb.AppendLine("  },");
        sb.AppendLine($"  \"SessionStats\": {{");
        sb.AppendLine($"    \"Kills\": {agent.SessionStat.Kills},");
        sb.AppendLine($"    \"Deaths\": {agent.SessionStat.Deaths},");
        sb.AppendLine($"    \"UptimeMinutes\": {agent.SessionStat.Minutes}");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    public override void Dispose()
    {
        decisionCache.Clear();
        base.Dispose();
    }
}
