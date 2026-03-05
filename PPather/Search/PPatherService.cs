using Microsoft.Extensions.Logging;

using PPather.Data;
using PPather.Graph;

using SharedLib;
using SharedLib.Data;

using System;
using System.Collections.Generic;
using System.Linq;
using static System.Diagnostics.Stopwatch;
using System.Numerics;

using WowTriangles;

namespace PPather;

public sealed class PPatherService
{
    private readonly object gate = new();
    private readonly ILogger<PPatherService> logger;
    private readonly DataConfig dataConfig;
    private readonly WorldMapAreaDB worldMapAreaDB;
    private readonly IHazardProvider hazardProvider;

    public event Action SearchBegin;
    public event Action<Path> OnPathCreated;
    public event Action<ChunkEventArgs> OnChunkAdded;

    public Action<LinesEventArgs> OnLinesAdded;
    public Action<SphereEventArgs> OnSphereAdded;

    private Search search { get; set; }

    public bool Initialised
    {
        get
        {
            lock (gate)
            {
                return search != null;
            }
        }
    }

    public bool IsSearching { get; set; }

    public Vector4 SearchFrom
    {
        get
        {
            lock (gate)
            {
                return search?.From ?? Vector4.Zero;
            }
        }
    }

    public Vector4 SearchTo
    {
        get
        {
            lock (gate)
            {
                return search?.Target ?? Vector4.Zero;
            }
        }
    }

    public Vector3 ClosestLocation
    {
        get
        {
            lock (gate)
            {
                return search?.PathGraph?.ClosestSpot?.Loc ?? Vector3.Zero;
            }
        }
    }

    public Vector3 PeekLocation
    {
        get
        {
            lock (gate)
            {
                return search?.PathGraph?.PeekSpot?.Loc ?? Vector3.Zero;
            }
        }
    }

    public Vector3[] TestPoints
    {
        get
        {
            lock (gate)
            {
                if (search?.PathGraph?.TestPoints == null || search.PathGraph.TestPoints.Count == 0)
                {
                    return [];
                }

                return search.PathGraph.TestPoints.ToArray();
            }
        }
    }

    public Vector3[] BlockedPoints
    {
        get
        {
            lock (gate)
            {
                if (search?.PathGraph?.BlockedPoints == null || search.PathGraph.BlockedPoints.Count == 0)
                {
                    return [];
                }

                return search.PathGraph.BlockedPoints.ToArray();
            }
        }
    }

    public PPatherService(
        ILogger<PPatherService> logger,
        DataConfig dataConfig,
        WorldMapAreaDB worldMapAreaDB,
        IHazardProvider hazardProvider = null)
    {
        this.dataConfig = dataConfig;
        this.logger = logger;
        this.worldMapAreaDB = worldMapAreaDB;
        this.hazardProvider = hazardProvider;
        ContinentDB.Init(worldMapAreaDB.Values);

        MPQSelfTest();
    }

    public void Reset()
    {
        lock (gate)
        {
            if (search == null)
            {
                return;
            }

            search.Clear();
            search = null;
        }
    }

    public void Initialise(float mapId)
    {
        lock (gate)
        {
            if (search != null && mapId == search.MapId)
            {
                return;
            }

            if (search != null && mapId != search.MapId)
            {
                search.Clear();
                search = null;
            }

            search = hazardProvider != null
                ? new Search(mapId, logger, dataConfig, hazardProvider)
                : new Search(mapId, logger, dataConfig);

            search.PathGraph.triangleWorld.NotifyChunkAdded = ChunkAdded;
        }
    }

    public bool MPQSelfTest()
    {
        string[] mpqFiles = MPQTriangleSupplier.GetArchiveNames(dataConfig);
        if (mpqFiles.Length == 0)
        {
            logger.LogInformation("No MPQ files found, refer to the Readme to download them!");
            return false;
        }

        logger.LogInformation($"MPQ files exist. {string.Join(' ', mpqFiles)}");
        return true;
    }

    public TriangleCollection GetChunkAt(int grid_x, int grid_y)
    {
        lock (gate)
        {
            if (search == null)
            {
                throw new InvalidOperationException("PPatherService is not initialised");
            }

            return search.PathGraph.triangleWorld.GetChunkAt(grid_x, grid_y);
        }
    }

    public void ChunkAdded(ChunkEventArgs e)
    {
        OnChunkAdded?.Invoke(e);
    }

    public Vector4[] CreateLocations(LineArgs lines)
    {
        var result = new Vector4[lines.Spots.Length];
        for (int i = 0; i < result.Length; i++)
        {
            Vector3 spot = lines.Spots[i];
            result[i] = ToWorld(lines.MapId, spot.X, spot.Y, spot.Z);
        }

        return result;
    }

    public Vector4 ToWorld(int uiMap, float mapX, float mapY, float z = 0)
    {
        if (!worldMapAreaDB.TryGet(uiMap, out WorldMapArea wma))
            return Vector4.Zero;

        float worldX = wma.ToWorldX(mapY);
        float worldY = wma.ToWorldY(mapX);

        lock (gate)
        {
            Initialise(wma.MapID);
            return search!.CreateWorldLocation(worldX, worldY, z, wma.MapID, null);
        }
    }

    public Vector4 ToWorldZ(int uiMap, float x, float y, float z, bool? startIndoors = null)
    {
        if (!worldMapAreaDB.TryGet(uiMap, out WorldMapArea wma))
            return Vector4.Zero;

        lock (gate)
        {
            Initialise(wma.MapID);
            return search!.CreateWorldLocation(x, y, z, wma.MapID, startIndoors);
        }
    }

    public int GetMapId(int uiMap)
    {
        return worldMapAreaDB.GetMapId(uiMap);
    }

    public Vector3 ToLocal(Vector3 world, float mapId, int uiMapId)
    {
        WorldMapArea wma = worldMapAreaDB.GetWorldMapArea(world.X, world.Y, (int)mapId, uiMapId);
        return new Vector3(wma.ToMapY(world.Y), wma.ToMapX(world.X), world.Z);
    }

    public Path DoSearch(SearchStrategy searchType)
    {
        SearchBegin?.Invoke();

        Path path;
        lock (gate)
        {
            if (search == null)
            {
                throw new InvalidOperationException("PPatherService is not initialised");
            }

            IsSearching = true;
            path = search.DoSearch(searchType);
            IsSearching = false;
        }

        OnPathCreated?.Invoke(path);
        return path;
    }

    public void Save()
    {
        lock (gate)
        {
            if (search == null)
            {
                return;
            }

            long timestamp = GetTimestamp();
            search.PathGraph.Save();

            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace($"Saved GraphChunks {GetElapsedTime(timestamp).TotalMilliseconds} ms");
        }
    }

    public void SetLocations(Vector4 from, Vector4 to)
    {
        lock (gate)
        {
            Initialise(from.W);

            search!.From = from;
            search.Target = to;
        }
    }

    public List<Vector3> GetCurrentSearchPath()
    {
        lock (gate)
        {
            return search == null || search.PathGraph == null
                ? []
                : search.PathGraph.CurrentSearchPath();
        }
    }

    public float TransformMapToWorld(int uiMapId, Vector3[] path)
    {
        float mapId = -1;
        for (int i = 0; i < path.Length; i++)
        {
            Vector3 p = path[i];
            if (p.Z != 0)
            {
                mapId = GetMapId(uiMapId);
                break;
            }

            Vector4 world = ToWorld(uiMapId, p.X, p.Y, p.Z);
            path[i] = world.AsVector3();
            mapId = world.W;
        }

        return mapId;
    }

    public void DrawPath(float mapId, ReadOnlySpan<Vector3> path)
    {
        Path created;
        lock (gate)
        {
            Vector4 from = new(path[0], mapId);
            Vector4 to = new(path[^1], mapId);

            SetLocations(from, to);

            if (search!.PathGraph == null)
            {
                search.CreatePathGraph(mapId);
            }

            List<Spot> spots = new(path.Length);
            for (int i = 0; i < path.Length; i++)
            {
                Spot spot = new(path[i]);
                spots.Add(spot);
                search.PathGraph.CreateSpotsAroundSpot(spot, false, spot);
            }

            created = new Path(spots);
        }

        OnPathCreated?.Invoke(created);
    }

    public (int, float) GetAreaIdAndZ(Vector3 location)
    {
        lock (gate)
        {
            if (search == null)
            {
                throw new InvalidOperationException("PPatherService is not initialised");
            }

            return search.GetAreaIdAndZ(location);
        }
    }
}
