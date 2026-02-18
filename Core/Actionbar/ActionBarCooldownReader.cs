using Microsoft.Extensions.Logging;

using System;

using static Core.ActionBar;
using static System.Diagnostics.Stopwatch;
using static System.Math;

namespace Core;

public sealed partial class ActionBarCooldownReader : IReader
{
#if DEBUG
    private const bool DEBUG = true;
#else
    private const bool DEBUG = false;
#endif

    private readonly struct Data
    {
        private readonly float durationSec;
        private readonly long start;

        public long End => start + (long)(durationSec * TimeSpan.TicksPerSecond);

        public Data(float durationSec, long start)
        {
            this.durationSec = durationSec;
            this.start = start;
        }
    }

    private const float FRACTION_PART = 10f;

    private const int cActionbarNum = 37;

    private readonly Data[] data;
    private readonly ILogger<ActionBarCooldownReader> logger;

    public ActionBarCooldownReader(ILogger<ActionBarCooldownReader> logger)
    {
        this.logger = logger;
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

        // Bounds check - protect against out-of-bounds access
        if (slotIdx < 0 || slotIdx >= data.Length)
        {
            LogInvalidSlotIndex(logger, slotIdx, value, data.Length);
            return;
        }

        if (DEBUG)
            LogCooldownUpdate(logger, slotIdx + 1, durationSec);

        data[slotIdx] = new(durationSec, GetTimestamp());
    }

    public void Reset()
    {
        var span = data.AsSpan();
        span.Fill(new(0, GetTimestamp()));
    }

    public int Get(KeyAction keyAction)
    {
        int index = keyAction.SlotIndex;

        ref readonly Data d = ref data[index];

        return Max((int)((d.End - GetTimestamp()) / TimeSpan.TicksPerMillisecond), 0);
    }

    #region Logging

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "ActionBarCooldownReader: Invalid slot index {slotIdx} from value {value} (array length: {arrayLength}). Skipping update.")]
    static partial void LogInvalidSlotIndex(ILogger logger, int slotIdx, int value, int arrayLength);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Trace,
        Message = "ActionBarCooldownReader: Slot {slot} cooldown {durationSec}s")]
    static partial void LogCooldownUpdate(ILogger logger, int slot, float durationSec);

    #endregion
}
