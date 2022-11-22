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
            var creatures = JsonConvert.DeserializeObject<Creature[]>(File.ReadAllText(Path.Join(dataConfig.ExpDbc, "creatures.json")));

            for (int i = 0; i < creatures.Length; i++)
            {
                Entries.Add(creatures[i].Entry, creatures[i]);
            }
        }

    }
}
