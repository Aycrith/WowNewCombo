# Bot Capability Improvements - Implementation Summary

## Overview
This implementation adds comprehensive failure detection, prevention, and recovery systems to reduce bot failures caused by terrain issues, NO PLAN states, and multi-mob encounters.

## 🚀 New Features

### 1. Smart Blacklist System (`Core/GoalsComponent/Blacklist/`)
**Problem:** Original blacklist was binary and didn't persist
**Solution:** Tiered temporal blacklisting with disk persistence

**Features:**
- **Three severity tiers:**
  - Temporary (5 min): Evade, tagged mobs
  - Medium (30 min): Player deaths
  - Permanent: Config-based
- **Disk persistence:** Auto-saves every 5 minutes to `%APPDATA%/WowClassicGrindBot/smart_blacklist.json`
- **Auto-expiration:** Expired entries automatically pruned
- **LRU eviction:** Oldest entries removed when max capacity reached

**Integration:** Automatically blacklists evade mobs and failed pulls

### 2. Predictive Stuck Detection (`Core/GoalsComponent/BreadcrumbTracker.cs`)
**Problem:** Original stuck detection waited until already stuck (2s+ delay)
**Solution:** Predictive analysis using movement patterns

**New Methods:**
- `CalculateVelocity()` - Speed and direction analysis
- `IsApproachingObstacle()` - Detects deceleration patterns
- `CalculateTerrainComplexity()` - Z-axis variance scoring (0-100)
- `CalculateStuckRisk()` - Composite risk score (0-100)
- `IsInNarrowCorridor()` - Corridor detection for pathfinding
- `GetAverageHeading()` - Direction consistency check

**Usage:** Can be queried before approaching to adjust path preemptively

### 3. NO PLAN Recovery Service (`Core/Recovery/NoPlanRecoveryService.cs`)
**Problem:** LLM integration unfinished, bot stuck when GOAP fails
**Solution:** Rule-based progressive recovery

**Recovery Strategies (escalating):**
1. **ClearTarget** (2nd failure): Clears current target
2. **ResetState** (4th failure): Triggers ResumeEvent to reset goals
3. **ForceReplan** (6th failure): Abort + Resume sequence
4. **EmergencyReset** (6+ failures): Full session reset

**Integration:** Automatically triggered on AbortEvent from GOAP

### 4. Multi-Mob Detection (`Core/Goals/ApproachTargetGoal.cs`)
**Problem:** Bot walks into melee range of multiple aggro mobs
**Solution:** Proximity-based threat detection

**Features:**
- Counts nearby hostiles using `CombatLog.DamageTaken`
- Detects `ToPull` count (mobs about to aggro)
- **Early retreat:** Aborts approach if >1 mob detected
- Automatic target clearing and turn away
- Logs threat for analytics

**Threshold:** Retreats when `mobCount > 1` and range < 15 yards

### 5. Failure Analytics (`Core/Analytics/FailureAnalytics.cs`)
**Problem:** No visibility into failure patterns
**Solution:** Comprehensive tracking with hotspot detection

**Tracked Events:**
- Stuck events (from StuckDetector)
- Deaths
- Failed pulls
- NO PLAN states
- Multi-mob retreats

**Features:**
- **Hot zone detection:** Identifies geographic failure clusters
- **Persistence:** Saves to `%APPDATA%/WowClassicGrindBot/failure_analytics.json`
- **30-day retention:** Auto-prunes old events
- **Session statistics:** Real-time failure counts by type

## ⚙️ Configuration

Add to `BlazorServer/runtime_feature_flags.json` or `HeadlessServer/runtime_feature_flags.json`:

```json
{
  "Features": {
    "SmartBlacklist": {
      "Enabled": true,
      "MaxEntries": 1000,
      "AutoSaveIntervalMinutes": 5
    },
    "StuckSensitivity": {
      "Enabled": true,
      "MinDistance": 0.1,
      "PredictiveRiskThreshold": 70
    },
    "NoPlanRecovery": {
      "Enabled": true,
      "ResetStateThreshold": 2,
      "EmergencyResetThreshold": 6
    },
    "FailureAnalytics": {
      "Enabled": true,
      "FlushIntervalMinutes": 5,
      "RetentionDays": 30
    }
  }
}
```

## 📊 Expected Improvements

### Before
- ❌ Stuck for 2+ seconds before detection
- ❌ No persistence for blacklists
- ❌ Bot stuck on NO PLAN indefinitely
- ❌ Walks blindly into multi-mob encounters
- ❌ No failure visibility

### After
- ✅ Predictive detection (risk score 0-100)
- ✅ Persistent smart blacklists with TTL
- ✅ Progressive NO PLAN recovery (4 strategies)
- ✅ Multi-mob early retreat
- ✅ Hot zone identification

## 🔄 Integration Flow

### Scenario 1: Getting Stuck on Terrain
1. BreadcrumbTracker detects velocity decrease
2. CalculateStuckRisk() returns score > 70
3. ApproachTargetGoal preemptively adjusts path
4. If still stuck, StuckDetector escalates through 6 states
5. FailureAnalytics records location for hot zone detection

### Scenario 2: NO PLAN State
1. GoapAgent logs "NO PLAN" (EventId 0053)
2. NoPlanRecoveryService receives AbortEvent
3. Consecutive failure counter increments
4. Progressive recovery strategy executed
5. If emergency threshold reached, full reset triggered

### Scenario 3: Multi-Mob Encounter
1. ApproachTargetGoal.Update() checks mob count
2. DetectMultiMobThreat() counts DamageTaken + ToPull
3. If count > 1, triggers HandleMultiMobThreat()
4. Target cleared, bot turns away
5. FailureAnalytics logs retreat event

## 🔧 Files Modified

### New Files:
- `Core/GoalsComponent/Blacklist/SmartBlacklistEntry.cs`
- `Core/GoalsComponent/Blacklist/SmartBlacklist.cs`
- `Core/Recovery/NoPlanRecoveryService.cs`
- `Core/Analytics/FailureAnalytics.cs`

### Enhanced Files:
- `Core/GoalsComponent/BreadcrumbTracker.cs` - Added predictive methods
- `Core/Goals/ApproachTargetGoal.cs` - Added multi-mob detection
- `Core/FeatureFlags/FeatureFlagsOptions.cs` - Added new options classes
- `Core/GoalsFactory/GoalFactory.cs` - Registered new services

## 🧪 Testing Recommendations

1. **Stuck Detection:** Force bot into corners/water - verify predictive alerts
2. **Blacklist:** Kill evade mob, verify 5-min blacklist then expiration
3. **NO PLAN:** Corrupt path file, verify progressive recovery
4. **Multi-Mob:** Approach 2+ mobs, verify early retreat
5. **Analytics:** Check `%APPDATA%/WowClassicGrindBot/` for JSON files

## 📈 Monitoring

Watch for these log patterns:
- `[SmartBlacklist]` - Blacklist operations
- `[BreadcrumbTracker]` - Risk scores > 50
- `[NoPlanRecovery]` - Recovery strategy execution
- `[ApproachTarget]` - Multi-mob retreats
- `[FailureAnalytics]` - Periodic flushes

## 🎯 Next Steps

1. **Tune thresholds** based on initial usage
2. **Add UI integration** for hot zone visualization
3. **Implement route rehabilitation** using hot zone data
4. **Add machine learning** for failure prediction (future)
