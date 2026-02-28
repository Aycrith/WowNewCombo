# Comprehensive System Validation Plan

**Branch:** fix/nav-recovery-baseline
**Date:** 2026-02-28
**Status:** IN PROGRESS

---

## Overview

Autonomous validation of all bot systems using live API endpoints. Each system tested for:
- Operational status (working/not working)
- Performance metrics (latency, accuracy)
- Error handling (graceful degradation)
- Feature completeness (all functions present)
- Integration (systems work together)

---

## Systems to Test

### 1. Frame Capture & Addon Integration
- [ ] DXGI frame capture running
- [ ] Frame data being decoded
- [ ] Addon communication live
- [ ] Pixel data accuracy
- [ ] Frame latency < 10ms

### 2. Navigation & Pathfinding
- [ ] Navigation server (RemoteV3) connected
- [ ] Fallback pathfinder (local PPather) available
- [ ] Route loading working
- [ ] Path calculations completing
- [ ] Route following goal functioning

### 3. GOAP Planning & Goals
- [ ] Goal selection working
- [ ] Goal transitions clean
- [ ] Hysteresis preventing oscillation
- [ ] All goal types functional
- [ ] Precondition evaluation correct

### 4. Combat System
- [ ] Target detection working
- [ ] Combat goal activating
- [ ] Rotation executing
- [ ] Damage dealing
- [ ] Kill credit tracking

### 5. Movement & Input
- [ ] Keyboard input sending
- [ ] Mouse movement working
- [ ] Player movement responding
- [ ] Focus guard preventing false inputs
- [ ] Input security active

### 6. State Reading (Addon Data)
- [ ] Player health reading
- [ ] Mana reading
- [ ] Target detection
- [ ] Bag state reading
- [ ] Action bar state reading

### 7. Feature Flags System
- [ ] Feature flag loading
- [ ] CombatRotationOptimizer enabled
- [ ] StuckRecoveryV2 enabled
- [ ] HazardAvoidance enabled
- [ ] Dynamic feature reloading

### 8. Diagnostics & Monitoring
- [ ] Health check endpoint responding
- [ ] Status API returning correct data
- [ ] Launch readiness checks working
- [ ] Performance metrics collection
- [ ] Error logging

### 9. Web UI & SignalR
- [ ] BlazorServer responding at :5000
- [ ] UI loading correctly
- [ ] Real-time updates via SignalR
- [ ] API endpoints accessible
- [ ] Profile management working

### 10. Stuck Detection & Recovery
- [ ] Stuck thresholds configured correctly
- [ ] Movement validation working
- [ ] Distance tracking accurate
- [ ] Recovery mechanisms responsive
- [ ] No false positives

---

## Test Execution

### Test 1: Frame Capture
```
Endpoint: /api/health
Expected: DXGI frame data available, latency < 10ms
Metrics: Timestamp age < 5ms
```

### Test 2: Navigation
```
Endpoint: /api/launch/status
Expected: RemoteV3 connected, route loaded
Metrics: Server reachable, path valid
```

### Test 3: GOAP Goals
```
Endpoint: /api/diagnostics/bot/state
Expected: Valid goal, goal stack, clean transitions
Metrics: Goal change frequency, transition duration
```

### Test 4: Combat
```
Endpoint: /api/bot/status
Expected: Active state, combat engagement
Metrics: Kill count, target detection success
```

### Test 5: Movement
```
Expected: Player position changes, no stuck events
Metrics: Position delta > 0, distance covered
```

### Test 6: State Reading
```
Endpoint: /api/diagnostics/*
Expected: Health, mana, targets, inventory
Metrics: Data freshness, accuracy
```

### Test 7: Features
```
File: runtime_feature_flags.json
Expected: All 3 features enabled
Metrics: Feature state, functionality
```

### Test 8: Diagnostics
```
Endpoint: /api/health, /api/launch/status
Expected: All checks passing
Metrics: System health, error count
```

### Test 9: Web UI
```
Endpoint: http://localhost:5000
Expected: UI loads, API responsive
Metrics: Response time, functionality
```

### Test 10: Stuck Detection
```
Log monitoring: No false "Stuck" events
Expected: Movement progressing normally
Metrics: Distance covered, stuck events
```

---

## Test Execution Log

Will be updated as tests execute. Each test will log:
- Timestamp
- System being tested
- Expected vs actual
- Pass/Fail status
- Any issues found

---

## Success Criteria

All 10 systems must:
- ✅ Respond to API calls
- ✅ Return valid data
- ✅ Perform their primary function
- ✅ Not cause errors
- ✅ Work together without conflicts

---

## Blockers

If any system fails:
1. Log the failure
2. Attempt recovery (restart, reset state)
3. Document root cause
4. Escalate if unrecoverable

---

**Starting comprehensive system validation now...**
