# AGENTS.md - Agentic Coding Guidelines

## Quick Reference

### Build Commands
```bash
dotnet build MasterOfPuppets.sln          # Build entire solution
dotnet build Core/Core.csproj             # Build single project
dotnet run --project BlazorServer         # Run main application
dotnet run --project HeadlessServer       # Run headless server
dotnet run --project PathingAPI           # Run pathing API server
```

### Test Commands
```bash
dotnet test                                           # Run all tests
dotnet run --project CoreTests                        # Run integration tests
dotnet run --project Benchmarks -c Release            # Run benchmarks
dotnet run --project Benchmarks -c Release -- --filter "*SpecificBenchmark*"
```

### Project Entry Points
| Project | Purpose |
|---------|---------|
| `BlazorServer` | Main web UI application |
| `HeadlessServer` | Server without UI |
| `PathingAPI` | Pathfinding API service |
| `CoreTests` | Integration test runner |
| `Benchmarks` | Performance benchmarks |

---

## Language & Framework

- **.NET 10** (`net10.0`) with **C# 14** (`LangVersion: preview`)
- **SDK:** 10.0.100 (see `global.json`)
- **Nullable reference types:** Enabled project-wide
- Use modern C# features: primary constructors, collection expressions, `field` keyword

---

## Code Style (from .editorconfig)

### Naming Conventions
| Element | Convention | Example |
|---------|------------|---------|
| Interfaces | PascalCase with `I` prefix | `IWowScreen` |
| Classes/Structs | PascalCase | `NpcNameFinder` |
| Methods/Properties | PascalCase | `CanRun()`, `DisplayName` |
| Private fields | camelCase | `private readonly ILogger logger;` |
| Local variables | camelCase | `int frameCount = 0;` |
| Constants | PascalCase | `const int MaxFrames = 324;` |

### Formatting Rules
- **Indentation:** 4 spaces
- **Line endings:** CRLF (Windows)
- **Namespaces:** File-scoped (`namespace Core;`)
- **Braces:** Prefer braces for control statements
- **Types:** Explicit types preferred over `var`
- **Expression bodies:** Use for properties/indexers/accessors, NOT for methods/constructors

### Import Organization
```csharp
// System namespaces first
using System;
using System.Collections.Generic;

// Microsoft namespaces
using Microsoft.Extensions.Logging;

// Third-party packages
using Serilog;

// Project namespaces
using Core.Goals;
using SharedLib;
```

---

## Error Handling

- Use structured exception handling with Serilog logging
- Prefer returning result types over throwing exceptions in hot paths
- Log exceptions with context: `Log.Error(ex, "[ClassName] Message {Param}", value);`
- Use pattern: `[ClassName          ]` in log messages (padded to 18 chars)

---

## Performance Requirements

### Critical Patterns
- **Avoid allocations in hot paths** - use spans, pooling, structs
- Prefer `Span<T>`/`ReadOnlySpan<T>` over arrays for buffers
- Use `ArrayPool<T>.Shared` for temporary buffers
- Prefer `ValueTask<T>` when results are often synchronous
- Use `FrozenDictionary`/`FrozenSet` for read-only lookup tables
- Use `SearchValues<T>` for character/byte searching
- Apply `[SkipLocalsInit]` to performance-critical methods

### Before Writing Code
1. Explore existing similar implementations
2. Do not duplicate code (functions, variables, constants)
3. Use collection types with Empty semantics (e.g., `Array.Empty<T>()`)

---

## Type Safety Warning

**Critical Bug Pattern (byte overflow):**
```csharp
// WRONG: byte (0-255) compared to int that can exceed 255
if (pixel.B == frameIndex)  // Silent failure when frameIndex >= 256

// CORRECT: Encode int to expected RGB bytes
byte expectedR = (byte)((frameIndex >> 16) & 255);
byte expectedG = (byte)((frameIndex >> 8) & 255);
byte expectedB = (byte)(frameIndex & 255);
```

Always validate type ranges match your data domain.

## ArrayPool Use-After-Return Warning

**Critical Bug Pattern (ArrayPool race condition):**
```csharp
// WRONG: Return array to pool, then use it
pooler.Return(segments);
return new(segments, 0, count); // segments may be overwritten by another thread!

// CORRECT: Copy data before returning to pool
int resultCount = Math.Min(segments.Length, counter.count);
LineSegment[] result = new LineSegment[resultCount];
Array.Copy(segments, result, resultCount);
pooler.Return(segments);
return new(result, 0, resultCount);
```

Always copy pooled array data before returning to the pool if the data will be accessed after the method returns.

---

## Project Structure

```
WowClassicGrindBot/
├── BlazorServer/     # Main Blazor web application
├── Core/             # Core business logic, goals, GOAP
├── Game/             # Game interaction layer
├── Frontend/         # Blazor UI components
├── PPather/          # Pathfinding implementation
├── SharedLib/        # Shared utilities
├── WinAPI/           # Windows API interop (P/Invoke)
├── HeadlessServer/   # Server without UI
├── PathingAPI/       # Pathfinding HTTP API
├── CoreTests/        # Integration tests
├── Benchmarks/       # BenchmarkDotNet tests
├── DataConfig/       # Configuration models
├── WowheadDB/        # Wowhead data models
└── Addons/           # WoW Lua addons
```

---

## Dependencies

Central package management: `Directory.Packages.props`

| Category | Packages |
|----------|----------|
| Logging | Serilog.* |
| Serialization | Newtonsoft.Json, MessagePack, MemoryPack |
| UI | Blazor.Bootstrap, MatBlazor |
| Graphics | SixLabors.ImageSharp, Vortice.Direct3D11 |
| Benchmarking | BenchmarkDotNet |

---

## Architecture Patterns

- **DI:** Constructor injection via `Microsoft.Extensions.DependencyInjection`
- **Real-time:** SignalR with MessagePack protocol
- **Async:** Use async/await throughout, avoid blocking calls
- **GOAP:** Goal-Oriented Action Planning in `Core/GOAP/`

---

## Git Workflow

- **Main branch:** `dev`
- Create feature branches from `dev`
- Commit message style: conventional commits

---

## Lua Addon Development (Addons/DataToColor/)

WoW uses **Lua 5.1**. The addon encodes game state as pixel colors.

### Performance-Critical Patterns
```lua
-- Cache globals at file scope
local floor = math.floor
local band = bit.band
local UnitHealth = UnitHealth

-- Reuse tables instead of creating new ones
local cache = {}
local function getData()
    wipe(cache)  -- Reuse, don't recreate
    return cache
end

-- Throttle OnUpdate handlers
local elapsed = 0
local function onUpdate(self, dt)
    elapsed = elapsed + dt
    if elapsed < 0.1 then return end
    elapsed = 0
    -- actual work
end
```

### Memory Rules
- Never create tables in `OnUpdate` handlers
- Use `wipe(t)` instead of `t = {}`
- Avoid closures in hot paths
- Cache loop lengths: `local len = #items`

---

## LLM Agent Integration

This section documents integration points for AI agents to monitor and control the bot.

### Bot Monitoring & Intervention Reference

#### Health API Endpoints
- **`GET http://localhost:5000/api/health`** — Full health status (app version, PID, uptime, OS, thread count, startup state, config options)
- **`GET http://localhost:5000/api/health/startup`** — Startup state snapshot only

#### Feature Flag Hot-Reload
- **File:** `BlazorServer/runtime_feature_flags.json` (and `HeadlessServer/runtime_feature_flags.json`)
- **Mechanism:** `FileSystemWatcher` with 100ms debounce + `IOptionsMonitor<FeatureFlagsOptions>.OnChange`
- **No restart required** — changes are applied immediately
- **Event notification:** `FeatureFlagService.OnFlagsChanged` event fires on any change

#### Log Sink Architecture
- **Class:** `Frontend.LoggerSink` — Serilog `ILogEventSink` implementation
- **Ring buffer:** 256 entries (power-of-2 with bitmask indexing)
- **Event:** `OnLogChanged` fires on every log emit
- **Access:** Registered as singleton in DI, available via `IServiceProvider`

#### GOAP Event Bus
- **Pattern:** Full-mesh broadcast — every `GoapGoal` subscribes to every other goal's `GoapEvent` delegate
- **Base type:** `GoapEventArgs` (in `Core/GOAP/Events/`)
- **Event types:**
  - `GoapStateEvent` — World state bit change (`GoapKey` + `bool Value`)
  - `CorpseEvent` / `SkinCorpseEvent` — Spatial events with map coordinates
  - `AbortEvent` / `ResumeEvent` — Lifecycle markers
  - `ScreenCaptureEvent` — Screenshot trigger
  - `RemoveClosestPoi` — POI management
  - `FollowRouteChanged` — Route updates
- **Interface:** `IGoapEventListener` — single method `void OnGoapEvent(GoapEventArgs e)`

#### Key Error Signals
| Signal | Location | Meaning |
|--------|----------|---------|
| **"NO PLAN"** | `GoapAgent.cs:456`, `EventId = 0053`, `LogLevel.Warning` | GOAP planner found no valid goal — bot is stuck or misconfigured |
| **Death events** | `CombatLog.cs`, `SessionStat.Deaths++` | Player died, triggers corpse run |
| **Stuck detection** | `StuckRecoveryV2Options`, backtracking thresholds | Bot hasn't moved, initiating recovery |
| **Circuit breaker trips** | `CircuitBreaker<T>`, `State = Open` | External service (pathfinding API, LLM API) unavailable |

#### Context7 Library ID
- **ID:** `/xian55/wowclassicgrindbot`
- **Coverage:** 282 code snippets — class profiles, combat rotations, requirement syntax, GOAP architecture

---

### LLM Feature Flags Reference

All feature flags are in `Core/FeatureFlags/FeatureFlagsOptions.cs` and `runtime_feature_flags.json`.

#### HybridLLMDecision (Disabled by default)
```json
"HybridLLMDecision": {
  "Enabled": false,
  "ConfidenceThreshold": 0.6,
  "MaxLatencyMs": 2000,
  "EnableForUnexpectedStates": true,
  "CacheDecisionsSeconds": 5,
  "Description": "Hybrid GOAP+LLM decision system for handling edge cases"
}
```

**Integration point:** When GOAP returns "NO PLAN" or encounters unexpected states, query an LLM endpoint for a decision. Uses circuit breaker (see below).

#### AIProfileGenerator (Disabled by default)
```json
"AIProfileGenerator": {
  "Enabled": false,
  "APIProvider": "none",
  "MaxTokensPerRequest": 4000,
  "RateLimitPerHour": 20,
  "CacheResponsesMinutes": 60,
  "AllowedProviders": ["openai", "anthropic", "local"],
  "Description": "LLM-powered class profile generation from natural language descriptions"
}
```

**Integration point:** Generate class profile JSON from natural language (e.g., "level 20-30 warrior grinding build with execute spam").

#### CircuitBreaker — LLM Thresholds
```json
"CircuitBreaker": {
  "Enabled": true,
  "LLMThreshold": 3,
  "LLMCooldownSeconds": 120,
  "Description": "Prevents cascading failures when external services are unavailable"
}
```

**Behavior:** After 3 consecutive LLM API failures, circuit opens for 120 seconds (rejects all requests, returns fallback). Auto-recovers via half-open state.

#### MonitoringThresholds — LLM Latency
```json
"Thresholds": {
  "LLMLatencyWarningMs": 3000,
  "LLMLatencyCriticalMs": 10000
}
```

**Warning:** LLM response took >3s. **Critical:** LLM response took >10s (may degrade bot performance).

---

### Class Profile Schema Reference

Class profiles are JSON files in `Json/class/` (100+ profiles). They define combat rotations, pull sequences, pathing, and requirements.

#### Profile Structure
```json
{
  "ClassName": "Warrior",
  "Mode": "Grind",
  "PathFilename": "_pack\\1-20\\Human\\18-20_Duskwood.json",
  "PathThereAndBack": true,
  "Loot": true,
  "Skin": false,
  "UseMount": false,
  "NPCMaxLevels_Above": 1,
  "NPCMaxLevels_Below": 7,
  "Blacklist": ["Defias Messenger", "Hogger"],

  "Pull": { "Sequence": [ /* KeyAction array */ ] },
  "Combat": { "Sequence": [ /* KeyAction array */ ] },
  "Adhoc": { "Sequence": [ /* KeyAction array */ ] },
  "NPC": { "Sequence": [ /* KeyAction array */ ] }
}
```

#### Available Modes
| Mode | Description |
|------|-------------|
| `Grind` | Standard grinding — kill mobs along a path |
| `CorpseRun` | Recovery mode — run to corpse and resurrect |
| `AttendedGather` | Semi-automated gathering (herbs/mining/skinning) |
| `AttendedGrind` | Requires user input for targeting |
| `AssistFocus` | Assist a focus target in combat |

#### KeyAction Properties
```json
{
  "Name": "Frostbolt",
  "Key": "1",
  "WhenUsable": true,
  "HasCastBar": true,
  "Cost": 18,
  "Requirement": "TargetHealth% > 20",
  "Requirements": ["!InMeleeRange", "HasTarget", "TargetAlive"],
  "AfterCastWaitCastbar": true,
  "BeforeCastStop": true
}
```

#### Requirement Syntax
| Requirement | Meaning |
|-------------|---------|
| `Health% < 40` | Player health below 40% |
| `TargetHealth% > 50` | Target health above 50% |
| `!BuffName` | Buff not active (e.g., `!Battle Shout`) |
| `SpellInRange:0` | Spell slot 0 in range (for pulls) |
| `InMeleeRange` | Target within melee range |
| `HasTarget` | Has a valid target |
| `TargetAlive` | Target is alive |
| `MobCount < 2` | Fewer than 2 nearby mobs |
| `MainHandSwing > -400` | Main hand swing timer (>-400ms means ready) |
| `BagFull` | Any bag is full |
| `Durability% < 35` | Item durability below 35% |

Full requirement syntax is in `Core/ClassConfig/RequirementFactory.cs`.

---

### Agent Intervention Playbook

#### Emergency Stop
**Action:** Set `GlobalKillSwitch: true` in `runtime_feature_flags.json`

**Effect:** All feature flags disabled immediately, bot enters safe state.

#### Toggle Features
**Files:** `BlazorServer/runtime_feature_flags.json` or `HeadlessServer/runtime_feature_flags.json`

**Examples:**
- Enable humanization: `"Humanization": { "Enabled": true }`
- Disable hazard avoidance: `"HazardAvoidance": { "Enabled": false }`
- Enable LLM decision system: `"HybridLLMDecision": { "Enabled": true }`

**Trigger:** File save automatically reloads within 100ms (no restart).

#### Edit Combat Rotation
**Files:** `Json/class/{ProfileName}.json`

**Example:** Reorder abilities in `Combat.Sequence`, adjust requirements, change key bindings.

**Trigger:** Reload class profile from UI or restart the bot.

#### Adjust Pathfinding
**Files:** `Json/path/{PathName}.json`

**Format:** Array of `{ "X": float, "Y": float, "Z": float }` waypoints.

**Trigger:** Route reload via UI or `/reload` command.

#### CircuitBreaker Behavior
**Pathfinding API:** After 5 failures, circuit opens for 60 seconds (falls back to local pathfinding).

**LLM API:** After 3 failures, circuit opens for 120 seconds (falls back to pure GOAP).

**Manual reset:** Not exposed via API (automatic recovery only).

