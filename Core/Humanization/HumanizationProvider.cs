using Core.FeatureFlags;

using Microsoft.Extensions.Logging;

using SharedLib.Humanization;

using SixLabors.ImageSharp;

using System;
using System.Threading;

namespace Core.Humanization;

public sealed class HumanizationProvider : IHumanizationProvider, IDisposable
{
    private sealed class RuntimeConfig
    {
        public bool Enabled { get; init; }

        public int KeyHoldMeanMs { get; init; }
        public int KeyHoldStdDevMs { get; init; }
        public int KeyHoldMinMs { get; init; }
        public int KeyHoldMaxMs { get; init; }

        public int ReactionMaxMs { get; init; }

        public bool MouseEnabled { get; init; }
        public int MouseSteps { get; init; }
        public double MouseCurveIntensity { get; init; }
        public int MouseMicroJitterPixels { get; init; }
        public double MouseOvershootProbability { get; init; }
        public int MouseStepDelayMinMs { get; init; }
        public int MouseStepDelayMaxMs { get; init; }

        public int MicroPauseMinMs { get; init; }
        public int MicroPauseMaxMs { get; init; }
    }

    private readonly ILogger<HumanizationProvider> logger;
    private readonly FeatureFlagService featureFlags;
    private readonly FatigueSimulator fatigueSimulator;
    private readonly MicroPauseService microPauseService;
    private readonly HumanizationMetrics? metrics;

    private RuntimeConfig runtime = new RuntimeConfig();

    public HumanizationProvider(
        ILogger<HumanizationProvider> logger,
        FeatureFlagService featureFlags,
        FatigueSimulator fatigueSimulator,
        MicroPauseService microPauseService,
        HumanizationMetrics? metrics = null)
    {
        this.logger = logger;
        this.featureFlags = featureFlags;
        this.fatigueSimulator = fatigueSimulator;
        this.microPauseService = microPauseService;
        this.metrics = metrics;

        ApplyOptions(featureFlags.Current);
        featureFlags.OnFlagsChanged += ApplyOptions;
    }

    public bool Enabled => Volatile.Read(ref runtime).Enabled;

    public double FatigueMultiplier => fatigueSimulator.FatigueMultiplier;

    public bool IsOnBreak => fatigueSimulator.IsOnBreak;

    public TimeSpan RemainingBreakTime => fatigueSimulator.RemainingBreakTime;

    public int GetKeyHoldDurationMs(int baseMs)
    {
        RuntimeConfig cfg = Volatile.Read(ref runtime);
        if (!cfg.Enabled)
        {
            return baseMs;
        }

        int mean = baseMs > 0 ? baseMs : cfg.KeyHoldMeanMs;
        double fatigue = fatigueSimulator.FatigueMultiplier;
        double meanFatigued = mean * fatigue;

        int min = cfg.KeyHoldMinMs <= 0 ? 10 : cfg.KeyHoldMinMs;
        int max = cfg.KeyHoldMaxMs <= 0 ? Math.Max(min, 200) : cfg.KeyHoldMaxMs;
        if (baseMs > max)
        {
            max = baseMs * 3;
        }

        int duration = HumanizedRandom.NextGaussianInt(meanFatigued, cfg.KeyHoldStdDevMs, min, max);
        metrics?.RecordKeyPress(duration);
        return duration;
    }

    public int GetInterKeyDelayMs(int baseMs)
    {
        RuntimeConfig cfg = Volatile.Read(ref runtime);
        if (!cfg.Enabled)
        {
            return baseMs <= 0 ? 0 : baseMs;
        }

        // Special case: used for mouse step pacing.
        if (baseMs <= 0)
        {
            int min = Math.Max(0, cfg.MouseStepDelayMinMs);
            int max = Math.Max(min, cfg.MouseStepDelayMaxMs);
            return Random.Shared.Next(min, max + 1);
        }

        int stdDev = Math.Max(1, baseMs / 4);
        return HumanizedRandom.NextGaussianInt(baseMs, stdDev, 0, baseMs * 3);
    }

    public int GetPreActionReactionDelayMs(int complexity, bool isMovementAction)
    {
        RuntimeConfig cfg = Volatile.Read(ref runtime);
        if (!cfg.Enabled || isMovementAction)
        {
            return 0;
        }

        (double mean, double stdDev) = complexity switch
        {
            0 => (250.0, 50.0),
            2 => (500.0, 150.0),
            _ => (350.0, 80.0)
        };

        double fatigue = fatigueSimulator.FatigueMultiplier;
        int max = cfg.ReactionMaxMs <= 0 ? 500 : cfg.ReactionMaxMs;

        int reaction = HumanizedRandom.NextGaussianInt(
            mean * fatigue,
            stdDev * fatigue,
            min: 0,
            max: max);

        if (microPauseService.TryGetMicroPauseExtraDelayMs(cfg.MicroPauseMinMs, cfg.MicroPauseMaxMs, out int extra))
        {
            reaction = Math.Min(max, reaction + extra);
        }

        metrics?.RecordReactionDelay(reaction, $"Complexity-{complexity}");
        return reaction;
    }

    public int BuildMousePath(Point start, Point end, Span<Point> buffer)
    {
        RuntimeConfig cfg = Volatile.Read(ref runtime);
        if (!cfg.Enabled || !cfg.MouseEnabled)
        {
            return 0;
        }

        int pathPoints = HumanizedMousePath.BuildPath(
            start,
            end,
            cfg.MouseSteps,
            cfg.MouseCurveIntensity,
            cfg.MouseMicroJitterPixels,
            cfg.MouseOvershootProbability,
            buffer);

        // Calculate approximate distance
        int distance = (int)Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
        metrics?.RecordMouseMovement(pathPoints, distance);

        return pathPoints;
    }

    private void ApplyOptions(FeatureFlagsOptions options)
    {
        bool enabled = !options.GlobalKillSwitch && options.Humanization.Enabled;
        HumanizationOptions h = options.Humanization;

        RuntimeConfig cfg = new()
        {
            Enabled = enabled,
            KeyHoldMeanMs = h.InputTiming.KeyHoldMeanMs,
            KeyHoldStdDevMs = h.InputTiming.KeyHoldStdDevMs,
            KeyHoldMinMs = h.InputTiming.KeyHoldMinMs,
            KeyHoldMaxMs = h.InputTiming.KeyHoldMaxMs,
            ReactionMaxMs = h.InputTiming.ReactionMaxMs,
            MouseEnabled = h.MouseMovement.Enabled,
            MouseSteps = h.MouseMovement.StepsPerMovement,
            MouseCurveIntensity = h.MouseMovement.CurveIntensity,
            MouseMicroJitterPixels = h.MouseMovement.MicroJitterPixels,
            MouseOvershootProbability = h.MouseMovement.OvershootProbability,
            MouseStepDelayMinMs = h.MouseMovement.StepDelayMinMs,
            MouseStepDelayMaxMs = h.MouseMovement.StepDelayMaxMs,
            MicroPauseMinMs = h.Behavior.MicroPauseMinMs,
            MicroPauseMaxMs = h.Behavior.MicroPauseMaxMs
        };

        Volatile.Write(ref runtime, cfg);
        fatigueSimulator.ApplyOptions(h.Fatigue, enabled);

        logger.LogDebug(
            "[HumanizationProv ] Enabled={Enabled} Mouse={Mouse} Fatigue={Fatigue}",
            enabled,
            h.MouseMovement.Enabled,
            h.Fatigue.Enabled);
    }

    public void Dispose()
    {
        featureFlags.OnFlagsChanged -= ApplyOptions;
    }
}
