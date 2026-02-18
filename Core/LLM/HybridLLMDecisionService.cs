using System;
using System.Threading;
using System.Threading.Tasks;

using Core.FeatureFlags;
using Core.Resilience;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.LLM;

/// <summary>
/// Hosted service that manages the Hybrid LLM decision engine lifecycle.
/// Previously also implemented IGoapEventListener - now that concern is handled by HybridLlmEventListener.
/// </summary>
public sealed class HybridLLMDecisionService : BackgroundService
{
    private readonly ILogger<HybridLLMDecisionService> logger;
    private readonly HybridLlmDecisionEngine engine;
    private readonly IOptionsMonitor<FeatureFlagsOptions> featureFlags;

    public HybridLLMDecisionService(
        ILogger<HybridLLMDecisionService> logger,
        HybridLlmDecisionEngine engine,
        IOptionsMonitor<FeatureFlagsOptions> featureFlags)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        this.featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        HybridLLMDecisionOptions options = featureFlags.CurrentValue.HybridLLMDecision;

        if (!options.Enabled)
        {
            logger.LogInformation("[HybridLLM       ] Service disabled by feature flag");
            return;
        }

        logger.LogInformation("[HybridLLM       ] Service started (confidence threshold: {Threshold})", options.ConfidenceThreshold);

        // Keep service alive until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
