using System.Collections.Specialized;

namespace Core
{
    public class ActionBarBits
    {
        private readonly int[] cells;

        private readonly BitVector32[] bits;

        public ActionBarBits(params int[] cells)
        {
            this.cells = cells;
            bits = new BitVector32[cells.Length];
        }

        public void Update(AddonDataProvider reader)
        {
            for (int i = 0; i < bits.Length; i++)
            {
                bits[i] = new(reader.GetInt(cells[i]));
            }
        }

        // https://wowwiki-archive.fandom.com/wiki/ActionSlot
        public bool Is(KeyAction keyAction)
        {
            if (keyAction.Slot == 0) return false;

            int index = keyAction.SlotIndex;
            return bits[index / ActionBar.BIT_PER_CELL][Mask.M[index % ActionBar.BIT_PER_CELL]];
        }
    }
}
