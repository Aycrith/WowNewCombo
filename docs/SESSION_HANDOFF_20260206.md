# Session Handoff: Multi-Resolution Frame Config + Stability Assessment

## Session Summary
**Date**: 2026-02-06
**Branch**: `dev`
**Latest Commit**: `4209535b` — Multi-resolution frame config support + pipeline stability assessment

## What Was Done

### 1. System Audit (Complete)
- Verified clean system state: no ghosted processes, ports 5000/47110 clear
- All 14 `dotnet` processes = MSBuild worker nodes (VS Code IDE), no stale servers
- Config file audit: cleaned stale `frame_config_temp_backup.json` and `frame_config_1080p.json`
- Addon version consistency: all 1.9.3 across source/installed/config
- WoW Config.wtf analysis: 3840x2160, DX12, uiScale=1, RTX 3090

### 2. Multi-Resolution Frame Config (Committed)
**Files changed** (commit `4209535b`):
| File | Change |
|------|--------|
| `Core/DataFrame/FrameConfig.cs` | Added `GetResolutionPath()`, `ExistsForResolution()`, `ListResolutionConfigs()`, `TryActivateForResolution()`. Modified `Save()` to write resolution-named copies + source write-back |
| `Core/DependencyInjection.cs` | Added `TryActivateForResolution()` fallback before deleting mismatched config |
| `Frontend/Controllers/FrameConfigController.cs` | Added `/api/frameconfig/resolutions` endpoint |
| `BlazorServer/BlazorServer.csproj` | Added `frame_config_*x*.json` copy glob |
| `HeadlessServer/HeadlessServer.csproj` | Same glob addition |
| `BlazorServer/frame_config_1920x1080.json` | Verified 1080p baseline config |
| `Switch-Resolution.ps1` | Manual resolution toggle script |
| `docs/PIPELINE_STABILITY_ASSESSMENT.md` | Full stability/tooling assessment |

### 3. Build & Test Status
- **Build**: 0 errors, 0 warnings
- **Tests**: 161/161 CoreUnit passing
- **Clean build verified**: `dotnet clean` + rebuild

## What Remains

### P0: Generate 4K Frame Config (Blocked on WoW)
WoW is not currently running. Steps to generate:
1. Launch WoW Classic via Battle.net → log in → enter world at 3840x2160
2. Start BlazorServer: `dotnet run --project BlazorServer`
3. The FrameConfigurator should auto-start if no valid config exists for current rect
4. If auto-config stalls, manually:
   - Ensure WoW is focused
   - Type `/dc` in WoW chat to enter config mode
   - Call `POST http://localhost:5000/api/frameconfig/auto-configure`
   - Or let the BlazorServer UI handle it via the config page
5. Verify: `GET http://localhost:5000/api/frameconfig/resolutions` should show both 1920x1080 and 3840x2160

### P1: Test Resolution Toggle
Once both configs exist:
- Verify `TryActivateForResolution()` works by switching WoW resolution in Config.wtf
- Test `Switch-Resolution.ps1` script manually
- Verify `/api/frameconfig/resolutions` endpoint returns correct data

### P2: Known Auto-Config Issues
The previous auto-config attempt stalled at `Stage.Reset` — potential causes:
- `AddonValidator.Validate()` may fail in pre-flight (worth logging)
- `SetForegroundWindow()` may not bring WoW to front if BlazorServer doesn't own foreground lock
- Singleton state corruption from previous failed attempts (restart BlazorServer fresh)

**Workaround**: Manually type `/dc` in WoW, then start BlazorServer — it should detect config mode pixels.

### P3: Pipeline Stability Improvements
See `docs/PIPELINE_STABILITY_ASSESSMENT.md` for full assessment. Key next items:
- Health check endpoint (`/api/health`)
- Process guard script (watchdog for BlazorServer + AmeisenNav)
- WoW connection monitor (`IHostedService`)

## Technical Notes

### 4K Pixel Mapping
- WoW UI coordinates map ~2:1 to screen pixels at 4K with uiScale=1
- Addon CELL_SIZE=4 → ~8 screen pixels per cell at 4K
- Metadata pixel offset: x=10 at 4K (vs x=0 at 1080p) — `metaPixelXOffset` handles this
- Expected frame grid: ~70px wide × ~500px tall at 4K
- `TryGetNextPoint()` scans full image for RGB-encoded frame indices — resolution agnostic

### Config File Locations
| Location | Purpose |
|----------|---------|
| `BlazorServer/frame_config.json` | Active config (source, copied to bin on build) |
| `BlazorServer/frame_config_1920x1080.json` | Resolution-specific 1080p backup |
| `bin/Debug/net10.0/frame_config.json` | Runtime active config |
| `bin/Debug/net10.0/frame_config_*x*.json` | Runtime resolution backups |

### Key API Endpoints
| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/frameconfig/status` | GET | Config + configurator status |
| `/api/frameconfig/diagnostics` | GET | Screen capture info + pixel scan results |
| `/api/frameconfig/resolutions` | GET | Available resolution configs |
| `/api/frameconfig/auto-configure` | POST | Trigger auto-config (120s timeout) |
| `/api/frameconfig/screenshot` | GET | HTML page with captured screen |
