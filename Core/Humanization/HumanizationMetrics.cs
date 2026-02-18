using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SharedLib.Humanization;
using SharedLib.InputSecurity;

namespace Core.Humanization;

/// <summary>
/// Tracks real-time metrics about humanization behavior for monitoring and debugging.
/// </summary>
public sealed class HumanizationMetrics
{
    private readonly ILogger<HumanizationMetrics>? logger;

    // Timing metrics
    private long totalKeyPresses;
    private long totalKeyHoldTimeMs;
    private long totalReactionDelays;
    private long totalReactionDelayTimeMs;
    private long totalWaypointDelays;
    private long totalWaypointDelayTimeMs;

    // Mouse metrics
    private long totalMouseMovements;
    private long totalMousePathPoints;
    private long totalMouseDistancePixels;

    // Break metrics
    private long breaksTaken;
    private long totalBreakTimeMs;
    private DateTime lastBreakStart;

    // Session metrics
    private readonly DateTime sessionStart;
    private long sessionKeyPresses;
    private long sessionMouseMovements;

    // Recent samples (circular buffer)
    private readonly ConcurrentQueue<TimingSample> recentKeyHoldTimes = new();
    private readonly ConcurrentQueue<TimingSample> recentReactionDelays = new();
    private const int MaxSamples = 100;

    public HumanizationMetrics(ILogger<HumanizationMetrics>? logger = null)
    {
        this.logger = logger;
        sessionStart = DateTime.UtcNow;
    }

    #region Metric Recording

    /// <summary>
    /// Records a key press with its hold duration.
    /// </summary>
    public void RecordKeyPress(int holdDurationMs)
    {
        Interlocked.Increment(ref totalKeyPresses);
        Interlocked.Increment(ref sessionKeyPresses);
        Interlocked.Add(ref totalKeyHoldTimeMs, holdDurationMs);

        AddSample(recentKeyHoldTimes, new TimingSample(DateTime.UtcNow, holdDurationMs));

        logger?.LogTrace("[HumanizationMetrics] Key press recorded: {Duration}ms", holdDurationMs);
    }

    /// <summary>
    /// Records a reaction delay before goal execution.
    /// </summary>
    public void RecordReactionDelay(int delayMs, string goalName)
    {
        Interlocked.Increment(ref totalReactionDelays);
        Interlocked.Add(ref totalReactionDelayTimeMs, delayMs);

        AddSample(recentReactionDelays, new TimingSample(DateTime.UtcNow, delayMs, goalName));

        logger?.LogDebug("[HumanizationMetrics] Reaction delay recorded: {Delay}ms for {Goal}", delayMs, goalName);
    }

    /// <summary>
    /// Records a waypoint delay during navigation.
    /// </summary>
    public void RecordWaypointDelay(int delayMs, int remainingWaypoints)
    {
        Interlocked.Increment(ref totalWaypointDelays);
        Interlocked.Add(ref totalWaypointDelayTimeMs, delayMs);

        logger?.LogTrace("[HumanizationMetrics] Waypoint delay recorded: {Delay}ms ({Remaining} remaining)", delayMs, remainingWaypoints);
    }

    /// <summary>
    /// Records a mouse movement.
    /// </summary>
    public void RecordMouseMovement(int pathPoints, int distancePixels)
    {
        Interlocked.Increment(ref totalMouseMovements);
        Interlocked.Increment(ref sessionMouseMovements);
        Interlocked.Add(ref totalMousePathPoints, pathPoints);
        Interlocked.Add(ref totalMouseDistancePixels, distancePixels);
    }

    /// <summary>
    /// Records the start of a break.
    /// </summary>
    public void RecordBreakStart()
    {
        lastBreakStart = DateTime.UtcNow;
        Interlocked.Increment(ref breaksTaken);

        logger?.LogInformation("[HumanizationMetrics] Break started (#{BreakCount})", breaksTaken);
    }

    /// <summary>
    /// Records the end of a break.
    /// </summary>
    public void RecordBreakEnd()
    {
        long durationMs = (long)(DateTime.UtcNow - lastBreakStart).TotalMilliseconds;
        Interlocked.Add(ref totalBreakTimeMs, durationMs);

        logger?.LogInformation("[HumanizationMetrics] Break ended: {Duration:F1} minutes", durationMs / 60000.0);
    }

    #endregion

    #region Metric Queries

    /// <summary>
    /// Gets a snapshot of current metrics.
    /// </summary>
    public MetricsSnapshot GetSnapshot()
    {
        return new MetricsSnapshot
        {
            SessionDuration = DateTime.UtcNow - sessionStart,
            TotalKeyPresses = Interlocked.Read(ref totalKeyPresses),
            SessionKeyPresses = Interlocked.Read(ref sessionKeyPresses),
            AverageKeyHoldTimeMs = CalculateAverage(Interlocked.Read(ref totalKeyHoldTimeMs), Interlocked.Read(ref totalKeyPresses)),
            TotalReactionDelays = Interlocked.Read(ref totalReactionDelays),
            AverageReactionDelayMs = CalculateAverage(Interlocked.Read(ref totalReactionDelayTimeMs), Interlocked.Read(ref totalReactionDelays)),
            TotalWaypointDelays = Interlocked.Read(ref totalWaypointDelays),
            AverageWaypointDelayMs = CalculateAverage(Interlocked.Read(ref totalWaypointDelayTimeMs), Interlocked.Read(ref totalWaypointDelays)),
            TotalMouseMovements = Interlocked.Read(ref totalMouseMovements),
            SessionMouseMovements = Interlocked.Read(ref sessionMouseMovements),
            AverageMousePathPoints = CalculateAverage(Interlocked.Read(ref totalMousePathPoints), Interlocked.Read(ref totalMouseMovements)),
            BreaksTaken = Interlocked.Read(ref breaksTaken),
            TotalBreakTimeMinutes = Interlocked.Read(ref totalBreakTimeMs) / 60000.0,
            RecentKeyHoldTimes = recentKeyHoldTimes.ToArray(),
            RecentReactionDelays = recentReactionDelays.ToArray()
        };
    }

    /// <summary>
    /// Gets the average key hold time over the last N samples.
    /// </summary>
    public double GetRecentAverageKeyHoldTime(int sampleCount = 10)
    {
        var samples = recentKeyHoldTimes.TakeLast(sampleCount).Select(s => s.DurationMs);
        return samples.Any() ? samples.Average() : 0;
    }

    /// <summary>
    /// Gets the average reaction delay over the last N samples.
    /// </summary>
    public double GetRecentAverageReactionDelay(int sampleCount = 10)
    {
        var samples = recentReactionDelays.TakeLast(sampleCount).Select(s => s.DurationMs);
        return samples.Any() ? samples.Average() : 0;
    }

    #endregion

    #region Private Helpers

    private static void AddSample(ConcurrentQueue<TimingSample> queue, TimingSample sample)
    {
        queue.Enqueue(sample);

        // Trim old samples
        while (queue.Count > MaxSamples && queue.TryDequeue(out _)) { }
    }

    private static double CalculateAverage(long total, long count)
    {
        return count > 0 ? (double)total / count : 0;
    }

    #endregion
}

/// <summary>
/// A single timing sample for metric tracking.
/// </summary>
public sealed record TimingSample(DateTime Timestamp, int DurationMs, string? Context = null);

/// <summary>
/// A snapshot of humanization metrics at a point in time.
/// </summary>
public sealed class MetricsSnapshot
{
    public TimeSpan SessionDuration { get; init; }
    public long TotalKeyPresses { get; init; }
    public long SessionKeyPresses { get; init; }
    public double AverageKeyHoldTimeMs { get; init; }
    public long TotalReactionDelays { get; init; }
    public double AverageReactionDelayMs { get; init; }
    public long TotalWaypointDelays { get; init; }
    public double AverageWaypointDelayMs { get; init; }
    public long TotalMouseMovements { get; init; }
    public long SessionMouseMovements { get; init; }
    public double AverageMousePathPoints { get; init; }
    public long BreaksTaken { get; init; }
    public double TotalBreakTimeMinutes { get; init; }
    public TimingSample[] RecentKeyHoldTimes { get; init; } = Array.Empty<TimingSample>();
    public TimingSample[] RecentReactionDelays { get; init; } = Array.Empty<TimingSample>();
}
