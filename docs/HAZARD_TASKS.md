# Hazard Detection System - Implementation Tasks

**Quick Reference for Developers**

## Build Commands
```bash
dotnet build MasterOfPuppets.sln
dotnet test --filter "FullyQualifiedName~Hazard"
```

---

## Task Checklist

### Phase 1: Data Models (P0)

- [x] **1.1** Create `Core/Hazard/HazardEventType.cs`
  ```csharp
  public enum HazardEventType { Stuck, Death, MultiMobAggro, PathingFailure, Evade, OscillationLoop }
  ```

- [x] **1.2** Create `Core/Hazard/HazardEvent.cs`
  - Record with: `Vector3 WorldPosition`, `float MapId`, `DateTime Timestamp`, `HazardEventType Type`

- [x] **1.3** Create `Core/Hazard/HazardCluster.cs`
  - Model: `Vector3 Centroid`, `float Radius`, `double SeverityScore`, `List<HazardEvent> Events`

- [x] **1.4** Create `Core/Hazard/HazardZoneStore.cs`
  - Spatial index: `Dictionary<(int,int), List<HazardCluster>>`
  - Method: `float GetHazardCost(Vector3 position, float mapId)`

- [x] **1.5** Create `Core/Hazard/LocalHazardDAO.cs`
  - Path: `Json/HazardData/{expansion}/hazards_{mapId}.json`
  - Follow `LocalGrindSessionDAO.cs` pattern

### Phase 2: Event Collection (P0)

- [x] **2.1** Create `SharedLib/IHazardProvider.cs` (moved to SharedLib to avoid Core↔PPather circular reference)
  ```csharp
  public interface IHazardProvider { float GetHazardCost(Vector3 position, float mapId); }
  ```

- [x] **2.2** Create `Core/Hazard/HazardEventCollector.cs`
  - Subscribe to: `StuckDetector.OnStuckDetected`, `CombatLog.PlayerDeath`, `CombatLog.TargetEvade`

- [x] **2.3** Modify `Core/GoalsComponent/StuckDetector.cs`
  - Add `MapId`, `UIMapId`, `Zone` to `StuckEventData`

### Phase 3: Analytics (P0)

- [x] **3.1** Create `Core/Hazard/HazardAnalytics.cs`
  - Temporal decay: `λ = ln(2) / 30` (30-day half-life)
  - Severity weights: Death=10, Stuck=5, MultiMob=4, PathFail=3, Evade=2, Oscillation=1

- [x] **3.2** Create `Core/Hazard/HazardClusterAnalyzer.cs`
  - DBSCAN: `ε=15 world units`, `minPts=2`
  - Methods: `RunDBSCAN()`, `RangeQuery()`, `ExpandCluster()`

- [x] **3.3** Create `Core/Hazard/RouteRehabilitator.cs`
  - Reduce cluster severity on successful traversal

### Phase 4: Navigation Integration (P0)

- [x] **4.1** Modify `PPather/Graph/PathGraph.cs`
  - Add `IHazardProvider? _hazardProvider` field
  - Inject via constructor

- [x] **4.2** Modify `ScoreSpot_A_Star_With_Model_And_Gradient_Avoidance`
  ```csharp
  float hazardPenalty = _hazardProvider?.GetHazardCost(spot.Loc, MapId) ?? 0f;
  float F_Score = baseScore + (hazardPenalty * HazardCostMultiplier);
  ```

### Phase 5: Visualization (P1)

- [x] **5.1** Add `Frontend/wwwroot/leaflet-heat/leaflet-heat.js`
  - Source: https://github.com/Leaflet/Leaflet.heat

- [x] **5.2** Create `Frontend/wwwroot/script/hazardHeatMap.js`
  - Methods: `init()`, `show()`, `hide()`, `updateData(points)`

- [x] **5.3** Extend `Frontend/Pages/LeafletComponent.razor`
  - Add toggle button, heat layer initialization

### Phase 6: DI & Services (P0)

- [x] **6.1** Create `Core/Hazard/HazardServiceCollectionExtensions.cs`
  ```csharp
  public static IServiceCollection AddHazardAvoidance(this IServiceCollection services)
  ```

- [x] **6.2** Create `Core/Hazard/HazardAnalyticsBackgroundService.cs`
  - Clustering interval: 60 seconds
  - Save interval: 5 minutes

- [x] **6.3** Modify `BlazorServer/Program.cs`
  - Add: `services.AddHazardAvoidance();`

---

## Key Patterns

### DBSCAN Parameters
```
ε (epsilon) = 15 world units
minPts = 2 events minimum to form cluster
```

### A* Cost Formula
```
F(n) = G(n) + H(n) + HazardCost(n)
```

### Temporal Decay
```csharp
double lambda = Math.Log(2) / 30.0; // 30-day half-life
double weight = Math.Exp(-lambda * daysSinceEvent);
```

### Spatial Chunk Key
```csharp
const float ChunkSize = 100f;
(int, int) key = ((int)(pos.X / ChunkSize), (int)(pos.Y / ChunkSize));
```

---

## Verification Checklist

- [ ] All new files follow `namespace Core.Hazard;` pattern
- [ ] `IHazardProvider` returns 0 when no hazard data exists
- [ ] JSON persistence loads gracefully when files don't exist
- [ ] Background service handles exceptions without crashing
- [ ] PathGraph works unchanged when `IHazardProvider` is null

---

## Files to Create

```
Core/Hazard/
├── HazardEventType.cs
├── HazardEvent.cs
├── HazardCluster.cs
├── IHazardProvider.cs
├── HazardZoneStore.cs
├── HazardAnalytics.cs
├── HazardClusterAnalyzer.cs
├── HazardEventCollector.cs
├── RouteRehabilitator.cs
├── LocalHazardDAO.cs
├── HazardServiceExtensions.cs
└── HazardAnalyticsBackgroundService.cs
```

## Files to Modify

```
Core/GoalsComponent/StuckDetector.cs     # Extend StuckEventData
PPather/Graph/PathGraph.cs               # Add hazard cost injection
Frontend/Pages/LeafletComponent.razor    # Add heat map toggle
BlazorServer/Program.cs                  # DI registration
DataConfig/DataConfig.cs                 # Add ExpHazardData path
```
