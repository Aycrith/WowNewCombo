using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Core.FeatureFlags;
using Core.GOAP;
using Core.Resilience;
using Core.Session;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.LLM;

/// <summary>
/// Core decision engine for hybrid GOAP+LLM integration.
/// Contains the business logic for querying LLM and handling decisions.
/// </summary>
public sealed class HybridLlmDecisionEngine
{
    private readonly ILogger<HybridLlmDecisionEngine> logger;
    private readonly ILLMClient llmClient;
    private readonly IOptionsMonitor<FeatureFlagsOptions> featureFlags;
    private readonly GoapAgent? goapAgent;
    private readonly CircuitBreaker<LLMDecision> circuitBreaker;
    private readonly TimeProvider timeProvider;

    // Simple decision cache (bounded to prevent memory leaks)
    private const int MaxCacheEntries = 100;
    private readonly Dictionary<string, (LLMDecision Decision, DateTime Expiry)> decisionCache = new();
    private readonly object cacheLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HybridLlmDecisionEngine(
        ILogger<HybridLlmDecisionEngine> logger,
        ILLMClient llmClient,
        IOptionsMonitor<FeatureFlagsOptions> featureFlags,
        CircuitBreaker<LLMDecision> circuitBreaker,
        GoapAgent? goapAgent = null,
        TimeProvider? timeProvider = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        this.featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        this.circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
        this.goapAgent = goapAgent;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Processes a "NO PLAN" event by querying the LLM for a decision.
    /// </summary>
    public async Task ProcessNoPlanEventAsync(CancellationToken cancellationToken = default)
    {
        HybridLLMDecisionOptions options = featureFlags.CurrentValue.HybridLLMDecision;

        if (!options.Enabled || goapAgent == null)
            return;

        LLMDecision decision = await QueryLLMWithCircuitBreaker(options, cancellationToken);
        HandleLLMDecision(decision, options);
    }

    private async Task<LLMDecision> QueryLLMWithCircuitBreaker(
        HybridLLMDecisionOptions options,
        CancellationToken cancellationToken)
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
                    logger.LogDebug("[HybridLLM-Engine] Returning cached decision");
                    return cached.Decision;
                }
                decisionCache.Remove(context);
            }
        }

        // Use circuit breaker for LLM queries
        LLMDecision decision = await circuitBreaker.ExecuteAsync(async () =>
        {
            using CancellationTokenSource cts = new(options.MaxLatencyMs);
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                cts.Token, cancellationToken);
            return await llmClient.QueryAsync(context, linked.Token);
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
            logger.LogWarning("[HybridLLM-Engine] Low confidence decision ({Confidence:F2}): {Action}",
                decision.Confidence, decision.SuggestedAction);
            return;
        }

        logger.LogInformation("[HybridLLM-Engine] Decision: {Action} (confidence: {Confidence:F2}, reasoning: {Reasoning})",
            decision.SuggestedAction, decision.Confidence, decision.Reasoning);
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
                expired ??= new List<string>();
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

        return JsonSerializer.Serialize(context, JsonOptions);
    }

    /// <summary>
    /// Clears the decision cache.
    /// </summary>
    public void ClearCache()
    {
        lock (cacheLock)
        {
            decisionCache.Clear();
        }
    }
}
