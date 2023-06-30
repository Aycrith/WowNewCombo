using Core.Database;

using SharedLib;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Core;

public enum BagItemChange
{
    New,
    Remove,
    Update
}

public sealed class BagReader : IDisposable, IReader
{
    private const int cBagMeta = 20;
    private const int cItemNumCount = 21;
    private const int cItemId = 22;

    private readonly EquipmentReader equipmentReader;

    private readonly ItemDB ItemDB;

    public List<BagItem> BagItems { get; } = new();

    public Bag[] Bags { get; } = new Bag[5];

    public event Action? DataChanged;
    public event Action<BagItem, BagItemChange>? BagItemChange;

    public int Hash { private set; get; }
    public int HashNewOrStackGain { private set; get; }

    public BagReader(ItemDB itemDb, EquipmentReader equipmentReader)
    {
        this.ItemDB = itemDb;
        this.equipmentReader = equipmentReader;

        this.equipmentReader.OnEquipmentChanged -= OnEquipmentChanged;
        this.equipmentReader.OnEquipmentChanged += OnEquipmentChanged;

        for (int i = 0; i < Bags.Length; i++)
        {
            Bags[i] = new Bag();
            if (i == 0)
            {
                Bags[i].Name = "Backpack";
            }
        }
    }

    public void Dispose()
    {
        this.equipmentReader.OnEquipmentChanged -= OnEquipmentChanged;
    }

    public void Update(IAddonDataProvider reader)
    {
        ReadBagMeta(reader, out bool metaChanged);

        ReadInventory(reader, out bool inventoryChanged);

        if (metaChanged || inventoryChanged)
        {
            DataChanged?.Invoke();

            Hash++;
        }
    }

    private void ReadBagMeta(IAddonDataProvider reader, out bool changed)
    {
        int data = reader.GetInt(cBagMeta);
        if (data == 0)
        {
            changed = false;
            return;
        }

        //bagType * 1000000 + bagNum * 100000 + freeSlots * 1000 + slotCount
        int bagType = data / 1000000;
        int index = data / 100000 % 10;
        int freeSlots = data / 1000 % 100;
        int slotCount = data % 1000;

        if (index >= 0 && index < Bags.Length)
        {
            Bag bag = Bags[index];

            // default bag, the first has no equipment slot
            if (index != 0)
            {
                bag.ItemId = equipmentReader.GetId((int)InventorySlotId.Bag_0 + index - 1);
                UpdateBagName(index);
            }

            bag.BagType = (BagType)bagType;
            bag.SlotCount = slotCount;
            bag.FreeSlot = freeSlots;

            BagItems.RemoveAll(b => b.Bag == index && b.BagIndex > bag.SlotCount);

            changed = true;
        }
        else
        {
            changed = false;
        }
    }

    private void ReadInventory(IAddonDataProvider reader, out bool hasChanged)
    {
        hasChanged = false;

        int data = reader.GetInt(cItemNumCount);
        if (data == 0) return;

        // 21 -- 0-4 bagNum + 1-21 itemNum + 1-1000 quantity
        int bag = data / 1000000;
        int slot = data / 10000 % 100;
        int itemCount = data % 10000;

        int itemId = reader.GetInt(cItemId);

        BagItem? existingItem = BagItems.FirstOrDefault(b => b.Bag == bag && b.BagIndex == slot);

        if (itemCount > 0)
        {
            bool addItem = true;

            if (existingItem != null)
            {
                if (existingItem.ItemId != itemId)
                {
                    BagItemChange?.Invoke(existingItem, Core.BagItemChange.Remove);
                    BagItems.Remove(existingItem);
                    addItem = true;
                }
                else
                {
                    addItem = false;

                    if (existingItem.Count != itemCount)
                    {
                        if (existingItem.Count < itemCount)
                            HashNewOrStackGain++;

                        existingItem.UpdateCount(itemCount);
                        BagItemChange?.Invoke(existingItem, Core.BagItemChange.Update);
                        hasChanged = true;
                    }
                }
            }

            if (addItem)
            {
                hasChanged = true;

                if (ItemDB.Items.TryGetValue(itemId, out Item item))
                {
                    BagItem newItem = new(bag, slot, itemId, itemCount, item);
                    BagItems.Add(newItem);
                    BagItemChange?.Invoke(newItem, Core.BagItemChange.New);
                }
                else
                {
                    BagItem unknownItem =
                        new(bag, slot, itemId, itemCount, new Item() { Entry = itemId, Name = "Unknown" });
                    BagItems.Add(unknownItem);
                    BagItemChange?.Invoke(unknownItem, Core.BagItemChange.New);
                }

                HashNewOrStackGain++;
            }
        }
        else
        {
            if (existingItem != null)
            {
                BagItemChange?.Invoke(existingItem, Core.BagItemChange.Remove);
                BagItems.Remove(existingItem);
                hasChanged = true;
            }
        }
    }

    public int BagItemCount() => BagItems.Count;

    public int SlotCount => Bags.Sum(BagSlotCount);

    public bool BagsFull() => Bags.Sum(BagFreeSlotCount) == 0;

    public bool AnyGreyItem() => BagItems.Any(BagItemCommonQuality);

    public int ItemCount(int itemId)
    {
        int count = 0;

        var span = CollectionsMarshal.AsSpan(BagItems);
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i].ItemId == itemId)
            {
                count += span[i].Count;
            }
        }

        return count;
    }

    public bool HasItem(int itemId) => BagItems.Exists(x => x.ItemId == itemId);

    public int HighestQuantityOfDrinkItemId()
    {
        return ItemDB.DrinkIds.
            OrderByDescending(ItemCount).
            FirstOrDefault();
    }

    public int DrinkItemCount()
    {
        return ItemCount(HighestQuantityOfDrinkItemId());
    }

    public int HighestQuantityOfFoodItemId()
    {
        return ItemDB.FoodIds.
            OrderByDescending(ItemCount).
            FirstOrDefault();
    }
    public int FoodItemCount()
    {
        return ItemCount(HighestQuantityOfFoodItemId());
    }

    private void OnEquipmentChanged(object? s, (int, int) tuple)
    {
        if (tuple.Item1 is
            not >= ((int)InventorySlotId.Bag_0) or
            not <= ((int)InventorySlotId.Bag_3))
        {
            return;
        }
        int index = tuple.Item1 - (int)InventorySlotId.Tabard;
        Bags[index].ItemId = tuple.Item2;

        UpdateBagName(index);
    }

    private void UpdateBagName(int index)
    {
        Bags[index].Name =
            ItemDB.Items.TryGetValue(Bags[index].ItemId, out Item item)
            ? item.Name
            : string.Empty;
    }


    #region Helpers

    private static int BagSlotCount(Bag b) => b.SlotCount;

    private static int BagFreeSlotCount(Bag b) => b.BagType == BagType.Unspecified ? b.FreeSlot : 0;

    private static bool BagItemCommonQuality(BagItem bi) => bi.Item.Quality == 0;

    public int MaxBagSlot() => Bags.Max(BagSlotCount);

    #endregion
}