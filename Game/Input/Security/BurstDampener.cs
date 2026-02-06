#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

using Microsoft.Extensions.Logging;

namespace Game.Input.Security;

/// <summary>
/// Tracks recent input event timestamps and dampens bursts that are too regular.
/// Combat rotations can produce GCD-locked sequences with mechanical precision,
/// which is a detection vector (F7).
/// </summary>
public sealed class BurstDampener
{
    private readonly long[] timestamps;
    private int index;
    private readonly double maxActionsPerSecond;
    private readonly int windowSize;
    private readonly ILogger<BurstDampener>? logger;

    /// <summary>
    /// Creates a new BurstDampener.
    /// </summary>
    /// <param name="windowSize">Number of recent timestamps to track</param>
    /// <param name="maxActionsPerSecond">Maximum allowed actions per second before dampening</param>
    /// <param name="logger">Optional logger</param>
    public BurstDampener(int windowSize = 8, double maxActionsPerSecond = 12.0, ILogger<BurstDampener>? logger = null)
    {
        this.windowSize = windowSize;
        this.maxActionsPerSecond = maxActionsPerSecond;
        this.logger = logger;
        timestamps = new long[windowSize];
    }

    /// <summary>
    /// Call before dispatching an input event. May sleep briefly to dampen bursts.
    /// </summary>
    /// <param name="waitHandle">The wait handle to use for sleeping</param>
    /// <returns>True if dampening was applied, false otherwise</returns>
    public bool CheckAndDampen(WaitHandle waitHandle)
    {
        long now = Stopwatch.GetTimestamp();
        int slot = index % timestamps.Length;
        long oldest = timestamps[slot];
        timestamps[slot] = now;
        index++;

        // Ring buffer not full yet
        if (oldest == 0)
            return false;

        double windowSec = (double)(now - oldest) / Stopwatch.Frequency;
        if (windowSec <= 0)
            return false;

        double rate = timestamps.Length / windowSec;

        if (rate > maxActionsPerSecond)
        {
            // Burst detected - add random delay (20-99ms)
            int dampMs = 20 + Random.Shared.Next(80);
            waitHandle.WaitOne(dampMs);

            logger?.LogDebug("[BurstDampener] Burst detected ({Rate:F1} actions/sec), dampened {DampMs}ms",
                rate, dampMs);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the current action rate without applying dampening.
    /// </summary>
    public double GetCurrentRate()
    {
        long now = Stopwatch.GetTimestamp();
        int slot = (index - 1 + timestamps.Length) % timestamps.Length;
        long oldest = timestamps[slot];

        if (oldest == 0)
            return 0;

        double windowSec = (double)(now - oldest) / Stopwatch.Frequency;
        if (windowSec <= 0)
            return 0;

        int filledSlots = Math.Min(index, timestamps.Length);
        return filledSlots / windowSec;
    }

    /// <summary>
    /// Resets the dampener state.
    /// </summary>
    public void Reset()
    {
        Array.Clear(timestamps);
        index = 0;
    }
}
