using System;

namespace Core
{
    public sealed class ActionBarCooldownReader
    {
        private readonly struct Data
        {
            public float DurationSec { get; }
            public DateTime StartTime { get; }

            public Data(float duration, DateTime startTime)
            {
                DurationSec = duration;
                StartTime = startTime;
            }
        }

        private const float FRACTION_PART = 10f;

        private readonly int cActionbarNum;

        private readonly Data[] data;

        public ActionBarCooldownReader(int cActionbarNum)
        {
            this.cActionbarNum = cActionbarNum;

            data = new Data[ActionBar.CELL_COUNT * ActionBar.BIT_PER_CELL];
            Reset();
        }

        public void Read(IAddonDataProvider reader)
        {
            int value = reader.GetInt(cActionbarNum);
            if (value == 0 || value < ActionBar.ACTION_SLOT_MUL)
                return;

            int slotIdx = (value / ActionBar.ACTION_SLOT_MUL) - 1;
            float durationSec = value % ActionBar.ACTION_SLOT_MUL / FRACTION_PART;

            data[slotIdx] = new(durationSec, DateTime.UtcNow);
        }

        public void Reset()
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = new(0, DateTime.UtcNow);
            }
        }

        public int GetRemainingCooldown(KeyAction keyAction)
        {
            int index = keyAction.SlotIndex;
            return Math.Clamp((int)(data[index].StartTime.AddSeconds(data[index].DurationSec) - DateTime.UtcNow).TotalMilliseconds, 0, int.MaxValue);
        }
    }
}
