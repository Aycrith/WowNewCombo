namespace Core.Goals;

/// <summary>
/// Centralized timeout and threshold constants shared across goal implementations.
/// Consolidates magic numbers that were previously duplicated across goal files,
/// enabling future per-profile tuning from a single location.
/// </summary>
public static class GoalTimeouts
{
    /// <summary>Maximum time in milliseconds to reach melee range before aborting.</summary>
    public const int MaxTimeToReachMeleeMs = 10_000;
}
