using Core.Hazard;

using Microsoft.JSInterop;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Frontend.Services;

public sealed class HazardHeatMapService
{
    private const int MaxRecentEvents = 300;
    private static readonly TimeSpan RecentEventWindow = TimeSpan.FromMinutes(20);

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
        IReadOnlyList<HazardEvent> events = hazardStore.GetEventsSnapshot(mapId);

        if (clusters.Count == 0 && events.Count == 0)
        {
            await jsRuntime.InvokeVoidAsync("hazardHeatMap.updateData", Array.Empty<HazardMapPoint>());
            return;
        }

        List<HazardMapPoint> points = new(clusters.Count + Math.Min(events.Count, MaxRecentEvents));

        for (int i = 0; i < clusters.Count; i++)
        {
            HazardCluster c = clusters[i];
            points.Add(new HazardMapPoint(
                X: c.Centroid.X,
                Y: c.Centroid.Y,
                Intensity: NormalizeClusterIntensity(c.SeverityScore),
                Category: "Cluster"));
        }

        DateTime minTimestamp = DateTime.UtcNow - RecentEventWindow;
        int addedEvents = 0;

        for (int i = events.Count - 1; i >= 0 && addedEvents < MaxRecentEvents; i--)
        {
            HazardEvent e = events[i];
            if (e.Timestamp < minTimestamp)
            {
                continue;
            }

            points.Add(new HazardMapPoint(
                X: e.WorldPosition.X,
                Y: e.WorldPosition.Y,
                Intensity: NormalizeEventIntensity(e),
                Category: "Event"));
            addedEvents++;
        }

        await jsRuntime.InvokeVoidAsync("hazardHeatMap.updateData", points);
    }

    public ValueTask ShowAsync() => jsRuntime.InvokeVoidAsync("hazardHeatMap.show");

    public ValueTask HideAsync() => jsRuntime.InvokeVoidAsync("hazardHeatMap.hide");

    private static float NormalizeClusterIntensity(float severityScore)
    {
        float intensity = severityScore / 10f;
        return Math.Clamp(intensity, 0.25f, 1f);
    }

    private static float NormalizeEventIntensity(HazardEvent hazardEvent)
    {
        float baseIntensity = hazardEvent.Type switch
        {
            HazardEventType.Death => 1f,
            HazardEventType.Stuck => 0.9f,
            HazardEventType.PathfindingFailure => 0.85f,
            HazardEventType.TargetEvade => 0.75f,
            HazardEventType.UnexpectedAggro => 0.7f,
            HazardEventType.ManualMarker => 0.65f,
            _ => 0.6f
        };

        float attemptWeight = MathF.Min(0.15f, hazardEvent.AttemptCount * 0.03f);
        float durationWeight = MathF.Min(0.15f, hazardEvent.DurationMs / 5_000f);

        return Math.Clamp(baseIntensity + attemptWeight + durationWeight, 0.35f, 1f);
    }

    private sealed record HazardMapPoint(float X, float Y, float Intensity, string Category);
}
