using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

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
    private readonly Queue<BreadcrumbEntry> _trail = new();
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

            // Remove oldest if at capacity
            if (_trail.Count >= _maxSize)
            {
                _trail.Dequeue();
            }

            // Record new breadcrumb
            var entry = new BreadcrumbEntry(
                Position: position,
                MapId: mapId,
                Timestamp: DateTime.UtcNow
            );

            _trail.Enqueue(entry);
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

            // Convert to list for indexed access (trail is oldest-to-newest)
            int index = _trail.Count - stepsBack;
            return _trail.ElementAt(index);
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
            return _trail.Count > 0 ? _trail.Peek() : null;
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

            while (_trail.Count > 0 && _trail.Peek().Timestamp < cutoff)
            {
                _trail.Dequeue();
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
                OldestTimestamp: _trail.Count > 0 ? _trail.Peek().Timestamp : null,
                NewestTimestamp: _trail.Count > 0 ? _trail.Last().Timestamp : null
            );
        }
    }
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
