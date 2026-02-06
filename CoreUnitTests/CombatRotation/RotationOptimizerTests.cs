using Core;
using Core.CombatRotation;
using Core.FeatureFlags;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace CoreUnitTests.CombatRotation;

/// <summary>
/// Tests the DpsRoleStrategy scoring formula behavior.
/// Since KeyAction.CanRun()/OnCooldown() require runtime state,
/// these tests verify the strategy's constants and structural properties.
/// </summary>
public sealed class DpsRoleStrategyTests
{
    [Fact]
    public void RoleName_ReturnsDPS()
    {
        DpsRoleStrategy strategy = new(NullLogger<DpsRoleStrategy>.Instance);
        Assert.Equal("DPS", strategy.RoleName);
    }

    [Fact]
    public void Strategy_CanBeConstructed()
    {
        // Verify DI-friendly constructor
        DpsRoleStrategy strategy = new(NullLogger<DpsRoleStrategy>.Instance);
        Assert.NotNull(strategy);
    }
}

/// <summary>
/// Tests the RotationOptimizer orchestration logic using a
/// deterministic mock strategy.
/// </summary>
public sealed class RotationOptimizerTests
{
    /// <summary>
    /// A test strategy that assigns scores based on a predetermined map.
    /// </summary>
    private sealed class FixedScoreStrategy : IRoleStrategy
    {
        private readonly float[] scores;

        public string RoleName => "Test";

        public FixedScoreStrategy(params float[] scores)
        {
            this.scores = scores;
        }

        public float ScoreAbility(KeyAction action, in GameStateSnapshot state, int sequenceIndex)
        {
            return sequenceIndex < scores.Length ? scores[sequenceIndex] : 0f;
        }
    }

    private static GameStateSnapshot CreateDefaultSnapshot()
    {
        return new GameStateSnapshot(
            HealthPercent: 80,
            ResourcePercent: 50,
            ResourceCurrent: 50,
            ResourceMax: 100,
            ComboPoints: 0,
            TargetHealthPercent: 60,
            GcdRemainingMs: 0,
            NetworkLatencyMs: 50,
            SpellQueueMs: 400,
            MainHandSwingElapsedMs: 0,
            MainHandSpeedMs: 2600,
            MobCount: 1,
            InCombat: true,
            TargetAlive: true,
            IsTargetCasting: false,
            TickTimestamp: Environment.TickCount64);
    }

    private static RotationOptimizer CreateOptimizer(
        IRoleStrategy strategy,
        bool enabled = true,
        bool fallback = true)
    {
        FeatureFlagsOptions flagsOptions = new()
        {
            CombatRotationOptimizer = new CombatRotationOptimizerOptions
            {
                Enabled = enabled,
                FallbackToStaticPriority = fallback,
                BaseWeightMultiplier = 1.0f
            }
        };

        FeatureFlagService flagService = new(
            NullLogger<FeatureFlagService>.Instance,
            new FixedOptionsMonitor<FeatureFlagsOptions>(flagsOptions),
            Options.Create(new FeatureFlagServiceOptions()));

        return new RotationOptimizer(
            NullLogger<RotationOptimizer>.Instance,
            flagService,
            strategy);
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenFeatureFlagDisabled()
    {
        RotationOptimizer optimizer = CreateOptimizer(
            new FixedScoreStrategy(1f), enabled: false);

        Assert.False(optimizer.IsEnabled);
    }

    [Fact]
    public void IsEnabled_ReturnsTrue_WhenFeatureFlagEnabled()
    {
        RotationOptimizer optimizer = CreateOptimizer(
            new FixedScoreStrategy(1f), enabled: true);

        Assert.True(optimizer.IsEnabled);
    }

    [Fact]
    public void Optimize_SortsByDescendingScore()
    {
        // Abilities scored: [1.0, 3.0, 2.0] → expected order: [1, 2, 0]
        FixedScoreStrategy strategy = new(1.0f, 3.0f, 2.0f);
        RotationOptimizer optimizer = CreateOptimizer(strategy);

        KeyAction[] keys = [new KeyAction(), new KeyAction(), new KeyAction()];
        GameStateSnapshot snapshot = CreateDefaultSnapshot();
        Span<int> sorted = stackalloc int[3];

        int count = optimizer.Optimize(keys, in snapshot, sorted);

        Assert.Equal(3, count);
        Assert.Equal(1, sorted[0]); // highest score (3.0)
        Assert.Equal(2, sorted[1]); // middle score (2.0)
        Assert.Equal(0, sorted[2]); // lowest score (1.0)
    }

    [Fact]
    public void Optimize_EmptyKeys_ReturnsZero()
    {
        FixedScoreStrategy strategy = new();
        RotationOptimizer optimizer = CreateOptimizer(strategy);

        ReadOnlySpan<KeyAction> keys = ReadOnlySpan<KeyAction>.Empty;
        GameStateSnapshot snapshot = CreateDefaultSnapshot();
        Span<int> sorted = Span<int>.Empty;

        int count = optimizer.Optimize(keys, in snapshot, sorted);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Optimize_SingleAbility_ReturnsIndex0()
    {
        FixedScoreStrategy strategy = new(5.0f);
        RotationOptimizer optimizer = CreateOptimizer(strategy);

        KeyAction[] keys = [new KeyAction()];
        GameStateSnapshot snapshot = CreateDefaultSnapshot();
        Span<int> sorted = stackalloc int[1];

        int count = optimizer.Optimize(keys, in snapshot, sorted);

        Assert.Equal(1, count);
        Assert.Equal(0, sorted[0]);
    }

    [Fact]
    public void Optimize_EqualScores_PreservesOriginalOrder()
    {
        // All same score — insertion sort is stable, should keep 0,1,2 order
        FixedScoreStrategy strategy = new(5.0f, 5.0f, 5.0f);
        RotationOptimizer optimizer = CreateOptimizer(strategy);

        KeyAction[] keys = [new KeyAction(), new KeyAction(), new KeyAction()];
        GameStateSnapshot snapshot = CreateDefaultSnapshot();
        Span<int> sorted = stackalloc int[3];

        int count = optimizer.Optimize(keys, in snapshot, sorted);

        Assert.Equal(3, count);
        Assert.Equal(0, sorted[0]);
        Assert.Equal(1, sorted[1]);
        Assert.Equal(2, sorted[2]);
    }

    [Fact]
    public void Optimize_ReverseOrder_CorrectlySorts()
    {
        // Scores in ascending order should be reversed
        FixedScoreStrategy strategy = new(1.0f, 2.0f, 3.0f, 4.0f, 5.0f);
        RotationOptimizer optimizer = CreateOptimizer(strategy);

        KeyAction[] keys = [
            new KeyAction(), new KeyAction(), new KeyAction(),
            new KeyAction(), new KeyAction()
        ];
        GameStateSnapshot snapshot = CreateDefaultSnapshot();
        Span<int> sorted = stackalloc int[5];

        int count = optimizer.Optimize(keys, in snapshot, sorted);

        Assert.Equal(5, count);
        Assert.Equal(4, sorted[0]); // score 5.0
        Assert.Equal(3, sorted[1]); // score 4.0
        Assert.Equal(2, sorted[2]); // score 3.0
        Assert.Equal(1, sorted[3]); // score 2.0
        Assert.Equal(0, sorted[4]); // score 1.0
    }

    [Fact]
    public void RecordCastResult_DoesNotThrow()
    {
        FixedScoreStrategy strategy = new(1.0f);
        RotationOptimizer optimizer = CreateOptimizer(strategy);
        KeyAction action = new();

        // Should not throw even without metrics collector
        optimizer.RecordCastResult(action, success: true);
        optimizer.RecordCastResult(action, success: false);
    }

    private sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        private readonly T value = value;

        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
