using Microsoft.Extensions.Logging;

using PPather.Graph;

using System;
using System.Numerics;

using WowTriangles;

namespace PPather;

public sealed class Search
{
    public PathGraph PathGraph { get; set; }
    public float MapId { get; set; }

    private readonly DataConfig dataConfig;
    private readonly ILogger logger;

    public Vector4 From { get; set; }
    public Vector4 Target { get; set; }

    private const float toonHeight = PathGraph.toonHeight;
    private const float toonSize = PathGraph.toonSize;

    private const float howClose = PathGraph.MaxStepLength;

    public Search(float mapId, ILogger logger, DataConfig dataConfig)
    {
        this.logger = logger;
        this.MapId = mapId;
        this.dataConfig = dataConfig;

        CreatePathGraph(mapId);
    }

    public void Clear()
    {
        MapId = 0;
        PathGraph.Clear();
        PathGraph = null;
    }

    private const float bigExtend = 2000;
    private const float smallExtend = 2 * toonHeight;

    public Vector4 CreateWorldLocation(float x, float y, float z, int mapId, bool? startIndoors)
    {
        float min_z = z == 0 ? z - bigExtend : z - smallExtend;
        float max_z = z == 0 ? z + bigExtend : z + smallExtend;

        float zTerrain = GetZValueAt(x, y, min_z, max_z, TriangleType.Terrain);
        float zWater = GetZValueAt(x, y, min_z, max_z, TriangleType.Water);

        if (zWater > zTerrain)
        {
            return new Vector4(x, y, zWater, mapId);
        }

        float zModel = GetZValueAt(x, y, min_z, max_z, TriangleType.Model | TriangleType.Object);

        if (zModel != float.MinValue)
        {
            bool bModel = PathGraph.triangleWorld.IsSpotBlocked(x, y, zModel, toonHeight, 2 * toonSize);
            bool bTerrain = PathGraph.triangleWorld.IsSpotBlocked(x, y, zTerrain, toonHeight, 2 * toonSize);

            if (startIndoors.HasValue)
            {
                float preferred = startIndoors.Value
                    ? MathF.Min(zModel, zTerrain)
                    : MathF.Max(zModel, zTerrain);

                float other = (preferred == zModel) ? zTerrain : zModel;

                bool bPreferred = (preferred == zModel) ? bModel : bTerrain;
                bool bOther = (preferred == zModel) ? bTerrain : bModel;

                if (!bPreferred)
                {
                    return new Vector4(x, y, preferred, mapId);
                }
                else if (!bOther)
                {
                    return new Vector4(x, y, other, mapId);
                }
                else
                {
                    return new Vector4(x, y, preferred, mapId); // both blocked, still prefer intended
                }
            }
            else
            {
                // Legacy logic
                if (!bTerrain &&
                    zTerrain != float.MinValue &&
                    MathF.Abs(zModel - zTerrain) > PathGraph.toonHeightHalf)
                {
                    return new Vector4(x, y, zTerrain, mapId);
                }
                else
                {
                    return new Vector4(x, y, zModel, mapId);
                }
            }
        }
        else
        {
            // If zTerrain is not found, try with bigger extend
            if (zTerrain == float.MinValue)
            {
                min_z = z - bigExtend;
                max_z = z + bigExtend;
                zTerrain = GetZValueAt(x, y, min_z, max_z, TriangleType.Terrain);
            }

            return new Vector4(x, y, zTerrain, mapId);
        }
    }

    private float GetZValueAt(float x, float y, float min_z, float max_z, TriangleType allowedFlags)
    {
        return PathGraph.triangleWorld.FindStandableAt1
            (x, y, min_z, max_z, out float z1, out _, toonHeight, toonSize, true, allowedFlags)
            ? z1
            : float.MinValue;
    }

    public void CreatePathGraph(float mapId)
    {
        this.MapId = mapId;

        MPQTriangleSupplier mpq = new(logger, dataConfig, mapId);
        ChunkedTriangleCollection triangleWorld = new(logger, 64, mpq);
        PathGraph = new(mapId, triangleWorld, logger, dataConfig);
    }

    public Path DoSearch(SearchStrategy searchScoreSpot)
    {
        try
        {
            return PathGraph.CreatePath(From.AsVector3(), Target.AsVector3(), searchScoreSpot, howClose);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
        }
        return null;
    }

    public (int, float) GetAreaIdAndZ(Vector3 location)
    {
        return PathGraph.triangleWorld.GetAreaIdAndZ(location);
    }
}