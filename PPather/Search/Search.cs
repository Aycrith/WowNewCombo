using Microsoft.Extensions.Logging;
using PPather.Graph;
using System;
using WowTriangles;
using System.Numerics;
using SharedLib.Extensions;

namespace PPather
{
    public sealed class Search
    {
        public PathGraph PathGraph { get; set; }
        public float MapId { get; set; }

        private readonly DataConfig dataConfig;
        private readonly ILogger logger;

        public Vector4 locationFrom { get; set; }
        public Vector4 locationTo { get; set; }

        private const float toonHeight = 2.0f;
        private const float toonSize = 0.5f;

        public Search(float mapId, ILogger logger, DataConfig dataConfig)
        {
            this.logger = logger;
            this.MapId = mapId;
            this.dataConfig = dataConfig;

            CreatePathGraph(mapId);
        }

        public Vector4 CreateWorldLocation(float x, float y, float z, int mapId)
        {
            float zTerrain = GetZValueAt(x, y, z,
                new int[] { ChunkedTriangleCollection.TriangleTerrain });

            float zWater = GetZValueAt(x, y, z,
                new int[] { ChunkedTriangleCollection.TriangleFlagDeepWater });

            if (zWater > zTerrain)
            {
                return new Vector4(x, y, zWater, mapId);
            }

            float zModel = GetZValueAt(x, y, z,
            new int[] { ChunkedTriangleCollection.TriangleFlagModel, ChunkedTriangleCollection.TriangleFlagObject });

            if (zModel != float.MinValue)
            {
                if (MathF.Abs(zModel - zTerrain) > toonHeight / 2)
                {
                    return new Vector4(x, y, zTerrain, mapId);
                }
                else
                {
                    return new Vector4(x, y, zModel, mapId);
                }
            }

            return new Vector4(x, y, zTerrain, mapId);
        }

        private float GetZValueAt(float x, float y, float z, int[] allowedModels)
        {
            float min = -1000;
            float max = 2000;

            if (z != 0)
            {
                min = z - 1000;
                max = z + 2000;
            }

            if (PathGraph.triangleWorld.FindStandableAt1(x, y, min, max, out float z1, out int flags1, toonHeight, toonSize, true, allowedModels))
            {
                return z1;
            }

            return float.MinValue;
        }

        public void CreatePathGraph(float mapId)
        {
            this.MapId = mapId;

            MPQTriangleSupplier mpq = new(logger, dataConfig, mapId);
            ChunkedTriangleCollection triangleWorld = new(logger, 64, mpq);
            PathGraph = new(mapId, triangleWorld, logger, dataConfig);
        }

        public Path DoSearch(PathGraph.eSearchScoreSpot searchType)
        {
            PathGraph.SearchEnabled = true;

            // tell the pathgraph which type of search to do
            PathGraph.searchScoreSpot = searchType;

            try
            {
                return PathGraph.CreatePath(locationFrom.AsVector3(), locationTo.AsVector3(), 5, null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                return null;
            }
        }
    }
}