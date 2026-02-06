using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Core.CombatRotation;

/// <summary>
/// Per-ability usage statistics for metrics reporting.
/// </summary>
public sealed class AbilityUsageStat
{
    public string Name { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int SuccessCount { get; set; }
    public float AverageScore { get; set; }
    public float TotalScore { get; set; }

    public float SuccessRate => AttemptCount > 0
        ? (float)SuccessCount / AttemptCount
        : 0f;
}

/// <summary>
/// Aggregated rotation metrics for a combat session.
/// </summary>
public sealed class RotationSessionMetrics
{
    public long SessionStartTicks { get; set; }
    public long SessionEndTicks { get; set; }
    public int TotalTicks { get; set; }
    public int OptimizedTicks { get; set; }
    public int FallbackTicks { get; set; }
    public ConcurrentDictionary<string, AbilityUsageStat> AbilityStats { get; } = new();

    /// <summary>
    /// Records an ability attempt, creating the stat entry if necessary.
    /// </summary>
    public void RecordAttempt(string abilityName, float score, bool success)
    {
        AbilityUsageStat stat = AbilityStats.GetOrAdd(abilityName, _ => new AbilityUsageStat { Name = abilityName });
        stat.AttemptCount++;
        stat.TotalScore += score;
        stat.AverageScore = stat.TotalScore / stat.AttemptCount;

        if (success)
        {
            stat.SuccessCount++;
        }
    }

    /// <summary>
    /// Gets a summary of ability usage ordered by attempt count.
    /// </summary>
    public IEnumerable<AbilityUsageStat> GetOrderedStats()
    {
        List<AbilityUsageStat> stats = new(AbilityStats.Values);
        stats.Sort((a, b) => b.AttemptCount.CompareTo(a.AttemptCount));
        return stats;
    }
}
