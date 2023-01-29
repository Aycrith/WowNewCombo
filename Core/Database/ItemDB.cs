using System.Collections.Generic;
using static System.IO.File;
using static System.IO.Path;
using static Newtonsoft.Json.JsonConvert;
using SharedLib;
using System;

namespace Core.Database;

public sealed class ItemDB
{
    private static readonly Item _emptyItem = new() { Entry = 0, Name = string.Empty, Quality = 0, SellPrice = 0 };
    public static ref readonly Item EmptyItem => ref _emptyItem;

    public Dictionary<int, Item> Items { get; } = new();
    public int[] FoodIds { get; }
    public int[] DrinkIds { get; }

    public ItemDB(DataConfig dataConfig)
    {
        ReadOnlySpan<Item> items = DeserializeObject<Item[]>(ReadAllText(Join(dataConfig.ExpDbc, "items.json")))!;
        for (int i = 0; i < items.Length; i++)
        {
            Items.Add(items[i].Entry, items[i]);
        }

        FoodIds = DeserializeObject<int[]>(ReadAllText(Join(dataConfig.ExpDbc, "foods.json")))!;
        DrinkIds = DeserializeObject<int[]>(ReadAllText(Join(dataConfig.ExpDbc, "waters.json")))!;
    }
}
