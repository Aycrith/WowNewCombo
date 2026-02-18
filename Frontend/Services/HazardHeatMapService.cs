using Core.Hazard;

using Microsoft.JSInterop;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Frontend.Services;

public sealed class HazardHeatMapService
{
    private readonly HazardZoneStore hazardStore;
    private readonly IJSRuntime jsRuntime;

    public HazardHeatMapService(HazardZoneStore hazardStore, IJSRuntime jsRuntime)
    {
        this.hazardStore = hazardStore;
        this.jsRuntime = jsRuntime;
    }

    public async Task UpdateHeatMapAsync(int mapId)
    {
        IReadOnlyList<HazardCluster> clusters = hazardStore.GetClustersSnapshot(mapId);

        if (clusters.Count == 0)
        {
            await jsRuntime.InvokeVoidAsync("hazardHeatMap.updateClusters", Array.Empty<HazardClusterPoint>());
            return;
        }

        HazardClusterPoint[] points = new HazardClusterPoint[clusters.Count];
        for (int i = 0; i < clusters.Count; i++)
        {
            HazardCluster c = clusters[i];
            points[i] = new HazardClusterPoint(c.Centroid.X, c.Centroid.Y, c.SeverityScore);
        }

        await jsRuntime.InvokeVoidAsync("hazardHeatMap.updateClusters", points);
    }

    public ValueTask ShowAsync() => jsRuntime.InvokeVoidAsync("hazardHeatMap.show");

    public ValueTask HideAsync() => jsRuntime.InvokeVoidAsync("hazardHeatMap.hide");

    private sealed record HazardClusterPoint(float X, float Y, float SeverityScore);
}

