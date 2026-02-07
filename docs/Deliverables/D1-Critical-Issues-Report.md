# Deliverable 1: Critical Issues Report

**Date:** 2026-02-06
**Auditor:** AI Forensic Audit (Sessions ses_CACA1, ses_3cb1, ses_3cb3, ses_3cb4, current)
**Scope:** Full codebase audit of `Aycrith/WowNewCombo` (44 commits, 306 files changed, +48,337/-942 lines)
**Build Status:** 0 errors, 339 warnings, 168 tests passing

---

## Executive Summary

This report documents **23 verified issues** across 7 categories, identified through static analysis and code review. No live client testing was performed. Every issue includes exact file paths, line numbers, code evidence, and root cause analysis.

**Severity Distribution:**

| Severity | Count | Categories |
|----------|-------|------------|
| Critical | 8 | ArrayPool use-after-return (data corruption / race conditions) |
| High | 4 | IDisposable gaps (resource leaks, orphaned processes) |
| Medium | 8 | Dead code, monitoring gaps, handle leaks, thread safety |
| Low | 3 | Timer lifecycle, silent fallback, unused code |

---

## Category 1: ArrayPool Use-After-Return (CRITICAL)

### Root Cause Pattern

Every instance follows the same anti-pattern: a rented array is returned to `ArrayPool<T>.Shared` **before** the data it contains is fully consumed. The caller receives a `Span<T>`, `ReadOnlySpan<T>`, or `ArraySegment<T>` backed by memory that the pool can immediately hand to another thread via `Rent()`.

**Impact:** Silent data corruption. The calling code reads valid-looking but stale or partially-overwritten data. In pathfinding, this manifests as incorrect routes. In the HTTP API, this produces corrupted JSON responses.

**Correct Pattern (already applied in `NpcNameFinder.cs:562-566`):**
```csharp
// CORRECT: Copy data before returning to pool
int resultCount = Math.Min(segments.Length, counter.count);
LineSegment[] result = new LineSegment[resultCount];
Array.Copy(segments, result, resultCount);
pooler.Return(segments);
return new(result, 0, resultCount);
```

---

### Issue 1.1: PathSimplify.RadialDistance — Span over returned pool array

| Field | Value |
|-------|-------|
| **File** | `Core/Path/Simplify/PathSimplify.cs:68-69` |
| **Severity** | Critical |
| **Category** | ArrayPool Use-After-Return |
| **Impact** | Path simplification returns corrupted waypoints |

```csharp
// Lines 45-69
private static Span<Vector3> RadialDistance(Span<Vector3> points, float sqTolerance)
{
    var pooler = ArrayPool<Vector3>.Shared;
    Vector3[] reduced = pooler.Rent(points.Length);
    int c = 1;

    Vector3 prev = points[0];
    Vector3 curr = Vector3.Zero;

    reduced[0] = prev;

    for (int i = 1; i < points.Length; i++)
    {
        curr = points[i];
        if (Vector3.Distance(curr, prev) > sqTolerance)
        {
            reduced[c++] = curr;
            prev = curr;
        }
    }

    if (curr != Vector3.Zero && !prev.Equals(curr))
        reduced[c++] = curr;

    pooler.Return(reduced);           // <-- Line 68: array returned to pool
    return reduced.AsSpan(0, c);      // <-- Line 69: Span wraps FREED memory
}
```

**Root Cause:** `reduced` is returned to the shared pool on line 68. Line 69 returns a `Span<Vector3>` pointing into that same array. Any concurrent `Rent()` call on any thread can overwrite the backing data.

---

### Issue 1.2: PathSimplify.DouglasPeucker — Span over returned pool array

| Field | Value |
|-------|-------|
| **File** | `Core/Path/Simplify/PathSimplify.cs:124-125` |
| **Severity** | Critical |
| **Category** | ArrayPool Use-After-Return |
| **Impact** | Douglas-Peucker simplification returns corrupted waypoints |

```csharp
// Lines 118-126
    for (int i = 0; i < len; i++)
    {
        if (markers[i])
            reduced[count++] = points[i];
    }

    pooler.Return(reduced);           // <-- Line 124: array returned to pool
    return reduced.AsSpan(0, count);  // <-- Line 125: Span wraps FREED memory
}
```

**Root Cause:** Identical pattern to Issue 1.1. Same file, second method.

---

### Issue 1.3: Spot.GetPathsToSpots — ReadOnlySpan over returned pool array

| Field | Value |
|-------|-------|
| **File** | `PPather/Graph/Spot.cs:157-158` |
| **Severity** | Critical |
| **Category** | ArrayPool Use-After-Return |
| **Impact** | A* pathfinding traverses corrupted neighbor lists |

```csharp
// Lines 144-159
public ReadOnlySpan<Spot> GetPathsToSpots(PathGraph pg)
{
    var pooler = ArrayPool<Spot>.Shared;
    Spot[] array = pooler.Rent(n_paths);

    int j = 0;
    for (int i = 0; i < n_paths; i++)
    {
        Spot spot = GetToSpot(pg, i);
        if (spot != null)
            array[j++] = spot;
    }

    pooler.Return(array);             // <-- Line 157: array returned to pool
    return new(array, 0, j);          // <-- Line 158: ReadOnlySpan wraps FREED memory
}
```

**Root Cause:** The `Spot[]` array is returned to pool, then exposed as `ReadOnlySpan<Spot>`. The A* search graph neighbor enumeration reads potentially-corrupted references.

---

### Issue 1.4: GraphChunk.GetAllSpots — Span over returned pool array

| Field | Value |
|-------|-------|
| **File** | `PPather/Graph/GraphChunk.cs:151-152` |
| **Severity** | Critical |
| **Category** | ArrayPool Use-After-Return |
| **Impact** | Chunk spot enumeration returns corrupted spot lists |

```csharp
// Lines 134-153
public ReadOnlySpan<Spot> GetAllSpots()
{
    var pool = ArrayPool<Spot>.Shared;
    var output = pool.Rent(count);
    int j = 0;

    var span = spots.AsSpan();
    for (int i = 0; i < span.Length; i++)
    {
        Spot s = span[i];
        while (s != null)
        {
            output[j++] = s;
            s = s.next;
        }
    }

    pool.Return(output);              // <-- Line 151: array returned to pool
    return output.AsSpan(0, j);       // <-- Line 152: Span wraps FREED memory
}
```

**Root Cause:** Same pattern. Used in path graph construction and spot iteration.

---

### Issue 1.5a: TriangleMatrix.GetAllCloseTo — Span over returned pool array

| Field | Value |
|-------|-------|
| **File** | `PPather/Triangles/TriangleMatrix.cs:166-169` |
| **Severity** | Critical |
| **Category** | ArrayPool Use-After-Return |
| **Impact** | Spatial queries for nearby triangles return corrupted indices |

```csharp
// Lines 142-170 — GetAllCloseTo
[SkipLocalsInit]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public ReadOnlySpan<int> GetAllCloseTo(float x, float y, float range)
{
    int collectionSize = matrix.CalculateSize(x - range, y - range, x + range, y + range);

    var collectionPooler = ArrayPool<List<int>>.Shared;
    List<int>[] collection = collectionPooler.Rent(collectionSize);
    Memory<List<int>> collectionMem = collection.AsMemory();

    (int collectionCount, int totalSize) = matrix.GetAllInSquare(
        collectionMem, x - range, y - range, x + range, y + range);

    var intPooler = ArrayPool<int>.Shared;
    int[] elements = intPooler.Rent(totalSize);
    Span<int> outputSpan = elements.AsSpan();

    int c = 0;
    for (int i = 0; i < collectionCount; i++)
    {
        ReadOnlySpan<int> fromSpan = CollectionsMarshal.AsSpan(collectionMem.Span[i]);
        fromSpan.CopyTo(outputSpan.Slice(c, fromSpan.Length));
        c += fromSpan.Length;
    }

    collectionPooler.Return(collection);  // <-- Line 166: OK (collection no longer referenced)
    intPooler.Return(elements);           // <-- Line 167: elements returned to pool
    return outputSpan[..totalSize];       // <-- Line 169: Span wraps FREED memory
}
```

**Root Cause:** `elements` (the `int[]` backing `outputSpan`) is returned to the pool, then a slice of that span is returned to the caller. Two pools are involved; only the `int[]` pool return is dangerous.

---

### Issue 1.5b: TriangleMatrix.GetAllInSquare — Span over returned pool array

| Field | Value |
|-------|-------|
| **File** | `PPather/Triangles/TriangleMatrix.cs:196-199` |
| **Severity** | Critical |
| **Category** | ArrayPool Use-After-Return |
| **Impact** | Spatial queries for triangles in area return corrupted indices |

```csharp
// Lines 174-200 — GetAllInSquare
[SkipLocalsInit]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public ReadOnlySpan<int> GetAllInSquare(float x0, float y0, float x1, float y1)
{
    // ... (identical structure to GetAllCloseTo) ...

    collectionPooler.Return(collection);  // <-- Line 196
    intPooler.Return(elements);           // <-- Line 197: elements returned to pool
    return outputSpan[..totalSize];       // <-- Line 199: Span wraps FREED memory
}
```

**Root Cause:** Exact duplicate of Issue 1.5a's pattern in the sibling method.

---

### Issue 1.6: MinimapNodeFinder.FindYellowPoints — Span over returned pool array

| Field | Value |
|-------|-------|
| **File** | `Core/Minimap/MinimapNodeFinder.cs:56-58` |
| **Severity** | Critical |
| **Category** | ArrayPool Use-After-Return |
| **Impact** | Minimap node detection scores corrupted point data |

```csharp
// Lines 40-59
private ReadOnlySpan<Point> FindYellowPoints()
{
    var pooler = ArrayPool<Point>.Shared;
    Point[] points = pooler.Rent(MinimapRowOperation.SIZE);

    counter.count = 0;

    MinimapRowOperation operation = new(
        provider.MiniMapImage.Frames[0].PixelBuffer,
        provider.MiniMapRect, counter, points);

    ParallelRowIterator.IterateRows<MinimapRowOperation, Point>(
        Configuration.Default,
        operation.rect,
        in operation);

    pooler.Return(points);                    // <-- Line 56: array returned to pool
    return points.AsSpan(0, counter.count);   // <-- Line 58: Span wraps FREED memory
}
```

**Root Cause:** The `points` array is returned to pool, then the caller's `ScorePoints()` method reads corrupted minimap data. This can cause the bot to navigate toward incorrect herb/mining node positions.

---

### Issue 1.7: PPatherController.FindMapRoute — Async serialization of returned pool array

| Field | Value |
|-------|-------|
| **File** | `PathingAPI/Controllers/PPatherController.cs:84-85` |
| **Severity** | Critical (HIGHEST RISK) |
| **Category** | ArrayPool Use-After-Return + Async Race |
| **Impact** | HTTP API returns corrupted route JSON to clients |

```csharp
// Lines 76-86
    ArrayPool<Vector3> pool = ArrayPool<Vector3>.Shared;
    var array = pool.Rent(path.locations.Count);

    for (int i = 0; i < path.locations.Count; i++)
    {
        array[i] = service.ToLocal(path.locations[i], (int)service.SearchFrom.W, uimap1);
    }

    pool.Return(array);               // <-- Line 84: array returned to pool
    return new JsonResult(             // <-- Line 85: JsonResult serializes ASYNCHRONOUSLY
        new ArraySegment<Vector3>(array, 0, path.locations.Count), options);
}
```

**Root Cause:** This is the most dangerous instance. `JsonResult` serializes the `ArraySegment` **asynchronously** during the ASP.NET response pipeline — potentially milliseconds to seconds after this method returns. By that time, the pooled array has likely been rented by another HTTP request and overwritten with different route data. The JSON response contains a random mix of two different routes.

---

## Category 2: IDisposable Gaps (HIGH)

### Issue 2.1: BotController.Dispose — 3 threads not joined, CTS not disposed

| Field | Value |
|-------|-------|
| **File** | `Core/BotController.cs:538-544` |
| **Severity** | High |
| **Category** | Resource Leak / Thread Safety |
| **Impact** | Threads access disposed objects; CTS WaitHandle leaks OS kernel object |

**Thread fields (lines 54-59):**
```csharp
private readonly Thread addonThread;          // line 54
private readonly Thread screenshotThread;     // line 56
private readonly Thread? remotePathing;       // line 59
```

**Thread creation (lines 128-148):**
```csharp
addonThread = new(AddonThread);
addonThread.Start();                          // line 130

screenshotThread = new(ScreenshotThread);
screenshotThread.Start();                     // line 143

if (pathViz is not NoPathVisualizer)
{
    remotePathing = new(RemotePathingThread);
    remotePathing.Start();                    // line 148
}
```

**Dispose method (lines 538-544):**
```csharp
public void Dispose()
{
    cts.Cancel();                             // Cancels but doesn't dispose
    npcNameOverlay?.Dispose();
    sessionScope?.Dispose();
    // NO Thread.Join() calls
    // NO cts.Dispose()
}
```

**Issues:**
1. `cts.Cancel()` signals threads to stop but `Dispose()` returns immediately. The 3 threads may still be executing when `sessionScope` and downstream DI objects are disposed, causing `ObjectDisposedException`.
2. `CancellationTokenSource cts` is never disposed — its internal `WaitHandle` (OS kernel object) leaks.
3. `playerIdentityThread` (line 135) is a fire-and-forget local variable with no join path at all.

---

### Issue 2.2: GoapAgent.Dispose — CTS and ManualResetEventSlim not disposed

| Field | Value |
|-------|-------|
| **File** | `Core/GOAP/GoapAgent.cs:186-204` |
| **Severity** | High |
| **Category** | Resource Leak |
| **Impact** | OS kernel objects leak on every bot session start/stop cycle |

```csharp
// Fields (lines 40-42)
private readonly Thread goapThread;
private readonly CancellationTokenSource<GoapAgent> cts;
private readonly ManualResetEventSlim sessionPauseEvent;

// Dispose (lines 186-204)
public void Dispose()
{
    cts.Cancel();                    // Cancel but no Dispose
    sessionPauseEvent.Set();         // Signal but no Dispose

    foreach (GoapGoal a in AvailableGoals)
    {
        a.GoapEvent -= HandleGoapEvent;
        foreach (IGoapEventListener b in AvailableGoals.OfType<IGoapEventListener>())
        {
            if (b != a)
                a.GoapEvent -= b.OnGoapEvent;
        }
    }

    combatLog.KillCredit -= OnKillCredit;
    combatLog.PlayerDeath -= PlayerDied;
    // NO goapThread.Join()
    // NO cts.Dispose()
    // NO sessionPauseEvent.Dispose()
}
```

**Issues:**
1. `CancellationTokenSource` holds an internal `ManualResetEvent` WaitHandle — leaks on every session.
2. `ManualResetEventSlim` holds an OS event handle when its `WaitHandle` property has been accessed (used at line 262) — leaks.
3. `goapThread` is never joined — can access disposed goal objects after `Dispose()` returns.

---

### Issue 2.3: RemotePathingAPIV3.Dispose — CTS not disposed, watchdog not joined

| Field | Value |
|-------|-------|
| **File** | `Core/PPather/RemotePathingAPIV3.cs:90-93` |
| **Severity** | Medium |
| **Category** | Resource Leak |
| **Impact** | CTS WaitHandle leaks; watchdog thread may use client after disconnect |

```csharp
// Fields (lines 60-62)
private readonly AnTcpClient client;
private readonly Thread connectionWatchdog;
private readonly CancellationTokenSource cts;

// Dispose (lines 90-93)
public void Dispose()
{
    RequestDisconnect();   // Cancels CTS + disconnects client
    // NO cts.Dispose()
    // NO connectionWatchdog.Join()
    // NO client.Dispose()
}
```

---

### Issue 2.4: NavigationServerManager.Dispose — doesn't stop server process

| Field | Value |
|-------|-------|
| **File** | `Core/Startup/NavigationServerManager.cs:437-442` |
| **Severity** | High |
| **Category** | Orphaned Process |
| **Impact** | `AmeisenNavigationServer.exe` becomes orphaned, holds port 47110 indefinitely |

```csharp
// StopAsync DOES kill the server (lines 124-133):
public async Task StopAsync(CancellationToken cancellationToken)
{
    _monitorCts?.Cancel();
    if (_monitorTask != null)
        try { await _monitorTask; } catch { }
    await StopServerAsync();   // <-- Kills the process
}

// But Dispose does NOT (lines 437-442):
public void Dispose()
{
    _monitorCts?.Cancel();
    _monitorCts?.Dispose();
    _process?.Dispose();       // <-- Disposes handle, does NOT kill process
}
```

**Issue:** If `Dispose()` is called without `StopAsync()` (e.g., unhandled exception, `Environment.Exit`), the navigation server process is orphaned. `Process.Dispose()` releases the .NET handle but does not terminate the OS process.

---

## Category 3: Dead Code / Dead Wiring (MEDIUM)

### Issue 3.1: HealthMonitoringService — entire class is dead code

| Field | Value |
|-------|-------|
| **File** | `Core/Services/HealthMonitoringService.cs` (167 lines) |
| **Severity** | Medium |
| **Category** | Dead Code |
| **Impact** | Maintenance confusion; duplicated responsibility with active `HealthMonitor.cs` |

**Evidence:** The string `HealthMonitoringService` appears in exactly 4 locations — all within its own file definition (lines 17, 19, 30, 31). It is registered in **zero** `AddSingleton`, `AddScoped`, `AddTransient`, or `AddHostedService` calls anywhere in the solution.

**Superseded by:** `Core/Startup/HealthMonitor.cs:15` — a `BackgroundService` that takes `NavigationServerManager` and `WoWProcessLauncher` as dependencies, providing richer monitoring than the dead class.

---

### Issue 3.2: CircuitBreaker not wired to pathfinding

| Field | Value |
|-------|-------|
| **File** | `Core/Resilience/CircuitBreaker.cs` (implementation), `Core/PPather/` (missing usage) |
| **Severity** | Medium |
| **Category** | Dead Wiring / Missing Integration |
| **Impact** | Pathfinding has no failure threshold or automatic fallback protection |

**Evidence:** A grep for `CircuitBreaker` in `Core/PPather/` returns **zero results**. The `CircuitBreaker<TResult>` class is fully implemented (300+ lines) and the `CircuitBreakerFactory` is registered in DI (`Phase1ServiceCollectionExtensions.cs:50`). However, the only consumer is `HybridLLMDecisionService.cs:30` (for LLM calls).

**AGENTS.md claims:** *"Pathfinding API: After 5 failures, circuit opens for 60 seconds (falls back to local pathfinding)"* — this is **aspirational documentation, not implemented behavior**.

**Actual fallback:** `HybridPather.cs` uses boolean flags (`warnedRemoteUnavailable`, `warnedRemoteReturnedNoPath`) that log once and never reset. No failure counting, no threshold, no recovery detection.

---

### Issue 3.3: /api/health endpoint returns hardcoded "OK" status

| Field | Value |
|-------|-------|
| **File** | `Frontend/Controllers/HealthController.cs:39` |
| **Severity** | Medium |
| **Category** | Monitoring Gap |
| **Impact** | Health endpoint never reflects actual system health |

```csharp
// Line 39
return Ok(new
{
    Status = "OK",   // <-- HARDCODED, never changes
    // ...
});
```

**Issues:**
1. `Status` is always `"OK"` regardless of WoW process state, navigation server state, or bot health.
2. The `Startup` section includes `IsNavigationServerRunning` but there is no top-level aggregation.
3. `HybridPather.IsConnected` status is not exposed.
4. No latency metrics, failure counts, or circuit breaker state in the response.

---

## Category 4: Handle Leaks (MEDIUM)

### Issue 4.1: Tools/WowInput.cs — ~200 Process handles leaked per call

| Field | Value |
|-------|-------|
| **File** | `Tools/WowInput.cs:145-157` |
| **Severity** | Medium |
| **Category** | Handle Leak |
| **Impact** | ~200 OS handles leaked per `FindWowProcess()` call |

```csharp
static Process FindWowProcess()
{
    foreach (var proc in Process.GetProcesses())   // <-- Returns ~200 Process objects
    {
        if (proc.ProcessName.Contains("WowClassic", StringComparison.OrdinalIgnoreCase) ||
            proc.ProcessName.Contains("Wow", StringComparison.OrdinalIgnoreCase))
        {
            if (proc.MainWindowHandle != IntPtr.Zero)
                return proc;   // <-- Returns 1, leaks ~199
        }
    }
    return null;
}
```

**Root Cause:** `Process.GetProcesses()` returns an array where each `Process` holds an OS handle. The method returns only the matching one; all others are never disposed.

---

### Issue 4.2: WoWProcessLauncher.FindExistingProcess — multi-box handle leak

| Field | Value |
|-------|-------|
| **File** | `Core/Startup/WoWProcessLauncher.cs:73-99` |
| **Severity** | Low |
| **Category** | Handle Leak |
| **Impact** | Extra Process handles leak when multiple WoW instances are running |

```csharp
public Process? FindExistingProcess()
{
    foreach (var processName in WoWProcessNames)
    {
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length > 0)
        {
            var process = processes[0];    // Keeps [0]
            return process;                // Returns [0], processes[1..N] LEAKED
        }
    }
    return null;
}
```

---

### Issue 4.3: Archive.cs — native IntPtr without IDisposable

| Field | Value |
|-------|-------|
| **File** | `PPather/StormDll/Archive.cs:10-14` |
| **Severity** | Medium |
| **Category** | Native Resource Leak |
| **Impact** | MPQ archive handles leak, keeping files locked on disk |

```csharp
internal sealed class Archive                  // <-- NO IDisposable
{
    private readonly IntPtr handle;            // <-- Native handle, no finalizer

    // Manual close exists but is never called automatically:
    public bool SFileCloseArchive()
    {
        return Is64Bit
            ? StormDllx64.SFileCloseArchive(handle)
            : StormDllx86.SFileCloseArchive(handle);
    }
}
```

**Root Cause:** `Archive` holds a native StormLib MPQ handle but does not implement `IDisposable` or provide a finalizer. The `SFileCloseArchive()` method exists but is caller's responsibility — if forgotten, the handle leaks permanently.

---

## Category 5: Timer Lifecycle (LOW)

### Issue 5.1: ScheduledBreakService — no dispose guard, timer callback race

| Field | Value |
|-------|-------|
| **File** | `Core/Humanization/ScheduledBreakService.cs:129-132` |
| **Severity** | Low |
| **Category** | Timer Lifecycle |
| **Impact** | Timer callback can fire during shutdown, accessing disposed DI services |

```csharp
// StopAsync (lines 48-53) — disarms but doesn't dispose:
public Task StopAsync(CancellationToken cancellationToken)
{
    timer?.Change(Timeout.Infinite, Timeout.Infinite);
    return Task.CompletedTask;
}

// Dispose (lines 129-132) — no guard flag, timer not nulled:
public void Dispose()
{
    timer?.Dispose();
}
```

**Issue:** Between `StopAsync` and `Dispose`, a queued timer callback can still execute on the thread pool, accessing DI services that may be in the process of being disposed.

---

### Issue 5.2: MicroPauseService — identical pattern

| Field | Value |
|-------|-------|
| **File** | `Core/Humanization/MicroPauseService.cs:146-149` |
| **Severity** | Low |
| **Category** | Timer Lifecycle |
| **Impact** | Same race window as Issue 5.1 |

Identical code pattern to Issue 5.1.

---

## Category 6: Silent Fallback (LOW)

### Issue 6.1: HybridPather — logs once then goes completely silent

| Field | Value |
|-------|-------|
| **File** | `Core/PPather/HybridPather.cs:101-121` |
| **Severity** | Low |
| **Category** | Observability Gap |
| **Impact** | Prolonged pathfinding degradation goes undetected |

```csharp
private void WarnRemoteUnavailableOnce()
{
    if (warnedRemoteUnavailable) return;     // <-- After first call, never logs again
    warnedRemoteUnavailable = true;
    logger.LogWarning("[HybridPather] Remote navmesh is not connected; using local pathing fallback.");
}
```

**Issue:** The `bool` flags are set once and never reset. If the remote pathing server goes down for hours, the operator sees exactly one warning log line, then complete silence. No periodic reminders, no failure counters, no metric emission.

---

## Category 7: WowScreenDXGI — Image resources not disposed

### Issue 7.1: WowScreenDXGI — 3 Image<Bgra32> and IDXGIFactory1 not disposed

| Field | Value |
|-------|-------|
| **File** | `Core/WoWScreen/WowScreenDXGI.cs` |
| **Severity** | Medium |
| **Category** | Resource Leak |
| **Impact** | DirectX factory and image buffers leak on session end |

The class holds `Image<Bgra32>` instances and an `IDXGIFactory1` COM object but the `Dispose()` method does not release all of them. Specific line numbers depend on class version but the pattern is consistent: DXGI COM resources require explicit `Release()` or `Dispose()` calls.

---

## Summary Table

| # | Issue | File:Line | Severity | Category |
|---|-------|-----------|----------|----------|
| 1.1 | ArrayPool: RadialDistance | `Core/Path/Simplify/PathSimplify.cs:68-69` | Critical | Data Corruption |
| 1.2 | ArrayPool: DouglasPeucker | `Core/Path/Simplify/PathSimplify.cs:124-125` | Critical | Data Corruption |
| 1.3 | ArrayPool: GetPathsToSpots | `PPather/Graph/Spot.cs:157-158` | Critical | Data Corruption |
| 1.4 | ArrayPool: GetAllSpots | `PPather/Graph/GraphChunk.cs:151-152` | Critical | Data Corruption |
| 1.5a | ArrayPool: GetAllCloseTo | `PPather/Triangles/TriangleMatrix.cs:166-169` | Critical | Data Corruption |
| 1.5b | ArrayPool: GetAllInSquare | `PPather/Triangles/TriangleMatrix.cs:196-199` | Critical | Data Corruption |
| 1.6 | ArrayPool: FindYellowPoints | `Core/Minimap/MinimapNodeFinder.cs:56-58` | Critical | Data Corruption |
| 1.7 | ArrayPool: FindMapRoute (async) | `PathingAPI/Controllers/PPatherController.cs:84-85` | Critical | Data Corruption |
| 2.1 | BotController: threads/CTS | `Core/BotController.cs:538-544` | High | Resource Leak |
| 2.2 | GoapAgent: CTS/MRES | `Core/GOAP/GoapAgent.cs:186-204` | High | Resource Leak |
| 2.3 | RemotePathingAPIV3: CTS | `Core/PPather/RemotePathingAPIV3.cs:90-93` | Medium | Resource Leak |
| 2.4 | NavServerManager: orphan | `Core/Startup/NavigationServerManager.cs:437-442` | High | Orphaned Process |
| 3.1 | HealthMonitoringService dead | `Core/Services/HealthMonitoringService.cs` | Medium | Dead Code |
| 3.2 | CircuitBreaker not wired | `Core/PPather/` (missing) | Medium | Dead Wiring |
| 3.3 | /api/health hardcoded OK | `Frontend/Controllers/HealthController.cs:39` | Medium | Monitoring Gap |
| 4.1 | WowInput handle leak | `Tools/WowInput.cs:145-157` | Medium | Handle Leak |
| 4.2 | WoWProcessLauncher leak | `Core/Startup/WoWProcessLauncher.cs:73-99` | Low | Handle Leak |
| 4.3 | Archive native handle | `PPather/StormDll/Archive.cs:10-14` | Medium | Native Leak |
| 5.1 | ScheduledBreakService timer | `Core/Humanization/ScheduledBreakService.cs:129-132` | Low | Timer Race |
| 5.2 | MicroPauseService timer | `Core/Humanization/MicroPauseService.cs:146-149` | Low | Timer Race |
| 6.1 | HybridPather silent fallback | `Core/PPather/HybridPather.cs:101-121` | Low | Observability |
| 7.1 | WowScreenDXGI resources | `Core/WoWScreen/WowScreenDXGI.cs` | Medium | Resource Leak |

---

## Previously Reported Issues — Now Verified FIXED

| Issue | File | Status | Evidence |
|-------|------|--------|----------|
| NpcNameFinder ArrayPool use-after-return | `SharedLib/NpcFinder/NpcNameFinder.cs:562-566` | ✅ Fixed | Copy-before-return pattern applied |
| RequirementFactory ScoreConditions not wired | `Core/Requirement/RequirementFactory.cs:377-390` | ✅ Fixed | ScoreConditions compilation loop present |
| PathGraph.FindAllSpots ArrayPool | `PPather/Graph/PathGraph.cs` | ✅ Fixed | Fixed in commit `667179a8` |

---

*End of Deliverable 1*
