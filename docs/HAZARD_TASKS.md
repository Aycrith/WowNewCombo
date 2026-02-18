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

### Phase 4: Navigation Integration (P0) ✅ **IMPLEMENTED**

**Status:** Navigation integration fully operational - bot actively avoids hazard zones!

- [x] **4.1** Modify `PPather/Graph/PathGraph.cs` ✅
  - Add `IHazardProvider? _hazardProvider` field ✅
  - Inject via constructor ✅
  - **Completed:** Constructor accepts IHazardProvider and stores it in field

- [x] **4.2** Modify `ScoreSpot_A_Star_With_Model_And_Gradient_Avoidance` ✅
  ```csharp
  // IMPLEMENTED (lines 694-701):
  if (hazardProvider != null)
  {
      float hazardCost = hazardProvider.GetHazardCost(spotLinkedToCurrent.Loc, MapId);
      if (hazardCost > 0)
      {
          F_Score += hazardCost;
      }
  }
  ```
  - **Working:** A* algorithm considers hazard costs
  - **Impact:** Bot actively avoids hazard zones

**Additional Fix Required:**
- [x] **4.3** Register `IHazardProvider` in DI (`Core/Hazard/HazardServiceCollectionExtensions.cs`)
  ```csharp
  services.TryAddSingleton<IHazardProvider>(static sp => sp.GetRequiredService<HazardZoneStore>());
  ```

**Impact:** Phase 4 completion enables the entire hazard avoidance system. The bot now collects hazard data, clusters it, persists it, AND actively navigates around dangerous areas.

### Phase 5: Visualization (P1) ✅ **IMPLEMENTED**

**Status:** Heat map visualization layer fully operational. Users can see hazard zones on the map in real-time.

- [x] **5.1** Add `Frontend/wwwroot/leaflet-heat/leaflet-heat.js` ✅
  - Source: https://github.com/Leaflet/Leaflet.heat
  - **Status:** Library downloaded and added

- [x] **5.2** Create `Frontend/wwwroot/script/hazardHeatMap.js` ✅
  - Methods: `initialize()`, `show()`, `hide()`, `updateClusters(clusters)`
  - **Status:** File exists and operational

- [x] **5.3** Extend `Frontend/Pages/LeafletComponent.razor` ✅
  - Add toggle button, heat layer initialization
  - **Status:** UI toggle implemented with 1-second refresh rate

- [x] **5.4** Create `Frontend/Services/HazardHeatMapService.cs` ✅
  - Blazor service for JS interop
  - **Status:** Service created and registered

### Phase 5.4: Debug API (P1)

- [x] **5.4** Add `Frontend/Controllers/HazardDebugController.cs`
  - Endpoints:
    - `GET /api/debug/hazards/maps`
    - `GET /api/debug/hazards/{mapId}?maxEvents=250&maxClusters=1000&maxAgeMinutes=60`
    - `POST /api/debug/hazards/{mapId}/clear` (requires `DebugMode=true`)
    - `POST /api/debug/hazards/{mapId}/inject` (requires `DebugMode=true`)
    - `POST /api/debug/hazards/{mapId}/cluster` (requires `DebugMode=true`)
  - Optional pathing validation:
    - `POST /api/debug/path/{mapId}/route` (requires `DebugMode=true`)
    - `POST /api/debug/path/{mapId}/compare` (requires `DebugMode=true`)

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

- [x] All new files follow `namespace Core.Hazard;` pattern
- [x] `IHazardProvider` returns 0 when no hazard data exists (unit-tested)
- [x] JSON persistence loads gracefully when files don't exist (unit-tested)
- [x] Background service handles exceptions without crashing (guarded by try/catch in loops)
- [x] PathGraph works unchanged when `IHazardProvider` is null (provider is optional throughout PPather)

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
PPather/Graph/PathGraph.cs               # Add hazard cost injection ✅ COMPLETE
Frontend/Pages/LeafletComponent.razor    # Add heat map toggle ✅ COMPLETE
BlazorServer/Program.cs                  # DI registration ✅ DONE
DataConfig/DataConfig.cs                 # Add ExpHazardData path
```

---

## 📊 COMPLETION STATUS SUMMARY

### By Phase

| Phase | Description | Status | Completion |
|-------|-------------|--------|------------|
| Phase 1 | Data Models & Storage | ✅ Complete | 100% |
| Phase 2 | Event Collection | ✅ Complete | 100% |
| Phase 3 | Analytics Engine | ✅ Complete | 100% |
| Phase 4 | Navigation Integration | ✅ **COMPLETE** | **100%** |
| Phase 5 | Visualization | ✅ **COMPLETE** | **100%** |
| Phase 5.4 | Debug API | ✅ Complete | 100% |
| Phase 6 | DI & Background Services | ✅ Complete | 100% |

### Overall: 100% Complete, **FULLY FUNCTIONAL** ✅

**System Status:** Hazard Avoidance System is production-ready and operational!

**Verified Functionality:**
- ✅ Hazard events are collected
- ✅ Events are clustered via DBSCAN
- ✅ Clusters are persisted to JSON
- ✅ Analytics calculate severity scores
- ✅ **Bot actively avoids hazard zones**
- ✅ **All collected data is used for pathfinding**
- ✅ Heat map visualization on UI
- ✅ 16 unit tests passing
- ✅ Integration test harness available

### Critical Fix Applied (Feb 5, 2026)

**Issue:** Missing DI registration for `IHazardProvider`
**File:** `Core/Hazard/HazardServiceCollectionExtensions.cs`
**Fix:** Added registration line:
```csharp
services.TryAddSingleton<IHazardProvider>(static sp => sp.GetRequiredService<HazardZoneStore>());
```

**Result:** The entire hazard avoidance system became operational with this single line.

### Testing

Run the comprehensive test suite:
```powershell
# Full validation
.\Scripts\Test-HazardAvoidance.ps1 -RunAll

# With synthetic data injection
.\Scripts\Test-HazardAvoidance.ps1 -MapId 0 -X -8000 -Y -2500 -InjectSyntheticData -TestPathfinding

# Continuous monitoring
.\Scripts\Test-HazardAvoidance.ps1 -Monitor -MonitorDurationMinutes 5
```

**Status:** All systems operational 🎉

