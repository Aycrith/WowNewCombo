# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Project Overview

**WowClassicGrindBot** is a World of Warcraft automation bot built on .NET 10 with a Blazor Server frontend. The architecture includes:

- **BlazorServer**: Web UI + bot runtime (GOAP planner, input simulation, screen reading)
- **Core/Game**: Business logic (pathing, combat, character state)
- **DataToColor addon** (Lua): Encodes game state as pixel colors into a known screen region
- **Screen capture**: DXGI-based pixel reader that decodes addon data
- **Navigation**: Pluggable pathfinder (RemoteV3/RemoteV1/Local) with auto-fallback

**Main solution:** `MasterOfPuppets.sln` with 14+ projects including test suites and benchmarks.

## Language & Framework
- **Target:** .NET 10 (`net10.0`) with C# 14 (`LangVersion: preview`)
- **SDK:** 10.0.100 (defined in `global.json`)
- Use latest C# 14 features: primary constructors, collection expressions, `field` keyword, extension types, etc.
- Nullable reference types are enabled project-wide

## Common Development Commands

### Build
```bash
dotnet build MasterOfPuppets.sln                          # Full debug build
dotnet build MasterOfPuppets.sln -c Release               # Full release build
dotnet build --project Core/Core.csproj                   # Build single project
```

### Run Application
```bash
dotnet run --project BlazorServer                         # Launch bot UI (http://localhost:5000)
dotnet run --project BlazorServer -c Release              # Launch with optimizations
dotnet run --project HeadlessServer                       # Run bot without web UI (needs config files)
```

### Testing
```bash
dotnet test                                               # Run all tests
dotnet test --project CoreUnitTests                       # Run unit tests only
dotnet test --project FrontendUnitTests                   # Run frontend tests only
dotnet test CoreUnitTests/PathfindingTests.cs            # Run specific test class
dotnet test --filter "ClassName=MyTest"                  # Run tests matching filter
dotnet test --verbosity detailed                         # Show detailed output
```

### Benchmarking
```bash
dotnet run --project Benchmarks -c Release                # Run all benchmarks (Release mode required)
dotnet run --project Benchmarks -c Release --filter "MousePath"  # Run specific benchmark
```

## Performance Guidelines

Follow .NET performance best practices from:
- https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/
- https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-9/
- https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-8/

Key patterns to apply (in priority order):
1. **Before writing new code**, explore existing similar implementations to avoid duplication
2. Avoid allocations in hot paths - use structs, spans, and object pooling
3. Use `Collections.*.Empty*` rather than allocating new empty collections
4. Prefer `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>` over arrays for buffer operations
5. Use `stackalloc` for small, fixed-size allocations
6. Prefer `ValueTask<T>` over `Task<T>` when results are often synchronous
7. Use `ArrayPool<T>.Shared` and `MemoryPool<T>.Shared` for temporary buffers
8. Prefer `FrozenDictionary`/`FrozenSet` for read-heavy lookup tables
9. Use `SearchValues<T>` for character/byte searching
10. Use `[InlineArray]` for fixed-size inline buffers
11. Prefer `string.Create` and `ISpanFormattable` over string concatenation
12. Use `CompositeFormat` for repeated format string usage
13. Apply `[SkipLocalsInit]` to performance-critical methods when safe

## Code Style
Existing `.editorconfig` defines style rules. Key conventions:
- File-scoped namespaces
- Explicit types preferred over `var`
- Expression-bodied properties/indexers/accessors, but not methods/constructors
- Use pattern matching and null propagation
- Prefer braces for control statements

## Architecture Patterns

### Data Flow: Game State → Decisions → Actions
1. **Frame capture**: DXGI grabs screen pixels every frame
2. **Pixel decoding**: `AddonDataProvider` reads encoded RGB values from known screen region
3. **Readers**: Transform pixels into structured world state (player HP, target location, UI state, etc.)
4. **GOAP planner**: Evaluates goals, selects best goal based on current world state
5. **Input simulation**: Sends keyboard/mouse actions to execute goal

### Pluggable Pathfinding (Auto-fallback)
Located in `Core/DependencyInjection.cs`:
- **RemoteV3** (AmeisenNavigationServer, TCP port 47110) - MMAP-based, most accurate
- **RemoteV1** (PathingAPI, HTTP port 5001) - MPQ-based, good quality
- **Local** (in-process PPather) - No external process needed, uses `Json/MPQ/expansion.MPQ`

### Dependency Injection
- Uses `Microsoft.Extensions.DependencyInjection` throughout
- Constructor-based injection via primary constructors (C# 14 feature)
- Scoped services for per-request state (e.g., WorldState)
- Singleton services for game readers and frame config

### Real-time Communication
- **SignalR** with MessagePack protocol for Blazor↔API communication
- Web UI auto-updates with bot status, diagnostics, test results
- HTTP APIs under `Frontend/Controllers/` for automation and testing

### Testing Architecture
- **CoreUnitTests**: Standalone unit tests with mocked I/O
- **FrontendUnitTests**: Blazor component tests
- **CoreManualTests**: Manual/integration tests (run separately)
- **MockWoWClient**: Shared test utilities and mock game state
- Tests use BenchmarkDotNet for performance regression detection

## Critical Lessons Learned

### Type Safety in Comparisons (Feb 2026)
**Context**: Frame detection byte overflow bug

**Issue**: Comparing `byte` values against `int` loop variables can silently fail when values exceed 255:
```csharp
// WRONG: pixel.B is byte (0-255), i can be 0-323
if (pixel.B == i && pixel.R == 0 && pixel.G == 0)  // Fails when i >= 256
```

**Correct**:
```csharp
// Decode int to expected RGB bytes matching addon's encoding
byte expectedR = (byte)((i >> 16) & 255);
byte expectedG = (byte)((i >> 8) & 255);
byte expectedB = (byte)(i & 255);
if (pixel.R == expectedR && pixel.G == expectedG && pixel.B == expectedB)
```

**Key Takeaways**:
1. Always validate type ranges match your data domain
2. Test boundary values (255, 256, etc.) explicitly
3. Document encoding schemes clearly (e.g., how integers map to RGB)
4. Add comprehensive logging for detection failures
5. Consider adding unit tests for edge cases

**See Also**: `CRITICAL_BUG_FIX_FRAME_DETECTION.md` for full technical details

## Project Structure

### Runtime Projects
- **BlazorServer/** - Web UI + bot runtime; entry point for user-facing bot (`http://localhost:5000`)
- **HeadlessServer/** - Headless bot variant; runs without web UI (requires pre-existing config files)
- **Core/** - Business logic: GOAP planner, game readers, state management, startup orchestration
- **Game/** - Game interaction: input simulation, combat logic, pathing integration
- **Frontend/** - Blazor razor components and HTTP APIs (`/api/bot/*`, `/api/test/*`, `/api/diagnostics/*`)

### Pathfinding & Navigation
- **PPather/** - In-process local pathfinder using MPQ data
- **PathingAPI/** - HTTP pathfinding service (RemoteV1) listening on port 5001
- **Navigation/** - External AmeisenNavigationServer (RemoteV3) on port 47110 (separate executable)

### Utilities & Data
- **SharedLib/** - Common types, helpers, constants shared across projects
- **WinAPI/** - Windows API interop (window detection, input, screen capture)
- **DataConfig/** - Runtime data processing
- **Tools/** - Utility command-line tools (e.g., WowInput for testing)
- **Addons/** - Lua addon source (`DataToColor/`) for WoW client-side integration

### Testing & Benchmarking
- **CoreUnitTests/** - Primary unit test suite
- **FrontendUnitTests/** - Blazor component tests
- **CoreManualTests/** - Manual/integration test harness (run separately)
- **MockWoWClient/** - Shared test mocks and fixtures
- **Benchmarks/** - BenchmarkDotNet performance suite (run with `-c Release`)

### Configuration
- **Json/class/** - Class profiles (rotation, stats, talents)
- **Json/MPQ/** - Game data files (navigation maps)
- **Json/mail/** - Mail configuration templates
- Configuration files:
  - `frame_config.json` - Pixel coordinates for addon data frame sampling
  - `data_config.json` - World state encoding definitions
  - `addon_config.json` - Addon initialization settings
  - `runtime_feature_flags.json` - Feature toggles

## Key Dependencies

Managed centrally via `Directory.Packages.props`:

- **Logging:** Serilog (structured logging with environment/process enrichment)
- **Serialization:** Newtonsoft.Json (legacy), MessagePack (SignalR protocol), MemoryPack (high performance)
- **Web/DI:** ASP.NET Core 10.0, Extensions (Hosting, DependencyInjection, Configuration, Options)
- **UI:** Blazor Bootstrap 3.4.0, MatBlazor 2.10.0, BlazorTable 1.17.0
- **Graphics:** Vortice.Direct3D11 (DXGI screen capture), System.Drawing.Common
- **Benchmarking:** BenchmarkDotNet 0.15.6
- **Networking:** GameOverlay.Net, Makaretu.Dns.Multicast
- **CLI:** CommandLineParser 2.9.1

Avoid directly adding new packages; first check `Directory.Packages.props` for existing versions.

## Development Workflows

### Debugging a Single Test
```bash
dotnet test CoreUnitTests --filter "MyTest" --verbosity detailed
```

### Running Tests with Live Output
```bash
dotnet test CoreUnitTests --logger "console;verbosity=detailed"
```

### Building Release Artifacts (no rebuild)
```bash
dotnet run --project BlazorServer -c Release --no-build
```

### Profiling Performance
```bash
# Run benchmarks to detect regressions
dotnet run --project Benchmarks -c Release

# Results saved to BenchmarkDotNet.Artifacts/results/
```

### Working with Configuration
- Edit configuration in JSON files (e.g., `frame_config.json`, `addon_config.json`)
- Changes take effect on next bot startup
- Use `/api/diagnostics/*` endpoints to validate configuration without full restart

### Adding a New Feature
1. **Plan the feature** in Core/Game (avoid mutable state, prefer immutable records)
2. **Write unit tests first** in CoreUnitTests
3. **Implement** with async/await patterns
4. **Add HTTP API** in Frontend/Controllers if user-facing
5. **Update Blazor UI** in Frontend/ if needed
6. **Test** with `dotnet test` and manual runs

## Testing & Quality

### Unit Testing
- CoreUnitTests uses standard xUnit/NUnit patterns
- Tests should be isolated and not depend on external services
- Use MockWoWClient for game state mocking
- Target >80% coverage for critical paths

### End-to-End Testing
- `/api/test/*` endpoints provide bot validation tests
- Tests verify movement, combat cycles, loot flows
- Snapshot-based validation for frame detection accuracy

### Benchmarking
- `dotnet run --project Benchmarks -c Release` (Release mode required!)
- Results saved to `BenchmarkDotNet.Artifacts/results/`
- Add new benchmarks for performance-sensitive code paths
- Monitor for regressions in high-traffic code

### Pre-commit Checklist
- [ ] `dotnet build MasterOfPuppets.sln` succeeds
- [ ] `dotnet test` passes (or skip non-relevant tests with `--filter`)
- [ ] No new warnings in build output
- [ ] Code follows `.editorconfig` conventions (auto-formatted in VS/Rider)
- [ ] For performance-critical changes, run benchmarks to verify no regression

## Important Known Issues & Fixes

### Frame Detection Type Safety (Feb 2026)
Comparing `byte` pixel values against `int` loop indices can overflow silently:
```csharp
// ❌ WRONG: pixel.B (0-255) vs i (0-323+)
if (pixel.B == i && pixel.R == 0 && pixel.G == 0) { }

// ✅ CORRECT: Decode int to RGB bytes
byte expectedR = (byte)((i >> 16) & 255);
byte expectedG = (byte)((i >> 8) & 255);
byte expectedB = (byte)(i & 255);
if (pixel.R == expectedR && pixel.G == expectedG && pixel.B == expectedB) { }
```
See `docs/archived/bug-fixes/CRITICAL_BUG_FIX_FRAME_DETECTION.md` for full details.

### Navigation Server Port (Feb 2026)
- AmeisenNavigationServer uses **port 47110** (not 47111)
- Standardized across code, scripts, and documentation
- Verify in logs if nav server fails to connect

### Pathfinding Auto-fallback
If RemoteV3 unavailable, system auto-falls back to RemoteV1 then Local. Monitor logs for fallback chains.

## Git Workflow
- Main branch: `dev`
- Create feature branches from `dev`
- Commit messages: clear, descriptive, reference issue numbers if applicable

---

## DataToColor WoW Addon (Lua 5.1)

**Location:** `Addons/DataToColor/`

World of Warcraft uses **Lua 5.1** (all versions including Classic). The addon encodes game state as pixel colors for external reading.

### Lua 5.1 Performance Guidelines

**Goal:** Minimize memory allocations and optimize execution time.

#### Local Variable Caching (Critical)
Cache global lookups at file scope - global access is slow:
```lua
-- DO: Cache at file scope
local floor = math.floor
local band = bit.band
local UnitHealth = UnitHealth
local GetTime = GetTime

-- DON'T: Access globals in hot paths
local function update()
    return math.floor(GetTime())  -- Two global lookups per call
end
```

#### Avoid Table Allocations in Loops
```lua
-- DON'T: Creates new table every call
local function getData()
    return { x = 1, y = 2 }
end

-- DO: Reuse pre-allocated tables
local dataCache = { x = 0, y = 0 }
local function getData()
    dataCache.x = 1
    dataCache.y = 2
    return dataCache
end
```

#### String Operations
```lua
-- DON'T: String concatenation creates garbage
local msg = "Player: " .. name .. " HP: " .. hp

-- DO: Use string.format (single allocation)
local msg = string.format("Player: %s HP: %d", name, hp)

-- BETTER for hot paths: Avoid string creation entirely
```

#### Table Pooling for Temporary Objects
```lua
-- Reuse tables instead of creating/discarding
local pool = {}
local function acquire()
    return table.remove(pool) or {}
end
local function release(t)
    wipe(t)  -- WoW API: clears table without dealloc
    pool[#pool + 1] = t
end
```

#### Numeric Operations
```lua
-- Use locals for repeated calculations
local x = someValue
local x2 = x * x  -- Reuse intermediate results

-- Prefer multiplication over division
local half = x * 0.5  -- Faster than x / 2

-- Use bit operations for powers of 2
local doubled = bit.lshift(x, 1)  -- x * 2
local halved = bit.rshift(x, 1)   -- x / 2 (integer)
```

#### Loop Optimization
```lua
-- Cache length outside loop
local len = #items
for i = 1, len do
    -- use items[i]
end

-- Use numeric for-loops over pairs/ipairs when possible
-- pairs/ipairs create iterator closures

-- Avoid function calls in loop conditions
for i = 1, GetNumItems() do  -- DON'T: calls every iteration
```

#### Closure Avoidance
```lua
-- DON'T: Creates new function every call
local function setup(callback)
    frame:SetScript("OnUpdate", function() callback() end)
end

-- DO: Define functions once at file scope
local function onUpdate()
    -- implementation
end
frame:SetScript("OnUpdate", onUpdate)
```

#### WoW-Specific Optimizations
```lua
-- Use C_Timer.After instead of OnUpdate for delayed actions
-- Throttle OnUpdate handlers (don't run every frame)
local elapsed = 0
local THROTTLE = 0.1
local function onUpdate(self, dt)
    elapsed = elapsed + dt
    if elapsed < THROTTLE then return end
    elapsed = 0
    -- actual work
end

-- Batch API calls when possible
-- Cache UnitGUID results when target doesn't change
```

#### Memory-Critical Patterns
- Never create tables in `OnUpdate` handlers
- Pre-size arrays with known sizes: `local t = {nil, nil, nil, nil}`
- Use `wipe(t)` instead of `t = {}` to reuse table memory
- Avoid varargs (`...`) in hot paths - they allocate
- Use `select(n, ...)` sparingly - prefer indexed access

### Addon File Structure
```
Addons/DataToColor/
├── init.lua              - Addon initialization, AceAddon setup
├── DataToColor.lua       - Main frame update loop (performance critical)
├── Constants.lua         - Static data tables
├── Query.lua             - Game state queries
├── Storage.lua           - Data storage structures
├── EventHandlers.lua     - WoW event handling
├── Collections.lua       - Data structure implementations
├── ActionBarTextures.lua - Action bar texture tracking
├── ActionBarMacros.lua   - Macro detection
└── libs/                 - Ace3 libraries (external, don't modify)
```
