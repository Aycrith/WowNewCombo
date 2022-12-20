using PPather.Graph;
using System;
using System.Collections.Generic;
using WowTriangles;
using PPather.Data;
using SharedLib;
using SharedLib.Data;
using Microsoft.Extensions.Logging;
using System.Numerics;

namespace PPather
{
    public sealed class PPatherService
    {
        private readonly ILogger logger;
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

        public PPatherService(ILogger logger, DataConfig dataConfig, WorldMapAreaDB worldMapAreaDB)
        {
            this.dataConfig = dataConfig;
            this.logger = logger;
            this.worldMapAreaDB = worldMapAreaDB;
            ContinentDB.Init(worldMapAreaDB.Values);

            ClearTemporaryFiles();
        }

        private void ClearTemporaryFiles()
        {
            System.IO.DirectoryInfo di = new(dataConfig.PPather);
            System.IO.FileInfo[] files = di.GetFiles("*.tmp");
            for (int i = 0; i < files.Length; i++)
            {
                files[i].Delete();
            }
        }

        public void Reset()
        {
            if (search != null)
            {
                search.PathGraph.triangleWorld.NotifyChunkAdded = null;
                search.Clear();
                GC.Collect();
            }
            search = null;
        }

        private void Initialise(float mapId)
        {
            if (search != null && mapId == search.MapId)
                return;

            PathGraph.SearchEnabled = false;
            search = new Search(mapId, logger, dataConfig);
            search.PathGraph.triangleWorld.NotifyChunkAdded = ChunkAdded;
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

        public void DrawPath(float mapId, List<float[]> coords)
        {
            var first = coords[0];
            var last = coords[^1];

            var fromLoc = new Vector4(first[0], first[1], first[2], mapId);
            var toLoc = new Vector4(last[0], last[1], last[2], mapId);

            SetLocations(fromLoc, toLoc);

            if (search.PathGraph == null)
            {
                search.CreatePathGraph(mapId);
            }

            List<Spot> spots = new();
            for (int i = 0; i < coords.Count; i++)
            {
                Spot spot = new(new(coords[i][0], coords[i][1], coords[i][2]));
                spots.Add(spot);
                search.PathGraph.CreateSpotsAroundSpot(spot, false);
            }

            var path = new Path(spots);
            OnPathCreated?.Invoke(path);
        }
    }
}