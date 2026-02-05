using Microsoft.Extensions.Logging;

using System.Linq;
using System.Numerics;

namespace Core.Hazard;

public sealed class RouteRehabilitator
{
    private readonly HazardZoneStore store;
    private readonly ILogger<RouteRehabilitator> logger;

    public RouteRehabilitator(HazardZoneStore store, ILogger<RouteRehabilitator> logger)
    {
        this.store = store;
        this.logger = logger;
    }

    public void ReportSuccessfulTraversal(Vector3 position, int mapId, float radius = 20f)
    {
        var clusters = store.GetClustersSnapshot(mapId)
            .Where(c => c.ContainsPosition(position, safetyMargin: radius))
            .ToList();

        for (int i = 0; i < clusters.Count; i++)
        {
            HazardCluster cluster = clusters[i];
            cluster.SeverityScore *= 0.8f;

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "[Rehabilitator     ] Reduced severity for cluster {Id} at {Pos}",
                    cluster.Id, cluster.Centroid);
            }
        }
    }
}

