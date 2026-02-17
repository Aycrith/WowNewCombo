using System;

using Core.FeatureFlags;
using Core.GOAP;
using Core.GoalsComponent;

using Microsoft.Extensions.Logging;

namespace Core.Analytics;

/// <summary>
/// Event listener adapter that delegates GOAP events to the Failure Analytics engine.
/// Separates the IGoapEventListener concern from the hosted service lifecycle.
/// </summary>
public sealed class FailureAnalyticsEventListener : IGoapEventListener, IDisposable
{
    private readonly ILogger<FailureAnalyticsEventListener> logger;
    private readonly FailureAnalyticsEngine engine;
    private readonly FeatureFlagService featureFlags;
    private readonly StuckDetector? stuckDetector;

    public FailureAnalyticsEventListener(
        ILogger<FailureAnalyticsEventListener> logger,
        FailureAnalyticsEngine engine,
        FeatureFlagService featureFlags,
        StuckDetector? stuckDetector = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        this.featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        this.stuckDetector = stuckDetector;

        // Subscribe to stuck events if detector available
        if (stuckDetector != null)
        {
            stuckDetector.OnStuckDetected += OnStuckDetected;
        }
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (!featureFlags.Current.FailureAnalytics.Enabled)
            return;

        switch (e)
        {
            case AbortEvent:
                engine.RecordNoPlanFailure("GOAP could not find valid plan");
                break;
        }
    }

    private void OnStuckDetected(StuckEventData data)
    {
        if (!featureFlags.Current.FailureAnalytics.Enabled)
            return;

        engine.RecordStuckEvent(data);
    }

    public void Dispose()
    {
        if (stuckDetector != null)
        {
            stuckDetector.OnStuckDetected -= OnStuckDetected;
        }

        // Engine is managed by DI, flushed/disposed separately
        engine.FlushToDisk();
    }
}
