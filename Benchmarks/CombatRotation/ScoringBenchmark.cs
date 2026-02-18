using System.Diagnostics.CodeAnalysis;

using BenchmarkDotNet.Attributes;

using Core.CombatRotation;

namespace Benchmarks.CombatRotation;

/// <summary>
/// Benchmarks the scoring engine hot path to ensure it stays under 50μs per tick.
/// Uses GameStateSnapshot directly and measures allocation overhead.
/// </summary>
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "BenchmarkDotNet requires instance methods")]
[MemoryDiagnoser]
[ShortRunJob]
public class ScoringBenchmark
{
    private GameStateSnapshot snapshot;

    [GlobalSetup]
    public void Setup()
    {
        snapshot = new GameStateSnapshot(
            HealthPercent: 85,
            ResourcePercent: 60,
            ResourceCurrent: 60,
            ResourceMax: 100,
            ComboPoints: 3,
            TargetHealthPercent: 45,
            GcdRemainingMs: 200,
            NetworkLatencyMs: 50,
            SpellQueueMs: 400,
            MainHandSwingElapsedMs: 0,
            MainHandSpeedMs: 2600,
            MobCount: 1,
            InCombat: true,
            TargetAlive: true,
            IsTargetCasting: false,
            TickTimestamp: 0);
    }

    [Benchmark]
    public bool SnapshotCreation()
    {
        GameStateSnapshot s = new(
            HealthPercent: 85,
            ResourcePercent: 60,
            ResourceCurrent: 60,
            ResourceMax: 100,
            ComboPoints: 3,
            TargetHealthPercent: 45,
            GcdRemainingMs: 200,
            NetworkLatencyMs: 50,
            SpellQueueMs: 400,
            MainHandSwingElapsedMs: 0,
            MainHandSpeedMs: 2600,
            MobCount: 1,
            InCombat: true,
            TargetAlive: true,
            IsTargetCasting: false,
            TickTimestamp: 0);

        return s.IsExecutePhase;
    }

    [Benchmark]
    public int ResourceForecast()
    {
        return snapshot.ProjectedResourceAtGcdEnd(regenPerMs: 0.02f);
    }

    [Benchmark]
    public float ResourceFraction()
    {
        return snapshot.ResourceFraction;
    }

    [Benchmark]
    public bool ExecutePhaseCheck()
    {
        return snapshot.IsExecutePhase;
    }
}
