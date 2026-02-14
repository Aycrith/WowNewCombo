# AGENTS.md - Agentic Coding Guidelines

## Build Commands

```bash
# Build
dotnet build MasterOfPuppets.sln              # Entire solution
dotnet build Core/Core.csproj                 # Single project

# Run
dotnet run --project BlazorServer             # Main web UI
dotnet run --project HeadlessServer           # Headless server
dotnet run --project PathingAPI               # Pathing API server

# Test
dotnet test                                   # All tests
dotnet test --filter "FullyQualifiedName~TestClassName"  # Single test class
dotnet test --filter "FullyQualifiedName~TestMethodName" # Single test method
dotnet run --project CoreTests                # Integration tests
dotnet run --project Benchmarks -c Release    # Benchmarks
```

## Project Structure

| Project | Purpose |
|---------|---------|
| `BlazorServer` | Main web UI application |
| `HeadlessServer` | Server without UI |
| `PathingAPI` | Pathfinding HTTP API |
| `Core` | Business logic, goals, GOAP |
| `Game` | Game interaction layer |
| `Frontend` | Blazor UI components |
| `PPather` | Pathfinding implementation |
| `CoreTests` / `CoreUnitTests` / `FrontendUnitTests` | Tests (xUnit) |
| `Benchmarks` | BenchmarkDotNet |
| `Addons/DataToColor/` | WoW Lua addon |

## Language & Framework

- **.NET 10** (`net10.0`) with **C# 14** preview
- **SDK:** 10.0.100
- **Nullable reference types:** Enabled
- Use modern C#: primary constructors, collection expressions, `field` keyword

## Code Style

### Naming
| Element | Convention | Example |
|---------|------------|---------|
| Interfaces | PascalCase with `I` | `IWowScreen` |
| Classes/Structs | PascalCase | `NpcNameFinder` |
| Methods/Properties | PascalCase | `CanRun()`, `DisplayName` |
| Private fields | camelCase | `private readonly ILogger logger;` |
| Constants | PascalCase | `const int MaxFrames = 324;` |

### Formatting
- **Indentation:** 4 spaces
- **Line endings:** CRLF
- **Namespaces:** File-scoped (`namespace Core;`)
- **Braces:** Prefer braces for control statements
- **Types:** Explicit types over `var`
- **Expression bodies:** Use for properties/indexers/accessors, NOT methods/constructors

### Import Order
```csharp
// System
using System;
using System.Collections.Generic;

// Microsoft
using Microsoft.Extensions.Logging;

// Third-party
using Serilog;

// Project
using Core.Goals;
using SharedLib;
```

## Error Handling

- Structured exception handling with Serilog
- Prefer result types over exceptions in hot paths
- Log pattern: `Log.Error(ex, "[ClassName] Message {Param}", value);`
- Use `[ClassName          ]` (padded to 18 chars)

## Performance

- **Avoid allocations in hot paths** — use spans, pooling, structs
- Prefer `Span<T>`/`ReadOnlySpan<T>` for buffers
- Use `ArrayPool<T>.Shared` for temporary buffers
- Use `ValueTask<T>` when synchronous results are common
- Use `FrozenDictionary`/`FrozenSet` for read-only lookups
- Use `SearchValues<T>` for character/byte searching
- Apply `[SkipLocalsInit]` to performance-critical methods

### Before Writing Code
1. Explore existing similar implementations
2. Do not duplicate code
3. Use collection types with Empty semantics (`Array.Empty<T>()`)

## Critical Bug Patterns

### Type Safety (byte overflow)
```csharp
// WRONG
if (pixel.B == frameIndex)  // Silent failure when frameIndex >= 256

// CORRECT
byte expectedB = (byte)(frameIndex & 255);
```

### ArrayPool Use-After-Return
```csharp
// WRONG
pooler.Return(segments);
return new(segments, 0, count); // Race condition!

// CORRECT
LineSegment[] result = new LineSegment[count];
Array.Copy(segments, result, count);
pooler.Return(segments);
return new(result, 0, count);
```

## Architecture

- **DI:** Constructor injection via `Microsoft.Extensions.DependencyInjection`
- **Real-time:** SignalR with MessagePack
- **Async:** Use async/await, avoid blocking calls
- **GOAP:** Goal-Oriented Action Planning in `Core/GOAP/`

## Git Workflow

- **Main branch:** `dev`
- Create feature branches from `dev`
- Conventional commits

## Lua Addon (Addons/DataToColor/)

- WoW uses **Lua 5.1**
- Cache globals at file scope: `local floor = math.floor`
- Reuse tables: `wipe(cache)` instead of `cache = {}`
- Never create tables in `OnUpdate` handlers
- Throttle: use elapsed time tracking

## Key Integration Points

### Health API
- `GET http://localhost:5000/api/health`
- `GET http://localhost:5000/api/health/startup`

### Feature Flags
- Files: `BlazorServer/runtime_feature_flags.json`, `HeadlessServer/runtime_feature_flags.json`
- Hot-reload via `FileSystemWatcher` (100ms debounce)
- Event: `FeatureFlagService.OnFlagsChanged`

### Log Sink
- `Frontend.LoggerSink` — Serilog sink with 256-entry ring buffer
- Event: `OnLogChanged`

### GOAP Event Bus
- Full-mesh: every `GoapGoal` subscribes to every other's `GoapEvent`
- Base: `GoapEventArgs`
- Interface: `IGoapEventListener`

### Key Error Signals
| Signal | Meaning |
|--------|---------|
| "NO PLAN" | GOAP stuck — `GoapAgent.cs:456`, EventId 0053 |
| Death events | `SessionStat.Deaths++` triggers corpse run |
| Stuck detection | Backtracking thresholds in `StuckRecoveryV2Options` |
| Circuit breaker | `State = Open` — external service unavailable |

## Dependencies

Central package management: `Directory.Packages.props`

| Category | Packages |
|----------|----------|
| Logging | Serilog.* |
| Serialization | Newtonsoft.Json, MessagePack, MemoryPack |
| UI | Blazor.Bootstrap, MatBlazor |
| Graphics | SixLabors.ImageSharp, Vortice.Direct3D11 |
| Testing | xunit |
| Benchmarking | BenchmarkDotNet |

## Context7

- **ID:** `/xian55/wowclassicgrindbot`
- **Coverage:** 282 code snippets — class profiles, GOAP, requirements
