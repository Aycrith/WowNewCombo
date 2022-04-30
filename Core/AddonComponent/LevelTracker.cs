using System;

namespace Core
{
    public class LevelTracker : IDisposable
    {
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;

        private DateTime levelStartTime = DateTime.UtcNow;
        private int levelStartXP;

        public string TimeToLevel { get; private set; } = "∞";
        public DateTime PredictedLevelUpTime { get; private set; } = DateTime.MaxValue;

        public int MobsKilled { get; private set; }
        public int Death { get; private set; }

        public LevelTracker(AddonReader addonReader)
        {
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;

            playerReader.Level.Changed -= PlayerLevel_Changed;
            playerReader.Level.Changed += PlayerLevel_Changed;

            playerReader.PlayerXp.Changed -= PlayerExp_Changed;
            playerReader.PlayerXp.Changed += PlayerExp_Changed;

            addonReader.PlayerDeath -= OnPlayerDeath;
            addonReader.PlayerDeath += OnPlayerDeath;

            addonReader.CreatureHistory.KillCredit -= OnKillCredit;
            addonReader.CreatureHistory.KillCredit += OnKillCredit;
        }

        public void Dispose()
        {
            playerReader.Level.Changed -= PlayerLevel_Changed;
            playerReader.PlayerXp.Changed -= PlayerExp_Changed;
            addonReader.PlayerDeath -= OnPlayerDeath;
            addonReader.CreatureHistory.KillCredit -= OnKillCredit;
        }

        public void Reset()
        {
            MobsKilled = 0;
            Death = 0;

            UpdateExpPerHour();
        }

        private void PlayerExp_Changed()
        {
            UpdateExpPerHour();
        }

        private void PlayerLevel_Changed()
        {
            levelStartTime = DateTime.UtcNow;
            levelStartXP = playerReader.PlayerXp.Value;
        }

        private void OnPlayerDeath()
        {
            Death++;
        }

        private void OnKillCredit()
        {
            MobsKilled++;
        }

        public void UpdateExpPerHour()
        {
            var runningSeconds = (DateTime.UtcNow - levelStartTime).TotalSeconds;
            var xpPerSecond = (playerReader.PlayerXp.Value - levelStartXP) / runningSeconds;
            var secondsLeft = (playerReader.PlayerMaxXp - playerReader.PlayerXp.Value) / xpPerSecond;

            if (xpPerSecond > 0)
            {
                TimeToLevel = new TimeSpan(0, 0, (int)secondsLeft).ToString();
            }
            else
            {
                TimeToLevel = "∞";
            }

            if (secondsLeft > 0 && secondsLeft < 60 * 60 * 10)
            {
                PredictedLevelUpTime = DateTime.UtcNow.AddSeconds(secondsLeft).ToLocalTime();
            }
        }
    }
}