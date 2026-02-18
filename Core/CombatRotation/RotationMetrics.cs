using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Core.CombatRotation;

/// <summary>
/// Per-ability usage statistics for metrics reporting.
/// Thread-safe for concurrent RecordAttempt calls.
/// </summary>
public sealed class AbilityUsageStat
{
    public string Name { get; set; } = string.Empty;

    private int attemptCount;
    public int AttemptCount
    {
        get => attemptCount;
        set => attemptCount = value;
    }

    private int successCount;
    public int SuccessCount
    {
        get => successCount;
        set => successCount = value;
    }

    private float totalScore;
    public float TotalScore
    {
        get => totalScore;
        set => totalScore = value;
    }

    public float AverageScore
    {
        get
        {
            int count = attemptCount;
            return count > 0 ? totalScore / count : 0f;
        }
        set { } // no-op setter for backward compatibility
    }

    public float SuccessRate => attemptCount > 0
        ? (float)successCount / attemptCount
        : 0f;

    /// <summary>
    /// Thread-safe increment of AttemptCount.
    /// </summary>
    internal void IncrementAttemptCount() =>
        Interlocked.Increment(ref attemptCount);

    /// <summary>
    /// Thread-safe increment of SuccessCount.
    /// </summary>
    internal void IncrementSuccessCount() =>
        Interlocked.Increment(ref successCount);

    /// <summary>
    /// Thread-safe addition to TotalScore.
    /// </summary>
    internal void AddScore(float score)
    {
        float initial, updated;
        do
        {
            initial = totalScore;
            updated = initial + score;
        } while (Interlocked.CompareExchange(ref totalScore, updated, initial) != initial);
    }
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
        stat.IncrementAttemptCount();
        stat.AddScore(score);

        if (success)
        {
            stat.IncrementSuccessCount();
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
