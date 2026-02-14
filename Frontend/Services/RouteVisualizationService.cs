using System.Collections.Concurrent;
using System.Numerics;
using System.Timers;

using Core;
using Core.Analytics;
using Core.GoalsComponent;
using Core.Hazard;
using Core.Testing;

using Microsoft.Extensions.Logging;

namespace Frontend.Services;

/// <summary>
/// Service that manages real-time route visualization including hot zones, stuck events,
/// and rehabilitation markers on the route display.
/// </summary>
public sealed class RouteVisualizationService : IDisposable
{
    private readonly ILogger<RouteVisualizationService> _logger;
    private readonly FailureAnalytics? _failureAnalytics;
    private readonly HazardZoneStore? _hazardStore;
    private readonly StuckDetector? _stuckDetector;
    private readonly RouteRehabilitator? _rehabilitator;
    private readonly IFailureSimulationService? _failureSimulation;

    // State collections
    private readonly ConcurrentDictionary<Guid, StuckMarker> _activeStuckMarkers = new();
    private readonly ConcurrentDictionary<Guid, HotZoneView> _activeHotZones = new();
    private readonly ConcurrentDictionary<Guid, RehabilitationMarker> _rehabilitationMarkers = new();
    private readonly ConcurrentDictionary<string, int> _failureCountsByGrid = new();

    // Configuration
    private readonly TimeSpan _markerFlashDuration = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _markerCleanupInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _hotZoneThreshold = TimeSpan.FromMinutes(5);
    private readonly System.Timers.Timer _cleanupTimer;

    // Events
    public event Action? OnVisualizationStateChanged;
    public event Action<StuckMarker>? OnStuckMarkerAdded;
    public event Action<HotZoneView>? OnHotZoneAdded;
    public event Action<RehabilitationMarker>? OnRehabilitationMarkerAdded;

    public RouteVisualizationService(
        ILogger<RouteVisualizationService> logger,
        FailureAnalytics? failureAnalytics = null,
        HazardZoneStore? hazardStore = null,
        StuckDetector? stuckDetector = null,
        RouteRehabilitator? rehabilitator = null,
        IFailureSimulationService? failureSimulation = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _failureAnalytics = failureAnalytics;
        _hazardStore = hazardStore;
        _stuckDetector = stuckDetector;
        _rehabilitator = rehabilitator;
        _failureSimulation = failureSimulation;

        // Subscribe to events
        if (_stuckDetector != null)
        {
            _stuckDetector.OnStuckDetected += OnStuckDetected;
        }

        if (_failureSimulation != null)
        {
            _failureSimulation.OnStuckSimulated += OnStuckSimulated;
            _failureSimulation.OnDeathSimulated += OnDeathSimulated;
            _failureSimulation.OnHotZoneCreated += OnHotZoneCreated;
            _failureSimulation.OnRehabSimulated += OnRehabSimulated;
        }

        // Start cleanup timer
        _cleanupTimer = new System.Timers.Timer(_markerCleanupInterval.TotalMilliseconds);
        _cleanupTimer.Elapsed += OnCleanupTimerElapsed;
        _cleanupTimer.AutoReset = true;
        _cleanupTimer.Start();

        _logger.LogInformation("[RouteVisService   ] Initialized");
    }

    #region Event Handlers

    private void OnStuckDetected(StuckEventData data)
    {
        var marker = new StuckMarker
        {
            Id = Guid.NewGuid(),
            Position = data.Position,
            MapX = data.MapX,
            MapY = data.MapY,
            MapId = data.MapId,
            Timestamp = DateTime.UtcNow,
            State = data.State,
            DurationMs = data.DurationMs,
            IsFlashing = true,
            Color = GetStateColor(data.State),
            Severity = CalculateStuckSeverity(data)
        };

        _activeStuckMarkers[marker.Id] = marker;

        // Update grid failure count
        UpdateGridFailureCount(data.Position, data.MapId);

        OnStuckMarkerAdded?.Invoke(marker);
        OnVisualizationStateChanged?.Invoke();

        _logger.LogDebug("[RouteVisService   ] Added stuck marker at {Position} (state={State})",
            data.Position, data.State);

        // Schedule flash end
        Task.Run(async () =>
        {
            await Task.Delay(_markerFlashDuration);
            if (_activeStuckMarkers.TryGetValue(marker.Id, out var m))
            {
                m.IsFlashing = false;
                OnVisualizationStateChanged?.Invoke();
            }
        });
    }

    private void OnStuckSimulated(SimulatedStuckEvent evt)
    {
        var marker = new StuckMarker
        {
            Id = evt.Id,
            Position = evt.Position,
            MapX = evt.Position.X,
            MapY = evt.Position.Y,
            MapId = evt.MapId,
            Timestamp = evt.Timestamp,
            State = evt.State,
            DurationMs = evt.DurationMs,
            IsFlashing = evt.IsFlashingMarker,
            Color = GetStateColor(evt.State),
            Severity = CalculateSeverityFromAttempt(evt.AttemptCount)
        };

        _activeStuckMarkers[evt.Id] = marker;
        UpdateGridFailureCount(evt.Position, evt.MapId);

        OnStuckMarkerAdded?.Invoke(marker);
        OnVisualizationStateChanged?.Invoke();
    }

    private void OnDeathSimulated(SimulatedDeathEvent evt)
    {
        // Deaths create permanent markers (not flashing)
        var marker = new StuckMarker
        {
            Id = evt.Id,
            Position = evt.Position,
            MapX = evt.Position.X,
            MapY = evt.Position.Y,
            MapId = evt.MapId,
            Timestamp = evt.Timestamp,
            State = UnstuckState.None,
            DurationMs = 0,
            IsFlashing = false,
            Color = "#8B0000", // Dark red for death
            Severity = 10,
            IsDeathMarker = true,
            Tooltip = $"Death: {evt.Cause}"
        };

        _activeStuckMarkers[evt.Id] = marker;
        UpdateGridFailureCount(evt.Position, evt.MapId);

        OnVisualizationStateChanged?.Invoke();
    }

    private void OnHotZoneCreated(SimulatedHotZone zone)
    {
        var view = new HotZoneView
        {
            Id = zone.Id,
            Center = zone.Center,
            MapId = zone.MapId,
            Radius = zone.Radius,
            FailureCount = zone.FailureCount,
            PrimaryType = zone.PrimaryType,
            CreatedAt = zone.CreatedAt,
            Color = GetHotZoneColor(zone.FailureCount),
            IsPulsing = zone.FailureCount >= 5
        };

        _activeHotZones[zone.Id] = view;
        OnHotZoneAdded?.Invoke(view);
        OnVisualizationStateChanged?.Invoke();

        _logger.LogInformation("[RouteVisService   ] Hot zone created at {Center} with {Count} failures",
            zone.Center, zone.FailureCount);
    }

    private void OnRehabSimulated(SimulatedRehabEvent evt)
    {
        var marker = new RehabilitationMarker
        {
            Id = evt.Id,
            Position = evt.Position,
            Radius = evt.Radius,
            Timestamp = evt.Timestamp,
            SeverityReduction = evt.SeverityReduction,
            IsActive = true
        };

        _rehabilitationMarkers[evt.Id] = marker;
        OnRehabilitationMarkerAdded?.Invoke(marker);
        OnVisualizationStateChanged?.Invoke();

        // Update hot zones in the area
        foreach (var zone in _activeHotZones.Values)
        {
            if (Vector3.Distance(zone.Center, evt.Position) < evt.Radius)
            {
                zone.IsRehabilitated = true;
                zone.RehabilitatedAt = evt.Timestamp;
            }
        }
    }

    private void OnCleanupTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        CleanupOldMarkers();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets all active stuck markers for display.
    /// </summary>
    public IReadOnlyCollection<StuckMarker> GetActiveStuckMarkers()
    {
        return _activeStuckMarkers.Values.ToList();
    }

    /// <summary>
    /// Gets all active hot zones.
    /// </summary>
    public IReadOnlyCollection<HotZoneView> GetActiveHotZones()
    {
        return _activeHotZones.Values.ToList();
    }

    /// <summary>
    /// Gets rehabilitation markers.
    /// </summary>
    public IReadOnlyCollection<RehabilitationMarker> GetRehabilitationMarkers()
    {
        return _rehabilitationMarkers.Values.ToList();
    }

    /// <summary>
    /// Gets failure counts by grid cell.
    /// </summary>
    public IReadOnlyDictionary<string, int> GetFailureCountsByGrid()
    {
        return _failureCountsByGrid.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Checks if there's a hot zone ahead on the route.
    /// </summary>
    public HotZoneView? GetUpcomingHotZone(Vector3 currentPos, Vector3 direction, float lookAheadDistance = 50f)
    {
        foreach (var zone in _activeHotZones.Values.Where(z => !z.IsRehabilitated))
        {
            float distance = Vector3.Distance(currentPos, zone.Center);
            if (distance <= lookAheadDistance)
            {
                // Check if it's roughly in front of player
                Vector3 toZone = Vector3.Normalize(zone.Center - currentPos);
                float dot = Vector3.Dot(Vector3.Normalize(direction), toZone);
                if (dot > 0.5f) // Within ~60 degrees
                {
                    return zone;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Manually rehabilitate a hot zone.
    /// </summary>
    public bool RehabilitateHotZone(Guid zoneId)
    {
        if (_activeHotZones.TryGetValue(zoneId, out var zone))
        {
            zone.IsRehabilitated = true;
            zone.RehabilitatedAt = DateTime.UtcNow;

            // Call the rehabilitator if available
            if (_rehabilitator != null)
            {
                var hotZone = new FailureHotZone
                {
                    Center = zone.Center,
                    MapId = zone.MapId,
                    FailureCount = zone.FailureCount,
                    PrimaryType = zone.PrimaryType,
                    LastFailure = zone.CreatedAt
                };
                _rehabilitator.RehabilitateHotZone(hotZone);
            }

            OnVisualizationStateChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all markers and hot zones.
    /// </summary>
    public void ClearAll()
    {
        _activeStuckMarkers.Clear();
        _activeHotZones.Clear();
        _rehabilitationMarkers.Clear();
        _failureCountsByGrid.Clear();
        OnVisualizationStateChanged?.Invoke();
    }

    #endregion

    #region Private Helpers

    private void UpdateGridFailureCount(Vector3 position, int mapId)
    {
        // 10-yard grid cells
        string gridKey = $"{mapId}:{(int)(position.X / 10)}:{(int)(position.Y / 10)}";
        _failureCountsByGrid.AddOrUpdate(gridKey, 1, (_, count) => count + 1);
    }

    private void CleanupOldMarkers()
    {
        DateTime cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(10);

        // Remove old stuck markers (keep deaths)
        var oldStuckMarkers = _activeStuckMarkers
            .Where(kvp => !kvp.Value.IsDeathMarker && kvp.Value.Timestamp < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldStuckMarkers)
        {
            _activeStuckMarkers.TryRemove(key, out _);
        }

        // Remove old rehabilitation markers
        var oldRehabs = _rehabilitationMarkers
            .Where(kvp => kvp.Value.Timestamp < cutoff - TimeSpan.FromMinutes(5))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in oldRehabs)
        {
            _rehabilitationMarkers.TryRemove(key, out _);
        }

        if (oldStuckMarkers.Count > 0 || oldRehabs.Count > 0)
        {
            OnVisualizationStateChanged?.Invoke();
        }
    }

    private static string GetStateColor(UnstuckState state)
    {
        return state switch
        {
            UnstuckState.None => "#808080",
            UnstuckState.InitialAttempt => "#FFFF00", // Yellow
            UnstuckState.StrafeAttempt => "#FFA500",   // Orange
            UnstuckState.ReverseAttempt => "#FF8C00",  // Dark orange
            UnstuckState.BreadcrumbBacktrack => "#FF4500", // Red-orange
            UnstuckState.PathClearAttempt => "#FF0000",    // Red
            UnstuckState.EmergencyEscape => "#8B0000",     // Dark red
            _ => "#808080"
        };
    }

    private static string GetHotZoneColor(int failureCount)
    {
        return failureCount switch
        {
            <= 2 => "#90EE90",  // Light green
            <= 4 => "#FFFF00",  // Yellow
            <= 6 => "#FFA500",  // Orange
            <= 8 => "#FF4500",  // Red-orange
            _ => "#8B0000"     // Dark red
        };
    }

    private static int CalculateStuckSeverity(StuckEventData data)
    {
        int baseSeverity = data.State switch
        {
            UnstuckState.None => 0,
            UnstuckState.InitialAttempt => 2,
            UnstuckState.StrafeAttempt => 3,
            UnstuckState.ReverseAttempt => 4,
            UnstuckState.BreadcrumbBacktrack => 6,
            UnstuckState.PathClearAttempt => 8,
            UnstuckState.EmergencyEscape => 10,
            _ => 1
        };

        // Increase severity for repeated attempts
        return Math.Min(10, baseSeverity + (data.AttemptCount - 1));
    }

    private static int CalculateSeverityFromAttempt(int attemptCount)
    {
        return Math.Min(10, 2 + attemptCount);
    }

    #endregion

    public void Dispose()
    {
        _cleanupTimer?.Stop();
        _cleanupTimer?.Dispose();

        if (_stuckDetector != null)
        {
            _stuckDetector.OnStuckDetected -= OnStuckDetected;
        }

        if (_failureSimulation != null)
        {
            _failureSimulation.OnStuckSimulated -= OnStuckSimulated;
            _failureSimulation.OnDeathSimulated -= OnDeathSimulated;
            _failureSimulation.OnHotZoneCreated -= OnHotZoneCreated;
            _failureSimulation.OnRehabSimulated -= OnRehabSimulated;
        }
    }
}

/// <summary>
/// Represents a stuck marker on the route visualization.
/// </summary>
public sealed class StuckMarker
{
    public Guid Id { get; set; }
    public Vector3 Position { get; set; }
    public float MapX { get; set; }
    public float MapY { get; set; }
    public int MapId { get; set; }
    public DateTime Timestamp { get; set; }
    public UnstuckState State { get; set; }
    public double DurationMs { get; set; }
    public bool IsFlashing { get; set; }
    public string Color { get; set; } = "#FFFF00";
    public int Severity { get; set; }
    public bool IsDeathMarker { get; set; }
    public string? Tooltip { get; set; }
}

/// <summary>
/// Represents a hot zone view model.
/// </summary>
public sealed class HotZoneView
{
    public Guid Id { get; set; }
    public Vector3 Center { get; set; }
    public int MapId { get; set; }
    public float Radius { get; set; }
    public int FailureCount { get; set; }
    public FailureType PrimaryType { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Color { get; set; } = "#FFFF00";
    public bool IsPulsing { get; set; }
    public bool IsRehabilitated { get; set; }
    public DateTime? RehabilitatedAt { get; set; }
}

/// <summary>
/// Represents a rehabilitation marker.
/// </summary>
public sealed class RehabilitationMarker
{
    public Guid Id { get; set; }
    public Vector3 Position { get; set; }
    public float Radius { get; set; }
    public DateTime Timestamp { get; set; }
    public float SeverityReduction { get; set; }
    public bool IsActive { get; set; }
}
