using SharedLib;

using Microsoft.Extensions.Logging;

using System.Collections.Frozen;

using static Newtonsoft.Json.JsonConvert;
using static System.IO.File;
using static System.IO.Path;

namespace Core.Database;

public sealed class SpellDB
{
    public FrozenDictionary<int, Spell> Spells { get; }

    public SpellDB(DataConfig dataConfig, ILogger<SpellDB> logger)
    {
        string path = Join(dataConfig.ExpDbc, "spells.json");
        if (!System.IO.File.Exists(path))
        {
            logger.LogWarning("[SpellDB           ] Missing DBC file: {Path}. SpellDB disabled.", path);
            Spells = FrozenDictionary<int, Spell>.Empty;
            return;
        }

        Spell[]? spells = DeserializeObject<Spell[]>(ReadAllText(path));
        if (spells == null || spells.Length == 0)
        {
            logger.LogWarning("[SpellDB           ] Empty/invalid DBC file: {Path}. SpellDB disabled.", path);
            Spells = FrozenDictionary<int, Spell>.Empty;
            return;
        }

        Spells = spells.ToFrozenDictionary(spell => spell.Id);
    }
}
