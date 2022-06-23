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
            WorldMapArea[] wmas = JsonConvert.DeserializeObject<WorldMapArea[]>(File.ReadAllText(Path.Join(dataConfig.Dbc, "WorldMapArea.json")));
            for (int i = 0; i < wmas.Length; i++)
            {
                areas.Add(wmas[i].UIMapId, wmas[i]);
            }
        }

        public int GetAreaId(int uiMapId)
        {
            if (areas.TryGetValue(uiMapId, out var map))
            {
                return map.AreaID;
            }

            return -1;
        }

        public Vector3 ToWorld(int uiMapId, Vector3 local, bool flipXY)
        {
            var worldMapArea = areas[uiMapId];
            if (flipXY)
            {
                return new Vector3(worldMapArea.ToWorldX(local.Y), worldMapArea.ToWorldY(local.X), local.Z);
            }
            else
            {
                return new Vector3(worldMapArea.ToWorldX(local.X), worldMapArea.ToWorldY(local.Y), local.Z);
            }
        }

        public Vector3 ToLocal(Vector3 world, float mapId, int uiMap)
        {
            var area = WorldMapAreaFactory.GetWorldMapArea(areas.Values, world.X, world.Y, mapId, uiMap);
            return new Vector3(area.ToMapY(world.Y), area.ToMapX(world.X), world.Z);
        }

        public bool TryGet(int uiMapId, out WorldMapArea area)
        {
            if (areas.TryGetValue(uiMapId, out var map))
            {
                area = map;
                return true;
            }

            area = default;
            return false;
        }
    }
}
