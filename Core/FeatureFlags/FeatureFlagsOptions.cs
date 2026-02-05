using System.Collections.Generic;

namespace Core.FeatureFlags;

/// <summary>
/// Runtime feature flags options bound from runtime_feature_flags.json.
/// Supports hot-reload without application restart.
/// </summary>
public sealed class FeatureFlagsOptions
{
    public const string Position = "Features";

    public ObjectPoolingOptions ObjectPooling { get; set; } = new();
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
    public PathSmoothingOptions PathSmoothing { get; set; } = new();
    public StuckRecoveryV2Options StuckRecoveryV2 { get; set; } = new();
    public HazardAvoidanceOptions HazardAvoidance { get; set; } = new();
    public HumanizationOptions Humanization { get; set; } = new();
    public AIProfileGeneratorOptions AIProfileGenerator { get; set; } = new();
    public ProfileMarketplaceOptions ProfileMarketplace { get; set; } = new();
    public BehaviorTreeCombatOptions BehaviorTreeCombat { get; set; } = new();
    public HybridLLMDecisionOptions HybridLLMDecision { get; set; } = new();

    public bool GlobalKillSwitch { get; set; }
    public bool DebugMode { get; set; }
}

/// <summary>
/// Object pooling to reduce GC pressure in hot paths.
/// </summary>
public sealed class ObjectPoolingOptions
{
    public bool Enabled { get; set; } = true;
    public int RolloutPercentage { get; set; } = 100;
    public int MaxPoolSize { get; set; } = 100;
}

/// <summary>
/// Circuit breaker for external service resilience.
/// </summary>
public sealed class CircuitBreakerOptions
{
    public bool Enabled { get; set; } = true;
    public int PathfindingThreshold { get; set; } = 5;
    public int PathfindingCooldownSeconds { get; set; } = 60;
    public int LLMThreshold { get; set; } = 3;
    public int LLMCooldownSeconds { get; set; } = 120;
}

/// <summary>
/// Path simplification using Ramer-Douglas-Peucker algorithm.
/// </summary>
public sealed class PathSmoothingOptions
{
    public bool Enabled { get; set; } = true;
    public float RDPTolerance { get; set; } = 2.0f;
    public int MinWaypointsToSimplify { get; set; } = 5;
}

/// <summary>
/// Enhanced stuck recovery with breadcrumb tracking.
/// </summary>
public sealed class StuckRecoveryV2Options
{
    public bool Enabled { get; set; } = true;
    public int BreadcrumbTrailSize { get; set; } = 50;
    public int BacktrackSteps { get; set; } = 3;
    public int EmergencyHearthstoneThreshold { get; set; } = 10;
}

/// <summary>
/// Self-learning hazard avoidance using DBSCAN clustering.
/// </summary>
public sealed class HazardAvoidanceOptions
{
    public bool Enabled { get; set; }
    public float DBSCANEpsilon { get; set; } = 15.0f;
    public int DBSCANMinPoints { get; set; } = 2;
    public float HazardCostMultiplier { get; set; } = 10.0f;
    public int DecayHalfLifeDays { get; set; } = 30;
    public int MaxEventsBeforePrune { get; set; } = 10000;
    public int ClusteringIntervalSeconds { get; set; } = 60;
    public int SaveIntervalMinutes { get; set; } = 5;
}

/// <summary>
/// Human-like behavior timing and input shaping. Disabled by default.
/// </summary>
public sealed class HumanizationOptions
{
    public bool Enabled { get; set; }

    public HumanizationInputTimingOptions InputTiming { get; set; } = new();

    public HumanizationMouseMovementOptions MouseMovement { get; set; } = new();

    public HumanizationFatigueOptions Fatigue { get; set; } = new();

    public HumanizationBehaviorOptions Behavior { get; set; } = new();
}

public sealed class HumanizationInputTimingOptions
{
    public int KeyHoldMeanMs { get; set; } = 55;
    public int KeyHoldStdDevMs { get; set; } = 15;
    public int KeyHoldMinMs { get; set; } = 30;
    public int KeyHoldMaxMs { get; set; } = 200;

    public int ReactionMaxMs { get; set; } = 500;
}

public sealed class HumanizationMouseMovementOptions
{
    public bool Enabled { get; set; } = true;

    public int StepsPerMovement { get; set; } = 15;

    public double CurveIntensity { get; set; } = 0.30;

    public int MicroJitterPixels { get; set; } = 2;

    public double OvershootProbability { get; set; } = 0.08;

    public int StepDelayMinMs { get; set; } = 1;

    public int StepDelayMaxMs { get; set; } = 6;
}

public sealed class HumanizationFatigueOptions
{
    public bool Enabled { get; set; } = true;

    public int BreakIntervalMinutes { get; set; } = 45;

    public double BreakDurationMinMinutes { get; set; } = 1.0;

    public double BreakDurationMaxMinutes { get; set; } = 5.0;

    public double FatigueRatePerHour { get; set; } = 0.10;

    public double MaxFatigueMultiplier { get; set; } = 1.5;
}

public sealed class HumanizationBehaviorOptions
{
    public bool MicroPauseEnabled { get; set; } = true;

    public int MicroPauseIntervalSeconds { get; set; } = 60;

    public int MicroPauseMinMs { get; set; } = 200;

    public int MicroPauseMaxMs { get; set; } = 2000;
}

/// <summary>
/// LLM-powered profile generation from natural language.
/// </summary>
public sealed class AIProfileGeneratorOptions
{
    public bool Enabled { get; set; }
    public string APIProvider { get; set; } = "none";
    public int MaxTokensPerRequest { get; set; } = 4000;
    public int RateLimitPerHour { get; set; } = 20;
    public int CacheResponsesMinutes { get; set; } = 60;
    public string[] AllowedProviders { get; set; } = ["openai", "anthropic", "local"];
}

/// <summary>
/// Community profile marketplace from GitHub repository.
/// </summary>
public sealed class ProfileMarketplaceOptions
{
    public bool Enabled { get; set; }
    public string RepositoryUrl { get; set; } = "https://api.github.com/repos/Xian55/WowClassicGrindBot-Profiles";
    public int CacheDurationMinutes { get; set; } = 5;
    public int MaxDownloadsPerSession { get; set; } = 10;
}

/// <summary>
/// Alternative combat system using behavior trees.
/// </summary>
public sealed class BehaviorTreeCombatOptions
{
    public bool Enabled { get; set; }
    public bool FallbackToGOAP { get; set; } = true;
    public bool EnableVisualization { get; set; }
}

/// <summary>
/// Hybrid GOAP+LLM decision system for edge cases.
/// </summary>
public sealed class HybridLLMDecisionOptions
{
    public bool Enabled { get; set; }
    public float ConfidenceThreshold { get; set; } = 0.6f;
    public int MaxLatencyMs { get; set; } = 2000;
    public bool EnableForUnexpectedStates { get; set; } = true;
    public int CacheDecisionsSeconds { get; set; } = 5;
}

/// <summary>
/// Monitoring thresholds for alerting.
/// </summary>
public sealed class MonitoringOptions
{
    public const string Position = "Monitoring";

    public bool Enabled { get; set; } = true;
    public int MetricsIntervalSeconds { get; set; } = 30;
    public MonitoringThresholds Thresholds { get; set; } = new();
}

public sealed class MonitoringThresholds
{
    public int PathfindingLatencyWarningMs { get; set; } = 500;
    public int PathfindingLatencyCriticalMs { get; set; } = 2000;
    public int HazardClusterCountWarning { get; set; } = 1000;
    public int HazardClusterCountCritical { get; set; } = 5000;
    public int LLMLatencyWarningMs { get; set; } = 3000;
    public int LLMLatencyCriticalMs { get; set; } = 10000;
    public int StuckRecoveryAttemptsWarning { get; set; } = 5;
    public int StuckRecoveryAttemptsCritical { get; set; } = 10;
    public int MemoryUsageMBWarning { get; set; } = 500;
    public int MemoryUsageMBCritical { get; set; } = 1000;
    public int FrameProcessingMsWarning { get; set; } = 100;
    public int FrameProcessingMsCritical { get; set; } = 200;
}

/// <summary>
/// Extension methods for feature flag checking.
/// </summary>
public static class FeatureFlagExtensions
{
    /// <summary>
    /// Checks if a named feature is enabled, respecting global kill switch.
    /// </summary>
    public static bool IsEnabled(this FeatureFlagsOptions flags, string featureName)
    {
        if (flags.GlobalKillSwitch) return false;

        return featureName switch
        {
            nameof(FeatureFlagsOptions.ObjectPooling) => flags.ObjectPooling.Enabled,
            nameof(FeatureFlagsOptions.CircuitBreaker) => flags.CircuitBreaker.Enabled,
            nameof(FeatureFlagsOptions.PathSmoothing) => flags.PathSmoothing.Enabled,
            nameof(FeatureFlagsOptions.StuckRecoveryV2) => flags.StuckRecoveryV2.Enabled,
            nameof(FeatureFlagsOptions.HazardAvoidance) => flags.HazardAvoidance.Enabled,
            nameof(FeatureFlagsOptions.Humanization) => flags.Humanization.Enabled,
            nameof(FeatureFlagsOptions.AIProfileGenerator) => flags.AIProfileGenerator.Enabled,
            nameof(FeatureFlagsOptions.ProfileMarketplace) => flags.ProfileMarketplace.Enabled,
            nameof(FeatureFlagsOptions.BehaviorTreeCombat) => flags.BehaviorTreeCombat.Enabled,
            nameof(FeatureFlagsOptions.HybridLLMDecision) => flags.HybridLLMDecision.Enabled,
            _ => false
        };
    }

    /// <summary>
    /// Gets all enabled feature names for logging/diagnostics.
    /// </summary>
    public static IEnumerable<string> GetEnabledFeatures(this FeatureFlagsOptions flags)
    {
        if (flags.GlobalKillSwitch)
            yield break;

        if (flags.ObjectPooling.Enabled) yield return nameof(FeatureFlagsOptions.ObjectPooling);
        if (flags.CircuitBreaker.Enabled) yield return nameof(FeatureFlagsOptions.CircuitBreaker);
        if (flags.PathSmoothing.Enabled) yield return nameof(FeatureFlagsOptions.PathSmoothing);
        if (flags.StuckRecoveryV2.Enabled) yield return nameof(FeatureFlagsOptions.StuckRecoveryV2);
        if (flags.HazardAvoidance.Enabled) yield return nameof(FeatureFlagsOptions.HazardAvoidance);
        if (flags.Humanization.Enabled) yield return nameof(FeatureFlagsOptions.Humanization);
        if (flags.AIProfileGenerator.Enabled) yield return nameof(FeatureFlagsOptions.AIProfileGenerator);
        if (flags.ProfileMarketplace.Enabled) yield return nameof(FeatureFlagsOptions.ProfileMarketplace);
        if (flags.BehaviorTreeCombat.Enabled) yield return nameof(FeatureFlagsOptions.BehaviorTreeCombat);
        if (flags.HybridLLMDecision.Enabled) yield return nameof(FeatureFlagsOptions.HybridLLMDecision);
    }
}
