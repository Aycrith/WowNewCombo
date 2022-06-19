using System;

namespace Core
{
    public class ActionBarCostEventArgs : EventArgs
    {
        public int Index { get; }
        public ActionBarCost ActionBarCost { get; }

        public ActionBarCostEventArgs(int index, PowerType powerType, int cost)
        {
            Index = index;
            ActionBarCost = new(powerType, cost);
        }
    }

    public readonly struct ActionBarCost
    {
        public readonly PowerType PowerType { get; }
        public readonly int Cost { get; }

        public ActionBarCost(PowerType powerType, int cost)
        {
            PowerType = powerType;
            Cost = cost;
        }
    }

    public class ActionBarCostReader
    {
        private readonly AddonDataProvider reader;
        private readonly int cActionbarNum;

        private const float MAX_POWER_TYPE = 1000000f;
        private const float MAX_ACTION_IDX = 1000f;

        //https://wowwiki-archive.fandom.com/wiki/ActionSlot
        private readonly ActionBarCost defaultCost = new(PowerType.Mana, 0);
        private readonly ActionBarCost[] data;

        public int Count { get; private set; }

        public event EventHandler<ActionBarCostEventArgs>? OnActionCostChanged;

        public ActionBarCostReader(AddonDataProvider reader, int cActionbarNum)
        {
            this.cActionbarNum = cActionbarNum;
            this.reader = reader;

            data = new ActionBarCost[ActionBar.CELL_COUNT * ActionBar.BIT_PER_CELL];
            Reset();
        }

        public void Read()
        {
            // formula
            // MAX_POWER_TYPE * type + MAX_ACTION_IDX * slot + cost
            int cost = reader.GetInt(cActionbarNum);
            if (cost == 0) return;

            int type = (int)(cost / MAX_POWER_TYPE);
            cost -= (int)MAX_POWER_TYPE * type;

            int index = (int)(cost / MAX_ACTION_IDX);
            cost -= (int)MAX_ACTION_IDX * index;

            ActionBarCost temp = data[index];
            data[index] = new((PowerType)type, cost);

            if (cost != temp.Cost)
                OnActionCostChanged?.Invoke(this, new(index, (PowerType)type, cost));

            if (index > Count)
                Count = index;
        }

        public void Reset()
        {
            Count = 0;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = defaultCost;
            }
        }

        public ActionBarCost GetCostByActionBarSlot(PlayerReader playerReader, KeyAction keyAction)
        {
            int index = keyAction.Slot + Stance.RuntimeSlotToActionBar(keyAction, playerReader, keyAction.Slot);
            return data[index];
        }
    }
}
