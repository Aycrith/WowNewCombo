using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SharedLib;

namespace Core.Database
{
    public class SpellDB
    {
        public Dictionary<int, Spell> Spells { get; } = new();

        public SpellDB(DataConfig dataConfig)
        {
            Spell[] temp = JsonConvert.DeserializeObject<Spell[]>(File.ReadAllText(Path.Join(dataConfig.Dbc, "spells.json")));
            for (int i = 0; i < temp.Length; i++)
            {
                Spells.Add(temp[i].Id, temp[i]);
            }
        }
    }
}
