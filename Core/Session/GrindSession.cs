using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;

namespace Core.Session
{
    public class GrindSession : IGrindSession
    {
        private readonly IBotController botController;
        private readonly IGrindSessionHandler grindSessionHandler;
        private readonly CancellationTokenSource cts;

        private readonly int[] expList;

        private Thread? thread;

        public GrindSession(IBotController botController, IGrindSessionHandler grindSessionHandler, CancellationTokenSource cts)
        {
            this.botController = botController;
            this.grindSessionHandler = grindSessionHandler;
            this.cts = cts;

            expList = ExperienceProvider.GetExperienceList();
        }

        [JsonIgnore]
        public bool Active { get; set; }
        public Guid SessionId { get; set; }
        public string PathName { get; set; } = "No path selected";
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
                int maxLevel = expList.Length + 1;
                if (LevelFrom == maxLevel)
                    return 0;

                if (LevelFrom == maxLevel - 1 && LevelTo == maxLevel)
                    return expList[LevelFrom - 1] - XpFrom;

                if (LevelTo == LevelFrom)
                {
                    return XpTo - XpFrom;
                }

                if (LevelTo > LevelFrom)
                {
                    float expSoFar = XpTo;

                    for (int i = 0; i < LevelTo - LevelFrom; i++)
                    {
                        expSoFar += expList[LevelFrom - 1 + i] - XpFrom;
                        XpFrom = 0;
                        if (LevelTo > maxLevel)
                            break;
                    }

                    return expSoFar;
                }

                return 0;
            }
        }

        public void StartBotSession()
        {
            Active = true;

            SessionId = Guid.NewGuid();
            PathName = botController.SelectedPathFilename ?? botController.ClassConfig?.PathFilename ?? "No Path Selected";
            PlayerClass = botController.AddonReader.PlayerReader.Class;
            SessionStart = DateTime.UtcNow;
            LevelFrom = botController.AddonReader.PlayerReader.Level.Value;
            XpFrom = botController.AddonReader.PlayerReader.PlayerXp.Value;
            MobsKilled = botController.AddonReader.LevelTracker.MobsKilled;

            thread = new Thread(PeriodicSave);
            thread.Start();
        }

        public void StopBotSession(string reason, bool active)
        {
            Active = active;

            SessionEnd = DateTime.UtcNow;
            LevelTo = botController.AddonReader.PlayerReader.Level.Value;
            XpTo = botController.AddonReader.PlayerReader.PlayerXp.Value;
            Reason = reason;
            Death = botController.AddonReader.LevelTracker.Death;
            MobsKilled = botController.AddonReader.LevelTracker.MobsKilled;
            Save();
        }

        private void PeriodicSave()
        {
            while (Active && !cts.IsCancellationRequested)
            {
                StopBotSession("auto save", true);
                cts.Token.WaitHandle.WaitOne(TimeSpan.FromMinutes(1));
            }
        }

        public void Save()
        {
            grindSessionHandler.Save(this);
        }

        public List<GrindSession> Load()
        {
            return grindSessionHandler.Load();
        }
    }
}
