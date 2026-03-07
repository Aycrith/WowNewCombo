# Diagnostic & Troubleshooting Guide

## Overview

This guide provides comprehensive diagnostic tools and workflows for troubleshooting bot issues.

## 🔍 New Diagnostic Endpoints

### Frame Slot Monitoring

#### Read Single Slot Value
```bash
GET /api/Diagnostics/slot/{slotNumber}
```

**Example:**
```bash
curl http://localhost:5000/api/Diagnostics/slot/106
```

**Response:**
```json
{
  "slot": 106,
  "value": 0,
  "hex": "0x00000000",
  "timestamp": "2026-02-03T21:45:00Z"
}
```

**Use Case:** Check raw value of any addon frame slot (0-323)

---

#### Read Slot Range
```bash
GET /api/Diagnostics/slots/range?start=0&end=10
```

**Example:**
```bash
curl "http://localhost:5000/api/Diagnostics/slots/range?start=100&end=110"
```

**Response:**
```json
{
  "start": 100,
  "end": 110,
  "count": 11,
  "slots": [
    {"slot": 100, "value": 12345, "hex": "0x00003039"},
    {"slot": 101, "value": 0, "hex": "0x00000000"},
    ...
  ],
  "timestamp": "2026-02-03T21:45:00Z"
}
```

**Use Case:** Inspect multiple slots at once (max 50 slots per request)

---

#### Real-Time Slot 106 Monitoring
```bash
GET /api/Diagnostics/monitor/slot106?duration=5
```

**Example:**
```bash
curl "http://localhost:5000/api/Diagnostics/monitor/slot106?duration=10"
```

**Response:**
```json
{
  "durationSeconds": 10,
  "totalReadings": 5,
  "nonZeroCount": 2,
  "values": [
    {"value": 0, "hex": "0x00000000", "count": 234, "timestamp": "16:45:01.123"},
    {"value": 12345678, "hex": "0x00BC614E", "count": 3, "timestamp": "16:45:02.456"},
    {"value": 0, "hex": "0x00000000", "count": 142, "timestamp": "16:45:03.789"}
  ],
  "startTime": "16:45:00.000",
  "endTime": "16:45:10.123"
}
```

**Use Case:** Monitor keybinding queue for activity after triggering `/dcbindings` command

---

### Keybinding Statistics

#### Get Read Statistics
```bash
GET /api/Diagnostics/keybindings/stats
```

**Example:**
```bash
curl http://localhost:5000/api/Diagnostics/keybindings/stats
```

**Response:**
```json
{
  "totalReads": 5432,
  "nonZeroReads": 0,
  "consecutiveZeros": 5432,
  "isInitialized": false,
  "bindingCount": 0,
  "percentageNonZero": 0.0,
  "timestamp": "2026-02-03T21:45:00Z"
}
```

**Use Case:** See if slot 106 has EVER returned non-zero values since bot started

---

### Bot State

#### Get Current Bot State
```bash
GET /api/Diagnostics/bot/state
```

**Example:**
```bash
curl http://localhost:5000/api/Diagnostics/bot/state
```

**Response:**
```json
{
  "botActive": true,
  "currentGoal": "PullTargetGoal",
  "goalStackDepth": 3,
  "goalStack": ["PullTargetGoal", "CombatGoal", "GrindGoal"],
  "profile": {
    "fileName": "BloodElf_Rogue_L1-5.json",
    "mode": "Grind",
    "pathCount": 1
  },
  "system": {
    "avgScreenLatency": 2.33,
    "avgNPCLatency": 0.0,
    "keybindingsInitialized": false,
    "actionBarInitialized": true
  },
  "timestamp": "2026-02-03T21:45:00Z"
}
```

**Use Case:** Quick overview of bot health and current activity

---

## 🐛 Troubleshooting Workflows

### Issue: Keybindings Not Initializing

**Symptoms:**
- `totalBindings: 0`
- `isInitialized: false`
- Logs show "Waiting for bindings - current count=0"

**Diagnostic Steps:**

1. **Check if frames are configured:**
   ```bash
   curl http://localhost:5000/api/Test/frames
   ```
   ✅ Should show 324 frames with valid data

2. **Check keybinding statistics:**
   ```bash
   curl http://localhost:5000/api/Diagnostics/keybindings/stats
   ```
   📊 Look at `nonZeroReads` - should be > 0 if addon is sending data

3. **Monitor slot 106 live:**
   ```bash
   # In terminal 1: Start monitoring
   curl "http://localhost:5000/api/Diagnostics/monitor/slot106?duration=10"
   
   # In terminal 2: Trigger binding refresh
   curl -X POST http://localhost:5000/api/Diagnostics/fix/bindings
   ```
   🎯 You should see non-zero values appear within the 10-second window

4. **Check consecutive zero reads:**
   - If `consecutiveZeros` is very high (>1000) and `nonZeroReads: 0`, the addon queue may be expiring too quickly

**Possible Causes:**

| Symptom | Cause | Solution |
|---------|-------|----------|
| `nonZeroReads: 0` consistently | Addon not pushing to queue | Check addon logs in-game, try `/dc flush` |
| Non-zero reads appear briefly then disappear | Queue lifetime too short (5 ticks) | Increase queue `tickLifetime` in addon |
| Frames show 0 valid count | Frame config issue | Reconfigure frames via `/api/FrameConfig/configure` |
| Server can't read frames | DXGI not capturing | Check WoW is fullscreen, check resolution matches |

---

### Issue: Bot Wandering in Circles

**Symptoms:**
- Bot moves in circular patterns
- Not engaging targets
- Goal stack seems stuck

**Diagnostic Steps:**

1. **Check current goal:**
   ```bash
   curl http://localhost:5000/api/Diagnostics/bot/state
   ```
   Look at `currentGoal` and `goalStack`

2. **Check if bot has target:**
   ```bash
   curl http://localhost:5000/api/Test/frames | grep -i "target"
   ```

3. **Check movement/combat state:**
   - Look at bot logs for movement commands
   - Check if abilities are being cast

4. **Common causes:**
   - **No target found:** Path may not have mobs, or NPC detection not working
   - **Target out of range:** Pathing issue, bot can't reach target
   - **Combat stuck:** Abilities not working (keybinding issue)
   - **Goal loop:** Two goals canceling each other out

**Solutions:**
- Try `/api/BotApi/stop` then `/api/BotApi/start` to reset goals
- Load a simpler profile with fewer goals
- Check path file has valid coordinates for current zone

---

### Issue: Action Bar Spells Not Detected

**Symptoms:**
- `isTextureInitialized: false`
- Bot tries to use abilities but nothing happens

**Diagnostic Steps:**

1. **Check texture reader status:**
   ```bash
   curl http://localhost:5000/api/Diagnostics/actionbar
   ```

2. **Check if frames are valid:**
   ```bash
   curl http://localhost:5000/api/Diagnostics/summary
   ```
   Look for `actionBar.issueCount`

3. **Try manual spell placement:**
   ```bash
   curl -X POST http://localhost:5000/api/Diagnostics/fix/place \
     -H "Content-Type: application/json" \
     -d '{"slot": 2, "name": "Sinister Strike"}'
   ```

---

## 📊 Enhanced Logging

### KeyBindingsReader Now Logs:
- **Every 100 zero reads:** Shows stats without spamming logs
- **Non-zero values immediately:** Any keybinding data is logged
- **Initialization state:** When bindings are first received

### Example Log Output:
```
[16:45:00:123 D] [KeyBindingsReader] Waiting for bindings - current count=0
[16:45:05:456 I] [KeyBindingsReader] [KeyBindings Slot 106 Stats] Consecutive zeros: 500, Total reads: 500, Non-zero reads: 0
[16:45:10:789 D] [KeyBindingsReader] Reading binding slot - encodedValue=12345678
[16:45:10:790 T] [KeyBindingsReader] Binding received: ACTIONBUTTON1 -> Numpad1
[16:45:10:791 I] [KeyBindingsReader] Key bindings initialized with 24 bindings from game
```

---

## 🧪 Testing Checklist

Use this checklist when validating bot functionality:

- [ ] **Frames Configured:** `/api/Test/frames` shows 324 frames
- [ ] **Addon Data Flowing:** Frame 323 validation marker = 2000001  
- [ ] **Keybindings Initialized:** `/api/Diagnostics/keybindings/stats` shows `nonZeroReads > 0`
- [ ] **Action Bar Initialized:** `/api/Diagnostics/actionbar` shows `isTextureInitialized: true`
- [ ] **Profile Loaded:** `/api/Diagnostics/summary` shows profile details
- [ ] **Bot Active:** `/api/BotApi/status` shows `isActive: true`
- [ ] **Goals Active:** `/api/Diagnostics/bot/state` shows non-empty `goalStack`

---

## 🚀 Quick Start: Full Diagnostic Run

Run this sequence to get complete system status:

```bash
# 1. Check frames are working
curl http://localhost:5000/api/Test/frames | jq '.checks[] | select(.passed == false)'

# 2. Check keybinding stats
curl http://localhost:5000/api/Diagnostics/keybindings/stats | jq

# 3. Monitor slot 106 for 5 seconds while triggering refresh
(curl "http://localhost:5000/api/Diagnostics/monitor/slot106?duration=5" &); sleep 1; curl -X POST http://localhost:5000/api/Diagnostics/fix/bindings

# 4. Get full summary
curl http://localhost:5000/api/Diagnostics/summary | jq

# 5. Check bot state
curl http://localhost:5000/api/Diagnostics/bot/state | jq
```

---

## 📝 Common API Commands Reference

### Trigger Addon Commands
```bash
# Setup default bindings (NumPad + F-keys)
curl -X POST http://localhost:5000/api/Diagnostics/fix/bindings

# Setup number key bindings (1-9, 0, -, =)
curl -X POST http://localhost:5000/api/Diagnostics/fix/numberkeys

# Create secure action buttons
curl -X POST http://localhost:5000/api/Diagnostics/fix/actions

# Reset addon state
curl -X POST http://localhost:5000/api/Diagnostics/fix/initstate

# Run all fixes
curl -X POST http://localhost:5000/api/Diagnostics/fix/all
```

### Bot Control
```bash
# Start bot
curl -X POST http://localhost:5000/api/BotApi/start

# Stop bot
curl -X POST http://localhost:5000/api/BotApi/stop

# Load profile
curl -X POST http://localhost:5000/api/BotApi/profile/load \
  -H "Content-Type: application/json" \
  -d '{"fileName": "BloodElf_Rogue_L1-5.json"}'

# Get status
curl http://localhost:5000/api/BotApi/status
```

---

## 🔧 Server Rebuild Instructions

To apply new diagnostic changes:

```bash
# 1. Stop the bot (if running)
curl -X POST http://localhost:5000/api/BotApi/stop

# 2. Stop the server
# Press Ctrl+C in server terminal

# 3. Rebuild
cd C:\WowClassicGrindBot
dotnet build MasterOfPuppets.sln

# 4. Run server
dotnet run --project BlazorServer

# 5. Restart bot
curl -X POST http://localhost:5000/api/BotApi/start
```

---

## 📖 File Changes Summary

### Modified Files:
- `Core/Addon/KeyBindingsReader.cs` - Added read statistics tracking and enhanced logging
- `Frontend/Controllers/DiagnosticsController.cs` - Added 5 new diagnostic endpoints

### New Capabilities:
1. ✅ Real-time slot monitoring
2. ✅ Keybinding read statistics  
3. ✅ Bot state inspection
4. ✅ Frame range reading
5. ✅ Enhanced debug logging (every 100 zero reads instead of every read)
