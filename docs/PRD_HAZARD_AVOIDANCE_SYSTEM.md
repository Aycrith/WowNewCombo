# Hazard Detection & Avoidance System
## Complete PRD / PRP / Task Breakdown

**Version:** 1.0  
**Date:** February 5, 2026  
**Author:** GitHub Copilot  
**Status:** ✅ Implemented (Feb 5, 2026)

---

## 1. Executive Summary

Build a self-learning hazard avoidance system that captures real-time stuck events, deaths, and navigation failures, then uses this data to dynamically adjust pathfinding to circumvent dangerous areas. The system extends the existing `StuckDetector`, `CombatLog`, and Navigation infrastructure.

### Proven Pattern Foundation

| Pattern | Source | Application |
|---------|--------|-------------|
| DBSCAN Clustering | Ester et al., 1996; scikit-learn | Cluster hazard events into danger zones |
| A* with Dynamic Costs | Hart, Nilsson, Raphael 1968; Red Blob Games | Inject hazard penalties into pathfinding |
| Exponential Decay | Wikipedia; Half-life formula | Temporal weighting of events |
| Domain Events | Microsoft DDD; MediatR | Decoupled event collection |
| IHostedService | Microsoft ASP.NET Core | Background analytics processing |
| Leaflet.heat | GitHub Leaflet | Heat map visualization |

---

## 2. Product Requirements Document (PRD)

### 2.1 Product Vision

Create a learning navigation system that prevents repeated failures by accumulating hazard knowledge across sessions and dynamically rerouting away from problem areas.

### 2.2 User Stories

| ID | Story | Acceptance Criteria |
|----|-------|---------------------|
| US-1 | As a bot user, I want stuck events logged with coordinates | Event includes Vector3, timestamp, zone name, event type |
| US-2 | As a bot user, I want the bot to avoid areas where it previously died | Death locations increase hazard cost; paths route around |
| US-3 | As a bot user, I want hazard data to persist between sessions | JSON files saved per expansion, loaded on startup |
| US-4 | As a bot user, I want to see a heat map of dangerous areas | Leaflet heat layer displays cluster severity |
| US-5 | As a bot user, I want old incidents to matter less | 30-day half-life decay reduces stale data weight |
| US-6 | As a bot user, I want successful traversals to rehabilitate areas | Cluster severity decreases after successful passes |

### 2.3 Functional Requirements

| ID | Requirement | Priority | Implementation |
|----|-------------|----------|----------------|
| FR-1 | Log hazard events with HazardEventType enum | P0 | `HazardEventType.cs` |
| FR-2 | Store events in `Json/HazardData/{expansion}/` | P0 | `LocalHazardDAO.cs` |
| FR-3 | Cluster events using DBSCAN (ε=15, minPts=2) | P0 | `HazardClusterAnalyzer.cs` |
| FR-4 | Inject hazard cost into A* pathfinding | P0 | `PathGraph.cs` modification |
| FR-5 | Apply 30-day half-life exponential decay | P1 | `HazardAnalytics.cs` |
| FR-6 | Display Leaflet.heat visualization layer | P1 | `LeafletComponent.razor` |
| FR-7 | Rehabilitate clusters on successful traversal | P2 | `RouteRehabilitator.cs` |
| FR-8 | Provide debug API to dump hazard snapshots | P1 | `Frontend/Controllers/HazardDebugController.cs` |

### 2.4 Non-Functional Requirements

| ID | Requirement | Target | Validation |
|----|-------------|--------|------------|
| NFR-1 | Hazard lookup performance | O(1) via spatial chunking | Benchmark test |
| NFR-2 | Memory footprint | <50MB for 10,000 events | Memory profiler |
| NFR-3 | Persistence reliability | Auto-save every 5 minutes | Integration test |
| NFR-4 | Clustering latency | <100ms for 1000 events | Benchmark test |

---

### 2.5 Debugging & Validation (Debug API)

For runtime validation without attaching a debugger, the Web UI host exposes lightweight debug endpoints:

- `GET /api/debug/hazards/maps`
  - Returns known `MapId` values with event/cluster counts.
- `GET /api/debug/hazards/{mapId}`
  - Returns a snapshot of events/clusters for the selected map.
  - Query parameters:
    - `includeEvents` (bool, default `true`)
    - `includeClusters` (bool, default `true`)
    - `maxEvents` (int, default `250`, max `10000`)
    - `maxClusters` (int, default `1000`, max `50000`)
    - `maxAgeMinutes` (int?, optional; filters events by `Timestamp >= now - maxAgeMinutes`)
    - `mostRecentFirst` (bool, default `true`)

Synthetic validation endpoints (require `DebugMode: true` in `BlazorServer/runtime_feature_flags.json`):

- `POST /api/debug/hazards/{mapId}/clear`
  - Clears in-memory events and clusters for that map.
- `POST /api/debug/hazards/{mapId}/inject`
  - Adds synthetic hazard events for fast UI/pathing validation.
  - Body (example):
    ```json
    { "x": 0, "y": 0, "z": 0, "uiMapId": 0, "type": 99, "count": 5, "zone": "Debug" }
    ```
- `POST /api/debug/hazards/{mapId}/cluster`
  - Runs DBSCAN clustering immediately for that map and replaces the in-memory cluster snapshot.

Pathing validation endpoint (requires `DebugMode: true`):

- `POST /api/debug/path/{mapId}/compare`
  - Computes two routes between the same endpoints using two `SearchStrategy` values.
  - Recommended for avoidance validation:
    - `HazardStrategy = A_Star_With_Model_Avoidance` (includes hazard penalty)
    - `BaselineStrategy = A_Star` (no hazard penalty)

## 3. Product Research Plan (PRP) - Proven Patterns

### 3.1 DBSCAN Clustering Algorithm

**Source:** [scikit-learn DBSCAN](https://scikit-learn.org/stable/modules/clustering.html#dbscan), Ester et al. "A Density-Based Algorithm for Discovering Clusters in Large Spatial Databases with Noise" (KDD-96)

**Why DBSCAN over K-means:**
- Does NOT require predefined cluster count (ideal for unknown hazard distribution)
- Finds arbitrarily-shaped clusters (terrain hazards aren't spherical)
- Built-in noise/outlier detection (isolated incidents don't form clusters)
- O(n log n) with spatial index

**From scikit-learn documentation:**
> "DBSCAN (Density-Based Spatial Clustering of Applications with Noise) is a density-based clustering algorithm that groups together points that are closely packed while marking points in low-density regions as outliers."

**Algorithm Parameters:**
```
ε (epsilon) = 15 world units  // neighborhood radius
minPts = 2                    // minimum points to form cluster
```

**C# Implementation (from research):**
```csharp
public class HazardClusterAnalyzer
{
    private const float Epsilon = 15f;  // world units
    private const int MinPoints = 2;
    
    public List<HazardCluster> RunDBSCAN(IEnumerable<HazardEvent> events)
    {
        var points = events.ToList();
        var labels = new int[points.Count];
        Array.Fill(labels, -1); // -1 = undefined
        
        int clusterId = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (labels[i] != -1) continue;
            
            var neighbors = RangeQuery(points, points[i], Epsilon);
            if (neighbors.Count < MinPoints)
            {
                labels[i] = 0; // noise
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
    
    private void ExpandCluster(List<HazardEvent> points, int[] labels, 
        List<int> neighbors, int clusterId)
    {
        var seedSet = new Queue<int>(neighbors);
        
        while (seedSet.Count > 0)
        {
            int q = seedSet.Dequeue();
            
            if (labels[q] == 0) // was noise
                labels[q] = clusterId;
            
            if (labels[q] != -1) continue;
            
            labels[q] = clusterId;
            var qNeighbors = RangeQuery(points, points[q], Epsilon);
            
            if (qNeighbors.Count >= MinPoints)
            {
                foreach (var n in qNeighbors)
                    if (labels[n] == -1 || labels[n] == 0)
                        seedSet.Enqueue(n);
            }
        }
    }
    
    private List<HazardCluster> BuildClusters(List<HazardEvent> points, 
        int[] labels, int maxClusterId)
    {
        var clusters = new List<HazardCluster>();
        
        for (int cid = 1; cid <= maxClusterId; cid++)
        {
            var clusterEvents = points
                .Where((_, idx) => labels[idx] == cid)
                .ToList();
            
            if (clusterEvents.Count == 0) continue;
            
            // Calculate centroid using average position
            var centroid = new Vector3(
                clusterEvents.Average(e => e.WorldPosition.X),
                clusterEvents.Average(e => e.WorldPosition.Y),
                clusterEvents.Average(e => e.WorldPosition.Z));
            
            // Calculate radius as max distance from centroid
            float radius = clusterEvents
                .Max(e => Vector3.Distance(e.WorldPosition, centroid));
            
            clusters.Add(new HazardCluster
            {
                Centroid = centroid,
                Radius = Math.Max(radius, Epsilon),
                Events = clusterEvents,
                EventCount = clusterEvents.Count,
                LastIncident = clusterEvents.Max(e => e.Timestamp)
            });
        }
        
        return clusters;
    }
}
```

---

### 3.2 A* Pathfinding with Dynamic Hazard Costs

**Source:** [Red Blob Games A* Implementation](https://www.redblobgames.com/pathfinding/a-star/implementation.html), [Stanford A* Theory](https://theory.stanford.edu/~amitp/GameProgramming/AStarComparison.html)

**Core Formula:**
```
f(n) = g(n) + h(n)
```
Where:
- `g(n)` = actual cost from start to node n
- `h(n)` = heuristic estimate from n to goal

**From Red Blob Games:**
> "A* is like Dijkstra's Algorithm in that it can be used to find a shortest path. A* is like Greedy Best-First-Search in that it can use a heuristic to guide itself."

**Hazard Cost Injection Pattern:**
```
f(n) = g(n) + h(n) + hazardCost(n)
```

**From Stanford theory:**
> "The lower h(n) is, the more nodes A* expands, making it slower."

Hazard costs should be **additive penalties**, not multiplicative, to preserve A* admissibility properties.

**Integration into existing PathGraph.cs:**
```csharp
// In ScoreSpot_A_Star_With_Model_And_Gradient_Avoidance method:

// Existing cost calculation
float baseScore = CalculateBaseScore(spotLinkedToCurrent);

// NEW: Add hazard penalty from IHazardProvider
float hazardPenalty = _hazardProvider?.GetHazardCost(
    spotLinkedToCurrent.Loc, MapId) ?? 0f;

// Scale hazard cost to match movement cost scale
// HazardCostMultiplier should be tuned (start with 10.0f)
float scaledHazardCost = hazardPenalty * HazardCostMultiplier;

// Combined F-score
float F_Score = baseScore + scaledHazardCost;
```

**Priority Queue Pattern (from Red Blob Games C# example):**
```csharp
// Modern .NET has PriorityQueue<TElement, TPriority>
var frontier = new PriorityQueue<Location, double>();
frontier.Enqueue(start, 0);

cameFrom[start] = start;
costSoFar[start] = 0;

while (frontier.Count > 0)
{
    var current = frontier.Dequeue();
    
    if (current.Equals(goal)) break;
    
    foreach (var next in graph.Neighbors(current))
    {
        double newCost = costSoFar[current] + graph.Cost(current, next);
        if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
        {
            costSoFar[next] = newCost;
            double priority = newCost + Heuristic(next, goal);
            frontier.Enqueue(next, priority);
            cameFrom[next] = current;
        }
    }
}
```

---

### 3.3 Exponential Decay for Temporal Weighting

**Source:** [Wikipedia Exponential Decay](https://en.wikipedia.org/wiki/Exponential_decay)

**Mathematical Formula:**
```
N(t) = N₀ × e^(-λt)
```

Where:
- `N₀` = initial value (base severity)
- `λ` = decay constant
- `t` = time elapsed (days)

**Half-Life Relationship:**
```
λ = ln(2) / t½
```

For 30-day half-life:
```
λ = ln(2) / 30 ≈ 0.0231 per day
```

**C# Implementation:**
```csharp
public static class HazardAnalytics
{
    // 30-day half-life: after 30 days, event weight is 50% of original
    private static readonly double Lambda = Math.Log(2) / 30.0;
    
    /// <summary>
    /// Calculates temporal weight using exponential decay.
    /// Returns 1.0 for current events, ~0.5 after 30 days, ~0.25 after 60 days.
    /// </summary>
    public static double CalculateTemporalWeight(DateTime eventTime)
    {
        double daysSinceEvent = (DateTime.UtcNow - eventTime).TotalDays;
        if (daysSinceEvent < 0) return 1.0; // Future events get full weight
        return Math.Exp(-Lambda * daysSinceEvent);
    }
    
    /// <summary>
    /// Event type weights based on severity impact.
    /// Death = highest impact, Oscillation = lowest.
    /// </summary>
    private static double GetEventTypeWeight(HazardEventType type) => type switch
    {
        HazardEventType.Death => 10.0,
        HazardEventType.Stuck => 5.0,
        HazardEventType.MultiMobAggro => 4.0,
        HazardEventType.PathingFailure => 3.0,
        HazardEventType.Evade => 2.0,
        HazardEventType.OscillationLoop => 1.0,
        _ => 1.0
    };
    
    /// <summary>
    /// Calculates total severity score for a cluster.
    /// Combines type weights, temporal decay, and frequency multiplier.
    /// </summary>
    public static double CalculateSeverityScore(HazardCluster cluster)
    {
        double totalWeight = 0;
        
        foreach (var evt in cluster.Events)
        {
            double typeWeight = GetEventTypeWeight(evt.Type);
            double temporalWeight = CalculateTemporalWeight(evt.Timestamp);
            totalWeight += typeWeight * temporalWeight;
        }
        
        // Frequency multiplier: log2 scaling rewards dense clusters
        // 2 events = 1.58x, 4 events = 2.32x, 8 events = 3.17x
        double frequencyMultiplier = Math.Log2(cluster.EventCount + 1);
        
        return totalWeight * frequencyMultiplier;
    }
}
```

---

### 3.4 Domain Events Pattern (Microsoft DDD)

**Source:** [Microsoft Domain Events Design](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)

**Key Principle:**
> "Domain events help you to express, explicitly, the domain rules, based in the ubiquitous language provided by the domain experts. Domain events also enable a better separation of concerns among classes within the same domain."

**Deferred Dispatch Pattern (Jimmy Bogard):**
> "Just before we commit our transaction, we dispatch our events to their respective handlers."

**Event Definition (from Microsoft example):**
```csharp
// Following Microsoft's pattern: events are immutable records
public record HazardEvent : INotification
{
    public required Vector3 WorldPosition { get; init; }
    public required float MapId { get; init; }
    public int UIMapId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required HazardEventType Type { get; init; }
    public double DurationMs { get; init; }
    public int AttemptCount { get; init; }
    public string? Zone { get; init; }
    public string? AdditionalInfo { get; init; }
}

public enum HazardEventType
{
    Stuck,
    Death,
    MultiMobAggro,
    PathingFailure,
    Evade,
    OscillationLoop
}
```

**Event Handler Pattern:**
```csharp
// From Microsoft eShop example pattern
public class HazardEventCollector : IHostedService
{
    private readonly ILogger<HazardEventCollector> _logger;
    private readonly StuckDetector _stuckDetector;
    private readonly CombatLog _combatLog;
    private readonly Navigation _navigation;
    private readonly HazardZoneStore _store;
    private readonly PlayerReader _playerReader;
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Subscribe to existing events in the codebase
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
            MapId = _playerReader.MapId,
            UIMapId = _playerReader.UIMapId,
            Type = HazardEventType.Stuck,
            DurationMs = data.DurationMs,
            Zone = _playerReader.ZoneText
        };
        
        _store.AddEvent(evt);
        _logger.LogWarning("[HazardCollector   ] Stuck event at {Pos}", 
            data.Position);
    }
    
    private void HandlePlayerDeath()
    {
        var evt = new HazardEvent
        {
            WorldPosition = _playerReader.PlayerLocation,
            MapId = _playerReader.MapId,
            UIMapId = _playerReader.UIMapId,
            Type = HazardEventType.Death,
            Zone = _playerReader.ZoneText
        };
        
        _store.AddEvent(evt);
        _logger.LogError("[HazardCollector   ] Death event at {Pos}", 
            _playerReader.PlayerLocation);
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

---

### 3.5 IHostedService Background Processing

**Source:** [Microsoft Background Tasks](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services), [Timer Service](https://learn.microsoft.com/en-us/dotnet/core/extensions/timer-service)

**From Microsoft documentation:**
> "In ASP.NET Core, background tasks can be implemented as hosted services. A hosted service is a class with background task logic that implements the IHostedService interface."

**Timed Background Service Pattern:**
```csharp
public sealed class HazardAnalyticsBackgroundService : IHostedService, IAsyncDisposable
{
    private readonly ILogger<HazardAnalyticsBackgroundService> _logger;
    private readonly HazardZoneStore _store;
    private readonly HazardClusterAnalyzer _analyzer;
    private readonly LocalHazardDAO _dao;
    private Timer? _clusterTimer;
    private Timer? _saveTimer;
    
    private const int ClusteringIntervalSeconds = 60;
    private const int SaveIntervalMinutes = 5;
    
    public HazardAnalyticsBackgroundService(
        ILogger<HazardAnalyticsBackgroundService> logger,
        HazardZoneStore store,
        HazardClusterAnalyzer analyzer,
        LocalHazardDAO dao)
    {
        _logger = logger;
        _store = store;
        _analyzer = analyzer;
        _dao = dao;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[HazardAnalytics   ] Background service starting");
        
        // Run clustering every 60 seconds
        _clusterTimer = new Timer(
            RunClustering, 
            null, 
            TimeSpan.FromSeconds(ClusteringIntervalSeconds),
            TimeSpan.FromSeconds(ClusteringIntervalSeconds));
        
        // Auto-save every 5 minutes
        _saveTimer = new Timer(
            SaveData,
            null,
            TimeSpan.FromMinutes(SaveIntervalMinutes),
            TimeSpan.FromMinutes(SaveIntervalMinutes));
        
        return Task.CompletedTask;
    }
    
    private void RunClustering(object? state)
    {
        try
        {
            var events = _store.GetAllEvents();
            if (events.Count < 2) return;
            
            var clusters = _analyzer.RunDBSCAN(events);
            _store.UpdateClusters(clusters);
            
            _logger.LogDebug("[HazardAnalytics   ] Clustered {Events} events into {Clusters} clusters",
                events.Count, clusters.Count);
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
            _dao.Save(_store.GetAllEvents());
            _logger.LogDebug("[HazardAnalytics   ] Data saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HazardAnalytics   ] Save failed");
        }
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[HazardAnalytics   ] Background service stopping");
        
        _clusterTimer?.Change(Timeout.Infinite, 0);
        _saveTimer?.Change(Timeout.Infinite, 0);
        
        // Final save on shutdown
        SaveData(null);
        
        return Task.CompletedTask;
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_clusterTimer is IAsyncDisposable timer1)
            await timer1.DisposeAsync();
        if (_saveTimer is IAsyncDisposable timer2)
            await timer2.DisposeAsync();
    }
}
```

---

### 3.6 Leaflet.heat Visualization

**Source:** [Leaflet.heat GitHub](https://github.com/Leaflet/Leaflet.heat)

**Basic Usage (from official docs):**
```javascript
var heat = L.heatLayer([
    [50.5, 30.5, 0.2], // lat, lng, intensity
    [50.6, 30.4, 0.5],
], {radius: 25}).addTo(map);
```

**Options:**
- `minOpacity` - minimum opacity the heat will start at
- `maxZoom` - zoom level where points reach maximum intensity
- `radius` - radius of each "point" (default: 25)
- `blur` - amount of blur (default: 15)
- `gradient` - color gradient config, e.g. `{0.4: 'blue', 0.65: 'lime', 1: 'red'}`

**Methods:**
- `setOptions(options)` - Sets new options and redraws
- `addLatLng(latlng)` - Adds a new point and redraws
- `setLatLngs(latlngs)` - Resets data and redraws
- `redraw()` - Redraws the heatmap

**Blazor Integration Pattern:**
```javascript
// wwwroot/js/hazardHeatMap.js
window.hazardHeatMap = {
    heatLayer: null,
    map: null,
    
    init: function(mapInstance) {
        this.map = mapInstance;
        this.heatLayer = L.heatLayer([], {
            radius: 20,
            blur: 15,
            maxZoom: 17,
            minOpacity: 0.3,
            gradient: {
                0.2: 'green',
                0.4: 'lime',
                0.6: 'yellow',
                0.8: 'orange',
                1.0: 'red'
            }
        });
    },
    
    show: function() {
        if (this.heatLayer && this.map) {
            this.heatLayer.addTo(this.map);
        }
    },
    
    hide: function() {
        if (this.heatLayer && this.map) {
            this.map.removeLayer(this.heatLayer);
        }
    },
    
    updateData: function(points) {
        // points: [[lat, lng, intensity], ...]
        if (this.heatLayer) {
            this.heatLayer.setLatLngs(points);
        }
    },
    
    addPoint: function(lat, lng, intensity) {
        if (this.heatLayer) {
            this.heatLayer.addLatLng([lat, lng, intensity]);
        }
    }
};
```

**Blazor Razor Component Integration:**
```razor
@inject IJSRuntime JSRuntime

<button class="btn btn-sm @(showHeatMap ? "btn-danger" : "btn-success")" 
        @onclick="ToggleHeatMap">
    @(showHeatMap ? "Hide" : "Show") Hazard Map
</button>

@code {
    private bool showHeatMap = false;
    
    private async Task ToggleHeatMap()
    {
        showHeatMap = !showHeatMap;
        
        if (showHeatMap)
        {
            await JSRuntime.InvokeVoidAsync("hazardHeatMap.show");
            await UpdateHeatMapData();
        }
        else
        {
            await JSRuntime.InvokeVoidAsync("hazardHeatMap.hide");
        }
    }
    
    private async Task UpdateHeatMapData()
    {
        var points = await HazardService.GetHeatMapPoints();
        await JSRuntime.InvokeVoidAsync("hazardHeatMap.updateData", points);
    }
}
```

---

### 3.7 Spatial Indexing with Chunked Grid

**Pattern from existing codebase** (`ChunkedTriangleCollection.cs`):

```csharp
public class HazardZoneStore : IHazardProvider
{
    private readonly Dictionary<(int chunkX, int chunkY), List<HazardCluster>> _spatialIndex = new();
    private readonly List<HazardEvent> _allEvents = new();
    private readonly ReaderWriterLockSlim _lock = new();
    
    private const float ChunkSize = 100f; // world units
    
    public void AddEvent(HazardEvent evt)
    {
        _lock.EnterWriteLock();
        try
        {
            _allEvents.Add(evt);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    public void UpdateClusters(List<HazardCluster> clusters)
    {
        _lock.EnterWriteLock();
        try
        {
            _spatialIndex.Clear();
            
            foreach (var cluster in clusters)
            {
                // Add to all chunks the cluster might influence
                var minChunk = GetChunkKey(cluster.Centroid - new Vector3(cluster.Radius * 2));
                var maxChunk = GetChunkKey(cluster.Centroid + new Vector3(cluster.Radius * 2));
                
                for (int x = minChunk.Item1; x <= maxChunk.Item1; x++)
                {
                    for (int y = minChunk.Item2; y <= maxChunk.Item2; y++)
                    {
                        var key = (x, y);
                        if (!_spatialIndex.TryGetValue(key, out var list))
                        {
                            list = new List<HazardCluster>();
                            _spatialIndex[key] = list;
                        }
                        list.Add(cluster);
                    }
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    /// <summary>
    /// O(1) lookup for hazard cost at a position.
    /// Uses inverse distance weighting within cluster influence zone.
    /// </summary>
    public float GetHazardCost(Vector3 position, float mapId)
    {
        _lock.EnterReadLock();
        try
        {
            var chunkKey = GetChunkKey(position);
            
            if (!_spatialIndex.TryGetValue(chunkKey, out var clusters))
                return 0f;
            
            float totalCost = 0f;
            
            foreach (var cluster in clusters)
            {
                float distance = Vector3.Distance(position, cluster.Centroid);
                float influenceRadius = cluster.Radius * 2;
                
                if (distance <= influenceRadius)
                {
                    // Inverse distance weighting: full effect at center, zero at edge
                    float influence = 1f - (distance / influenceRadius);
                    float severity = (float)HazardAnalytics.CalculateSeverityScore(cluster);
                    totalCost += severity * influence;
                }
            }
            
            return totalCost;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
    
    public bool IsHighRiskZone(Vector3 position, float threshold = 10f)
    {
        return GetHazardCost(position, 0) >= threshold;
    }
    
    private (int, int) GetChunkKey(Vector3 pos) => 
        ((int)(pos.X / ChunkSize), (int)(pos.Y / ChunkSize));
}
```

---

## 4. Task Breakdown

### Phase 1: Data Models & Storage (Est. 6 hours)

| Task | Description | Files | Est. |
|------|-------------|-------|------|
| 1.1 | Create `HazardEventType` enum | `Core/Hazard/HazardEventType.cs` | 0.5h |
| 1.2 | Create `HazardEvent` record | `Core/Hazard/HazardEvent.cs` | 1h |
| 1.3 | Create `HazardCluster` model | `Core/Hazard/HazardCluster.cs` | 1h |
| 1.4 | Create `HazardZoneStore` with spatial index | `Core/Hazard/HazardZoneStore.cs` | 2h |
| 1.5 | Create `LocalHazardDAO` (JSON persistence) | `Core/Hazard/LocalHazardDAO.cs` | 1.5h |

### Phase 2: Event Collection (Est. 4 hours)

| Task | Description | Files | Est. |
|------|-------------|-------|------|
| 2.1 | Create `IHazardProvider` interface | `SharedLib/IHazardProvider.cs` | 0.5h |
| 2.2 | Create `HazardEventCollector` service | `Core/Hazard/HazardEventCollector.cs` | 2.5h |
| 2.3 | Extend `StuckEventData` with map metadata | `Core/GoalsComponent/StuckDetector.cs` | 1h |

### Phase 3: Analytics Engine (Est. 8 hours)

| Task | Description | Files | Est. |
|------|-------------|-------|------|
| 3.1 | Implement `HazardAnalytics` (decay & severity) | `Core/Hazard/HazardAnalytics.cs` | 2h |
| 3.2 | Implement DBSCAN in `HazardClusterAnalyzer` | `Core/Hazard/HazardClusterAnalyzer.cs` | 4h |
| 3.3 | Create `RouteRehabilitator` for feedback | `Core/Hazard/RouteRehabilitator.cs` | 2h |

### Phase 4: Navigation Integration (Est. 4 hours)

| Task | Description | Files | Est. |
|------|-------------|-------|------|
| 4.1 | Add `IHazardProvider` to `PathGraph` constructor | `PPather/Graph/PathGraph.cs` | 1h |
| 4.2 | Inject hazard cost into `ScoreSpot_A_Star*` | `PPather/Graph/PathGraph.cs` | 2h |
| 4.3 | (Optional) Add hazard-specific search strategy enum | `PPather/Graph/SearchStrategy.cs` | 1h |

### Phase 5: Visualization (Est. 6 hours)

| Task | Description | Files | Est. |
|------|-------------|-------|------|
| 5.1 | Add Leaflet.heat JS to wwwroot | `Frontend/wwwroot/leaflet-heat/` | 0.5h |
| 5.2 | Create heat map JS interop module | `Frontend/wwwroot/script/hazardHeatMap.js` | 1.5h |
| 5.3 | Extend `LeafletComponent.razor` with toggle | `Frontend/Pages/LeafletComponent.razor` | 2h |
| 5.4 | Create `HazardHeatMapService` | `Frontend/Services/HazardHeatMapService.cs` | 2h |

### Phase 6: DI & Background Services (Est. 3 hours)

| Task | Description | Files | Est. |
|------|-------------|-------|------|
| 6.1 | Create DI registration extension | `Core/Hazard/HazardServiceCollectionExtensions.cs` | 1h |
| 6.2 | Create `HazardAnalyticsBackgroundService` | `Core/Hazard/HazardAnalyticsBackgroundService.cs` | 2h |

**Total Estimated Effort:** ~31 hours

---

## 5. Verification Plan

### 5.1 Unit Tests

```csharp
// CoreTests/Hazard/HazardAnalyticsTests.cs

[Fact]
public void TemporalWeight_ReturnsOne_ForCurrentEvents()
{
    var weight = HazardAnalytics.CalculateTemporalWeight(DateTime.UtcNow);
    Assert.InRange(weight, 0.99, 1.01);
}

[Fact]
public void TemporalWeight_Halves_After30Days()
{
    var recent = HazardAnalytics.CalculateTemporalWeight(DateTime.UtcNow);
    var old = HazardAnalytics.CalculateTemporalWeight(DateTime.UtcNow.AddDays(-30));
    
    Assert.InRange(old / recent, 0.48, 0.52); // ~0.5 ± tolerance
}

[Fact]
public void DBSCAN_Clusters_NearbyPoints()
{
    var events = new[]
    {
        new HazardEvent { WorldPosition = new Vector3(0, 0, 0), Type = HazardEventType.Stuck },
        new HazardEvent { WorldPosition = new Vector3(5, 0, 0), Type = HazardEventType.Stuck },
        new HazardEvent { WorldPosition = new Vector3(100, 0, 0), Type = HazardEventType.Stuck }
    };
    
    var analyzer = new HazardClusterAnalyzer();
    var clusters = analyzer.RunDBSCAN(events);
    
    Assert.Single(clusters); // 2 close points cluster, 1 isolated = noise
    Assert.Equal(2, clusters[0].EventCount);
}

[Fact]
public void DBSCAN_IdentifiesNoise()
{
    var events = new[]
    {
        new HazardEvent { WorldPosition = new Vector3(0, 0, 0), Type = HazardEventType.Stuck },
        new HazardEvent { WorldPosition = new Vector3(100, 0, 0), Type = HazardEventType.Stuck }
    };
    
    var analyzer = new HazardClusterAnalyzer();
    var clusters = analyzer.RunDBSCAN(events);
    
    Assert.Empty(clusters); // Both points are isolated noise
}

[Fact]
public void SeverityScore_WeightsDeathHigherThanStuck()
{
    var deathCluster = new HazardCluster
    {
        Events = new[] { new HazardEvent { Type = HazardEventType.Death, Timestamp = DateTime.UtcNow } }
    };
    
    var stuckCluster = new HazardCluster
    {
        Events = new[] { new HazardEvent { Type = HazardEventType.Stuck, Timestamp = DateTime.UtcNow } }
    };
    
    var deathScore = HazardAnalytics.CalculateSeverityScore(deathCluster);
    var stuckScore = HazardAnalytics.CalculateSeverityScore(stuckCluster);
    
    Assert.True(deathScore > stuckScore);
}
```

### 5.2 Integration Tests

```csharp
// CoreTests/Hazard/HazardIntegrationTests.cs

[Fact]
public async Task HazardData_PersistsAcrossRestart()
{
    // Arrange
    var dao = new LocalHazardDAO(testDataPath);
    var events = new[]
    {
        new HazardEvent { WorldPosition = new Vector3(100, 50, 0), Type = HazardEventType.Stuck }
    };
    
    // Act - Save
    await dao.SaveAsync(events);
    
    // Act - Load
    var loaded = await dao.LoadAsync();
    
    // Assert
    Assert.Single(loaded);
    Assert.Equal(100, loaded[0].WorldPosition.X);
}

[Fact]
public void PathGraph_AvoidsHighHazardAreas()
{
    // Arrange
    var hazardProvider = new MockHazardProvider();
    hazardProvider.SetHighRisk(new Vector3(50, 50, 0), severity: 100);
    
    var pathGraph = new PathGraph(/* ... */, hazardProvider);
    
    // Act - Find path through hazard area
    var pathThrough = pathGraph.Search(
        new Vector3(0, 50, 0), 
        new Vector3(100, 50, 0));
    
    // Assert - Path should deviate around hazard
    Assert.All(pathThrough.Path, point => 
        Assert.True(Vector3.Distance(point, new Vector3(50, 50, 0)) > 15));
}
```

### 5.3 Build Verification

```bash
# Build entire solution
dotnet build MasterOfPuppets.sln

# Run all tests
dotnet test

# Run specific hazard tests
dotnet test --filter "FullyQualifiedName~Hazard"
```

---

## 6. Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Clustering algorithm | DBSCAN over K-means | No predefined cluster count; handles noise |
| Decay half-life | 30 days | Balances recency vs. long-term patterns |
| Storage scope | Per-expansion JSON | Shared across characters; simple persistence |
| A* integration | Additive cost penalty | Preserves admissibility; easier tuning |
| Heat map library | Leaflet.heat | BSD license; official Leaflet plugin; simple API |
| Spatial index | Chunked grid (100 units) | O(1) lookups; matches existing `ChunkedTriangleCollection` |
| Background service | `IHostedService` + Timer | Microsoft recommended pattern; clean lifecycle |

---

## 7. File Structure

```
Core/
└── Hazard/
    ├── HazardEventType.cs           # Enum: Stuck, Death, etc.
    ├── HazardEvent.cs               # Event record with position, type, timestamp
    ├── HazardCluster.cs             # Clustered danger zone model
    ├── HazardZoneStore.cs           # In-memory store with spatial index
    ├── HazardAnalytics.cs           # Decay and severity calculations
    ├── HazardClusterAnalyzer.cs     # DBSCAN implementation
    ├── HazardEventCollector.cs      # Event subscription service
    ├── RouteRehabilitator.cs        # Success/failure feedback
    ├── LocalHazardDAO.cs            # JSON persistence
    ├── HazardServiceCollectionExtensions.cs   # DI registration
    └── HazardAnalyticsBackgroundService.cs  # Periodic clustering

SharedLib/
└── IHazardProvider.cs               # Interface for pathfinding integration

Frontend/
├── wwwroot/
│   ├── leaflet-heat/                # Leaflet.heat plugin
│   └── script/hazardHeatMap.js      # JS interop module
└── Services/
    └── HazardHeatMapService.cs      # Coordinate conversion

Json/
└── HazardData/
    └── {expansion}/
        └── hazards_{mapId}.json     # Persisted hazard events
```

---

## 8. References

### Academic Papers
1. Ester, M., H. P. Kriegel, J. Sander, and X. Xu. "A Density-Based Algorithm for Discovering Clusters in Large Spatial Databases with Noise." KDD-96, 1996.
2. Hart, P.E., Nilsson, N.J., Raphael, B. "A Formal Basis for the Heuristic Determination of Minimum Cost Paths." IEEE Transactions on Systems Science and Cybernetics, 1968.
3. Schubert, E., et al. "DBSCAN revisited, revisited: why and how you should (still) use DBSCAN." ACM TODS, 2017.

### Technical Documentation
- [scikit-learn DBSCAN](https://scikit-learn.org/stable/modules/clustering.html#dbscan)
- [Red Blob Games A* Implementation](https://www.redblobgames.com/pathfinding/a-star/implementation.html)
- [Stanford A* Theory](https://theory.stanford.edu/~amitp/GameProgramming/AStarComparison.html)
- [Microsoft Domain Events](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)
- [Microsoft Background Services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)
- [Leaflet.heat GitHub](https://github.com/Leaflet/Leaflet.heat)
- [Wikipedia Exponential Decay](https://en.wikipedia.org/wiki/Exponential_decay)

---

**Document Status:** Complete and ready for implementation approval.
