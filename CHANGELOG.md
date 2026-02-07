# Changelog

All notable changes to the WowClassicGrindBot project (WowCombo fork).

## [Unreleased] - February 2026

### Critical Bug Fixes & System Strengthening

#### ArrayPool Race Condition Fix (P0)
- **Fixed data corruption race in NpcNameFinder.PopulateLines**
- Root cause: Returning pooled array to pool before creating result span
- Solution: Copy data to new array before returning pooled buffer
- Impact: Prevents silent data corruption in NPC detection

#### Security Hardening
- **Fixed path traversal vulnerability in ProfileController**
- Added `IsValidProfileName()` validation
- Added `TryGetProfilePath()` with resolved path verification
- Prevents directory escape attacks via malicious profile names

#### CombatRotation System Completion
- **ScoreConditionsRuntime compilation now functional**
  - JSON profile conditions compile to runtime delegates
  - RequirementFactory.Init() properly wires ScoreConditions
- **Accurate metrics tracking**
  - RecordCastResult now passes actual scores (was hardcoded 0f)
  - Failed cast attempts tracked for accurate SuccessRate
  - Added `lastScores` dictionary to persist scores between Optimize() and RecordCastResult()

#### Code Quality Improvements
- Removed CombatGoal duplicate ClassConfiguration parameter
- Removed 25 lines dead code in CombatGoal.DealWithSoftInteract
- Fixed FleeGoal logger type (ILogger<CombatGoal> → ILogger<FleeGoal>)
- Fixed EventAggregationService ring buffer race condition
- Removed FeatureFlagController double-deserialization waste

#### Infrastructure Parity
- Added runtime_feature_flags.json to HeadlessServer
- Registered Phase 1/2 features in HeadlessServer:
  - HazardAvoidance (DBSCAN clustering, A* cost injection)
  - Humanization (Gaussian timing, Bezier mouse paths)
  - FeatureFlags (hot-reload, GlobalKillSwitch)

### Validation Results
- Build: 0 errors, 0 warnings
- Tests: 168 passing (161 CoreUnitTests + 7 FrontendUnitTests)
- All Phase 1/2 systems production-ready

## [Unreleased] - February 2026

### Major Features

#### Autonomous Testing Infrastructure
- Added `DiagnosticsController` API for system health monitoring
- Created `TestController` for bot control APIs
- Implemented `HealthMonitoringService` for continuous status checks
- Added `ProcessCleanupService` for graceful shutdown handling
- Created testing helpers (`TestHelpers.cs`, `TestResult.cs`, `PlayerStateSnapshot.cs`)

#### Navigation Server Improvements
- Fixed crash loop in navigation server manager
- Prevented window focus stealing during startup
- Added graceful fallback when navigation unavailable

#### Frame Detection Bug Fix (Critical)
- **Fixed byte overflow bug in frame detection for frames 256+**
- Root cause: byte (0-255) comparison against int that could exceed 255
- Solution: Proper RGB encoding/decoding for frame indices
- See `CRITICAL_BUG_FIX_FRAME_DETECTION.md` for technical details

#### Keybinding System Enhancements
- Added `KeyBindingsReader` for automatic keybinding detection from addon
- Enhanced `ActionBarPopulator` with validation and warnings
- Support for modifier keys (Shift, Ctrl, Alt)
- Automatic keybinding setup on first run

#### DataToColor Addon Updates
- Updated for 324-frame layout
- Enhanced `SetupDefaultBindings.lua` with better error handling
- Improved diagnostics output
- Fixed compatibility with TBC Classic

### New Files Added

#### Core Services
- `Core/Services/HealthMonitoringService.cs`
- `Core/Services/ProcessCleanupService.cs`
- `Core/Testing/TestHelpers.cs`
- `Core/Testing/TestResult.cs`
- `Core/Testing/PlayerStateSnapshot.cs`

#### API Controllers
- `Frontend/Controllers/DiagnosticsController.cs`
- `Frontend/Controllers/TestController.cs`
- `Frontend/Controllers/FrameConfigController.cs`

#### Configuration Files
- `Json/class/BloodElf_Rogue_L1-5.json`
- `Json/class/BloodElf_Rogue_Starter_Test.json`
- Multiple class profiles for all classes (1-60 and 60-70 variants)
- Gold farming profiles for various classes
- New grinding paths for gold farming locations

#### Documentation
- `AGENTS.md` - Agentic coding guidelines
- `BLOODELF_ROGUE_SETUP_GUIDE.md` - Character-specific setup guide
- `COMPREHENSIVE_TEST_PLAN.md` - Testing documentation
- `DIAGNOSTICS_GUIDE.md` - Diagnostics system guide
- `SESSION_PROGRESS_SUMMARY.md` - Session tracking
- `NEXT_STEPS_OPERATION_READINESS.md` - Operational readiness checklist
- `QUICK_START_CHECKLIST.md` - Quick start guide
- `KEYBINDING_SOLUTION.md` - Keybinding documentation
- `KEYBINDINGS_READER_FIX.md` - Fix documentation

#### Scripts
- `DIAGNOSTIC_STARTUP.bat` - Diagnostic startup script
- `StartBot.bat` - Quick launch script
- `test-bot-startup.ps1` - PowerShell test script

### Modified Files

#### Core
- `Core/DependencyInjection.cs` - Added new service registrations
- `Core/Addon/AddonReader.cs` - Enhanced reading capabilities
- `Core/Addon/KeyBindingsReader.cs` - New keybinding reader
- `Core/Actionbar/ActionBarPopulator.cs` - Validation enhancements

#### Addon
- `Addons/DataToColor/DataToColor.lua` - Frame configuration updates
- `Addons/DataToColor/SetupDefaultBindings.lua` - Enhanced keybinding setup

#### Tests
- `CoreTests/NpcNameFinder/MockWoWScreen.cs` - Test improvements

### Project Structure

```
WowClassicGrindBot/
├── AGENTS.md              [NEW] - Agentic coding guidelines
├── CHANGELOG.md           [NEW] - This file
├── CLAUDE.md              [UPDATED] - Extended guidelines
├── Core/
│   ├── Services/          [NEW] - Health monitoring services
│   └── Testing/           [NEW] - Testing infrastructure
├── Frontend/
│   └── Controllers/       [NEW] - API controllers for diagnostics
├── Json/
│   ├── class/             [NEW] - Additional class profiles
│   └── path/              [NEW] - Gold farming paths
├── Addons/
│   └── DataToColor/       [UPDATED] - Enhanced addon
└── Documentation/         [NEW] - Various guides
```

### Known Issues

See `KNOWN_ISSUES.md` for current known issues:
- Navigation server may crash on certain systems (fallback available)
- Web UI may show Error 500 (restart resolves)
- BindPad addon compatibility with newer WoW versions

### Technical Notes

#### .NET Version
- Target: .NET 10 (`net10.0`)
- C# Language: 14 (preview)
- SDK: 10.0.100

#### Dependencies
Central package management via `Directory.Packages.props`:
- Serilog for logging
- SignalR with MessagePack protocol
- BenchmarkDotNet for performance testing
- SixLabors.ImageSharp for graphics

---

*This changelog documents the WowCombo fork of WowClassicGrindBot.*
