using System;
using Core.Database;
using SharedLib;

namespace Core
{
    public class EquipmentReader
    {
        private const int MAX_EQUIPMENT_COUNT = 24;

        private readonly ItemDB itemDB;
        private readonly int cItemId;
        private readonly int cSlotNum;

        private readonly int[] equipmentIds = new int[MAX_EQUIPMENT_COUNT];
        public Item[] Items { get; private set; } = new Item[MAX_EQUIPMENT_COUNT];

        public event EventHandler<(int, int)>? OnEquipmentChanged;

        public EquipmentReader(ItemDB itemDB, int cSlotNum, int cItemId)
        {
            this.itemDB = itemDB;
            this.cSlotNum = cSlotNum;
            this.cItemId = cItemId;

            for (int i = 0; i < MAX_EQUIPMENT_COUNT; i++)
            {
                Items[i] = ItemDB.EmptyItem;
            }
        }

        public void Read(IAddonDataProvider reader)
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
                    else
                    {
                        Items[index] = new Item() { Entry = itemId, Name = "Unknown" };
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