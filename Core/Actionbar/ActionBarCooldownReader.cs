using System;

using static Core.ActionBar;
using static System.Math;
using static System.DateTime;

namespace Core;

public sealed class ActionBarCooldownReader : IReader
{
    private readonly struct Data
    {
        private readonly float durationSec;
        private readonly DateTime start;

        public DateTime End => start.AddSeconds(durationSec);

        public Data(float durationSec, DateTime start)
        {
            this.durationSec = durationSec;
            this.start = start;
        }
    }

    private const float FRACTION_PART = 10f;

    private const int cActionbarNum = 37;

    private readonly Data[] data;

    public ActionBarCooldownReader()
    {
        data = new Data[CELL_COUNT * BIT_PER_CELL];
        Reset();
    }

    public void Update(IAddonDataProvider reader)
    {
        int value = reader.GetInt(cActionbarNum);
        if (value == 0 || value < ACTION_SLOT_MUL)
            return;

        int slotIdx = (value / ACTION_SLOT_MUL) - 1;
        float durationSec = value % ACTION_SLOT_MUL / FRACTION_PART;

        data[slotIdx] = new(durationSec, UtcNow);
    }

    public void Reset()
    {
        var span = data.AsSpan();
        span.Fill(new(0, UtcNow));
    }

    public int Get(KeyAction keyAction)
    {
        int index = keyAction.SlotIndex;

        ref readonly Data d = ref data[index];

        return Max((int)(d.End - UtcNow).TotalMilliseconds, 0);
    }
}
