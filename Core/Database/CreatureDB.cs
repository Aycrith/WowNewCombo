using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SharedLib;

namespace Core.Database
{
    public sealed class CreatureDB
    {
        public Dictionary<int, Creature> Entries { get; } = new();

        public CreatureDB(DataConfig dataConfig)
        {
            var creatures = JsonConvert.DeserializeObject<List<Creature>>(File.ReadAllText(Path.Join(dataConfig.ExpDbc, "creatures.json")));
            creatures.ForEach(i => Entries.Add(i.Entry, i));
        }

    }
}
