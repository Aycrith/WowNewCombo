using SharedLib;

namespace Core
{
    public sealed class Bag
    {
        public int ItemId { get; set; }
        public BagType BagType { get; set; }
        public int SlotCount { get; set; }
        public int FreeSlot { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
