using System;

namespace Core
{
    public class ActionBarCooldownReader
    {
        private readonly struct Data
        {
            public int DurationSec { get; }
            public DateTime StartTime { get; }

            public Data(int duration, DateTime startTime)
            {
                DurationSec = duration;
                StartTime = startTime;
            }
        }

        private const float MAX_ACTION_IDX = 100000f;
        private const float MAX_VALUE_MUL = 100f;

        private readonly int cActionbarNum;

        private readonly Data[] data;

        public ActionBarCooldownReader(int cActionbarNum)
        {
            this.cActionbarNum = cActionbarNum;

            data = new Data[ActionBar.CELL_COUNT * ActionBar.BIT_PER_CELL];
            Reset();
        }

        public void Read(AddonDataProvider reader)
        {
            // formula
            // MAX_ACTION_IDX * slot + (cooldown / MAX_VALUE_MUL)
            float durationSec = reader.GetInt(cActionbarNum);
            if (durationSec == 0 || durationSec < MAX_ACTION_IDX) return;

            int slot = (int)(durationSec / MAX_ACTION_IDX);
            durationSec -= (int)MAX_ACTION_IDX * slot;
            durationSec /= MAX_VALUE_MUL;

            int index = slot - 1;
            if (index < 0)
                return;

            data[index] = new((int)durationSec, DateTime.UtcNow);
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
            return data[index].DurationSec > 0
                ? Math.Clamp((int)(data[index].StartTime.AddSeconds(data[index].DurationSec) - DateTime.UtcNow).TotalMilliseconds, 0, int.MaxValue)
                : 0;
        }

    }
}
