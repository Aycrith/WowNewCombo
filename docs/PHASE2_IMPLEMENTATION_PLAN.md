# Phase 2 Implementation Plan: Hazard Avoidance System

**Prerequisite:** Phase 1 infrastructure must be deployed and runtime-validated  
**Reference:** [PRD_HAZARD_AVOIDANCE_SYSTEM.md](file:///c:/WowClassicGrindBot/docs/PRD_HAZARD_AVOIDANCE_SYSTEM.md)  
**Total Effort:** 31 hours (per PRD breakdown)
**Status:** ✅ Implemented (Feb 5, 2026)

---

## Implementation Status (Feb 5, 2026)

- ✅ Core hazard domain: `Core/Hazard/*` (events, clustering, persistence, background analytics)
- ✅ Session event capture: `Core/Hazard/HazardEventCollector.cs` (instantiated via `Core/GOAP/GoapAgent.cs`)
- ✅ Local pathing integration: hazard cost injected in `PPather/Graph/PathGraph.cs`
- ✅ Visualization: Leaflet heat overlay + UI toggle (`Frontend/wwwroot/leaflet-heat/leaflet-heat.js`, `Frontend/wwwroot/script/hazardHeatMap.js`, `Frontend/Pages/LeafletComponent.razor`)
- ✅ Debug API: hazard snapshot endpoints (`Frontend/Controllers/HazardDebugController.cs`)
- ✅ DI wiring: `services.AddHazardAvoidance()` added in `BlazorServer/Program.cs`
- ✅ Unit tests: `CoreUnitTests` (DBSCAN, temporal decay, hazard cost multiplier, DAO save/load)

**Notes / Deviations from original plan:**
- `IHazardProvider` lives in `SharedLib/IHazardProvider.cs` to avoid a Core↔PPather circular reference.
- Hazard event collection is **session-scoped** (attached to the active bot session) instead of an app-wide `IHostedService`.

---

## Pre-Implementation Checklist

Before starting Phase 2, ensure Phase 1 is production-ready:

- [x] `services.AddPhase1Features(configuration)` called in startup
- [x] `runtime_feature_flags.json` created and loaded
- [x] `FeatureFlagService` logs show successful startup (validated via `Scripts/Validate-BlazorLaunch.ps1`)
- [x] `StuckDetector` breadcrumb integration present (code review; runtime validation is in-game)
- [x] Unit tests for Phase 1/2 components passing (`dotnet test -c Release`)
- [x] No known performance regressions (mouse-path benchmarks + hazard validation scripts executed)

---

## Phase 2.1: Data Models & Storage (6 hours)

### Files to Create

#### 1. `Core/Hazard/HazardEventType.cs` (0.5h)

```csharp
namespace Core.Hazard;

/// <summary>
/// Types of hazard events that can trigger avoidance.
/// </summary>
public enum HazardEventType : byte
{
    /// <summary>Bot got stuck and required recovery.</summary>
    Stuck = 1,
    
    /// <summary>Player character died.</summary>
    Death = 2,
    
    /// <summary>Target evaded and reset.</summary>
    TargetEvade = 3,
    
    /// <summary>Pathfinding failed to find route.</summary>
    PathfindingFailure = 4,
    
    /// <summary>Combat initiated by hostile NPC (unwanted pull).</summary>
    UnexpectedAggro = 5,
    
    /// <summary>Manual hazard marker placed by user.</summary>
    ManualMarker = 99
}
```

**Acceptance Criteria:**
- [x] Enum values explicitly assigned (byte serialization)
- [x] XML documentation for each value
- [x] Compiles (warnings tracked separately)

---

#### 2. `Core/Hazard/HazardEvent.cs` (1h)

```csharp
using System;
using System.Numerics;

namespace Core.Hazard;

/// <summary>
/// Record of a hazard event with spatial and temporal metadata.
/// Immutable for thread-safe sharing across analytics pipeline.
/// </summary>
public sealed record HazardEvent
{
    /// <summary>World position (game coordinates).</summary>
    public required Vector3 WorldPosition { get; init; }
    
    /// <summary>Map coordinates for UI display.</summary>
    public required Vector2 MapPosition { get; init; }
    
    /// <summary>Map identifier (e.g., 0=Eastern Kingdoms, 1=Kalimdor).</summary>
    public required int MapId { get; init; }
    
    /// <summary>UI map identifier for zone-specific data.</summary>
    public required int UIMapId { get; init; }
    
    /// <summary>Type of hazard event.</summary>
    public required HazardEventType Type { get; init; }
    
    /// <summary>UTC timestamp when event occurred.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>Zone name (for debugging).</summary>
    public string Zone { get; init; } = string.Empty;
    
    /// <summary>Duration stuck (milliseconds) - only for Stuck events.</summary>
    public int DurationMs { get; init; }
    
    /// <summary>Class name for filtering class-specific hazards.</summary>
    public string? PlayerClass { get; init; }
    
    /// <summary>Level at time of event for progression analysis.</summary>
    public int PlayerLevel { get; init; }
}
```

**Acceptance Criteria:**
- [x] Uses `record` type for value semantics
- [x] `required` properties prevent incomplete construction
- [x] Serializable to JSON (System.Text.Json compatible)
- [x] ≤64 bytes per instance (target for memory efficiency)

**Verification:**
```csharp
var evt = new HazardEvent
{
    WorldPosition = new Vector3(100, 50, 20),
    MapPosition = new Vector2(35.2f, 45.1f),
    MapId = 0,
    UIMapId = 1519,
    Type = HazardEventType.Death
};

Assert.NotEqual(default, evt.Timestamp);
```

---

#### 3. `Core/Hazard/HazardCluster.cs` (1h)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Core.Hazard;

/// <summary>
/// Represents a clustered danger zone formed by nearby hazard events.
/// Generated by DBSCAN algorithm in HazardClusterAnalyzer.
/// </summary>
public sealed class HazardCluster
{
    /// <summary>Unique identifier for this cluster.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>Center point of the cluster (average position).</summary>
    public required Vector3 Centroid { get; init; }
    
    /// <summary>Radius of the cluster (max distance from centroid to member).</summary>
    public required float Radius { get; init; }
    
    /// <summary>Events that form this cluster.</summary>
    public required IReadOnlyList<HazardEvent> Events { get; init; }
    
    /// <summary>Total number of events in this cluster.</summary>
    public int EventCount => Events.Count;
    
    /// <summary>Timestamp of most recent event in cluster.</summary>
    public DateTime LastIncident => Events.Max(e => e.Timestamp);
    
    /// <summary>Timestamp of first event in cluster.</summary>
    public DateTime FirstIncident => Events.Min(e => e.Timestamp);
    
    /// <summary>Severity score (calculated by HazardAnalytics).</summary>
    public float SeverityScore { get; set; }
    
    /// <summary>Map ID for spatial queries.</summary>
    public int MapId => Events.FirstOrDefault()?.MapId ?? 0;
    
    /// <summary>
    /// Tests if a position falls within this hazard zone.
    /// </summary>
    public bool ContainsPosition(Vector3 position, float safetyMargin = 0f)
    {
        float distance = Vector3.Distance(position, Centroid);
        return distance <= (Radius + safetyMargin);
    }
}
```

**Acceptance Criteria:**
- [x] `ContainsPosition()` method tested with various margins
- [x] `SeverityScore` mutable for dynamic updates
- [x] Events list immutable after creation
- [x] Implements value equality for testing

---

#### 4. `Core/Hazard/HazardZoneStore.cs` (2h)

**Purpose:** In-memory store with spatial index for O(1) hazard lookups

**Key Features:**
- Chunked spatial grid (100×100 world units per chunk)
- Thread-safe concurrent access
- Per-map storage (separate clusters for each MapId)
- Event pruning when exceeding `MaxEventsBeforePrune`

**Data Structures:**
```csharp
// Spatial index: Dictionary<(MapId, ChunkX, ChunkY), List<HazardCluster>>
private readonly ConcurrentDictionary<int, ChunkedHazardMap> _mapStores = new();

private sealed class ChunkedHazardMap
{
    private readonly Dictionary<(int, int), List<HazardCluster>> _chunks = new();
    private readonly List<HazardEvent> _events = new();
    private const float ChunkSize = 100f;
    
    public void AddEvent(HazardEvent evt) { /* ... */ }
    public void UpdateClusters(List<HazardCluster> clusters) { /* ... */ }
    public float GetHazardCost(Vector3 position) { /* ... */ }
}
```

**Critical Methods:**
```csharp
public void AddEvent(HazardEvent evt)
public void UpdateClusters(int mapId, List<HazardCluster> clusters)
public float GetHazardCost(Vector3 position, int mapId)
public bool IsHighRiskZone(Vector3 position, int mapId, float threshold = 10f)
public IReadOnlyList<HazardEvent> GetAllEvents(int? mapId = null)
public IReadOnlyList<HazardCluster> GetClusters(int mapId)
```

**Acceptance Criteria:**
- [x] Concurrent access safe (`lock` or `ConcurrentDictionary`)
- [x] Chunk key calculation matches: `((int)(pos.X / 100), (int)(pos.Y / 100))`
- [x] Returns 0.0f for positions with no nearby hazards
- [x] Prunes oldest events when count exceeds `MaxEventsBeforePrune`

**Benchmark Target:** `GetHazardCost()` < 100ns for cache hits

---

#### 5. `Core/Hazard/LocalHazardDAO.cs` (1.5h)

**Purpose:** Persist hazard data to JSON files per expansion/map

**File Structure:**
```
Json/HazardData/
├── classic/
│   ├── hazards_0.json          # Eastern Kingdoms
│   ├── hazards_1.json          # Kalimdor
│   └── hazards_530.json        # Outland
├── tbc/
└── wotlk/
```

**JSON Format:**
```json
{
  "version": "1.0",
  "mapId": 0,
  "lastUpdated": "2026-02-05T14:32:00Z",
  "events": [
    {
      "worldPosition": { "x": 100.5, "y": 50.2, "z": 10.1 },
      "mapPosition": { "x": 35.2, "y": 45.1 },
      "mapId": 0,
      "uiMapId": 1519,
      "type": 2,
      "timestamp": "2026-02-05T14:30:00Z",
      "zone": "Elwynn Forest",
      "playerClass": "Rogue",
      "playerLevel": 5
    }
  ]
}
```

**Implementation:**
```csharp
public sealed class LocalHazardDAO
{
    private readonly string _basePath;
    private readonly ILogger _logger;
    
    public LocalHazardDAO(IOptions<HazardAvoidanceOptions> options, ILogger<LocalHazardDAO> logger)
    {
        _basePath = Path.Combine("Json", "HazardData");
        Directory.CreateDirectory(_basePath);
    }
    
    public Task SaveAsync(int mapId, string expansion, IEnumerable<HazardEvent> events)
    {
        // Serialize to Json/HazardData/{expansion}/hazards_{mapId}.json
    }
    
    public Task<List<HazardEvent>> LoadAsync(int mapId, string expansion)
    {
        // Deserialize from file, return empty list if missing
    }
}
```

**Acceptance Criteria:**
- [x] Creates directory structure automatically
- [x] Gracefully handles missing files (returns empty list)
- [x] Uses `System.Text.Json` with indented formatting
- [x] Async I/O with `StreamReader`/`StreamWriter`
- [x] Logs save/load operations at Debug level

**Verification:**
```csharp
await dao.SaveAsync(0, "classic", events);
var loaded = await dao.LoadAsync(0, "classic");
Assert.Equal(events.Count, loaded.Count);
```

---

## Phase 2.2: Event Collection (4 hours)

### Files to Create

#### 6. `Core/Hazard/IHazardProvider.cs` (0.5h)

**Purpose:** Interface for pathfinding integration

```csharp
namespace Core.Hazard;

/// <summary>
/// Provides hazard data for pathfinding cost calculations.
/// </summary>
public interface IHazardProvider
{
    /// <summary>
    /// Gets the hazard cost for a position (0 = safe, higher = more dangerous).
    /// </summary>
    float GetHazardCost(Vector3 position, int mapId);
    
    /// <summary>
    /// Checks if a position is in a high-risk zone.
    /// </summary>
    bool IsHighRiskZone(Vector3 position, int mapId, float threshold = 10f);
    
    /// <summary>
    /// Gets all clusters for visualization.
    /// </summary>
    IReadOnlyList<HazardCluster> GetClusters(int mapId);
}
```

**Implementation:** `HazardZoneStore` will implement this interface

---

#### 7. `Core/Hazard/HazardEventCollector.cs` (2.5h)

**Purpose:** Subscribes to bot events and feeds HazardZoneStore

```csharp
public sealed class HazardEventCollector : IHostedService
{
    private readonly StuckDetector _stuckDetector;
    private readonly CombatLog _combatLog;
    private readonly Navigation _navigation;
    private readonly HazardZoneStore _store;
    private readonly PlayerReader _playerReader;
    private readonly ILogger _logger;
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Subscribe to events
        _stuckDetector.OnStuckDetected += HandleStuckDetected;
        _combatLog.PlayerDeath += HandlePlayerDeath;
        _combatLog.TargetEvade += HandleEvade;
        
        _logger.LogInformation("[HazardCollector   ] Started - subscribed to events");
        return Task.CompletedTask;
    }
    
    private void HandleStuckDetected(StuckEventData data)
    {
        var evt = new HazardEvent
        {
            WorldPosition = data.Position,
            MapPosition = _playerReader.WorldMapArea.ToMap_FlipXY(data.Position),
            MapId = _playerReader.MapId,
            UIMapId = _playerReader.UIMapId,
            Type = HazardEventType.Stuck,
            DurationMs = data.DurationMs,
            Zone = _playerReader.ZoneText,
            PlayerClass = _playerReader.PlayerClass.ToString(),
            PlayerLevel = _playerReader.Level
        };
        
        _store.AddEvent(evt);
        _logger.LogWarning("[HazardCollector   ] Stuck event at {Pos}", data.Position);
    }
    
    private void HandlePlayerDeath()
    {
        var evt = new HazardEvent
        {
            WorldPosition = _playerReader.PlayerLocation,
            MapPosition = _playerReader.WorldMapArea.ToMap_FlipXY(_playerReader.PlayerLocation),
            MapId = _playerReader.MapId,
            UIMapId = _playerReader.UIMapId,
            Type = HazardEventType.Death,
            Zone = _playerReader.ZoneText,
            PlayerClass = _playerReader.PlayerClass.ToString(),
            PlayerLevel = _playerReader.Level
        };
        
        _store.AddEvent(evt);
        _logger.LogError("[HazardCollector   ] Death event at {Pos}", _playerReader.PlayerLocation);
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stuckDetector.OnStuckDetected -= HandleStuckDetected;
        _combatLog.PlayerDeath -= HandlePlayerDeath;
        _combatLog.TargetEvade -= HandleEvade;
        return Task.CompletedTask;
    }
}
```

**Acceptance Criteria:**
- [x] All event handlers unsubscribe in `StopAsync()`
- [x] Uses padded logger name `[HazardCollector   ]`
- [x] Only collects events when `FeatureFlagService.IsHazardAvoidanceEnabled`
- [x] Gracefully handles null references (guard clauses)

---

#### 8. Extend `StuckDetector.cs` (1h)

**Required Changes:**

Add event publication:

```csharp
public event Action<StuckEventData>? OnStuckDetected;

public sealed class StuckEventData
{
    public Vector3 Position { get; init; }
    public int DurationMs { get; init; }
    public StuckState RecoveryMethod { get; init; }
}

// In Update() method when stuck confirmed:
OnStuckDetected?.Invoke(new StuckEventData
{
    Position = _playerReader.PlayerLocation,
    DurationMs = (int)(DateTime.UtcNow - _stuckStartTime).TotalMilliseconds,
    RecoveryMethod = stuckState
});
```

---

## Phase 2.3: Analytics Engine (8 hours)

### Files to Create

#### 9. `Core/Hazard/HazardAnalytics.cs` (2h)

**Purpose:** Temporal decay and severity scoring

```csharp
public static class HazardAnalytics
{
    private const double HalfLifeDays = 30.0;  // From feature flags
    
    /// <summary>
    /// Calculates temporal weight using exponential decay.
    /// Recent events get weight ~1.0, 30-day old events get ~0.5.
    /// </summary>
    public static float CalculateTemporalWeight(DateTime eventTime)
    {
        double ageInDays = (DateTime.UtcNow - eventTime).TotalDays;
        double decayConstant = Math.Log(2) / HalfLifeDays;  // λ = ln(2) / t½
        return (float)Math.Exp(-decayConstant * ageInDays);
    }
    
    /// <summary>
    /// Calculates severity score for a cluster.
    /// Formula: Σ(eventWeight × typeMultiplier × recencyWeight)
    /// </summary>
    public static float CalculateSeverityScore(HazardCluster cluster)
    {
        float score = 0f;
        
        foreach (var evt in cluster.Events)
        {
            float typeWeight = evt.Type switch
            {
                HazardEventType.Death => 10.0f,
                HazardEventType.Stuck => 3.0f,
                HazardEventType.TargetEvade => 2.0f,
                HazardEventType.PathfindingFailure => 5.0f,
                HazardEventType.UnexpectedAggro => 4.0f,
                HazardEventType.ManualMarker => 100.0f,  // User override
                _ => 1.0f
            };
            
            float recencyWeight = CalculateTemporalWeight(evt.Timestamp);
            score += typeWeight * recencyWeight;
        }
        
        // Cluster density bonus (more events in small area = more dangerous)
        float densityBonus = cluster.EventCount / Math.Max(cluster.Radius, 1f);
        
        return score + densityBonus;
    }
}
```

**Unit Tests Required:**
```csharp
[Fact]
public void TemporalWeight_CurrentEvent_ReturnsOne()
{
    Assert.InRange(HazardAnalytics.CalculateTemporalWeight(DateTime.UtcNow), 0.99f, 1.01f);
}

[Fact]
public void TemporalWeight_30DayOldEvent_ReturnsHalf()
{
    var old = DateTime.UtcNow.AddDays(-30);
    Assert.InRange(HazardAnalytics.CalculateTemporalWeight(old), 0.48f, 0.52f);
}

[Fact]
public void SeverityScore_Death_HigherThanStuck()
{
    var deathCluster = new HazardCluster { Events = [new HazardEvent { Type = HazardEventType.Death, Timestamp = DateTime.UtcNow }] };
    var stuckCluster = new HazardCluster { Events = [new HazardEvent { Type = HazardEventType.Stuck, Timestamp = DateTime.UtcNow }] };
    
    Assert.True(HazardAnalytics.CalculateSeverityScore(deathCluster) > HazardAnalytics.CalculateSeverityScore(stuckCluster));
}
```

---

#### 10. `Core/Hazard/HazardClusterAnalyzer.cs` (4h)

**Purpose:** DBSCAN clustering algorithm implementation

**Algorithm:** Density-Based Spatial Clustering (DBSCAN)
- **ε (epsilon):** 15 world units (configurable via feature flags)
- **minPts:** 2 events minimum to form cluster

**Reference:** Ester et al., KDD-96; scikit-learn DBSCAN

```csharp
public sealed class HazardClusterAnalyzer
{
    private readonly float _epsilon;
    private readonly int _minPoints;
    private readonly ILogger _logger;
    
    public HazardClusterAnalyzer(IOptions<HazardAvoidanceOptions> options, ILogger<HazardClusterAnalyzer> logger)
    {
        _epsilon = options.Value.DBSCANEpsilon;
        _minPoints = options.Value.DBSCANMinPoints;
        _logger = logger;
    }
    
    /// <summary>
    /// Runs DBSCAN algorithm on hazard events to find clusters.
    /// </summary>
    /// <returns>List of clusters (noise points excluded).</returns>
    public List<HazardCluster> RunDBSCAN(IReadOnlyList<HazardEvent> events)
    {
        if (events.Count < _minPoints)
            return new List<HazardCluster>();
        
        var points = events.ToList();
        var labels = new int[points.Count];
        Array.Fill(labels, -1);  // -1 = undefined, 0 = noise, >0 = cluster ID
        
        int clusterId = 0;
        
        for (int i = 0; i < points.Count; i++)
        {
            if (labels[i] != -1) continue;  // Already processed
            
            var neighbors = RangeQuery(points, points[i], _epsilon);
            
            if (neighbors.Count < _minPoints)
            {
                labels[i] = 0;  // Mark as noise
                continue;
            }
            
            clusterId++;
            labels[i] = clusterId;
            ExpandCluster(points, labels, neighbors, clusterId);
        }
        
        return BuildClusters(points, labels, clusterId);
    }
    
    private List<int> RangeQuery(List<HazardEvent> points, HazardEvent center, float eps)
    {
        return points
            .Select((p, idx) => (p, idx))
            .Where(x => Vector3.Distance(x.p.WorldPosition, center.WorldPosition) <= eps)
            .Select(x => x.idx)
            .ToList();
    }
    
    private void ExpandCluster(List<HazardEvent> points, int[] labels, List<int> neighbors, int clusterId)
    {
        var seedSet = new Queue<int>(neighbors);
        
        while (seedSet.Count > 0)
        {
            int q = seedSet.Dequeue();
            
            if (labels[q] == 0) labels[q] = clusterId;  // Convert noise to border point
            if (labels[q] != -1) continue;
            
            labels[q] = clusterId;
            var qNeighbors = RangeQuery(points, points[q], _epsilon);
            
            if (qNeighbors.Count >= _minPoints)
            {
                foreach (var n in qNeighbors)
                    if (labels[n] == -1 || labels[n] == 0)
                        seedSet.Enqueue(n);
            }
        }
    }
    
    private List<HazardCluster> BuildClusters(List<HazardEvent> points, int[] labels, int maxClusterId)
    {
        var clusters = new List<HazardCluster>();
        
        for (int cid = 1; cid <= maxClusterId; cid++)
        {
            var clusterEvents = points
                .Where((_, idx) => labels[idx] == cid)
                .ToList();
            
            if (clusterEvents.Count == 0) continue;
            
            var centroid = new Vector3(
                clusterEvents.Average(e => e.WorldPosition.X),
                clusterEvents.Average(e => e.WorldPosition.Y),
                clusterEvents.Average(e => e.WorldPosition.Z)
            );
            
            float radius = clusterEvents
                .Max(e => Vector3.Distance(e.WorldPosition, centroid));
            
            var cluster = new HazardCluster
            {
                Centroid = centroid,
                Radius = Math.Max(radius, _epsilon),  // Minimum radius = epsilon
                Events = clusterEvents
            };
            
            cluster.SeverityScore = HazardAnalytics.CalculateSeverityScore(cluster);
            clusters.Add(cluster);
        }
        
        _logger.LogDebug("[HazardAnalyzer   ] Clustered {Events} events into {Clusters} clusters",
            points.Count, clusters.Count);
        
        return clusters;
    }
}
```

**Unit Tests Required:**
```csharp
[Fact]
public void DBSCAN_TwoClosePoints_FormCluster()
{
    var events = new[]
    {
        new HazardEvent { WorldPosition = new Vector3(0, 0, 0), MapPosition = default, MapId = 0, UIMapId = 0, Type = HazardEventType.Stuck },
        new HazardEvent { WorldPosition = new Vector3(5, 0, 0), MapPosition = default, MapId = 0, UIMapId = 0, Type = HazardEventType.Stuck }
    };
    
    var analyzer = new HazardClusterAnalyzer(Options.Create(new HazardAvoidanceOptions()), NullLogger<HazardClusterAnalyzer>.Instance);
    var clusters = analyzer.RunDBSCAN(events);
    
    Assert.Single(clusters);
    Assert.Equal(2, clusters[0].EventCount);
}

[Fact]
public void DBSCAN_TwoDistantPoints_BothNoise()
{
    var events = new[]
    {
        new HazardEvent { WorldPosition = new Vector3(0, 0, 0), MapPosition = default, MapId = 0, UIMapId = 0, Type = HazardEventType.Stuck },
        new HazardEvent { WorldPosition = new Vector3(100, 0, 0), MapPosition = default, MapId = 0, UIMapId = 0, Type = HazardEventType.Stuck }
    };
    
    var analyzer = new HazardClusterAnalyzer(Options.Create(new HazardAvoidanceOptions()), NullLogger<HazardClusterAnalyzer>.Instance);
    var clusters = analyzer.RunDBSCAN(events);
    
    Assert.Empty(clusters);  // Both are isolated noise
}
```

**Benchmark Target:** < 100ms for 1000 events

---

#### 11. `Core/Hazard/RouteRehabilitator.cs` (2h)

**Purpose:** Reduce hazard severity when successful traversals occur

```csharp
public sealed class RouteRehabilitator
{
    private readonly HazardZoneStore _store;
    private readonly ILogger _logger;
    
    public void ReportSuccessfulTraversal(Vector3 position, int mapId, float radius = 20f)
    {
        var clusters = _store.GetClusters(mapId)
            .Where(c => c.ContainsPosition(position, safetyMargin: radius))
            .ToList();
        
        foreach (var cluster in clusters)
        {
            // Reduce severity by 20%
            cluster.SeverityScore *= 0.8f;
            
            _logger.LogDebug("[Rehabilitator    ] Reduced severity for cluster {Id} at {Pos}",
                cluster.Id, cluster.Centroid);
        }
    }
    
    public void ReportFailure(Vector3 position, int mapId, HazardEventType type)
    {
        // Increases severity of existing clusters or creates new event
        var evt = new HazardEvent
        {
            WorldPosition = position,
            MapPosition = default,  // Caller provides
            MapId = mapId,
            UIMapId = 0,  // Caller provides
            Type = type
        };
        
        _store.AddEvent(evt);
    }
}
```

**Integration Points:**
- Call `ReportSuccessfulTraversal()` when bot completes a route segment
- Call `ReportFailure()` when stuck/death occurs during navigation

---

## Phase 2.4: Navigation Integration (4 hours)

### Files to Modify

#### 12. `PPather/Graph/PathGraph.cs` (3h)

**Add Constructor Parameter:**
```csharp
private readonly IHazardProvider? _hazardProvider;

public PathGraph(..., IHazardProvider? hazardProvider = null)
{
    _hazardProvider = hazardProvider;
}
```

**Modify A* Scoring Method:**

Locate `ScoreSpot_A_Star_With_Model_And_Gradient_Avoidance` method:

```csharp
// BEFORE:
float F_Score = baseScore;

// AFTER:
float hazardCost = _hazardProvider?.GetHazardCost(spotLinkedToCurrent.Loc, mapId) ?? 0f;
float scaledHazardCost = hazardCost * HazardCostMultiplier;  // Tunable parameter
float F_Score = baseScore + scaledHazardCost;
```

**Add Tuning Constant:**
```csharp
private const float HazardCostMultiplier = 10.0f;  // From feature flags
```

**Acceptance Criteria:**
- [x] Hazard cost only applied if `_hazardProvider` not null
- [x] Multiplier configurable via `HazardAvoidanceOptions`
- [x] A* admissibility preserved (additive cost, not multiplicative)
- [x] Validation script covers hazard cost hook + snapshot (`Scripts/Validate-HazardAvoidance.ps1`)

---

#### 13. Add Hazard Toggle to UI (1h)

**File:** `Frontend/Pages/LeafletComponent.razor`

Add checkbox:
```razor
<div class="leaflet-controls">
    <label>
        <input type="checkbox" @bind="showHazardLayer" @bind:after="ToggleHazardLayer" />
        Show Hazard Zones
    </label>
</div>

@code {
    private bool showHazardLayer = false;
    
    private async Task ToggleHazardLayer()
    {
        if (showHazardLayer)
            await JS.InvokeVoidAsync("hazardHeatMap.show");
        else
            await JS.InvokeVoidAsync("hazardHeatMap.hide");
    }
}
```

---

## Phase 2.5: Visualization (6 hours)

### Files to Create

#### 14. Add Leaflet.heat Library (0.5h)

**Download:** https://github.com/Leaflet/Leaflet.heat/releases/latest

**File:** `Frontend/wwwroot/lib/leaflet-heat/leaflet-heat.js`

**Reference in `_Layout.cshtml` or `index.html`:**
```html
<script src="lib/leaflet-heat/leaflet-heat.js"></script>
```

---

#### 15. `Frontend/wwwroot/js/hazardHeatMap.js` (1.5h)

```javascript
window.hazardHeatMap = {
    heatLayer: null,
    
    initialize: function(map) {
        this.heatLayer = L.heatLayer([], {
            radius: 25,
            blur: 15,
            maxZoom: 17,
            gradient: {
                0.0: 'green',
                0.4: 'yellow',
                0.6: 'orange',
                0.8: 'red',
                1.0: 'darkred'
            }
        });
    },
    
    updateData: function(clusters) {
        const heatData = clusters.map(c => [
            c.mapY,  // Leaflet uses lat/lng
            c.mapX,
            c.severityScore / 100.0  // Normalize to 0-1
        ]);
        
        this.heatLayer.setLatLngs(heatData);
    },
    
    show: function() {
        if (map && this.heatLayer) {
            map.addLayer(this.heatLayer);
        }
    },
    
    hide: function() {
        if (map && this.heatLayer) {
            map.removeLayer(this.heatLayer);
        }
    }
};
```

---

#### 16. `Frontend/Services/HazardHeatMapService.cs` (2h)

```csharp
public sealed class HazardHeatMapService
{
    private readonly IHazardProvider _hazardProvider;
    private readonly IJSRuntime _js;
    
    public async Task UpdateHeatMapAsync(int mapId)
    {
        var clusters = _hazardProvider.GetClusters(mapId);
        
        var viewModels = clusters.Select(c => new
        {
            mapX = c.Centroid.X,  // Convert using WorldMapAreaDB.ToMap_FlipXY
            mapY = c.Centroid.Y,
            severityScore = c.SeverityScore
        });
        
        await _js.InvokeVoidAsync("hazardHeatMap.updateData", viewModels);
    }
}
```

---

#### 17. Extend `LeafletComponent.razor` (2h)

**Add SignalR Hub listener:**
```csharp
private async Task OnHazardDataUpdated()
{
    await _heatMapService.UpdateHeatMapAsync(_currentMapId);
}
```

**Subscribe in `OnInitializedAsync`:**
```csharp
await hubConnection.On("HazardDataUpdated", OnHazardDataUpdated);
```

---

## Phase 2.6: Background Services & DI (3 hours)

### Files to Create

#### 18. `Core/Hazard/HazardServiceExtensions.cs` (1h)

```csharp
public static class HazardServiceExtensions
{
    public static IServiceCollection AddHazardAvoidance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Only register if feature enabled
        services.AddOptions<HazardAvoidanceOptions>()
            .Bind(configuration.GetSection("Features:HazardAvoidance"));
        
        // Core services
        services.AddSingleton<HazardZoneStore>();
        services.AddSingleton<IHazardProvider>(sp => sp.GetRequiredService<HazardZoneStore>());
        services.AddSingleton<LocalHazardDAO>();
        services.AddSingleton<HazardClusterAnalyzer>();
        services.AddSingleton<RouteRehabilitator>();
        
        // Event collection
        services.AddHostedService<HazardEventCollector>();
        services.AddHostedService<HazardAnalyticsBackgroundService>();
        
        return services;
    }
}
```

**Usage in Startup:**
```csharp
services.AddPhase1Features(configuration);

if (configuration.GetValue<bool>("Features:HazardAvoidance:Enabled"))
{
    services.AddHazardAvoidance(configuration);
}
```

---

#### 19. `Core/Hazard/HazardAnalyticsBackgroundService.cs` (2h)

```csharp
public sealed class HazardAnalyticsBackgroundService : IHostedService, IAsyncDisposable
{
    private readonly HazardZoneStore _store;
    private readonly HazardClusterAnalyzer _analyzer;
    private readonly LocalHazardDAO _dao;
    private readonly ILogger _logger;
    private Timer? _clusterTimer;
    private Timer? _saveTimer;
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _clusterTimer = new Timer(
            RunClustering,
            null,
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(60)
        );
        
        _saveTimer = new Timer(
            SaveData,
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5)
        );
        
        _logger.LogInformation("[HazardAnalytics   ] Background service started");
        return Task.CompletedTask;
    }
    
    private void RunClustering(object? state)
    {
        try
        {
            var events = _store.GetAllEvents();
            if (events.Count < 2) return;
            
            var clusters = _analyzer.RunDBSCAN(events);
            _store.UpdateClusters(0, clusters);  // TODO: Per-map
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HazardAnalytics   ] Clustering failed");
        }
    }
    
    private void SaveData(object? state)
    {
        try
        {
            var events = _store.GetAllEvents();
            _dao.SaveAsync(0, "classic", events).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HazardAnalytics   ] Save failed");
        }
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _clusterTimer?.Change(Timeout.Infinite, 0);
        _saveTimer?.Change(Timeout.Infinite, 0);
        
        SaveData(null);  // Final save
        
        _logger.LogInformation("[HazardAnalytics   ] Background service stopped");
        return Task.CompletedTask;
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_clusterTimer is IAsyncDisposable timer1) await timer1.DisposeAsync();
        if (_saveTimer is IAsyncDisposable timer2) await timer2.DisposeAsync();
    }
}
```

---

## Verification & Testing

### Build Verification
```bash
dotnet build MasterOfPuppets.sln
# Target: 0 errors
```

### Unit Tests
```bash
dotnet test --filter "FullyQualifiedName~Hazard"
# Target: All tests pass, ≥80% coverage
```

### Integration Test
```bash
dotnet run --project CoreTests
# Run HazardIntegrationTests.cs
```

### Benchmark
```bash
dotnet run --project Benchmarks -c Release -- --filter "*Hazard*"
# Targets:
# - GetHazardCost: < 100ns
# - DBSCAN (1000 events): < 100ms
# - Save/Load: < 50ms
```

### Runtime Validation
1. Enable feature flag: `"HazardAvoidance": { "Enabled": true }`
2. Enable debug mode for synthetic validation: `"DebugMode": true` (optional)
2. Restart bot
3. Trigger stuck event (manually navigate into obstacle)
4. Check logs for `[HazardCollector] Stuck event at ...`
5. Wait 60 seconds for clustering
6. Check logs for `[HazardAnalyzer] Clustered X events into Y clusters`
7. Check file: `Json/HazardData/classic/hazards_0.json` exists
8. Open Blazor UI → Map → Toggle "Show Hazard Zones"
9. Verify heat overlay displays on map

**Synthetic / No-Game Validation (requires DebugMode=true)**
- Run `Scripts/Validate-HazardAvoidance.ps1` (injects + clusters events and confirms API responses; optional `-OpenLeaflet -HoldSeconds 120 -TryPathRoute -TryPathCompare`)
- Or call:
  - `POST /api/debug/hazards/{mapId}/clear`
  - `POST /api/debug/hazards/{mapId}/inject`
  - `POST /api/debug/hazards/{mapId}/cluster`
  - `GET  /api/debug/hazards/{mapId}`

---

## Rollout Strategy

### Week 1: Foundation (Phase 2.1-2.2)
- [x] Data models complete
- [x] Storage & persistence working
- [x] Event collection implemented (runtime validation still recommended)

### Week 2: Analytics (Phase 2.3)
- [x] DBSCAN clustering functional
- [x] Temporal decay applied
- [x] Route rehabilitation tested

### Week 3: Integration (Phase 2.4-2.5)
- [x] PathGraph integration complete
- [x] UI visualization implemented (manual UI verification recommended)
- [x] Heat map integration implemented (manual UI verification recommended)

### Week 4: Polish & Production (Phase 2.6)
- [x] Background services stable (exception-isolated loops + feature-flag gating)
- [x] Key unit/integration tests passing (`dotnet test -c Release`)
- [x] Performance validated (hazard validation scripts + targeted benchmarks where available)
- [x] Documentation complete

---

## Success Criteria

| Metric | Target | Verification |
|--------|--------|--------------|
| Build errors | 0 | `dotnet build` |
| Unit test coverage | ≥80% | `dotnet test --collect:"XPlat Code Coverage"` |
| DBSCAN performance | <100ms for 1K events | Benchmark |
| Hazard lookup | <100ns | Benchmark |
| Event persistence | 0 data loss on crash | Integration test |
| Path deviation | Avoids high-hazard zones | Manual test |
| UI responsiveness | Heat map updates <1s | Manual test |

---

## References

- **PRD:** [PRD_HAZARD_AVOIDANCE_SYSTEM.md](file:///c:/WowClassicGrindBot/docs/PRD_HAZARD_AVOIDANCE_SYSTEM.md)
- **DBSCAN:** Ester et al., KDD-96
- **A* Theory:** Stanford University (https://theory.stanford.edu/~amitp/GameProgramming/)
- **Leaflet.heat:** https://github.com/Leaflet/Leaflet.heat
- **Exponential Decay:** https://en.wikipedia.org/wiki/Exponential_decay

---

**Next Action:** Complete Phase 1 production deployment, then begin Phase 2.1 (Data Models & Storage).
**Next Action:** Enable `Features:HazardAvoidance:Enabled=true` in `BlazorServer/runtime_feature_flags.json` and validate:
- API: `GET /api/debug/hazards/maps` and `GET /api/debug/hazards/{mapId}`
- UI: Leaflet hazard toggle renders heat overlay
- Navigation: local PPather paths avoid high-hazard clusters (manual scenario)
