using SharedLib;

using Microsoft.Extensions.Logging;

using System.Collections.Frozen;

using static Newtonsoft.Json.JsonConvert;
using static System.IO.File;
using static System.IO.Path;

namespace Core.Database;

public sealed class CreatureDB
{
    public FrozenDictionary<int, Creature> Entries { get; }

    public CreatureDB(DataConfig dataConfig, ILogger<CreatureDB> logger)
    {
        string path = Join(dataConfig.ExpDbc, "creatures.json");
        if (!System.IO.File.Exists(path))
        {
            logger.LogWarning("[CreatureDB        ] Missing DBC file: {Path}. CreatureDB disabled.", path);
            Entries = FrozenDictionary<int, Creature>.Empty;
            return;
        }

        Creature[]? creatures = DeserializeObject<Creature[]>(ReadAllText(path));
        if (creatures == null || creatures.Length == 0)
        {
            logger.LogWarning("[CreatureDB        ] Empty/invalid DBC file: {Path}. CreatureDB disabled.", path);
            Entries = FrozenDictionary<int, Creature>.Empty;
            return;
        }

        Entries = creatures
            .ToFrozenDictionary(c => c.Entry, c => c);
    }
}
