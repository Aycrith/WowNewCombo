using Core.FeatureFlags;

using Microsoft.Extensions.Logging;

using SharedLib;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

namespace Core.Hazard;

public sealed class HazardZoneStore : IHazardProvider, IDisposable
{
    private const float ChunkSize = 100f;

    private readonly ILogger<HazardZoneStore> logger;
    private readonly FeatureFlagService featureFlagService;

    private readonly ConcurrentDictionary<int, ChunkedHazardMap> mapStores = new();

    private volatile bool enabled;
    private volatile int maxEventsBeforePrune;
    private volatile float hazardCostMultiplier;

    public HazardZoneStore(ILogger<HazardZoneStore> logger, FeatureFlagService featureFlagService)
    {
        this.logger = logger;
        this.featureFlagService = featureFlagService;

        ApplyFlags(featureFlagService.Current);
        featureFlagService.OnFlagsChanged += ApplyFlags;
    }

    public void Dispose()
    {
        featureFlagService.OnFlagsChanged -= ApplyFlags;
    }

    private void ApplyFlags(FeatureFlagsOptions options)
    {
        enabled = options.HazardAvoidance.Enabled;
        maxEventsBeforePrune = Math.Max(0, options.HazardAvoidance.MaxEventsBeforePrune);
        hazardCostMultiplier = MathF.Max(0f, options.HazardAvoidance.HazardCostMultiplier);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "[HazardStore       ] Enabled={Enabled} Eps={Eps} MinPts={MinPts} Mult={Mult} MaxEvents={Max}",
                enabled,
                options.HazardAvoidance.DBSCANEpsilon,
                options.HazardAvoidance.DBSCANMinPoints,
                hazardCostMultiplier,
                maxEventsBeforePrune);
        }
    }

    public IReadOnlyList<int> GetKnownMapIds()
    {
        return mapStores.Keys.Count == 0
            ? []
            : [.. mapStores.Keys];
    }

    public void AddEvent(HazardEvent hazardEvent)
    {
        if (!enabled)
        {
            return;
        }

        ChunkedHazardMap mapStore = mapStores.GetOrAdd(
            hazardEvent.MapId,
            static _ => new ChunkedHazardMap());

        mapStore.AddEvent(hazardEvent, maxEventsBeforePrune);
    }

    public void AddEvents(int mapId, IReadOnlyList<HazardEvent> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        ChunkedHazardMap mapStore = mapStores.GetOrAdd(mapId, static _ => new ChunkedHazardMap());
        mapStore.AddEvents(events, maxEventsBeforePrune);
    }

    public IReadOnlyList<HazardEvent> GetEventsSnapshot(int mapId)
    {
        if (!mapStores.TryGetValue(mapId, out ChunkedHazardMap? mapStore))
        {
            return [];
        }

        return mapStore.GetEventsSnapshot();
    }

    public IReadOnlyList<HazardCluster> GetClustersSnapshot(int mapId)
    {
        if (!mapStores.TryGetValue(mapId, out ChunkedHazardMap? mapStore))
        {
            return [];
        }

        return mapStore.GetClustersSnapshot();
    }

    public void ReplaceClusters(int mapId, IReadOnlyList<HazardCluster> clusters)
    {
        ChunkedHazardMap mapStore = mapStores.GetOrAdd(mapId, static _ => new ChunkedHazardMap());
        mapStore.ReplaceClusters(clusters);
    }

    public float GetHazardCost(Vector3 position, float mapId)
    {
        if (!enabled)
        {
            return 0f;
        }

        int map = (int)mapId;
        if (!mapStores.TryGetValue(map, out ChunkedHazardMap? mapStore))
        {
            return 0f;
        }

        float rawCost = mapStore.GetHazardCost(position);
        return rawCost <= 0f
            ? 0f
            : rawCost * hazardCostMultiplier;
    }

    private sealed class ChunkedHazardMap
    {
        private readonly object gate = new();
        private readonly List<HazardEvent> events = [];

        private HazardCluster[] clusters = [];
        private Dictionary<(int, int), HazardCluster[]> chunks = new();

        public void AddEvent(HazardEvent hazardEvent, int maxEventsBeforePrune)
        {
            lock (gate)
            {
                events.Add(hazardEvent);
                PruneIfNeeded(maxEventsBeforePrune);
            }
        }

        public void AddEvents(IReadOnlyList<HazardEvent> newEvents, int maxEventsBeforePrune)
        {
            lock (gate)
            {
                for (int i = 0; i < newEvents.Count; i++)
                {
                    events.Add(newEvents[i]);
                }

                PruneIfNeeded(maxEventsBeforePrune);
            }
        }

        private void PruneIfNeeded(int maxEventsBeforePrune)
        {
            if (maxEventsBeforePrune <= 0)
            {
                return;
            }

            int overBy = events.Count - maxEventsBeforePrune;
            if (overBy <= 0)
            {
                return;
            }

            events.RemoveRange(0, overBy);
        }

        public IReadOnlyList<HazardEvent> GetEventsSnapshot()
        {
            lock (gate)
            {
                return events.Count == 0
                    ? []
                    : [.. events];
            }
        }

        public IReadOnlyList<HazardCluster> GetClustersSnapshot()
        {
            lock (gate)
            {
                return clusters.Length == 0
                    ? []
                    : [.. clusters];
            }
        }

        public void ReplaceClusters(IReadOnlyList<HazardCluster> newClusters)
        {
            lock (gate)
            {
                clusters = newClusters.Count == 0
                    ? []
                    : [.. newClusters];

                chunks = BuildChunkIndex(clusters);
            }
        }

        public float GetHazardCost(Vector3 position)
        {
            (int, int) key = GetChunkKey(position);

            HazardCluster[] candidates;
            lock (gate)
            {
                if (!chunks.TryGetValue(key, out HazardCluster[]? list) || list.Length == 0)
                {
                    return 0f;
                }

                candidates = list;
            }

            float best = 0f;

            for (int i = 0; i < candidates.Length; i++)
            {
                HazardCluster cluster = candidates[i];
                float radius = MathF.Max(1f, cluster.Radius);
                float distance = Vector3.Distance(position, cluster.Centroid);

                if (distance > radius)
                {
                    continue;
                }

                float falloff = 1.0f - (distance / radius);
                float score = cluster.SeverityScore * MathF.Max(0f, falloff);
                if (score > best)
                {
                    best = score;
                }
            }

            return best;
        }

        private static (int, int) GetChunkKey(Vector3 position)
        {
            int cx = (int)MathF.Floor(position.X / ChunkSize);
            int cy = (int)MathF.Floor(position.Y / ChunkSize);
            return (cx, cy);
        }

        private static Dictionary<(int, int), HazardCluster[]> BuildChunkIndex(HazardCluster[] clusters)
        {
            if (clusters.Length == 0)
            {
                return new Dictionary<(int, int), HazardCluster[]>();
            }

            Dictionary<(int, int), List<HazardCluster>> index = new(clusters.Length * 2);

            for (int i = 0; i < clusters.Length; i++)
            {
                HazardCluster cluster = clusters[i];
                float r = MathF.Max(1f, cluster.Radius);

                float minX = cluster.Centroid.X - r;
                float maxX = cluster.Centroid.X + r;
                float minY = cluster.Centroid.Y - r;
                float maxY = cluster.Centroid.Y + r;

                int minCx = (int)MathF.Floor(minX / ChunkSize);
                int maxCx = (int)MathF.Floor(maxX / ChunkSize);
                int minCy = (int)MathF.Floor(minY / ChunkSize);
                int maxCy = (int)MathF.Floor(maxY / ChunkSize);

                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    for (int cy = minCy; cy <= maxCy; cy++)
                    {
                        (int, int) key = (cx, cy);
                        if (!index.TryGetValue(key, out List<HazardCluster>? list))
                        {
                            list = [];
                            index.Add(key, list);
                        }

                        list.Add(cluster);
                    }
                }
            }

            Dictionary<(int, int), HazardCluster[]> frozen = new(index.Count);
            foreach (KeyValuePair<(int, int), List<HazardCluster>> kvp in index)
            {
                frozen.Add(kvp.Key, [.. kvp.Value]);
            }

            return frozen;
        }
    }
}

