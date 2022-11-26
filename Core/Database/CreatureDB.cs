using System.Collections.Generic;
using static System.IO.File;
using static System.IO.Path;
using static Newtonsoft.Json.JsonConvert;

using SharedLib;

namespace Core.Database
{
    public sealed class CreatureDB
    {
        public Dictionary<int, Creature> Entries { get; } = new();

        public CreatureDB(DataConfig dataConfig)
        {
            var creatures = DeserializeObject<Creature[]>(ReadAllText(Join(dataConfig.ExpDbc, "creatures.json")))!;

            for (int i = 0; i < creatures.Length; i++)
            {
                Entries.Add(creatures[i].Entry, creatures[i]);
            }
        }

    }
}
