using System;

namespace Core
{
    public class ActionBarCostEventArgs : EventArgs
    {
        public int Slot { get; }
        public ActionBarCost ActionBarCost { get; }

        public ActionBarCostEventArgs(int slot, PowerType powerType, int cost)
        {
            Slot = slot;
            ActionBarCost = new(powerType, cost);
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
        private readonly int cActionbarMeta;
        private readonly int cActionbarNum;

        private const float COST_MAX_COST_IDX = 100000f;
        private const float COST_MAX_POWER_TYPE = 1000f;

        //https://wowwiki-archive.fandom.com/wiki/ActionSlot
        private readonly ActionBarCost defaultCost = new(PowerType.Mana, 0);
        private readonly ActionBarCost[][] data;

        public int Count { get; private set; }

        public event EventHandler<ActionBarCostEventArgs>? OnActionCostChanged;
        public event Action? OnActionCostReset;

        public ActionBarCostReader(int cActionbarMeta, int cActionbarNum)
        {
            this.cActionbarMeta = cActionbarMeta;
            this.cActionbarNum = cActionbarNum;

            data = new ActionBarCost[ActionBar.CELL_COUNT * ActionBar.BIT_PER_CELL][];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = new ActionBarCost[ActionBar.NUM_OF_COST];
                for (int j = 0; j < ActionBar.NUM_OF_COST; j++)
                {
                    data[i][j] = defaultCost;
                }
            }

            Reset();
        }

        public void Read(AddonDataProvider reader)
        {
            // formula
            // COST_MAX_COST_IDX * costNum + COST_MAX_POWER_TYPE * (type + offsetEnumPowerType) + slot
            int slot = reader.GetInt(cActionbarMeta);
            int cost = reader.GetInt(cActionbarNum);

            if (cost == 0 || slot == 0 || slot < COST_MAX_POWER_TYPE) return;

            int costNum = (int)(slot / COST_MAX_COST_IDX);
            slot -= (int)COST_MAX_COST_IDX * costNum;

            int type = (int)(slot / COST_MAX_POWER_TYPE);
            slot -= (int)COST_MAX_POWER_TYPE * type;

            int index = slot - 1;
            int costIndex = costNum - 1;

            ActionBarCost temp = data[index][costIndex];
            data[index][costIndex] = new((PowerType)type, cost);

            if (cost != temp.Cost)
                OnActionCostChanged?.Invoke(this, new(slot, (PowerType)type, cost));

            if (slot > Count)
                Count = slot;
        }

        public void Reset()
        {
            Count = 0;
            for (int i = 0; i < data.Length; i++)
            {
                for (int j = 0; j < ActionBar.NUM_OF_COST; j++)
                {
                    data[i][j] = defaultCost;
                }
            }
            OnActionCostReset?.Invoke();
        }

        public ActionBarCost GetCostByActionBarSlot(KeyAction keyAction, int costIndex = 0)
        {
            int index = keyAction.SlotIndex;
            return data[index][costIndex];
        }
    }
}
