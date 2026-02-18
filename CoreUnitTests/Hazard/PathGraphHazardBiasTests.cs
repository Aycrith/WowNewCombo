using Microsoft.Extensions.Logging.Abstractions;

using PPather.Graph;
using PPather.Triangles;
using PPather.Triangles.Data;

using SharedLib;
using SharedLib.Data;

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

using WowTriangles;

using Xunit;

namespace CoreUnitTests.Hazard;

public sealed class PathGraphHazardBiasTests
{
    [Fact]
    public void ScoreSpotAStarWithModelAvoidance_PrefersLowerHazard_WhenGeometryIsEqual()
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            IHazardProvider hazardProvider = new SelectiveHazardProvider(new Vector3(2f, -1f, 0f), hazardCost: 250f);
            PathGraph pathGraph = CreatePathGraph(root, hazardProvider);

            Spot start = new(0f, 0f, 0f);
            Spot destination = new(10f, 0f, 0f);
            Spot hazardous = new(2f, -1f, 0f);
            Spot safe = new(2f, 1f, 0f);

            pathGraph.currentSearchSpot = start;
            start.traceBackDistance = 0f;

            int searchId = 42;
            PriorityQueue<Spot, float> queue = new();

            pathGraph.ScoreSpot_A_Star_With_Model_And_Gradient_Avoidance(hazardous, destination, searchId, queue);
            pathGraph.ScoreSpot_A_Star_With_Model_And_Gradient_Avoidance(safe, destination, searchId, queue);

            float hazardousScore = hazardous.SearchScoreGet(searchId);
            float safeScore = safe.SearchScoreGet(searchId);

            Assert.True(safeScore < hazardousScore);
            Assert.True(queue.TryDequeue(out Spot? first, out _));
            Assert.Same(safe, first);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void ScoreSpotAStarWithModelAvoidance_HazardDelta_MatchesAddedCost()
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WowClassicGrindBot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            const float expectedHazardDelta = 75f;
            IHazardProvider hazardProvider = new SelectiveHazardProvider(new Vector3(2f, -1f, 0f), expectedHazardDelta);
            PathGraph pathGraph = CreatePathGraph(root, hazardProvider);

            Spot start = new(0f, 0f, 0f);
            Spot destination = new(10f, 0f, 0f);
            Spot hazardous = new(2f, -1f, 0f);
            Spot safe = new(2f, 1f, 0f);

            pathGraph.currentSearchSpot = start;
            start.traceBackDistance = 0f;

            int searchId = 43;
            PriorityQueue<Spot, float> queue = new();

            pathGraph.ScoreSpot_A_Star_With_Model_And_Gradient_Avoidance(hazardous, destination, searchId, queue);
            pathGraph.ScoreSpot_A_Star_With_Model_And_Gradient_Avoidance(safe, destination, searchId, queue);

            float hazardousScore = hazardous.SearchScoreGet(searchId);
            float safeScore = safe.SearchScoreGet(searchId);

            float delta = hazardousScore - safeScore;
            Assert.InRange(delta, expectedHazardDelta - 0.001f, expectedHazardDelta + 0.001f);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static PathGraph CreatePathGraph(string root, IHazardProvider hazardProvider)
    {
        if (!ContinentDB.IdToName.ContainsKey(0f))
        {
            ContinentDB.IdToName[0f] = "Azeroth";
        }

        MPQTriangleSupplier supplier =
            // Constructor bypass avoids MPQ/asset setup in unit tests; this is intentionally reflection-fragile.
            (MPQTriangleSupplier)RuntimeHelpers.GetUninitializedObject(typeof(MPQTriangleSupplier));
        ChunkedTriangleCollection triangleWorld = new(NullLogger.Instance, initCapacity: 4, supplier);

        SeedEmptyChunk(triangleWorld, 2f, -1f);
        SeedEmptyChunk(triangleWorld, 2f, 1f);

        DataConfig dataConfig = new()
        {
            Root = root,
            Exp = "wrath"
        };

        return new PathGraph(
            mapId: 0f,
            triangles: triangleWorld,
            logger: NullLogger<PathGraph>.Instance,
            dataConfig: dataConfig,
            hazardProvider: hazardProvider);
    }

    private static void SeedEmptyChunk(ChunkedTriangleCollection triangleWorld, float x, float y)
    {
        ChunkedTriangleCollection.GetGridStartAt(x, y, out int gridX, out int gridY);

        FieldInfo? field = typeof(ChunkedTriangleCollection).GetField("chunks", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        SparseMatrix2D<TriangleCollection>? chunks = field.GetValue(triangleWorld) as SparseMatrix2D<TriangleCollection>;
        Assert.NotNull(chunks);

        if (!chunks.ContainsKey(gridX, gridY))
        {
            // Seed minimal chunk state via private field access so path scoring can run in isolation.
            chunks.Add(gridX, gridY, new TriangleCollection(NullLogger.Instance));
        }
    }

    private sealed class SelectiveHazardProvider(Vector3 hazardousLocation, float hazardCost) : IHazardProvider
    {
        public float GetHazardCost(Vector3 position, float mapId)
        {
            return Vector3.Distance(position, hazardousLocation) < 0.01f
                ? hazardCost
                : 0f;
        }
    }
}
