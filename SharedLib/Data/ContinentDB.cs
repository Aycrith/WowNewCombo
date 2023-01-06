using System.Collections.Generic;

namespace SharedLib.Data;

public static class ContinentDB
{
    public static Dictionary<float, string> IdToName { get; } = new();
    public static Dictionary<string, float> NameToId { get; } = new();

    public static void Init(IEnumerable<WorldMapArea> list)
    {
        foreach (WorldMapArea area in list)
        {
            if (area.Continent == null) continue;
            IdToName.TryAdd(area.MapID, area.Continent);
            NameToId.TryAdd(area.Continent, area.MapID);
        }
    }
}
