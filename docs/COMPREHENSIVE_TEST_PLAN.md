# Comprehensive Test Plan - WowClassicGrindBot

**Date**: February 3, 2026  
**Fixes Applied**:
- Added bounds checking to `ActionBarCooldownReader.cs`
- Added bounds checking to `ActionBarCostReader.cs`
- Added comprehensive logging for debugging

---

## Root Cause Analysis

### The Bug
**Symptom**: BlazorServer.exe crashed immediately on bot start with `IndexOutOfRangeException`

**Location**: `Core/Actionbar/ActionBarCooldownReader.cs:46`

**Root Cause**:
1. WoW addon sends cooldown data encoded as: `slot * 100000 + duration`
2. C# code decodes: `slotIdx = (value / 100000) - 1`
3. Array size: `5 * 24 = 120` elements (indices 0-119)
4. WoW has action bar slots 1-120
5. **Missing bounds validation** before array access
6. If addon sends invalid slot or unexpected data → crash

**Similar Issue Found**: `ActionBarCostReader.cs` had identical vulnerability

### The Fix
```csharp
// Before array access
if (slotIdx < 0 || slotIdx >= data.Length)
{
    LogInvalidSlotIndex(logger, slotIdx, value, data.Length);
    return;  // Skip invalid updates instead of crashing
}
```

---

## Test Categories

### 1. ✅ Critical Path - Bot Startup
**Goal**: Verify bot starts without crashing

**Prerequisites**:
- WoW Classic running with character logged in
- DataToColor addon installed and loaded
- Frame detection working (324 frames)

**Test Steps**:
1. Start BlazorServer
2. Navigate to http://localhost:5000
3. Load Blood Elf Rogue profile
4. Click "Start Bot"
5. **Expected**: Bot starts successfully, no crash
6. **Expected**: Logs show no "Invalid slot index" warnings

**Pass Criteria**:
- ✅ Server stays running
- ✅ No crash in first 60 seconds
- ✅ GOAP planner starts
- ✅ Bot enters idle or active goal

---

### 2. 🔍 Frame Detection Validation
**Goal**: Verify all 324 frames are detected correctly

**API Endpoint**: `GET http://localhost:5000/api/test/frames`

**Expected Response**:
```json
{
  "totalFrames": 324,
  "detectedFrames": 324,
  "missingFrames": [],
  "status": "OK"
}
```

**Pass Criteria**:
- ✅ All 324 frames detected
- ✅ No missing frames
- ✅ Frame positions stable across multiple reads

---

### 3. 📊 Addon Communication Test
**Goal**: Verify data flows correctly from addon to bot

**Test Sequence**:
```
1. GET /api/test/status → Verify WoW process detected
2. GET /api/test/snapshot → Verify player data reading
3. Monitor logs for "Invalid slot index" warnings
```

**Player Snapshot Validation**:
- ✅ Player name matches character
- ✅ Level = 2
- ✅ Class = "Rogue"
- ✅ Zone = "Eversong Woods"
- ✅ Health > 0
- ✅ Mana/Energy > 0

---

### 4. 🎮 Input Simulation Tests
**Goal**: Verify bot can control WoW

**API Tests**:
```
POST /api/test/input/jump       → Character jumps
POST /api/test/input/forward    → Character moves forward
POST /api/test/movement/stop    → Character stops
```

**Pass Criteria**:
- ✅ Character responds to input
- ✅ No input lag > 100ms
- ✅ Input queue doesn't overflow

---

### 5. ⚔️ Combat System Test
**Goal**: Verify targeting and combat abilities

**Prerequisites**:
- Character near level-appropriate enemy
- Sinister Strike on action bar (slot 1)

**Test Steps**:
1. POST /api/test/combat/target → Press Tab
2. Verify target acquired
3. Start bot with combat goal
4. Observe combat sequence:
   - ✅ Approach to melee range
   - ✅ Auto-attack starts
   - ✅ Sinister Strike casts
   - ✅ Energy management working
   - ✅ Loot after kill

---

### 6. 🔧 Action Bar Readers Stress Test
**Goal**: Verify bounds checking prevents crashes

**Test Method**:
1. Fill all 120 action bar slots with abilities
2. Start bot
3. Monitor logs for warnings
4. Change action bars rapidly
5. Remove/add abilities while bot running

**Pass Criteria**:
- ✅ No crashes
- ✅ Warnings logged for invalid slots (if any)
- ✅ Bot adapts to action bar changes

---

### 7. 📝 Logging Validation
**Goal**: Verify debug logging works correctly

**Check Logs For**:
- ✅ ActionBarCooldownReader logging enabled (DEBUG mode)
- ✅ Invalid slot warnings clearly formatted
- ✅ Cooldown updates traced (if DEBUG = true)
- ✅ No sensitive data in logs

**Example Expected Log**:
```
[Trace] ActionBarCooldownReader: Slot 1 cooldown 6.0s
[Warning] ActionBarCooldownReader: Invalid slot index 150 from value 15000123 (array length: 120). Skipping update.
```

---

### 8. 🏃 Extended Runtime Test
**Goal**: Verify stability over time

**Duration**: 30 minutes

**Monitoring**:
- Memory usage (should be stable)
- CPU usage (should be < 20% average)
- No frame detection degradation
- No exception spam in logs

**Pass Criteria**:
- ✅ Bot runs continuously for 30 min
- ✅ No crashes or restarts needed
- ✅ Memory usage < 500 MB
- ✅ Frame read rate > 10 FPS

---

### 9. 🛡️ Edge Case Testing

#### Test Case 9a: Empty Action Bars
- Remove all abilities from action bars
- Start bot
- **Expected**: No crash, bot handles gracefully

#### Test Case 9b: Addon Not Loaded
- Disable DataToColor addon
- Start bot
- **Expected**: Clear error message, graceful degradation

#### Test Case 9c: WoW Not Running
- Close WoW
- Start bot
- **Expected**: Error shown, retry logic works

#### Test Case 9d: Rapid Bot Start/Stop
- Start/stop bot 10 times rapidly
- **Expected**: No deadlocks, clean state transitions

---

## Automated Test Script

Run: `.\test-bot-startup.ps1`

See below for PowerShell test automation script.

---

## Known Limitations

1. **Action Bar Slots**: Bot expects slots 1-120. If WoW expands this in future, array size must increase.
2. **Frame Detection**: Requires DXGI screen capture (may not work in VM/RDP).
3. **Navigation Server**: Currently disabled. Tests use PPather local mode only.

---

## Regression Test Checklist

Before deploying to production:
- [ ] Run all 9 test categories
- [ ] Check Event Viewer for application crashes
- [ ] Verify no new warnings in logs
- [ ] Test with fresh character (level 1)
- [ ] Test with high-level character (60+)
- [ ] Test with different classes (Warrior, Mage, Priest)

---

## Success Criteria Summary

**Critical**:
- ✅ Bot starts without crash
- ✅ Runs for 30+ minutes stable
- ✅ Combat system functional

**Important**:
- ✅ All 324 frames detected
- ✅ No bounds exceptions
- ✅ Action bar changes handled

**Nice to Have**:
- ✅ Debug logging helpful
- ✅ Performance optimized
- ✅ Memory usage low

---

## Test Report Template

```
## Test Run Report
**Date**: _____
**Tester**: _____
**WoW Version**: _____
**Character**: Level ___ _____ (Class)

### Results
- [ ] Test 1: Bot Startup - PASS / FAIL
- [ ] Test 2: Frame Detection - PASS / FAIL
- [ ] Test 3: Addon Communication - PASS / FAIL
- [ ] Test 4: Input Simulation - PASS / FAIL
- [ ] Test 5: Combat System - PASS / FAIL
- [ ] Test 6: Action Bar Stress - PASS / FAIL
- [ ] Test 7: Logging - PASS / FAIL
- [ ] Test 8: Extended Runtime - PASS / FAIL
- [ ] Test 9: Edge Cases - PASS / FAIL

### Issues Found
1. _____
2. _____

### Logs Attached
- BlazorServer logs: _____
- Event Viewer errors: _____
```
