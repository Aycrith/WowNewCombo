using System;
using System.Collections.Generic;
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
    private readonly TimeProvider timeProvider;

    // Simple decision cache to prevent redundant LLM queries (bounded to prevent memory leaks)
    private const int MaxCacheEntries = 100;
    private readonly Dictionary<string, (LLMDecision Decision, DateTime Expiry)> decisionCache = new();
    private readonly object cacheLock = new();

    public HybridLLMDecisionService(
        ILogger<HybridLLMDecisionService> logger,
        ILLMClient llmClient,
        IOptionsMonitor<FeatureFlagsOptions> featureFlags,
        GoapAgent? goapAgent = null,
        TimeProvider? timeProvider = null)
    {
        this.logger = logger;
        this.llmClient = llmClient;
        this.featureFlags = featureFlags;
        this.goapAgent = goapAgent;
        this.timeProvider = timeProvider ?? TimeProvider.System;

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
                if (cached.Expiry > timeProvider.GetUtcNow().DateTime)
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

        // Cache the decision (with bounded size)
        if (decision.Confidence >= options.ConfidenceThreshold)
        {
            lock (cacheLock)
            {
                // Evict expired entries if cache is at capacity
                if (decisionCache.Count >= MaxCacheEntries)
                {
                    EvictExpiredCacheEntries();
                }

                // If still at capacity after eviction, clear oldest entries
                if (decisionCache.Count >= MaxCacheEntries)
                {
                    decisionCache.Clear();
                }

                DateTime expiry = timeProvider.GetUtcNow().DateTime.AddSeconds(options.CacheDecisionsSeconds);
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

    /// <summary>
    /// Removes expired entries from the decision cache. Must be called under cacheLock.
    /// </summary>
    private void EvictExpiredCacheEntries()
    {
        DateTime now = timeProvider.GetUtcNow().DateTime;
        List<string>? expired = null;

        foreach (KeyValuePair<string, (LLMDecision Decision, DateTime Expiry)> kvp in decisionCache)
        {
            if (kvp.Value.Expiry <= now)
            {
                expired ??= [];
                expired.Add(kvp.Key);
            }
        }

        if (expired != null)
        {
            foreach (string key in expired)
            {
                decisionCache.Remove(key);
            }
        }
    }

    private static string BuildGameStateContext(GoapAgent agent)
    {
        Dictionary<string, bool> worldState = new();
        foreach (GoapKey key in Enum.GetValues<GoapKey>())
        {
            worldState[key.ToString()] = agent.WorldState[(int)key];
        }

        var context = new
        {
            CurrentGoal = agent.CurrentGoal?.Name ?? "None",
            WorldState = worldState,
            SessionStats = new
            {
                agent.SessionStat.Kills,
                agent.SessionStat.Deaths,
                UptimeMinutes = agent.SessionStat.Minutes
            }
        };

        return JsonSerializer.Serialize(context, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public override void Dispose()
    {
        decisionCache.Clear();
        base.Dispose();
    }
}
