using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

using Newtonsoft.Json;

namespace SharedLib
{
    public sealed class WorldMapAreaDB
    {
        private readonly Dictionary<int, WorldMapArea> wmas = new();

        public IEnumerable<WorldMapArea> Values => wmas.Values;

        public WorldMapAreaDB(DataConfig dataConfig)
        {
            WorldMapArea[] wmas = JsonConvert.DeserializeObject<WorldMapArea[]>(File.ReadAllText(Path.Join(dataConfig.ExpDbc, "WorldMapArea.json")));
            for (int i = 0; i < wmas.Length; i++)
                this.wmas.Add(wmas[i].UIMapId, wmas[i]);
        }

        public int GetAreaId(int uiMap)
        {
            return wmas.TryGetValue(uiMap, out WorldMapArea map) ? map.AreaID : -1;
        }

        public bool TryGet(int uiMap, out WorldMapArea wma)
        {
            return wmas.TryGetValue(uiMap, out wma);
        }

        //

        public static Vector3 ToWorld_FlipXY(Vector3 map, WorldMapArea wma)
        {
            return new Vector3(wma.ToWorldX(map.Y), wma.ToWorldY(map.X), map.Z);
        }

        public Vector3 ToWorld_FlipXY(int uiMap, Vector3 map)
        {
            WorldMapArea wma = wmas[uiMap];
            return new Vector3(wma.ToWorldX(map.Y), wma.ToWorldY(map.X), map.Z);
        }

        public void ToWorldXY_FlipXY(int uiMap, ref Vector3[] map)
        {
            WorldMapArea wma = wmas[uiMap];
            for (int i = 0; i < map.Length; i++)
            {
                Vector3 p = map[i];
                map[i] = new Vector3(wma.ToWorldX(p.Y), wma.ToWorldY(p.X), p.Z);
            }
        }

        //

        public Vector3 ToMap_FlipXY(Vector3 world, float mapId, int uiMap)
        {
            WorldMapArea wma = GetWorldMapArea(world.X, world.Y, (int)mapId, uiMap);
            return new Vector3(wma.ToMapY(world.Y), wma.ToMapX(world.X), world.Z);
        }

        public static Vector3 ToMap_FlipXY(Vector3 world, WorldMapArea wma)
        {
            return new Vector3(wma.ToMapY(world.Y), wma.ToMapX(world.X), world.Z);
        }

        public void ToMap_FlipXY(int uiMap, ref Vector3[] worlds)
        {
            if (!TryGet(uiMap, out WorldMapArea wma))
                return;

            for (int i = 0; i < worlds.Length; i++)
            {
                Vector3 world = worlds[i];
                worlds[i] = new Vector3(wma.ToMapY(world.Y), wma.ToMapX(world.X), world.Z);
            }
        }

        //

        public WorldMapArea GetWorldMapArea(float worldX, float worldY, int mapId, int uiMap)
        {
            IEnumerable<WorldMapArea> maps =
                wmas.Values.Where(i =>
                    worldX <= i.LocTop &&
                    worldX >= i.LocBottom &&
                    worldY <= i.LocLeft &&
                    worldY >= i.LocRight &&
                    i.MapID == mapId);

            if (!maps.Any())
            {
                throw new ArgumentOutOfRangeException(nameof(wmas), $"Failed to find map area for spot {worldX}, {worldY}, {mapId}");
            }

            if (maps.Count() > 1)
            {
                // sometimes we end up with 2 map areas which a coord could be in which is rather unhelpful. e.g. Silithus and Feralas overlap.
                // If we are in a zone and not moving between then the mapHint should take care of the issue
                // otherwise we are not going to be able to work out which zone we are actually in...

                if (uiMap > 0)
                {
                    return maps.First(m => m.UIMapId == uiMap);
                }
                throw new ArgumentOutOfRangeException(nameof(wmas), $"Found many map areas for spot {worldX}, {worldY}, {mapId} : {string.Join(", ", maps.Select(s => s.AreaName))}");
            }

            return maps.First();
        }

    }
}
