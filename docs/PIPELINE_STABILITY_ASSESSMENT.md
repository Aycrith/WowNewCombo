# Pipeline Stability & Tooling Assessment

## Current System Architecture

```
┌──────────────┐     ┌───────────────┐     ┌──────────────────┐
│  Battle.net   │────▶│  WoW Classic   │────▶│  DataToColor     │
│  Launcher     │     │  (DX12, 4K)    │     │  Addon (Lua)     │
└──────────────┘     └───────┬────────┘     └────────┬─────────┘
                             │ DXGI Capture           │ Pixel Encoding
                     ┌───────▼────────┐     ┌────────▼─────────┐
                     │  BlazorServer   │────▶│  FrameConfigurator│
                     │  (Port 5000)    │     │  + AddonReader    │
                     └───────┬────────┘     └──────────────────┘
                             │
                     ┌───────▼────────┐
                     │  Ameisenav     │
                     │  (Port 47110)  │
                     └───────────────┘
```

### Process Dependencies (ordered startup)
1. **WoW Classic** — Must be running + logged in + character in-world
2. **DataToColor addon** — Must be loaded, `/dc` sends config mode toggle
3. **AmeisenNavigationServer** — Must be listening on 47110
4. **BlazorServer** — Depends on all above; auto-configures frames on first run

---

## Identified Failure Points

### 1. Frame Config Resolution Mismatch (FIXED in this session)
- **Root cause**: `frame_config.json` stored 1920x1080 pixel positions; WoW running at 3840x2160
- **Fix**: Multi-resolution support via `TryActivateForResolution()` in DI startup
- **Remaining risk**: 4K config not yet generated

### 2. Auto-Config Stall (observed)
- **Symptom**: `FrameConfigurator.DoConfig()` enters `Stage.Reset` but never progresses
- **Root cause candidates**:
  - `AddonValidator.Validate()` failing silently in pre-flight
  - WoW window handle invalid / `GetRectangle()` returning empty rect
  - Previous failed config attempt corrupted singleton state
  - `ToggleInGameConfiguration()` sends keys but WoW doesn't receive them (background window)
- **Impact**: Auto-configure API endpoint times out (120s)

### 3. Window Focus / Input Delivery
- **Symptom**: `/dc` sent via `PostMessage(WM_CHAR)` doesn't reach WoW when not foreground
- **Root cause**: `SetForegroundWindow()` can fail if calling process doesn't own foreground lock
- **Fix needed**: Retry with `AllowSetForegroundWindow` or `AttachThreadInput`

### 4. Process Lifecycle Brittleness
- **No process watchdog**: If BlazorServer crashes, nothing restarts it
- **No WoW health check**: If WoW disconnects or crashes, bot continues polling dead pixel data
- **Ghost processes**: Multiple dotnet instances from previous runs can hold port 5000

### 5. Config File Scatter
- **3 config locations**: Source `BlazorServer/`, bin output `bin/Debug/net10.0/`, and installed WoW addon dir
- **Copy direction**: Source → bin (MSBuild), bin → source (FrameConfig.Save write-back)
- **Race conditions**: MSBuild `Always` copy overwrites bin config if source is stale

---

## Tooling Recommendations

### Tier 1: Immediate (Implement This Session)

#### A. Health Check Endpoint
Add a `/api/health` endpoint that validates the entire pipeline in one call:
```json
{
  "status": "degraded",
  "checks": {
    "wowProcess": { "status": "healthy", "pid": 12345, "version": "1.15.6" },
    "addonInstalled": { "status": "healthy", "version": "1.9.3" },
    "frameConfig": { "status": "unhealthy", "reason": "resolution_mismatch", "expected": "3840x2160", "actual": "1920x1080" },
    "screenCapture": { "status": "healthy", "fps": 30, "resolution": "3840x2160" },
    "navigationServer": { "status": "unhealthy", "reason": "connection_refused", "port": 47110 },
    "addonDataReading": { "status": "healthy", "framesRead": 324, "lastUpdate": "2026-02-06T10:30:00Z" }
  }
}
```

#### B. Startup Orchestrator with Retry
Replace the current "check once and set flags" DI setup with a resilient startup flow:
1. Poll for WoW process (configurable timeout, e.g., 60s)
2. Validate addon installation
3. Validate or generate frame config (with auto-retry)
4. Validate screen capture pipeline
5. Check navigation server connectivity
6. Enter operational mode

#### C. Process Guard Script
PowerShell wrapper that:
- Kills ghost processes on port 5000/47110 before starting
- Launches AmeisenNavigationServer
- Launches BlazorServer
- Monitors both processes, restarts on crash
- Captures stdout/stderr to log files with timestamps

### Tier 2: Near-Term (Next Sprint)

#### D. Frame Config Self-Healing
When `IsValid()` fails and no resolution-specific config matches:
1. Automatically attempt auto-configure (current behavior tries this)
2. If auto-configure fails 3 times, log detailed diagnostics and enter degraded mode
3. Expose "Reconfigure" button in Blazor UI that triggers manual re-scan
4. Save diagnostic screenshots on every failure for post-mortem analysis

#### E. WoW Connection Monitor (`IHostedService`)
Background service that:
- Checks WoW process health every 5s
- Detects disconnects via addon data staleness (no pixel changes for N frames)
- Detects character death, loading screens, or login queue
- Publishes health events via `IObservable<WowHealthEvent>`
- Auto-pauses bot on unhealthy state, auto-resumes on recovery

#### F. Structured Logging Pipeline
- Already using Serilog — extend with:
  - File sink with daily rotation + retention policy
  - Structured JSON log format for machine parsing
  - Performance counters: frame capture latency, addon read time, GOAP decision time
  - Error rate tracking with circuit breaker pattern

### Tier 3: Pipeline Stability Architecture

#### G. State Machine Supervisor
Wrap the entire bot lifecycle in a hierarchical state machine:
```
Startup → Configuring → Ready → Running → [Paused/Error/Recovery]
                                    ↓
                              Self-Healing
                                    ↓
                              Running (resumed)
```

Each state transition is logged, validated, and recoverable. The supervisor:
- Owns all process lifecycle decisions
- Implements exponential backoff for retries
- Has configurable thresholds for "give up and alert human"
- Persists state across BlazorServer restarts

#### H. Configuration Validation Framework
- Schema validation for all JSON configs on load
- Checksum verification to detect corruption
- Config diffing: compare current vs last-known-good
- Automatic rollback to last-known-good on validation failure

#### I. Diagnostic Dashboard
Blazor UI page showing:
- Real-time pixel data visualization (already partially exists)
- Frame config overlay on captured screen
- Process health indicators with auto-refresh
- Config file status (source vs bin vs addon versions)
- Log stream viewer with filtering

---

## Architecture for Self-Correcting Recovery

### Recovery Protocol
```
Error Detected
     │
     ▼
Classify Error ──▶ Transient (retry immediately)
     │                  └── WoW window unfocused, network glitch
     │
     ├──▶ Recoverable (retry with backoff)
     │         └── Frame config invalid, nav server down
     │
     └──▶ Fatal (alert + manual intervention)
              └── WoW crashed, addon not installed, hardware failure
```

### Self-Improving Patterns
1. **Error frequency tracking**: If same error recurs > N times in T minutes, escalate severity
2. **Adaptive timeouts**: Track historical success times, set timeouts to 95th percentile + margin
3. **Config learning**: After successful frame config at a resolution, cache exact pixel positions for instant activation next time (already implemented via resolution-specific configs)
4. **Diagnostic snapshots**: On any frame config failure, save the captured screen image to `logs/diagnostics/` with timestamp for human review

---

## What's Needed Right Now

| Priority | Item | Effort | Impact |
|----------|------|--------|--------|
| **P0** | Generate 4K frame config | 15 min | Unblocks runtime testing |
| **P0** | Commit multi-resolution changes | 5 min | Preserves work |
| **P1** | Health check endpoint | 2 hr | Pipeline visibility |
| **P1** | Process guard script | 1 hr | Crash recovery |
| **P2** | WoW connection monitor | 4 hr | Auto-recovery |
| **P2** | Structured diagnostic logging | 3 hr | Post-mortem capability |
| **P3** | State machine supervisor | 8 hr | Full self-correction |

---

## Summary

The current system works well when all components are running correctly. The main gaps are:
1. **No resilience to configuration changes** (resolution switches, addon updates) — addressed in this session
2. **No process lifecycle management** — needs watchdog/supervisor
3. **No health observability** — needs unified health endpoint
4. **No self-healing** — needs error classification + automatic recovery protocols

The multi-resolution frame config support implemented in this session is the foundation for #1. The health check endpoint and process guard script are the highest-impact next improvements.
