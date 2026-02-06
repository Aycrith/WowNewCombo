using System.Runtime.CompilerServices;

namespace Core.CombatRotation;

/// <summary>
/// Zero-allocation per-tick snapshot of all combat-relevant game state.
/// Captured once at the start of each CombatGoal.Update() tick.
/// </summary>
[SkipLocalsInit]
public readonly record struct GameStateSnapshot(
    int HealthPercent,
    int ResourcePercent,
    int ResourceCurrent,
    int ResourceMax,
    int ComboPoints,
    int TargetHealthPercent,
    int GcdRemainingMs,
    int NetworkLatencyMs,
    int SpellQueueMs,
    int MainHandSwingElapsedMs,
    int MainHandSpeedMs,
    int MobCount,
    bool InCombat,
    bool TargetAlive,
    bool IsTargetCasting,
    long TickTimestamp)
{
    /// <summary>
    /// Whether the target is in execute range (below 20% health).
    /// </summary>
    public bool IsExecutePhase => TargetHealthPercent > 0 && TargetHealthPercent <= 20;

    /// <summary>
    /// Percentage of resource available (0-100).
    /// </summary>
    public float ResourceFraction => ResourceMax > 0
        ? (float)ResourceCurrent / ResourceMax
        : 0f;

    /// <summary>
    /// Estimated resource at GCD end using linear projection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ProjectedResourceAtGcdEnd(float regenPerMs)
    {
        int projected = ResourceCurrent + (int)(regenPerMs * GcdRemainingMs);
        return projected > ResourceMax ? ResourceMax : projected;
    }
}
