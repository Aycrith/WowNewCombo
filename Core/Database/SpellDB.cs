using System.Collections.Generic;
using static System.IO.File;
using static System.IO.Path;
using static Newtonsoft.Json.JsonConvert;
using SharedLib;

namespace Core.Database;

public sealed class SpellDB
{
    public Dictionary<int, Spell> Spells { get; } = new();

    public SpellDB(DataConfig dataConfig)
    {
        Spell[] temp = DeserializeObject<Spell[]>(ReadAllText(Join(dataConfig.ExpDbc, "spells.json")))!;
        for (int i = 0; i < temp.Length; i++)
        {
            Spells.Add(temp[i].Id, temp[i]);
        }
    }
}
