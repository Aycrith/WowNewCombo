using System;
using System.Collections.Generic;

namespace Core
{
    public sealed class AuraTimeReader
    {
        public readonly struct Data
        {
            public int DurationSec { get; }
            public DateTime StartTime { get; }

            public Data(int duration, DateTime startTime)
            {
                DurationSec = duration;
                StartTime = startTime;
            }
        }

        private const int UNLIMITED = 14400; // 4 hours - anything above considered unlimited duration

        private readonly int cTextureId;
        private readonly int cDurationSec;

        private readonly Dictionary<int, Data> data = new();

        public AuraTimeReader(int cTextureId, int cDurationSec)
        {
            this.cTextureId = cTextureId;
            this.cDurationSec = cDurationSec;
            Reset();
        }

        public void Read(IAddonDataProvider reader)
        {
            int textureId = reader.GetInt(cTextureId);
            if (textureId == 0) return;

            int durationSec = reader.GetInt(cDurationSec);
            data[textureId] = new(durationSec, DateTime.UtcNow);
        }

        public void Reset()
        {
            data.Clear();
        }

        public int GetRemainingTimeMs(int textureId)
        {
            return data.TryGetValue(textureId, out Data d) ?
                Math.Max(0, d.DurationSec >= UNLIMITED ? 1 : (int)(d.StartTime.AddSeconds(d.DurationSec) - DateTime.UtcNow).TotalMilliseconds)
                : 0;
        }

        public int GetTotalTimeMs(KeyAction keyAction)
        {
            return data[keyAction.SlotIndex].DurationSec * 1000;
        }

    }
}
