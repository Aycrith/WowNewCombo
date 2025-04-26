using SharedLib;
using SharedLib.Data;

using System.Collections.Frozen;

using static Newtonsoft.Json.JsonConvert;
using static System.IO.File;
using static System.IO.Path;

namespace Core.Database;

public sealed class FactionTemplateDB
{
    public FrozenDictionary<int, int> Factions { get; }

    public FactionTemplateDB(DataConfig dataConfig)
    {
        FactionTemplate[] data = DeserializeObject<FactionTemplate[]>(
            ReadAllText(Join(dataConfig.ExpDbc, "factiontemplates.json")))!;

        Factions = data
            .ToFrozenDictionary(c => c.Id, c => c.FriendGroup);
    }
}
