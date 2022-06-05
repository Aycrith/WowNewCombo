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
using SharedLib.Extensions;

namespace PPather
{
    public class PPatherService
    {
        private readonly List<WorldMapArea> worldMapAreas;
        private Search search { get; set; }
        private readonly ILogger logger;
        private readonly DataConfig dataConfig;
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
                search.PathGraph.triangleWorld.NotifyChunkAdded = (e) => OnChunkAdded?.Invoke(e);
                OnReset?.Invoke();
                return true;
            }
            return false;
        }
        public Vector4 GetWorldLocation(int uiMapId, float x, float y, float z = 0)
        {
            WorldMapArea worldMapArea = worldMapAreas.First(i => i.UIMapId == uiMapId);
            float worldX = worldMapArea.ToWorldX(y);
            float worldY = worldMapArea.ToWorldY(x);

            Initialise(worldMapArea.MapID);

            return search.CreateLocation(worldX, worldY, z, worldMapArea.MapID);
        }

        public WorldMapAreaSpot ToMapAreaSpot(float x, float y, float z, int uimap)
        {
            var area = WorldMapAreaFactory.GetWorldMapArea(worldMapAreas, x, y, search.MapId, uimap);
            return new WorldMapAreaSpot
            {
                Y = area.ToMapX(x),
                X = area.ToMapY(y),
                Z = z,
                MapID = area.UIMapId
            };
        }

        public Path DoSearch(PathGraph.eSearchScoreSpot searchType)
        {
            var path = search.DoSearch(searchType);
            OnPathCreated?.Invoke(path);
            lastPath = path;
            return path;
        }

        public void Save()
        {
            search.PathGraph.Save();
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

        public List<TriangleCollection> SetNotifyChunkAdded(Action<ChunkAddedEventArgs> action)
        {
            OnChunkAdded = action;

            if (search == null)
            {
                return new List<TriangleCollection>();
            }

            return search.PathGraph.triangleWorld.LoadedChunks;
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

        public void DrawPath(int mapId, List<float[]> coords)
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

            List<Spot> spots = new List<Spot>();
            for (int i = 0; i < coords.Count; i++)
            {
                Vector3 l = new(coords[i][0], coords[i][1], coords[i][2]);
                Spot spot = new(l);
                spots.Add(spot);
                search.PathGraph.CreateSpotsAroundSpot(spot, false);
            }

            var path = new Path(spots);
            OnPathCreated?.Invoke(path);
        }
    }
}