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
