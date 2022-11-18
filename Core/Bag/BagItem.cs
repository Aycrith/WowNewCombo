using System;

using SharedLib;

namespace Core
{
    public sealed class BagItem
    {
        public int Bag { get; private set; }
        public int ItemId { get; private set; }
        public int BagIndex { get; private set; }
        public int Count { get; private set; }
        public int LastCount { get; private set; }
        public Item Item { get; private set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public int LastChange => Count - LastCount;

        public BagItem(int bag, int bagIndex, int itemId, int count, Item item)
        {
            this.Bag = bag;
            this.BagIndex = bagIndex;
            this.ItemId = itemId;
            this.Count = count;
            this.LastCount = count;
            this.Item = item;
        }

        public void UpdateCount(int count)
        {
            LastCount = Count;
            Count = count;

            LastUpdated = DateTime.UtcNow;
        }
    }
}