using System;
using System.Threading.Tasks;

using Core.FeatureFlags;
using Core.GOAP;

using Microsoft.Extensions.Logging;

namespace Core.LLM;

/// <summary>
/// Event listener adapter that delegates GOAP events to the Hybrid LLM decision service.
/// Separates the IGoapEventListener concern from the hosted service lifecycle.
/// </summary>
public sealed class HybridLlmEventListener : IGoapEventListener, IDisposable
{
    private readonly ILogger<HybridLlmEventListener> logger;
    private readonly HybridLlmDecisionEngine engine;
    private readonly FeatureFlagService? featureFlags;

    public HybridLlmEventListener(
        ILogger<HybridLlmEventListener> logger,
        HybridLlmDecisionEngine engine,
        FeatureFlagService? featureFlags = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        this.featureFlags = featureFlags;
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (!IsEnabled())
            return;

        // Only respond to "NO PLAN" events
        if (!IsNoPlanEvent(e))
            return;

        // Fire-and-forget async query
        _ = Task.Run(async () =>
        {
            try
            {
                await engine.ProcessNoPlanEventAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[HybridLLM-Listener] Error processing GOAP event");
            }
        });
    }

    private bool IsEnabled()
    {
        if (featureFlags == null)
            return true;

        return featureFlags.Current.HybridLLMDecision.Enabled;
    }

    private static bool IsNoPlanEvent(GoapEventArgs e)
    {
        return e is AbortEvent;
    }

    public void Dispose()
    {
        // Engine is managed by DI, not disposed here
    }
}
