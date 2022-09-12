using System;

namespace Core
{
    public sealed class ActionBarCostEventArgs : EventArgs
    {
        public int Slot { get; }
        public ActionBarCost ActionBarCost { get; }

        public ActionBarCostEventArgs(int slot, ActionBarCost abc)
        {
            Slot = slot;
            ActionBarCost = abc;
        }
    }

    public readonly struct ActionBarCost
    {
        public readonly PowerType PowerType;
        public readonly int Cost;

        public ActionBarCost(PowerType powerType, int cost)
        {
            PowerType = powerType;
            Cost = cost;
        }
    }

    public class ActionBarCostReader
    {
        private const int COST_ORDER = 10000;
        private const int POWER_TYPE_MOD = 100;

        private readonly int cActionbarMeta;
        private readonly int cActionbarNum;

        private static readonly ActionBarCost defaultCost = new(PowerType.Mana, 0);
        private readonly ActionBarCost[][] data;

        public int Count { get; private set; }

        public event EventHandler<ActionBarCostEventArgs>? OnActionCostChanged;
        public event Action? OnActionCostReset;

        public ActionBarCostReader(int cActionbarMeta, int cActionbarNum)
        {
            this.cActionbarMeta = cActionbarMeta;
            this.cActionbarNum = cActionbarNum;

            data = new ActionBarCost[ActionBar.CELL_COUNT * ActionBar.BIT_PER_CELL][];
            for (int s = 0; s < data.Length; s++)
            {
                data[s] = new ActionBarCost[ActionBar.NUM_OF_COST];
            }

            Reset();
        }

        public void Read(IAddonDataProvider reader)
        {
            int meta = reader.GetInt(cActionbarMeta);
            int cost = reader.GetInt(cActionbarNum);
            if ((cost == 0 && meta == 0) || meta < ActionBar.ACTION_SLOT_MUL)
                return;

            int slotIdx = (meta / ActionBar.ACTION_SLOT_MUL) - 1;
            int costIdx = (meta / COST_ORDER % 10) - 1;
            int type = meta % POWER_TYPE_MOD;

            ActionBarCost old = data[slotIdx][costIdx];
            data[slotIdx][costIdx] = new((PowerType)type, cost);

            if (cost != old.Cost || (PowerType)type != old.PowerType)
                OnActionCostChanged?.Invoke(this, new(slotIdx + 1, data[slotIdx][costIdx]));

            if (slotIdx > Count)
                Count = slotIdx;
        }

        public void Reset()
        {
            Count = 0;
            for (int s = 0; s < data.Length; s++)
            {
                for (int c = 0; c < ActionBar.NUM_OF_COST; c++)
                {
                    data[s][c] = defaultCost;
                }
            }
            OnActionCostReset?.Invoke();
        }

        public ActionBarCost GetCostByActionBarSlot(KeyAction keyAction, int costIndex = 0)
        {
            int slotIdx = keyAction.SlotIndex;
            return data[slotIdx][costIndex];
        }
    }
}
