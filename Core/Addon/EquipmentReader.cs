using System;
using System.Collections.Generic;
using Core.Database;
using SharedLib;

namespace Core
{
    public enum InventorySlotId
    {
        Ammo = 0,
        Head = 1,
        Neck = 2,
        Shoulder = 3,
        Shirt = 4,
        Chest = 5,
        Waist = 6,
        Legs = 7,
        Feet = 8,
        Wrists = 9,
        Hands = 10,
        Finger_1 = 11,
        Finger_2 = 12,
        Trinket_1 = 13,
        Trinket_2 = 14,
        Back = 15,
        Mainhand = 16,
        Offhand = 17,
        Ranged = 18,
        Tabard = 19,
        Bag_0 = 20,
        Bag_1 = 21,
        Bag_2 = 22,
        Bag_3 = 23
    }

    public class EquipmentReader
    {
        private const int MAX_EQUIPMENT_COUNT = 24;

        private readonly AddonDataProvider reader;
        private readonly ItemDB itemDB;
        private readonly int cItemId;
        private readonly int cSlotNum;

        private readonly int[] equipmentIds = new int[MAX_EQUIPMENT_COUNT];
        public Item[] Items { get; private set; } = new Item[MAX_EQUIPMENT_COUNT];

        public event EventHandler<(int, int)>? OnEquipmentChanged;

        public EquipmentReader(AddonDataProvider reader, ItemDB itemDB, int cSlotNum, int cItemId)
        {
            this.reader = reader;
            this.itemDB = itemDB;
            this.cSlotNum = cSlotNum;
            this.cItemId = cItemId;

            for (int i = 0; i < MAX_EQUIPMENT_COUNT; i++)
            {
                Items[i] = ItemDB.EmptyItem;
            }
        }

        public void Read()
        {
            int index = reader.GetInt(cSlotNum);
            if (index < MAX_EQUIPMENT_COUNT && index >= 0)
            {
                int itemId = reader.GetInt(cItemId);
                bool changed = equipmentIds[index] != itemId;

                equipmentIds[index] = itemId;

                if (changed)
                {
                    if (itemId == 0)
                    {
                        Items[index] = ItemDB.EmptyItem;
                    }
                    else if (itemDB.Items.TryGetValue(itemId, out Item item))
                    {
                        Items[index] = item;
                    }

                    OnEquipmentChanged?.Invoke(this, (index, itemId));
                }
            }
        }

        public string ToStringList()
        {
            return string.Join(", ", equipmentIds);
        }

        public bool HasRanged()
        {
            return equipmentIds[(int)InventorySlotId.Ranged] != 0;
        }

        public bool HasItem(int itemId)
        {
            for (int i = 0; i < equipmentIds.Length; i++)
            {
                if (equipmentIds[i] == itemId)
                    return true;
            }

            return false;
        }

        public int GetId(int slot)
        {
            return equipmentIds[slot];
        }
    }
}