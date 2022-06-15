namespace Core
{
    public class ActionBarBits
    {
        private readonly AddonDataProvider reader;
        private readonly int[] cells;

        private readonly BitStatus[] bits;
        private readonly PlayerReader playerReader;

        private bool isDirty;

        public ActionBarBits(PlayerReader playerReader, AddonDataProvider reader, params int[] cells)
        {
            this.reader = reader;
            this.playerReader = playerReader;
            this.cells = cells;

            bits = new BitStatus[cells.Length];
            for (int i = 0; i < bits.Length; i++)
            {
                bits[i] = new(reader.GetInt(cells[i]));
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
                bits[i].Update(reader.GetInt(cells[i]));
            }
        }


        // https://wowwiki-archive.fandom.com/wiki/ActionSlot
        public bool Is(KeyAction keyAction)
        {
            if (isDirty)
            {
                Update();
                isDirty = false;
            }

            if (keyAction.Slot == 0) return false;

            int index = keyAction.Slot + Stance.RuntimeSlotToActionBar(keyAction, playerReader, keyAction.Slot);
            int array = index / ActionBar.BIT_PER_CELL;
            return bits[array].IsBitSet((index - 1) % ActionBar.BIT_PER_CELL);
        }
    }
}
