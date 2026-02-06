# Combat Rotation Optimizer - Frontend Integration Handoff

**Date:** 2026-02-06  
**Status:** Superseded - frontend integration complete  
**Next Session Priority:** None for frontend; focus on test/documentation quality gates

> [!IMPORTANT]
> This handoff captured an earlier implementation snapshot. All frontend TODO items below were implemented in code and are now complete.
> Verified in `Frontend/Shared/MainLayout.razor` and `Frontend/Pages/CombatRotationSettings.razor`.

---

## ✅ Completed Work

### Backend (100% Complete)
- ✅ Core types: `GameStateSnapshot`, `IRoleStrategy`, `IRotationOptimizer`
- ✅ Scoring engine: `DpsRoleStrategy` with weighted-sum algorithm
- ✅ Orchestrator: `RotationOptimizer` with zero-allocation `Span<T>` sorting
- ✅ Metrics: `RotationMetricsCollector` (IHostedService) with JSON flush
- ✅ Feature flags: `CombatRotationOptimizerOptions` + hot-reload support
- ✅ Integration: `CombatGoal.Update()` dual-path (optimizer + static fallback)
- ✅ DI registration: `GoalFactory`, `BlazorServer/Program.cs`, `HeadlessServer/Program.cs`
- ✅ Unit tests: 42 tests across 6 test files (GameStateSnapshot, metrics, optimizer, backward compat, profile deserialization)
- ✅ Benchmarks: `ScoringBenchmark.cs` with allocation tracking
- ✅ Sample profiles: Warrior_40, Rogue_20, Mage_54 annotated with `Weight` and `ScoreConditions`

### Frontend (Complete)
- ✅ `Frontend/Services/CombatRotationAdminService.cs` — persists to `runtime_feature_flags.json`
- ✅ `Frontend/Pages/CombatRotationSettings.razor` — settings form + live metrics + auto-refresh + error handling + metrics viewer
- ✅ Registered in `Frontend/DependencyInjection.cs`
- ✅ `BlazorServer/runtime_feature_flags.json` — feature flag section added
- ✅ Navigation menu entry in `Frontend/Shared/MainLayout.razor`

---

## ✅ Frontend Work Status (Historical TODOs Resolved)

### 1. Navigation Menu Entry
**File:** `Frontend/Shared/MainLayout.razor`  
**Location:** Around line 158 (after Humanization entry)

```csharp
new() { Id = "14a", Href = "/humanization", Text = "Humanization"},
new() { Id = "14b", Href = "/combat-rotation", Text = "Combat Rotation"},  // ADD THIS
new() { Id = "20", Href = "/RestartServer", Text = "Restart"},
```

**Verification:** Navigate to BlazorServer UI, confirm "Combat Rotation" appears in left nav menu

---

### 2. Live Metrics Dashboard
**Current State:** `CombatRotationSettings.razor` has basic toggles but NO live metrics display  
**Missing:** Real-time ability usage stats, optimization rate, session metrics

#### Required Injection
```csharp
@inject RotationMetricsCollector metricsCollector
```

#### Add Status Section (Before Configuration Card)
```razor
<Card class="mt-2">
    <CardHeader>Live Metrics</CardHeader>
    <CardBody>
        <dl class="row mb-0">
            <dt class="col-sm-4">Total Ticks</dt>
            <dd class="col-sm-8">@metricsCollector.CurrentSession.TotalTicks</dd>

            <dt class="col-sm-4">Optimized Ticks</dt>
            <dd class="col-sm-8">
                @metricsCollector.CurrentSession.OptimizedTicks 
                (@(MetricsOptimizationRate.ToString("F1"))%)
            </dd>

            <dt class="col-sm-4">Fallback Ticks</dt>
            <dd class="col-sm-8">@metricsCollector.CurrentSession.FallbackTicks</dd>

            <dt class="col-sm-4">Session Duration</dt>
            <dd class="col-sm-8">@SessionDuration.ToString(@"hh\:mm\:ss")</dd>
        </dl>

        <h6 class="mt-3">Top Abilities (by usage)</h6>
        <table class="table table-sm table-striped">
            <thead>
                <tr>
                    <th>Ability</th>
                    <th>Attempts</th>
                    <th>Success Rate</th>
                    <th>Avg Score</th>
                </tr>
            </thead>
            <tbody>
                @foreach (var stat in TopAbilities)
                {
                    <tr>
                        <td>@stat.Name</td>
                        <td>@stat.AttemptCount</td>
                        <td>@(stat.SuccessRate.ToString("P0"))</td>
                        <td>@stat.AverageScore.ToString("F1")</td>
                    </tr>
                }
            </tbody>
        </table>
    </CardBody>
</Card>
```

#### Add Code-Behind (Bottom of .razor file)
```csharp
@code {
    private IEnumerable<AbilityUsageStat> TopAbilities => 
        metricsCollector.CurrentSession.GetOrderedStats().Take(10);

    private float MetricsOptimizationRate
    {
        get
        {
            int total = metricsCollector.CurrentSession.TotalTicks;
            if (total == 0) return 0f;
            return (float)metricsCollector.CurrentSession.OptimizedTicks / total * 100f;
        }
    }

    private TimeSpan SessionDuration
    {
        get
        {
            long start = metricsCollector.CurrentSession.SessionStartTicks;
            long end = metricsCollector.CurrentSession.SessionEndTicks;
            if (end == 0) end = Environment.TickCount64;
            return TimeSpan.FromMilliseconds(end - start);
        }
    }
}
```

**Missing `using`:**
```csharp
@using Core.CombatRotation
```

---

### 3. Auto-Refresh for Live Metrics
**Problem:** Metrics don't update in real-time, page requires manual refresh

**Solution:** Add `StateHasChanged()` polling or SignalR push

#### Option A: Timer-Based Polling (Simpler)
```csharp
@code {
    private System.Timers.Timer? refreshTimer;

    protected override void OnInitialized()
    {
        LoadFromCurrent();
        
        // Refresh metrics every 1 second
        refreshTimer = new System.Timers.Timer(1000);
        refreshTimer.Elapsed += async (sender, e) => await InvokeAsync(StateHasChanged);
        refreshTimer.Start();
    }

    public void Dispose()
    {
        refreshTimer?.Dispose();
    }
}
```

Add to top of file:
```csharp
@implements IDisposable
```

#### Option B: SignalR Push (More Scalable)
**Skip for MVP** — requires creating a dedicated hub. Consider for future if performance becomes an issue.

---

### 4. Error Handling in Admin Service
**Current State:** `CombatRotationAdminService.Save()` has no user-facing error handling  
**Missing:** Try/catch with user notification on save failure

#### Modify CombatRotationSettings.razor `Save()` method:
```csharp
private string? saveError;

private void Save()
{
    try
    {
        CombatRotationOptimizerOptions options = new()
        {
            Enabled = enabled,
            FallbackToStaticPriority = fallbackToStaticPriority,
            BaseWeightMultiplier = baseWeightMultiplier,
            EnableMetrics = enableMetrics,
            EnableResourceForecasting = enableResourceForecasting,
            EnableSwingTimerAlignment = enableSwingTimerAlignment,
            MetricsFlushIntervalSeconds = metricsFlushIntervalSeconds,
            MetricsOutputPath = metricsOutputPath
        };

        combatRotationAdmin.Save(options);
        showSaved = true;
        saveError = null;
    }
    catch (Exception ex)
    {
        saveError = $"Failed to save: {ex.Message}";
        showSaved = false;
    }
}
```

#### Add error display (after save button):
```razor
@if (!string.IsNullOrEmpty(saveError))
{
    <div class="alert alert-danger mt-2 small py-1 px-2">@saveError</div>
}

@if (showSaved)
{
    <div class="alert alert-success mt-2 small py-1 px-2">Settings saved.</div>
}
```

---

### 5. Metrics File Viewer
**Enhancement:** Add a "View Metrics File" button that displays the JSON content

```razor
<button class="btn btn-sm btn-outline-secondary" @onclick="ViewMetricsFile">
    View Metrics File
</button>

@if (!string.IsNullOrEmpty(metricsFileContent))
{
    <Card class="mt-2">
        <CardHeader>Metrics JSON</CardHeader>
        <CardBody>
            <pre class="small">@metricsFileContent</pre>
        </CardBody>
    </Card>
}
```

```csharp
@code {
    private string metricsFileContent = string.Empty;

    private void ViewMetricsFile()
    {
        try
        {
            string path = featureFlags.Current.CombatRotationOptimizer.MetricsOutputPath;
            if (System.IO.File.Exists(path))
            {
                metricsFileContent = System.IO.File.ReadAllText(path);
            }
            else
            {
                metricsFileContent = "Metrics file not found. Start a combat session to generate metrics.";
            }
        }
        catch (Exception ex)
        {
            metricsFileContent = $"Error reading metrics file: {ex.Message}";
        }
    }
}
```

---

## 📋 Testing Checklist

### Manual Testing Steps
1. **Launch BlazorServer:**
   ```powershell
   dotnet run --project BlazorServer
   ```
   
2. **Navigate to Combat Rotation page:**
   - Open browser to `http://localhost:5000`
   - Click "Combat Rotation" in left nav menu
   - Verify page loads without errors

3. **Test Feature Toggle:**
   - Toggle "Enable Combat Rotation Optimizer" to ON
   - Click "Save"
   - Verify success message appears
   - Open `BlazorServer/runtime_feature_flags.json`
   - Confirm `"CombatRotationOptimizer": { "Enabled": true }`

4. **Test Hot-Reload:**
   - In UI, enable optimizer
   - Start a bot session (if possible in test environment)
   - While bot is running, disable optimizer in UI
   - Verify `CombatGoal` immediately falls back to static priority

5. **Test Live Metrics:**
   - Start a combat session
   - Navigate to `/combat-rotation`
   - Verify metrics update every 1 second:
     - Total Ticks increases
     - Optimized Ticks increases
     - Ability usage table populates
   - Disable optimizer mid-combat
   - Verify Fallback Ticks increases

6. **Test Backward Compatibility:**
   - Load a profile WITHOUT `Weight`/`ScoreConditions` (e.g., `Warrior_10.json`)
   - Verify bot uses static priority (original ordering)
   - Load a profile WITH annotations (e.g., `Warrior_40.json`)
   - Verify Execute is prioritized when target < 20% HP

---

## 📁 Files to Modify

| File | Action | Status |
|------|--------|--------|
| `Frontend/Shared/MainLayout.razor` | Add nav menu entry | ✅ Completed |
| `Frontend/Pages/CombatRotationSettings.razor` | Add live metrics dashboard | ✅ Completed |
| `Frontend/Pages/CombatRotationSettings.razor` | Add auto-refresh timer | ✅ Completed |
| `Frontend/Pages/CombatRotationSettings.razor` | Add error handling | ✅ Completed |
| `Frontend/Pages/CombatRotationSettings.razor` | Add metrics file viewer | ✅ Completed |

---

## 🎯 Success Criteria

- [x] "Combat Rotation" appears in navigation menu
- [x] Page loads at `/combat-rotation` without errors
- [x] Live metrics update every 1 second during combat
- [x] Top 10 abilities by usage displayed in table
- [x] Save button persists changes to `runtime_feature_flags.json`
- [x] Success/error messages display after save attempt
- [x] Metrics file viewer displays JSON content
- [x] Feature flag toggle works (enable/disable mid-combat)
- [x] Backward compatibility verified (profiles without annotations work)

---

## 🚀 Estimated Effort

No remaining frontend engineering work required for this handoff scope. Keep this document for historical context only.

---

## 📚 Reference Files

### Documentation
- `docs/PRD_COMBAT_ROTATION_OPTIMIZER.md` — Full PRD with architecture
- `docs/COMBAT_ROTATION_TASKS.md` — Task breakdown (now marked complete)

### Key Implementation Files
- `Core/CombatRotation/RotationMetricsCollector.cs` — Singleton metrics service
- `Core/CombatRotation/RotationMetrics.cs` — `AbilityUsageStat`, `RotationSessionMetrics`
- `Core/CombatRotation/GameStateSnapshot.cs` — Combat state snapshot
- `Frontend/Services/CombatRotationAdminService.cs` — JSON persistence
- `Frontend/Pages/HumanizationSettings.razor` — Reference pattern for live metrics

### Test Files
- `CoreUnitTests/CombatRotation/` — 42 passing tests

---

## 🔧 Build & Run Commands

```powershell
# Build solution
dotnet build MasterOfPuppets.sln

# Run unit tests
dotnet test CoreUnitTests/CoreUnitTests.csproj --filter "FullyQualifiedName~CombatRotation"

# Run benchmarks
dotnet run --project Benchmarks -c Release -- --filter "*ScoringBenchmark*"

# Launch BlazorServer
dotnet run --project BlazorServer
```

---

## ⚠️ Known Issues / Notes

1. **No SignalR Hub:** Using timer-based polling instead of push notifications for MVP
2. **Metrics Collection:** Requires an active combat session to populate data
3. **Profile Variable Substitution:** Some profiles use string variables for int fields (e.g., `"MEND_PET_COOLDOWN"`). Deserialization tests handle this with error suppression.
4. **Session Reset:** `RotationMetricsCollector` doesn't have a manual reset method yet (metrics reset on app restart)

---

## 💡 Future Enhancements (Post-MVP)

- [ ] SignalR hub for push-based metrics updates
- [ ] Metrics reset button (clear current session)
- [ ] Export metrics to CSV
- [ ] Ability score heatmap visualization
- [ ] Tank/Healer role strategies (currently DPS-only)
- [ ] AoE rotation optimization (Phase 2)
- [ ] Profile editor with Weight/ScoreConditions UI
- [ ] A/B testing framework (compare optimizer vs static)
