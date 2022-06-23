using PPather.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using WowTriangles;
using PPather.Data;
using SharedLib;
using SharedLib.Data;
using Microsoft.Extensions.Logging;
using System.Numerics;

namespace PPather
{
    public class PPatherService
    {
        private readonly WorldMapArea[] worldMapAreas;
        private Search search { get; set; }
        public bool SearchExists => search != null;

        private readonly ILogger logger;
        private readonly DataConfig dataConfig;

        private Action SearchBegin;
        private Action<Path> OnPathCreated;
        public Action OnReset { get; set; }
        public Action<ChunkAddedEventArgs> OnChunkAdded { get; set; }
        public Action<LinesEventArgs> OnLinesAdded { get; set; }
        public Action<SphereEventArgs> OnSphereAdded { get; set; }
        

        private Path lastPath;
        public bool HasInitialised;

        public PPatherService(ILogger logger) : this(logger, DataConfig.Load()) { }

        public PPatherService(ILogger logger, DataConfig dataConfig)
        {
            this.dataConfig = dataConfig;
            this.logger = logger;
            this.worldMapAreas = WorldMapAreaFactory.Read(dataConfig);
            ContinentDB.Init(worldMapAreas);
        }

        public void Reset()
        {
            OnReset?.Invoke();
            this.search = null;
        }

        private bool Initialise(float mapId)
        {
            if (search == null || mapId != search.MapId)
            {
                HasInitialised = true;
                PathGraph.SearchEnabled = false;
                search = new Search(mapId, logger, dataConfig);
                search.PathGraph.triangleWorld.NotifyChunkAdded = ChunkAdded;
                OnReset?.Invoke();
                return true;
            }
            return false;
        }

        public void ChunkAdded(ChunkAddedEventArgs e)
        {
            OnChunkAdded?.Invoke(e);
        }

        public Vector4 ToWorld(int uiMapId, float x, float y, float z = 0)
        {
            WorldMapArea worldMapArea = worldMapAreas.First(i => i.UIMapId == uiMapId);
            float worldX = worldMapArea.ToWorldX(y);
            float worldY = worldMapArea.ToWorldY(x);

            Initialise(worldMapArea.MapID);

            return search.CreateWorldLocation(worldX, worldY, z, worldMapArea.MapID);
        }

        public Vector3 ToLocal(Vector3 world, float mapId, int uiMapId)
        {
            WorldMapArea area = WorldMapAreaFactory.GetWorldMapArea(worldMapAreas, world.X, world.Y, mapId, uiMapId);
            return new Vector3(area.ToMapY(world.Y), area.ToMapX(world.X), world.Z);
        }

        public Path DoSearch(PathGraph.eSearchScoreSpot searchType)
        {
            SearchBegin?.Invoke();
            var path = search.DoSearch(searchType);
            OnPathCreated?.Invoke(path);
            lastPath = path;
            return path;
        }

        public void Save()
        {
            search.PathGraph.Save();
        }

        public void SetOnSearchBegin(Action action)
        {
            SearchBegin = action;
        }

        public void SetOnPathCreated(Action<Path> action)
        {
            OnPathCreated = action;
            if (lastPath != null)
            {
                OnPathCreated?.Invoke(lastPath);
            }
        }

        public void SetOnLinesAdded(Action<Path> action)
        {
            OnPathCreated = action;
            if (lastPath != null)
            {
                OnPathCreated?.Invoke(lastPath);
            }
        }

        public void SetLocations(Vector4 from, Vector4 to)
        {
            Initialise(from.W);

            search.locationFrom = from;
            search.locationTo = to;
        }

        public void SetNotifyChunkAdded(Action<ChunkAddedEventArgs> action)
        {
            OnChunkAdded = action;
        }

        public List<TriangleCollection> GetLoadedChunks()
        {
            return search.PathGraph.triangleWorld.LoadedChunks ?? new();
        }

        public List<Spot> GetCurrentSearchPath()
        {
            if (search == null || search.PathGraph == null)
            {
                return null;
            }

            return search.PathGraph.CurrentSearchPath();
        }

        public Vector4? SearchFrom => search?.locationFrom;

        public Vector4? SearchTo => search?.locationTo;

        public Vector3? ClosestLocation => search?.PathGraph?.ClosestSpot?.Loc;

        public Vector3? PeekLocation => search?.PathGraph?.PeekSpot?.Loc;

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