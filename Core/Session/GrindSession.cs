using System;
using Newtonsoft.Json;

namespace Core.Session
{
    public class GrindSession
    {
        [JsonIgnore]
        public int[] ExpList { get; set; } = Array.Empty<int>();

        public Guid SessionId { get; set; }
        public string PathName { get; set; } = string.Empty;
        public PlayerClassEnum PlayerClass { get; set; }
        public DateTime SessionStart { get; set; }
        public DateTime SessionEnd { get; set; }
        [JsonIgnore]
        public int TotalTimeInMinutes => (int)(SessionEnd - SessionStart).TotalMinutes;
        public int LevelFrom { get; set; }
        public float XpFrom { get; set; }
        public int LevelTo { get; set; }
        public float XpTo { get; set; }
        public int MobsKilled { get; set; }
        public float MobsPerMinute => MathF.Round(MobsKilled / (float)TotalTimeInMinutes, 2);
        public int Death { get; set; }
        public string? Reason { get; set; }
        [JsonIgnore]
        public float ExperiencePerHour => TotalTimeInMinutes == 0 ? 0 : MathF.Round(ExpGetInBotSession / TotalTimeInMinutes * 60f, 0);
        [JsonIgnore]
        public float ExpGetInBotSession
        {
            get
            {
                if (ExpList.Length == 0)
                    return 0;

                int maxLevel = ExpList.Length + 1;
                if (LevelFrom == maxLevel)
                    return 0;

                if (LevelFrom == maxLevel - 1 && LevelTo == maxLevel)
                    return ExpList[LevelFrom - 1] - XpFrom;

                if (LevelTo == LevelFrom)
                {
                    return XpTo - XpFrom;
                }

                if (LevelTo > LevelFrom)
                {
                    float expSoFar = XpTo;

                    for (int i = 0; i < LevelTo - LevelFrom; i++)
                    {
                        expSoFar += ExpList[LevelFrom - 1 + i] - XpFrom;
                        XpFrom = 0;
                        if (LevelTo > maxLevel)
                            break;
                    }

                    return expSoFar;
                }

                return 0;
            }
        }
    }
}
