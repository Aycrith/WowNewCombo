namespace Core.AI.HybridDecision;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Core.AI.LLM;
using Core.FeatureFlags;
using Core.GOAP;
using Core.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Hybrid decision engine that combines GOAP planning with LLM fallback.
/// Uses GOAP for normal scenarios and LLM for edge cases or when confidence is low.
/// </summary>
public sealed class HybridDecisionEngine
{
    private readonly ILogger<HybridDecisionEngine> logger;
    private readonly GoapAgent goapAgent;
    private readonly ILLMClientFactory llmFactory;
    private readonly CircuitBreaker<LLMDecisionResult> circuitBreaker;
    private readonly FeatureFlagsOptions flags;

    private readonly HybridDecisionCache cache;
    private readonly GameStateSerializer stateSerializer;

    /// <summary>
    /// Creates a new hybrid decision engine.
    /// </summary>
    public HybridDecisionEngine(
        ILogger<HybridDecisionEngine> logger,
        GoapAgent goapAgent,
        ILLMClientFactory llmFactory,
        IOptions<FeatureFlagsOptions> options)
    {
        this.logger = logger;
        this.goapAgent = goapAgent;
        this.llmFactory = llmFactory;
        this.flags = options.Value;

        cache = new HybridDecisionCache();
        stateSerializer = new GameStateSerializer();

        // Initialize circuit breaker
        HybridLLMDecisionOptions? hybridConfig = flags.HybridLLMDecision;
        circuitBreaker = new CircuitBreaker<LLMDecisionResult>(
            logger,
            serviceName: "HybridLLM",
            failureThreshold: hybridConfig?.CircuitBreakerThreshold ?? 3,
            cooldownPeriod: TimeSpan.FromSeconds(hybridConfig?.CircuitBreakerCooldownSeconds ?? 60),
            fallback: () => new LLMDecisionResult(null, "Circuit breaker fallback", 0f));
    }

    /// <summary>
    /// Gets the next action using hybrid GOAP+LLM decision making.
    /// </summary>
    /// <param name="availableActions">Actions available to choose from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The selected action, or null if no action available.</returns>
    public async Task<KeyAction?> GetNextActionAsync(
        IReadOnlyList<KeyAction> availableActions,
        CancellationToken ct)
    {
        if (!flags.HybridLLMDecision?.Enabled ?? true)
        {
            // Feature disabled, use GOAP only
            logger.LogDebug("[HybridDecision ] Hybrid LLM disabled, using GOAP only");
            return GetGoapAction();
        }

        // Try GOAP first
        KeyAction? goapAction = GetGoapAction();
        float goapConfidence = CalculateGoapConfidence(goapAction);

        logger.LogDebug(
            "[HybridDecision ] GOAP confidence: {Confidence:F2} for {Action}",
            goapConfidence,
            goapAction?.Name ?? "none");

        // Use LLM only when GOAP is uncertain
        bool shouldUseLLM = ShouldUseLLM(goapConfidence);

        if (!shouldUseLLM)
        {
            logger.LogDebug("[HybridDecision ] GOAP confident, using GOAP action: {Action}", goapAction?.Name);
            return goapAction;
        }

        logger.LogInformation(
            "[HybridDecision ] GOAP uncertain ({Confidence:F2}), consulting LLM",
            goapConfidence);

        // Try LLM
        LLMDecisionResult? llmDecision = await GetLLMDecisionAsync(availableActions, ct);

        if (llmDecision?.Action != null)
        {
            logger.LogInformation(
                "[HybridDecision ] LLM selected: {Action} ({Reasoning})",
                llmDecision.Action.Name,
                llmDecision.Reasoning);
            return llmDecision.Action;
        }

        // Fallback to GOAP if LLM fails
        logger.LogWarning(
            "[HybridDecision ] LLM failed ({Error}), falling back to GOAP",
            llmDecision?.Reasoning ?? "unknown error");
        return goapAction;
    }

    /// <summary>
    /// Gets the best action from GOAP planner.
    /// </summary>
    private KeyAction? GetGoapAction()
    {
        try
        {
            // Use reflection or direct call to get GOAP action
            // For now, return the current goal's next action
            return null; // TODO: Implement proper GOAP integration
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[HybridDecision ] Error getting GOAP action");
            return null;
        }
    }

    /// <summary>
    /// Calculates confidence level for GOAP decision.
    /// </summary>
    private float CalculateGoapConfidence(KeyAction? action)
    {
        if (action == null)
        {
            return 0f;
        }

        // Base confidence
        float confidence = 0.8f;

        // Adjust based on unexpected state detection
        if (DetectUnexpectedState())
        {
            confidence -= 0.3f;
        }

        // Check cache for recent similar decisions
        if (cache.GetRecentDecision() != null)
        {
            // Lower confidence if we're repeating the same action frequently
            confidence -= 0.1f;
        }

        return Math.Clamp(confidence, 0f, 1f);
    }

    /// <summary>
    /// Determines if LLM should be consulted based on GOAP confidence.
    /// </summary>
    private bool ShouldUseLLM(float goapConfidence)
    {
        float threshold = flags.HybridLLMDecision?.ConfidenceThreshold ?? 0.6f;

        // Use LLM when:
        // 1. GOAP confidence is below threshold
        // 2. Unexpected state is detected (e.g., elite mob, multiple enemies)
        // 3. Feature flag is set to always use LLM for unexpected states

        if (goapConfidence < threshold)
        {
            return true;
        }

        if (flags.HybridLLMDecision?.EnableForUnexpectedStates ?? false)
        {
            return DetectUnexpectedState();
        }

        return false;
    }

    /// <summary>
    /// Detects situations where GOAP may not have good coverage.
    /// </summary>
    private static bool DetectUnexpectedState()
    {
        // Situations GOAP wasn't designed for:
        // 1. Large pulls (many enemies)
        // 2. Elites or rare mobs
        // 3. Emergency situations (very low health outside combat)
        // 4. Complex multi-target scenarios

        // TODO: Implement proper state detection
        // For now, return false
        return false;
    }

    /// <summary>
    /// Gets decision from LLM.
    /// </summary>
    private async Task<LLMDecisionResult?> GetLLMDecisionAsync(
        IReadOnlyList<KeyAction> availableActions,
        CancellationToken ct)
    {
        // Check cache first
        string stateHash = stateSerializer.GetStateHash();
        LLMDecisionResult? cached = cache.Get(stateHash);
        if (cached != null)
        {
            logger.LogDebug("[HybridDecision ] Using cached LLM decision");
            return cached;
        }

        int timeoutMs = flags.HybridLLMDecision?.MaxLatencyMs ?? 2000;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            // Execute through circuit breaker
            LLMDecisionResult result = await circuitBreaker.ExecuteAsync(
                () => ExecuteLLMQueryAsync(availableActions, cts.Token));

            // Cache the result
            int cacheSeconds = flags.HybridLLMDecision?.CacheDecisionsSeconds ?? 5;
            cache.Set(stateHash, result, TimeSpan.FromSeconds(cacheSeconds));

            return result;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("[HybridDecision ] LLM query timed out after {TimeoutMs}ms", timeoutMs);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[HybridDecision ] LLM query failed");
            return null;
        }
    }

    /// <summary>
    /// Executes the actual LLM query.
    /// </summary>
    private async Task<LLMDecisionResult> ExecuteLLMQueryAsync(
        IReadOnlyList<KeyAction> availableActions,
        CancellationToken ct)
    {
        // Get provider from feature flags or use default
        string providerName = flags.HybridLLMDecision?.Provider ?? "openai";
        ILLMClient? client = llmFactory.CreateClient(providerName);
        if (client == null)
        {
            return new LLMDecisionResult(null, "No LLM client available", 0f);
        }

        // Serialize current state
        string stateJson = stateSerializer.SerializeState();

        // Build prompt
        string prompt = BuildDecisionPrompt(stateJson, availableActions);

        // Get LLM response
        string response = await client.CompleteAsync(prompt, ct);

        // Parse response
        LLMResponseParser parser = new();
        LLMDecisionResult? result = parser.ParseDecision(response, availableActions);

        return result ?? new LLMDecisionResult(null, "Failed to parse LLM response", 0f);
    }

    /// <summary>
    /// Builds the prompt for LLM decision making.
    /// </summary>
    private static string BuildDecisionPrompt(string stateJson, IReadOnlyList<KeyAction> availableActions)
    {
        string actionsList = string.Join(
            "\n",
            availableActions.Select((a, i) => $"{i + 1}. {a.Name}: {a.GetType().Name}"));

        return $$"""
You are an AI assistant controlling a World of Warcraft character.
Your task is to select the best action from the available options.

Current Game State:
{{stateJson}}

Available Actions:
{{actionsList}}

Select the best action based on the current game state. Consider:
- Health and mana/resource levels
- Target status and distance
- Number of nearby enemies
- Combat vs. non-combat situations
- Cooldowns and resource availability

Respond with JSON in this exact format:
{
  "actionIndex": <number from 1 to {{availableActions.Count}}>,
  "reasoning": "brief explanation of why this action was chosen"
}

If no action is appropriate, use actionIndex: 0.
""";
    }
}

/// <summary>
/// Result of an LLM decision.
/// </summary>
/// <param name="Action">The selected action, or null if none.</param>
/// <param name="Reasoning">Explanation for the decision.</param>
/// <param name="Confidence">Confidence score (0-1).</param>
public sealed record LLMDecisionResult(KeyAction? Action, string Reasoning, float Confidence);

/// <summary>
/// Simple cache for hybrid decisions.
/// </summary>
internal sealed class HybridDecisionCache
{
    private readonly Dictionary<string, (LLMDecisionResult result, DateTime expiry)> cache = new();
    private readonly TimeSpan defaultTtl = TimeSpan.FromSeconds(5);

    public LLMDecisionResult? Get(string key)
    {
        if (cache.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow < entry.expiry)
            {
                return entry.result;
            }
            cache.Remove(key);
        }
        return null;
    }

    public void Set(string key, LLMDecisionResult result, TimeSpan? ttl = null)
    {
        cache[key] = (result, DateTime.UtcNow.Add(ttl ?? defaultTtl));
    }

    public LLMDecisionResult? GetRecentDecision()
    {
        // Return the most recent decision
        var recent = cache.Values
            .Where(e => DateTime.UtcNow < e.expiry)
            .OrderByDescending(e => e.expiry)
            .FirstOrDefault();

        return recent.result;
    }

    public void Clear()
    {
        cache.Clear();
    }
}
