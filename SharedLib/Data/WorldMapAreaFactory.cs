using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharedLib.Data
{
    public static class WorldMapAreaFactory
    {
        public static WorldMapArea[] Read(DataConfig dataConfig)
        {
            return JsonConvert.DeserializeObject<WorldMapArea[]>(File.ReadAllText(Path.Join(dataConfig.ExpDbc, "WorldMapArea.json")));
        }

        public static WorldMapArea GetWorldMapArea(IEnumerable<WorldMapArea> wmas, float worldX, float worldY, float mapId, int uiMap)
        {
            IEnumerable<WorldMapArea> maps =
                wmas.Where(i =>
                    worldX <= i.LocTop &&
                    worldX >= i.LocBottom &&
                    worldY <= i.LocLeft &&
                    worldY >= i.LocRight &&
                    i.MapID == (int)mapId);

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
