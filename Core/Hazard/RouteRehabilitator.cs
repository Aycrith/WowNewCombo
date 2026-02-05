using Core.FeatureFlags;

using Microsoft.Extensions.Logging;

using System.Linq;
using System.Numerics;

namespace Core.Hazard;

public sealed class RouteRehabilitator
{
    private readonly HazardZoneStore store;
    private readonly ILogger<RouteRehabilitator> logger;
    private readonly FeatureFlagService featureFlagService;

    public RouteRehabilitator(
        HazardZoneStore store,
        ILogger<RouteRehabilitator> logger,
        FeatureFlagService featureFlagService)
    {
        this.store = store;
        this.logger = logger;
        this.featureFlagService = featureFlagService;
    }

    public void ReportSuccessfulTraversal(
        Vector3 position,
        int mapId,
        float radius = 20f,
        float severityFactor = 0.95f)
    {
        if (!featureFlagService.IsHazardAvoidanceEnabled)
        {
            return;
        }

        float factor = severityFactor;
        if (factor < 0f)
        {
            factor = 0f;
        }
        if (factor > 1f)
        {
            factor = 1f;
        }

        int adjusted = store.ReduceSeverityNear(position, mapId, radius, factor);
        if (adjusted <= 0)
        {
            return;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "[Rehabilitator     ] Reduced severity for {Count} clusters near {Pos} (mapId={MapId})",
                adjusted,
                position,
                mapId);
        }
    }
}
