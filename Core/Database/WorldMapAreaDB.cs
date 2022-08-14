using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Newtonsoft.Json;
using SharedLib;
using SharedLib.Data;

namespace Core.Database
{
    public class WorldMapAreaDB
    {
        private readonly Dictionary<int, WorldMapArea> areas = new();

        public WorldMapAreaDB(DataConfig dataConfig)
        {
            WorldMapArea[] wmas = JsonConvert.DeserializeObject<WorldMapArea[]>(File.ReadAllText(Path.Join(dataConfig.ExpDbc, "WorldMapArea.json")));
            for (int i = 0; i < wmas.Length; i++)
            {
                areas.Add(wmas[i].UIMapId, wmas[i]);
            }
        }

        public int GetAreaId(int uiMap)
        {
            return areas.TryGetValue(uiMap, out WorldMapArea map) ? map.AreaID : -1;
        }

        public Vector3 ToWorld_FlipXY(int uiMap, Vector3 map)
        {
            WorldMapArea wma = areas[uiMap];
            return new Vector3(wma.ToWorldX(map.Y), wma.ToWorldY(map.X), map.Z);
        }

        public Vector3 ToWorld(int uiMap, Vector3 map)
        {
            WorldMapArea wma = areas[uiMap];
            return new Vector3(wma.ToWorldX(map.X), wma.ToWorldY(map.Y), map.Z);
        }

        public Vector3 ToMap_FlipXY(Vector3 world, float mapId, int uiMap)
        {
            var wma = WorldMapAreaFactory.GetWorldMapArea(areas.Values, world.X, world.Y, mapId, uiMap);
            return new Vector3(wma.ToMapY(world.Y), wma.ToMapX(world.X), world.Z);
        }

        public bool TryGet(int uiMap, out WorldMapArea wma)
        {
            return areas.TryGetValue(uiMap, out wma);
        }
    }
}
