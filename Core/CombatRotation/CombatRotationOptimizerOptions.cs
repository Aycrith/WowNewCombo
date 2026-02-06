namespace Core.CombatRotation;

/// <summary>
/// Feature flag options for the Combat Rotation Optimizer.
/// Follows the BehaviorTreeCombatOptions pattern.
/// Bound from runtime_feature_flags.json under Features.CombatRotationOptimizer.
/// </summary>
public sealed class CombatRotationOptimizerOptions
{
    /// <summary>
    /// Master enable/disable toggle. Default: false (disabled).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// When true, falls back to static priority ordering on any
    /// scoring error. Default: true.
    /// </summary>
    public bool FallbackToStaticPriority { get; set; } = true;

    /// <summary>
    /// Global multiplier applied to all base weights.
    /// Use 1.0 for default behavior. Values > 1.0 amplify weight
    /// differences; values &lt; 1.0 flatten them.
    /// </summary>
    public float BaseWeightMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Enable rotation performance metrics collection and logging.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Enable simple linear resource forecasting to predict
    /// resource availability at GCD end.
    /// </summary>
    public bool EnableResourceForecasting { get; set; } = true;

    /// <summary>
    /// Enable swing timer alignment scoring for instant abilities.
    /// Phase 2 feature — disabled by default.
    /// </summary>
    public bool EnableSwingTimerAlignment { get; set; }

    /// <summary>
    /// Interval in seconds between metrics log flushes.
    /// </summary>
    public int MetricsFlushIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Path for the JSON metrics log file.
    /// </summary>
    public string MetricsOutputPath { get; set; } = "logs/rotation_metrics.json";
}
