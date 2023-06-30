using System;

namespace Core;

public sealed class LevelTracker : IDisposable
{
    private readonly PlayerReader playerReader;

    private DateTime levelStartTime = DateTime.UtcNow;
    private int levelStartXP;

    public TimeSpan TimeToLevel { get; private set; } = TimeSpan.Zero;
    public DateTime PredictedLevelUpTime { get; private set; } = DateTime.MaxValue;

    public LevelTracker(PlayerReader playerReader)
    {
        this.playerReader = playerReader;

        playerReader.Level.Changed += PlayerLevel_Changed;
        playerReader.PlayerXp.Changed += PlayerExp_Changed;
    }

    public void Dispose()
    {
        playerReader.Level.Changed -= PlayerLevel_Changed;
        playerReader.PlayerXp.Changed -= PlayerExp_Changed;
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

    public void UpdateExpPerHour()
    {
        double runningSeconds = (DateTime.UtcNow - levelStartTime).TotalSeconds;
        double xpPerSecond = (playerReader.PlayerXp.Value - levelStartXP) / runningSeconds;
        double secondsLeft = (playerReader.PlayerMaxXp - playerReader.PlayerXp.Value) / xpPerSecond;

        TimeToLevel = xpPerSecond > 0 ? TimeSpan.FromSeconds(secondsLeft) : TimeSpan.Zero;

        if (secondsLeft > 0 && secondsLeft < 60 * 60 * 10)
        {
            PredictedLevelUpTime = DateTime.UtcNow.AddSeconds(secondsLeft).ToLocalTime();
        }
    }
}