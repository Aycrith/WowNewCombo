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
        public int[] DrinkIds { get; }

        public ItemDB(DataConfig dataConfig)
        {
            Item[] items = JsonConvert.DeserializeObject<Item[]>(File.ReadAllText(Path.Join(dataConfig.Dbc, "items.json")));
            for (int i = 0; i < items.Length; i++)
            {
                Items.Add(items[i].Entry, items[i]);
            }

            FoodIds = JsonConvert.DeserializeObject<int[]>(File.ReadAllText(Path.Join(dataConfig.Dbc, "foods.json")));
            DrinkIds = JsonConvert.DeserializeObject<int[]>(File.ReadAllText(Path.Join(dataConfig.Dbc, "waters.json")));
        }
    }
}
