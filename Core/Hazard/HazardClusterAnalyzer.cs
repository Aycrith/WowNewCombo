using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Core.Hazard;

public sealed class HazardClusterAnalyzer
{
    private readonly ILogger<HazardClusterAnalyzer> logger;

    public HazardClusterAnalyzer(ILogger<HazardClusterAnalyzer> logger)
    {
        this.logger = logger;
    }

    public List<HazardCluster> RunDBSCAN(
        IReadOnlyList<HazardEvent> events,
        float epsilon,
        int minPoints,
        int halfLifeDays)
    {
        if (minPoints <= 0 || events.Count < minPoints)
        {
            return [];
        }

        float cellSize = MathF.Max(1.0f, epsilon);
        Dictionary<(int, int), List<int>> grid = BuildGridIndex(events, cellSize);

        int[] labels = new int[events.Count];
        Array.Fill(labels, -1); // -1 = undefined, 0 = noise, >0 = clusterId

        int clusterId = 0;

        for (int i = 0; i < events.Count; i++)
        {
            if (labels[i] != -1)
            {
                continue;
            }

            List<int> neighbors = RangeQuery(events, grid, i, epsilon, cellSize);
            if (neighbors.Count < minPoints)
            {
                labels[i] = 0;
                continue;
            }

            clusterId++;
            labels[i] = clusterId;
            ExpandCluster(events, grid, labels, neighbors, clusterId, epsilon, minPoints, cellSize);
        }

        List<HazardCluster> clusters = BuildClusters(events, labels, clusterId, epsilon, halfLifeDays);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "[HazardAnalyzer    ] Clustered {Events} events into {Clusters} clusters (eps={Eps}, minPts={MinPts})",
                events.Count, clusters.Count, epsilon, minPoints);
        }

        return clusters;
    }

    private static Dictionary<(int, int), List<int>> BuildGridIndex(IReadOnlyList<HazardEvent> events, float cellSize)
    {
        Dictionary<(int, int), List<int>> grid = new(Math.Min(events.Count, 4096));

        for (int i = 0; i < events.Count; i++)
        {
            Vector3 p = events[i].WorldPosition;
            (int, int) key = GetCellKey(p, cellSize);

            if (!grid.TryGetValue(key, out List<int>? list))
            {
                list = [];
                grid.Add(key, list);
            }

            list.Add(i);
        }

        return grid;
    }

    private static (int, int) GetCellKey(Vector3 p, float cellSize)
    {
        int cx = (int)MathF.Floor(p.X / cellSize);
        int cy = (int)MathF.Floor(p.Y / cellSize);
        return (cx, cy);
    }

    private static List<int> RangeQuery(
        IReadOnlyList<HazardEvent> events,
        Dictionary<(int, int), List<int>> grid,
        int centerIndex,
        float eps,
        float cellSize)
    {
        Vector3 center = events[centerIndex].WorldPosition;
        (int cx, int cy) = GetCellKey(center, cellSize);

        List<int> neighbors = [];

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                (int, int) key = (cx + dx, cy + dy);
                if (!grid.TryGetValue(key, out List<int>? candidates))
                {
                    continue;
                }

                for (int i = 0; i < candidates.Count; i++)
                {
                    int idx = candidates[i];
                    float distance = Vector3.Distance(events[idx].WorldPosition, center);
                    if (distance <= eps)
                    {
                        neighbors.Add(idx);
                    }
                }
            }
        }

        return neighbors;
    }

    private static void ExpandCluster(
        IReadOnlyList<HazardEvent> events,
        Dictionary<(int, int), List<int>> grid,
        int[] labels,
        List<int> neighbors,
        int clusterId,
        float eps,
        int minPoints,
        float cellSize)
    {
        Queue<int> seedSet = new(neighbors);

        while (seedSet.Count > 0)
        {
            int q = seedSet.Dequeue();

            if (labels[q] == 0)
            {
                labels[q] = clusterId; // noise -> border
            }

            if (labels[q] != -1)
            {
                continue;
            }

            labels[q] = clusterId;

            List<int> qNeighbors = RangeQuery(events, grid, q, eps, cellSize);
            if (qNeighbors.Count >= minPoints)
            {
                for (int i = 0; i < qNeighbors.Count; i++)
                {
                    int n = qNeighbors[i];
                    if (labels[n] == -1 || labels[n] == 0)
                    {
                        seedSet.Enqueue(n);
                    }
                }
            }
        }
    }

    private static List<HazardCluster> BuildClusters(
        IReadOnlyList<HazardEvent> events,
        int[] labels,
        int maxClusterId,
        float epsilon,
        int halfLifeDays)
    {
        List<HazardCluster> clusters = [];

        for (int cid = 1; cid <= maxClusterId; cid++)
        {
            List<HazardEvent> clusterEvents = [];

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == cid)
                {
                    clusterEvents.Add(events[i]);
                }
            }

            if (clusterEvents.Count == 0)
            {
                continue;
            }

            Vector3 centroid = new(
                clusterEvents.Average(e => e.WorldPosition.X),
                clusterEvents.Average(e => e.WorldPosition.Y),
                clusterEvents.Average(e => e.WorldPosition.Z));

            float radius = 0f;
            for (int i = 0; i < clusterEvents.Count; i++)
            {
                float d = Vector3.Distance(clusterEvents[i].WorldPosition, centroid);
                if (d > radius)
                {
                    radius = d;
                }
            }

            HazardCluster cluster = new()
            {
                Centroid = centroid,
                Radius = MathF.Max(radius, epsilon),
                Events = clusterEvents
            };

            cluster.SeverityScore = HazardAnalytics.CalculateSeverityScore(cluster, halfLifeDays);
            clusters.Add(cluster);
        }

        return clusters;
    }
}

