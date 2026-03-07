using Core.GOAP;

using System;

namespace Core;

public sealed record CachedSessionStats(
    int Kills,
    int Deaths,
    int Seconds,
    int Minutes,
    int Hours,
    string RuntimeMode,
    string? CurrentGoal,
    bool BotActive,
    DateTime LastUpdatedUtc);

public sealed class SessionStatsCache
{
    private readonly object syncRoot = new();
    private CachedSessionStats? snapshot;

    public CachedSessionStats? GetSnapshot()
    {
        lock (syncRoot)
        {
            return snapshot;
        }
    }

    public void Capture(SessionStat sessionStat, string runtimeMode, string? currentGoal, bool botActive)
    {
        ArgumentNullException.ThrowIfNull(sessionStat);

        lock (syncRoot)
        {
            snapshot = new CachedSessionStats(
                Kills: sessionStat.Kills,
                Deaths: sessionStat.Deaths,
                Seconds: sessionStat.Seconds,
                Minutes: sessionStat.Minutes,
                Hours: sessionStat.Hours,
                RuntimeMode: runtimeMode,
                CurrentGoal: currentGoal,
                BotActive: botActive,
                LastUpdatedUtc: DateTime.UtcNow);
        }
    }

    public void Capture(GoapAgent goapAgent, string runtimeMode)
    {
        ArgumentNullException.ThrowIfNull(goapAgent);
        Capture(
            goapAgent.SessionStat,
            runtimeMode,
            goapAgent.CurrentGoal?.GetType().Name,
            goapAgent.Active);
    }
}
