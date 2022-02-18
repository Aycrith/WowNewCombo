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
            var items = JsonConvert.DeserializeObject<List<Spell>>(File.ReadAllText(Path.Join(dataConfig.Dbc, "spells.json")));
            items.ForEach(i =>
            {
                Spells.Add(i.Id, i);
            });
        }
    }
}
