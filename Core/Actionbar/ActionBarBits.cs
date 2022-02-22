namespace Core
{
    public class ActionBarBits
    {
        private readonly ISquareReader reader;
        private readonly int[] cells;

        private readonly BitStatus[] bits;
        private readonly PlayerReader playerReader;

        private bool isDirty;

        public ActionBarBits(PlayerReader playerReader, ISquareReader reader, params int[] cells)
        {
            this.reader = reader;
            this.playerReader = playerReader;
            this.cells = cells;

            bits = new BitStatus[cells.Length];
            for (int i = 0; i < bits.Length; i++)
            {
                bits[i] = new(reader.GetIntAtCell(cells[i]));
            }
        }

        public void SetDirty()
        {
            isDirty = true;
        }

        private void Update()
        {
            for (int i = 0; i < bits.Length; i++)
            {
                bits[i].Update(reader.GetIntAtCell(cells[i]));
            }
        }


        // https://wowwiki-archive.fandom.com/wiki/ActionSlot
        public bool Is(KeyAction item)
        {
            if (isDirty)
            {
                Update();
                isDirty = false;
            }

            if (KeyReader.ActionBarSlotMap.TryGetValue(item.Key, out int slot))
            {
                slot += Stance.RuntimeSlotToActionBar(item, playerReader, slot);

                int array = slot / 24;
                return bits[array].IsBitSet((slot - 1) % 24);
            }

            return false;
        }

        public int Num(KeyAction item)
        {
            if (KeyReader.ActionBarSlotMap.TryGetValue(item.Key, out int slot))
            {
                slot += Stance.RuntimeSlotToActionBar(item, playerReader, slot);
                return slot;
            }

            return 0;
        }
    }
}
