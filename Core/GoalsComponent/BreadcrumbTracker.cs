using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

using Microsoft.Extensions.Logging;

namespace Core.GoalsComponent;

/// <summary>
/// Tracks a trail of recent positions (breadcrumbs) for recovery navigation.
/// Used by the enhanced stuck recovery system to backtrack to known good positions.
/// </summary>
/// <remarks>
/// <para>
/// Thread-safe implementation suitable for concurrent updates from movement tracking.
/// </para>
/// <para>
/// Breadcrumbs are only recorded when the player has moved significantly,
/// avoiding duplicate positions when stuck or moving slowly.
/// </para>
/// </remarks>
public sealed class BreadcrumbTracker
{
    // Use List<T> instead of Queue<T> for O(1) indexed access
    private readonly List<BreadcrumbEntry> _trail = new();
    private readonly int _maxSize;
    private readonly float _minDistance;
    private readonly object _lock = new();

    private Vector3 _lastRecordedPosition;
    private DateTime _lastRecordedTime;
    private long _totalRecorded;
    private long _totalSkipped;

    /// <summary>
    /// Default minimum distance (world units) required to record a new breadcrumb.
    /// </summary>
    public const float DefaultMinDistance = 5f;

    /// <summary>
    /// Default trail size (number of positions to remember).
    /// </summary>
    public const int DefaultTrailSize = 50;

    /// <summary>
    /// Initializes a new breadcrumb tracker.
    /// </summary>
    /// <param name="maxSize">Maximum number of positions to retain.</param>
    /// <param name="minDistance">Minimum distance from last position to record new one.</param>
    public BreadcrumbTracker(int maxSize = DefaultTrailSize, float minDistance = DefaultMinDistance)
    {
        _maxSize = maxSize;
        _minDistance = minDistance;
        _lastRecordedPosition = Vector3.Zero;
    }

    /// <summary>
    /// Current number of breadcrumbs in the trail.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _trail.Count;
            }
        }
    }

    /// <summary>
    /// Maximum trail capacity.
    /// </summary>
    public int MaxSize => _maxSize;

    /// <summary>
    /// Total positions recorded since creation.
    /// </summary>
    public long TotalRecorded => Interlocked.Read(ref _totalRecorded);

    /// <summary>
    /// Positions skipped due to being too close to last recorded.
    /// </summary>
    public long TotalSkipped => Interlocked.Read(ref _totalSkipped);

    /// <summary>
    /// Records a position if significantly different from the last recorded position.
    /// </summary>
    /// <param name="position">Current world position.</param>
    /// <param name="mapId">Current map ID for context.</param>
    /// <returns>True if position was recorded, false if skipped.</returns>
    public bool RecordPosition(Vector3 position, float mapId = 0)
    {
        lock (_lock)
        {
            // Check if moved enough to record
            float distance = Vector3.Distance(_lastRecordedPosition, position);
            if (distance < _minDistance && _trail.Count > 0)
            {
                Interlocked.Increment(ref _totalSkipped);
                return false;
            }

            // Remove oldest if at capacity (List[0] is oldest)
            if (_trail.Count >= _maxSize)
            {
                _trail.RemoveAt(0);
            }

            // Record new breadcrumb (adds to end - newest)
            var entry = new BreadcrumbEntry(
                Position: position,
                MapId: mapId,
                Timestamp: DateTime.UtcNow
            );

            _trail.Add(entry);
            _lastRecordedPosition = position;
            _lastRecordedTime = DateTime.UtcNow;
            Interlocked.Increment(ref _totalRecorded);

            return true;
        }
    }

    /// <summary>
    /// Gets a position N steps back in the trail.
    /// </summary>
    /// <param name="stepsBack">Number of positions to go back (1 = most recent).</param>
    /// <returns>The position, or null if trail doesn't have enough entries.</returns>
    public BreadcrumbEntry? GetBacktrackPosition(int stepsBack)
    {
        if (stepsBack < 1) return null;

        lock (_lock)
        {
            if (_trail.Count < stepsBack)
                return null;

            // O(1) indexed access using List (trail is oldest-to-newest, so newest is at end)
            int index = _trail.Count - stepsBack;
            return _trail[index];
        }
    }

    /// <summary>
    /// Gets the most recent breadcrumb.
    /// </summary>
    public BreadcrumbEntry? GetLatest()
    {
        lock (_lock)
        {
            return _trail.Count > 0 ? _trail.Last() : null;
        }
    }

    /// <summary>
    /// Gets the oldest breadcrumb in the trail.
    /// </summary>
    public BreadcrumbEntry? GetOldest()
    {
        lock (_lock)
        {
            return _trail.Count > 0 ? _trail[0] : null;
        }
    }

    /// <summary>
    /// Finds the nearest breadcrumb to a given position.
    /// </summary>
    /// <param name="position">Position to search from.</param>
    /// <param name="maxDistance">Maximum search distance (optional).</param>
    /// <returns>Nearest breadcrumb within range, or null if none found.</returns>
    public BreadcrumbEntry? FindNearest(Vector3 position, float? maxDistance = null)
    {
        lock (_lock)
        {
            if (_trail.Count == 0) return null;

            BreadcrumbEntry? nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var entry in _trail)
            {
                float dist = Vector3.Distance(entry.Position, position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = entry;
                }
            }

            if (maxDistance.HasValue && nearestDist > maxDistance.Value)
                return null;

            return nearest;
        }
    }

    /// <summary>
    /// Gets all breadcrumbs from a specific map.
    /// </summary>
    /// <param name="mapId">Map ID to filter by.</param>
    /// <returns>Breadcrumbs matching the map ID.</returns>
    public IReadOnlyList<BreadcrumbEntry> GetByMap(float mapId)
    {
        lock (_lock)
        {
            return _trail.Where(e => e.MapId == mapId).ToList();
        }
    }

    /// <summary>
    /// Gets the entire trail as a list (oldest to newest).
    /// </summary>
    /// <returns>Copy of the breadcrumb trail.</returns>
    public IReadOnlyList<BreadcrumbEntry> GetTrail()
    {
        lock (_lock)
        {
            return _trail.ToList();
        }
    }

    /// <summary>
    /// Clears all breadcrumbs from the trail.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _trail.Clear();
            _lastRecordedPosition = Vector3.Zero;
        }
    }

    /// <summary>
    /// Removes breadcrumbs older than the specified duration.
    /// </summary>
    /// <param name="maxAge">Maximum age to retain.</param>
    /// <returns>Number of entries removed.</returns>
    public int PruneOlderThan(TimeSpan maxAge)
    {
        lock (_lock)
        {
            DateTime cutoff = DateTime.UtcNow - maxAge;
            int removed = 0;

            while (_trail.Count > 0 && _trail[0].Timestamp < cutoff)
            {
                _trail.RemoveAt(0);
                removed++;
            }

            return removed;
        }
    }

    /// <summary>
    /// Calculates the total distance covered by the breadcrumb trail.
    /// </summary>
    public float CalculateTotalDistance()
    {
        lock (_lock)
        {
            if (_trail.Count < 2) return 0;

            float total = 0;
            var entries = _trail.ToArray();

            for (int i = 1; i < entries.Length; i++)
            {
                total += Vector3.Distance(entries[i - 1].Position, entries[i].Position);
            }

            return total;
        }
    }

    /// <summary>
    /// Gets statistics about the breadcrumb trail.
    /// </summary>
    public BreadcrumbStats GetStats()
    {
        lock (_lock)
        {
            return new BreadcrumbStats(
                Count: _trail.Count,
                MaxSize: _maxSize,
                TotalRecorded: TotalRecorded,
                TotalSkipped: TotalSkipped,
                TotalDistance: CalculateTotalDistance(),
                OldestTimestamp: _trail.Count > 0 ? _trail[0].Timestamp : null,
                NewestTimestamp: _trail.Count > 0 ? _trail.Last().Timestamp : null
            );
        }
    }

    #region Predictive Stuck Detection

    /// <summary>
    /// Calculates the current velocity based on recent breadcrumbs.
    /// </summary>
    /// <returns>Velocity in yards per second, or null if insufficient data.</returns>
    public Vector3? CalculateVelocity(int sampleCount = 5)
    {
        lock (_lock)
        {
            if (_trail.Count < 2) return null;

            var recent = _trail.Skip(Math.Max(0, _trail.Count - sampleCount)).ToList();
            if (recent.Count < 2) return null;

            // Calculate average velocity
            Vector3 totalVelocity = Vector3.Zero;
            float totalTime = 0;

            for (int i = 1; i < recent.Count; i++)
            {
                Vector3 displacement = recent[i].Position - recent[i - 1].Position;
                double timeSeconds = (recent[i].Timestamp - recent[i - 1].Timestamp).TotalSeconds;

                if (timeSeconds > 0)
                {
                    totalVelocity += displacement / (float)timeSeconds;
                    totalTime++;
                }
            }

            return totalTime > 0 ? totalVelocity / totalTime : null;
        }
    }

    /// <summary>
    /// Calculates speed (magnitude of velocity).
    /// </summary>
    public float? CalculateSpeed(int sampleCount = 5)
    {
        Vector3? velocity = CalculateVelocity(sampleCount);
        return velocity.HasValue ? velocity.Value.Length() : null;
    }

    /// <summary>
    /// Analyzes the breadcrumb trail for patterns indicating approaching obstacles.
    /// </summary>
    /// <returns>True if obstacle pattern detected.</returns>
    public bool IsApproachingObstacle()
    {
        lock (_lock)
        {
            if (_trail.Count < 5) return false;

            // Check for velocity decrease pattern
            var recent = _trail.TakeLast(5).ToArray();

            float[] speeds = new float[recent.Length - 1];
            for (int i = 1; i < recent.Length; i++)
            {
                float dist = Vector3.Distance(recent[i].Position, recent[i - 1].Position);
                double time = (recent[i].Timestamp - recent[i - 1].Timestamp).TotalSeconds;
                speeds[i - 1] = time > 0 ? dist / (float)time : 0;
            }

            // Decelerating pattern: speeds are decreasing
            if (speeds.Length >= 3)
            {
                bool decelerating = speeds[0] > speeds[speeds.Length / 2] &&
                                   speeds[speeds.Length / 2] > speeds[speeds.Length - 1];

                // And current speed is very low
                if (decelerating && speeds[speeds.Length - 1] < 1.0f)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Calculates terrain complexity based on Z-axis changes.
    /// </summary>
    /// <returns>Complexity score (0-100).</returns>
    public int CalculateTerrainComplexity()
    {
        lock (_lock)
        {
            if (_trail.Count < 3) return 0;

            var positions = _trail.Select(e => e.Position).ToArray();

            // Calculate Z variance
            float[] zValues = positions.Select(p => p.Z).ToArray();
            float avgZ = zValues.Average();
            float variance = zValues.Select(z => (z - avgZ) * (z - avgZ)).Average();
            float stdDev = MathF.Sqrt(variance);

            // Calculate slope changes
            float totalSlopeChange = 0;
            for (int i = 2; i < positions.Length; i++)
            {
                float slope1 = (positions[i - 1].Z - positions[i - 2].Z) /
                              Vector2.Distance(
                                  new Vector2(positions[i - 1].X, positions[i - 1].Y),
                                  new Vector2(positions[i - 2].X, positions[i - 2].Y));

                float slope2 = (positions[i].Z - positions[i - 1].Z) /
                              Vector2.Distance(
                                  new Vector2(positions[i].X, positions[i].Y),
                                  new Vector2(positions[i - 1].X, positions[i - 1].Y));

                totalSlopeChange += MathF.Abs(slope2 - slope1);
            }

            // Combine into complexity score (0-100)
            float complexity = (stdDev * 10) + (totalSlopeChange * 5);
            return (int)Math.Clamp(complexity, 0, 100);
        }
    }

    /// <summary>
    /// Calculates a stuck risk score (0-100) based on movement patterns.
    /// </summary>
    public int CalculateStuckRisk()
    {
        lock (_lock)
        {
            if (_trail.Count < 5) return 0;

            int risk = 0;

            // Factor 1: Current speed (0-30 points)
            float? speed = CalculateSpeed(5);
            if (speed.HasValue)
            {
                if (speed.Value < 0.5f) risk += 30;
                else if (speed.Value < 1.0f) risk += 20;
                else if (speed.Value < 2.0f) risk += 10;
            }

            // Factor 2: Approaching obstacle (0-25 points)
            if (IsApproachingObstacle()) risk += 25;

            // Factor 3: Terrain complexity (0-20 points)
            int complexity = CalculateTerrainComplexity();
            risk += complexity / 5; // 0-20 points

            // Factor 4: Low breadcrumb recording rate (0-15 points)
            var recent = _trail.TakeLast(10).ToList();
            if (recent.Count >= 2)
            {
                TimeSpan span = recent.Last().Timestamp - recent.First().Timestamp;
                float expectedBreadcrumbs = (float)(span.TotalSeconds / 2.0); // Expect one every ~2 seconds
                float ratio = recent.Count / MathF.Max(1.0f, expectedBreadcrumbs);
                if (ratio < 0.3f) risk += 15;
                else if (ratio < 0.6f) risk += 8;
            }

            // Factor 5: Position oscillation (0-10 points)
            if (_trail.Count >= 10)
            {
                var last10 = _trail.TakeLast(10).ToArray();
                float totalDistance = 0;
                for (int i = 1; i < last10.Length; i++)
                {
                    totalDistance += Vector3.Distance(last10[i].Position, last10[i - 1].Position);
                }
                float straightLineDist = Vector3.Distance(last10[0].Position, last10[last10.Length - 1].Position);

                if (totalDistance > straightLineDist * 3) // Oscillating
                {
                    risk += 10;
                }
            }

            return Math.Min(risk, 100);
        }
    }

    /// <summary>
    /// Checks if we're in a narrow corridor (walls on both sides).
    /// </summary>
    public bool IsInNarrowCorridor(float threshold = 3.0f)
    {
        lock (_lock)
        {
            if (_trail.Count < 4) return false;

            var recent = _trail.TakeLast(4).ToArray();

            // Calculate position variance in X/Y
            float avgX = recent.Average(e => e.Position.X);
            float avgY = recent.Average(e => e.Position.Y);

            float varX = recent.Average(e => (e.Position.X - avgX) * (e.Position.X - avgX));
            float varY = recent.Average(e => (e.Position.Y - avgY) * (e.Position.Y - avgY));

            // Low variance in one dimension = corridor
            return varX < threshold || varY < threshold;
        }
    }

    /// <summary>
    /// Gets the average heading direction from recent movement.
    /// </summary>
    public float? GetAverageHeading(int sampleCount = 5)
    {
        lock (_lock)
        {
            if (_trail.Count < 2) return null;

            var recent = _trail.Skip(Math.Max(0, _trail.Count - sampleCount)).ToArray();
            if (recent.Length < 2) return null;

            Vector3 total = Vector3.Zero;
            for (int i = 1; i < recent.Length; i++)
            {
                total += Vector3.Normalize(recent[i].Position - recent[i - 1].Position);
            }

            if (total.LengthSquared() < 0.001f) return null;

            Vector3 avg = Vector3.Normalize(total);
            return MathF.Atan2(avg.Y, avg.X);
        }
    }

    #endregion
}

/// <summary>
/// A single breadcrumb entry in the trail.
/// </summary>
/// <param name="Position">World position.</param>
/// <param name="MapId">Map ID where position was recorded.</param>
/// <param name="Timestamp">When the position was recorded.</param>
public readonly record struct BreadcrumbEntry(
    Vector3 Position,
    float MapId,
    DateTime Timestamp);

/// <summary>
/// Statistics about the breadcrumb trail.
/// </summary>
public readonly record struct BreadcrumbStats(
    int Count,
    int MaxSize,
    long TotalRecorded,
    long TotalSkipped,
    float TotalDistance,
    DateTime? OldestTimestamp,
    DateTime? NewestTimestamp);
