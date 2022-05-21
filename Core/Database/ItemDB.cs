using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SharedLib;

namespace Core.Database
{
    public class ItemDB
    {
        public static readonly Item EmptyItem = new() { Entry = 0, Name = string.Empty, Quality = 0, SellPrice = 0 };

        public Dictionary<int, Item> Items { get; } = new();
        public HashSet<int> FoodIds { get; } = new();
        public HashSet<int> WaterIds { get; } = new();

        public HashSet<int> ContainerIds { get; } = new();

        public ItemDB(DataConfig dataConfig)
        {
            var items = JsonConvert.DeserializeObject<List<Item>>(File.ReadAllText(Path.Join(dataConfig.Dbc, "items.json")));
            items.ForEach(i =>
            {
                Items.Add(i.Entry, i);
            });

            var foods = JsonConvert.DeserializeObject<List<EntityId>>(File.ReadAllText(Path.Join(dataConfig.Dbc, "foods.json")));
            foods.ForEach(x => FoodIds.Add(x.Id));

            var waters = JsonConvert.DeserializeObject<List<EntityId>>(File.ReadAllText(Path.Join(dataConfig.Dbc, "waters.json")));
            waters.ForEach(x => WaterIds.Add(x.Id));
        }
    }
}
