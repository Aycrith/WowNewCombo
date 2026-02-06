using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Core.FeatureFlags;

using Microsoft.Extensions.Logging;

namespace Core.CombatRotation;

/// <summary>
/// Main rotation optimizer implementing IRotationOptimizer.
/// Scores all abilities per tick and produces sorted indices for CombatGoal.
///
/// Design:
/// - Zero-allocation hot path using Span&lt;T&gt; and stackalloc
/// - Falls back to original order on any exception when FallbackToStaticPriority is true
/// - Completely no-op when IsEnabled is false (zero overhead)
///
/// Pattern reference: SimulationCraft APL (Action Priority List)
/// </summary>
public sealed class RotationOptimizer : IRotationOptimizer
{
    private readonly ILogger<RotationOptimizer> logger;
    private readonly FeatureFlagService featureFlags;
    private readonly IRoleStrategy roleStrategy;
    private readonly RotationMetricsCollector? metricsCollector;

    public bool IsEnabled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => featureFlags.IsCombatRotationOptimizerEnabled;
    }

    public RotationOptimizer(
        ILogger<RotationOptimizer> logger,
        FeatureFlagService featureFlags,
        IRoleStrategy roleStrategy,
        RotationMetricsCollector? metricsCollector = null)
    {
        this.logger = logger;
        this.featureFlags = featureFlags;
        this.roleStrategy = roleStrategy;
        this.metricsCollector = metricsCollector;
    }

    /// <summary>
    /// Scores and sorts abilities by priority for the current tick.
    /// Uses insertion sort for small N (typically 5-20 abilities) which
    /// is cache-friendly and allocation-free on stack.
    /// </summary>
    [SkipLocalsInit]
    public int Optimize(ReadOnlySpan<KeyAction> keys, in GameStateSnapshot state, Span<int> sortedIndices)
    {
        int count = keys.Length;
        if (count == 0)
            return 0;

        CombatRotationOptimizerOptions options = featureFlags.Current.CombatRotationOptimizer;

        try
        {
            // Score each ability
            Span<float> scores = count <= 32
                ? stackalloc float[count]
                : new float[count]; // Fallback for unusually large rotations

            float weightMultiplier = options.BaseWeightMultiplier;

            for (int i = 0; i < count; i++)
            {
                sortedIndices[i] = i;
                float score = roleStrategy.ScoreAbility(keys[i], in state, i);
                scores[i] = score == float.MinValue ? float.MinValue : score * weightMultiplier;
            }

            // Insertion sort by descending score — optimal for small N, stable, zero-allocation
            for (int i = 1; i < count; i++)
            {
                int keyIdx = sortedIndices[i];
                float keyScore = scores[i];
                int j = i - 1;

                while (j >= 0 && scores[j] < keyScore)
                {
                    sortedIndices[j + 1] = sortedIndices[j];
                    scores[j + 1] = scores[j];
                    j--;
                }

                sortedIndices[j + 1] = keyIdx;
                scores[j + 1] = keyScore;
            }

            metricsCollector?.RecordOptimizedTick();
            return count;
        }
        catch (Exception ex) when (options.FallbackToStaticPriority)
        {
            logger.LogWarning(ex, "[RotationOptimizer] Scoring failed, falling back to static priority");
            metricsCollector?.RecordFallbackTick();

            // Restore original order
            for (int i = 0; i < count; i++)
            {
                sortedIndices[i] = i;
            }

            return count;
        }
    }

    public void RecordCastResult(KeyAction action, bool success)
    {
        metricsCollector?.RecordCastAttempt(action.Name, 0f, success);
    }
}
