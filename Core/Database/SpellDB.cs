using System.Collections.Frozen;

using SharedLib;

using static System.IO.File;
using static System.IO.Path;
using static Newtonsoft.Json.JsonConvert;

namespace Core.Database;

public sealed class SpellDB
{
    public FrozenDictionary<int, Spell> Spells { get; }

    public SpellDB(DataConfig dataConfig)
    {
        Spell[] spells = DeserializeObject<Spell[]>(
            ReadAllText(Join(dataConfig.ExpDbc, "spells.json")))!;

        this.Spells = spells
            .ToFrozenDictionary(spell => spell.Id);
    }
}
