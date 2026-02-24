using System;
using System.Collections.Generic;
using System.Linq;

using static System.MathF;
using static System.Diagnostics.Stopwatch;

namespace Core.Goals;

/// <summary>
/// Detects oscillation (rapid back-and-forth movement) in heading changes.
/// Prevents the bot from getting stuck in oscillation patterns.
/// </summary>
public sealed class OscillationDetector
{
    /// <summary>
    /// Tracks a heading entry for oscillation detection
    /// </summary>
    private readonly record struct HeadingHistoryEntry(float Heading, long Timestamp);

    // Oscillation detection constants
    private const int HEADING_HISTORY_SIZE = 10;
    private const int OSCILLATION_THRESHOLD = 5; // Direction changes in history
    private const double OSCILLATION_RESET_TIME_MS = 1500;
    private const float MIN_ANGLE_CHANGE_FOR_OSCILLATION = 0.2f; // ~11.5 degrees

    private readonly Queue<HeadingHistoryEntry> headingHistory = new(HEADING_HISTORY_SIZE);
    private long lastOscillationCheckTime;
    private int oscillationCount;

    /// <summary>
    /// Gets whether oscillation is currently detected
    /// </summary>
    public bool IsOscillating { get; private set; }

    /// <summary>
    /// Gets the number of oscillation events detected
    /// </summary>
    public int OscillationCount => oscillationCount;

    public OscillationDetector()
    {
    }

    /// <summary>
    /// Tracks current heading for oscillation detection
    /// </summary>
    /// <param name="currentHeading">The current heading direction</param>
    public void TrackHeading(float currentHeading)
    {
        long now = GetTimestamp();

        // Reset if too much time passed
        if (headingHistory.Count > 0)
        {
            double elapsedMs = GetElapsedTime(lastOscillationCheckTime).TotalMilliseconds;
            if (elapsedMs > OSCILLATION_RESET_TIME_MS)
            {
                Reset();
            }
        }

        // Add new entry
        headingHistory.Enqueue(new HeadingHistoryEntry(currentHeading, now));
        if (headingHistory.Count > HEADING_HISTORY_SIZE)
        {
            headingHistory.Dequeue();
        }

        lastOscillationCheckTime = now;

        // Check for oscillation
        IsOscillating = DetectOscillation();
    }

    /// <summary>
    /// Detects oscillation by analyzing heading history for rapid direction changes
    /// </summary>
    private bool DetectOscillation()
    {
        if (headingHistory.Count < Math.Max(5, HEADING_HISTORY_SIZE / 2))
            return false;

        int directionChanges = 0;
        float prevDelta = 0;

        var entries = headingHistory.ToArray();
        for (int i = 1; i < entries.Length; i++)
        {
            float delta = entries[i].Heading - entries[i - 1].Heading;

            // Normalize delta
            if (delta > PI)
                delta -= 2 * PI;
            else if (delta < -PI)
                delta += 2 * PI;

            // Check for significant direction change
            if (MathF.Abs(delta) > MIN_ANGLE_CHANGE_FOR_OSCILLATION)
            {
                // Check if direction reversed
                if (prevDelta != 0 && Math.Sign(delta) != Math.Sign(prevDelta))
                {
                    directionChanges++;
                }
                prevDelta = delta;
            }
        }

        if (directionChanges >= OSCILLATION_THRESHOLD)
        {
            oscillationCount++;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets oscillation tracking
    /// </summary>
    public void Reset()
    {
        headingHistory.Clear();
        IsOscillating = false;
        lastOscillationCheckTime = GetTimestamp();
    }
}
