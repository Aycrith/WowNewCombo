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
        public int[] FoodIds { get; }
        public int[] WaterIds { get; }

        public ItemDB(DataConfig dataConfig)
        {
            Item[] items = JsonConvert.DeserializeObject<Item[]>(File.ReadAllText(Path.Join(dataConfig.Dbc, "items.json")));
            for (int i = 0; i < items.Length; i++)
            {
                Items.Add(items[i].Entry, items[i]);
            }

            EntityId[] foods = JsonConvert.DeserializeObject<EntityId[]>(File.ReadAllText(Path.Join(dataConfig.Dbc, "foods.json")));
            FoodIds = new int[foods.Length];
            for (int i = 0; i < foods.Length; i++)
            {
                FoodIds[i] = foods[i].Id;
            }

            EntityId[] waters = JsonConvert.DeserializeObject<EntityId[]>(File.ReadAllText(Path.Join(dataConfig.Dbc, "waters.json")));
            WaterIds = new int[waters.Length];
            for (int i = 0; i < waters.Length; i++)
            {
                WaterIds[i] = waters[i].Id;
            }
        }
    }
}
