using PPather.Graph;
using System;
using System.Collections.Generic;
using WowTriangles;
using PPather.Data;
using SharedLib;
using SharedLib.Data;
using Microsoft.Extensions.Logging;
using System.Numerics;

namespace PPather;

public sealed class PPatherService
{
    private readonly ILogger<PPatherService> logger;
    private readonly DataConfig dataConfig;
    private readonly WorldMapAreaDB worldMapAreaDB;

    public event Action SearchBegin;
    public event Action<Path> OnPathCreated;
    public event Action<ChunkEventArgs> OnChunkAdded;

    public Action<LinesEventArgs> OnLinesAdded;
    public Action<SphereEventArgs> OnSphereAdded;

    private Search search { get; set; }

    public Vector4 SearchFrom => search.locationFrom;
    public Vector4 SearchTo => search.locationTo;
    public Vector3 ClosestLocation => search?.PathGraph?.ClosestSpot?.Loc ?? Vector3.Zero;
    public Vector3 PeekLocation => search?.PathGraph?.PeekSpot?.Loc ?? Vector3.Zero;

    public Vector3[] TestPoints => search?.PathGraph?.TestPoints ?? Array.Empty<Vector3>();

    public PPatherService(ILogger<PPatherService> logger, DataConfig dataConfig, WorldMapAreaDB worldMapAreaDB)
    {
        this.dataConfig = dataConfig;
        this.logger = logger;
        this.worldMapAreaDB = worldMapAreaDB;
        ContinentDB.Init(worldMapAreaDB.Values);

        MPQSelfTest();
    }

    public void Reset()
    {
        if (search == null)
            return;

        search.Clear();
        search = null;
    }

    private void Initialise(float mapId)
    {
        if (search != null && mapId == search.MapId)
            return;

        search = new Search(mapId, logger, dataConfig);
        search.PathGraph.triangleWorld.NotifyChunkAdded = ChunkAdded;
    }

    public bool MPQSelfTest()
    {
        string[] mpqFiles = MPQTriangleSupplier.GetArchiveNames(dataConfig);
        if (mpqFiles.Length == 0)
        {
            logger.LogInformation("No MPQ files found, refer to the Readme to download them!");
            return false;
        }

        logger.LogInformation("MPQ files exist.");
        return true;
    }

    public TriangleCollection GetChunkAt(int grid_x, int grid_y)
    {
        return search.PathGraph.triangleWorld.GetChunkAt(grid_x, grid_y);
    }

    public void ChunkAdded(ChunkEventArgs e)
    {
        OnChunkAdded?.Invoke(e);
    }

    public Vector4 ToWorld(int uiMap, float mapX, float mapY, float z = 0)
    {
        if (!worldMapAreaDB.TryGet(uiMap, out WorldMapArea wma))
            return Vector4.Zero;

        float worldX = wma.ToWorldX(mapY);
        float worldY = wma.ToWorldY(mapX);

        Initialise(wma.MapID);

        return search.CreateWorldLocation(worldX, worldY, z, wma.MapID);
    }

    public Vector4 ToWorldZ(int uiMap, float x, float y, float z)
    {
        if (!worldMapAreaDB.TryGet(uiMap, out WorldMapArea wma))
            return Vector4.Zero;

        Initialise(wma.MapID);

        return search.CreateWorldLocation(x, y, z, wma.MapID);
    }

    public Vector3 ToLocal(Vector3 world, float mapId, int uiMapId)
    {
        WorldMapArea wma = worldMapAreaDB.GetWorldMapArea(world.X, world.Y, (int)mapId, uiMapId);
        return new Vector3(wma.ToMapY(world.Y), wma.ToMapX(world.X), world.Z);
    }

    public Path DoSearch(PathGraph.eSearchScoreSpot searchType)
    {
        SearchBegin?.Invoke();
        var path = search.DoSearch(searchType);
        OnPathCreated?.Invoke(path);
        return path;
    }

    public void Save()
    {
        search.PathGraph.Save();
    }

    public void SetLocations(Vector4 from, Vector4 to)
    {
        Initialise(from.W);

        search.locationFrom = from;
        search.locationTo = to;
    }

    public List<Spot> GetCurrentSearchPath()
    {
        if (search == null || search.PathGraph == null)
        {
            return null;
        }

        return search.PathGraph.CurrentSearchPath();
    }

    public void DrawPath(float mapId, ReadOnlySpan<Vector3> path)
    {
        Vector4 from = new(path[0], mapId);
        Vector4 to = new(path[^1], mapId);

        SetLocations(from, to);

        if (search.PathGraph == null)
        {
            search.CreatePathGraph(mapId);
        }

        List<Spot> spots = new();
        for (int i = 0; i < path.Length; i++)
        {
            Spot spot = new(path[i]);
            spots.Add(spot);
            search.PathGraph.CreateSpotsAroundSpot(spot, false);
        }

        OnPathCreated?.Invoke(new(spots));
    }
}