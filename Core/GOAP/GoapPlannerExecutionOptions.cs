namespace Core.GOAP;

/// <summary>
/// Per-call planner execution options.
/// Defaults preserve legacy planner behavior (no caching).
/// </summary>
public readonly record struct GoapPlannerExecutionOptions(
    bool EnableUsableGoalCache = false,
    bool EnablePlanCache = false,
    int MaxUsableCacheEntries = 64,
    int MaxPlanCacheEntries = 64);
