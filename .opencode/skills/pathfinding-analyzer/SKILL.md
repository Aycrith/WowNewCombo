---
name: pathfinding-analyzer
description: |
  **PROJECT-SPECIFIC SKILL FOR WOW CLASSIC GRIND BOT**
  Pathfinding and navigation expert for WowClassicGrindBot's PPather system.
  Analyzes path generation, triangle mesh navigation, stuck detection, and
  movement optimization. Use for debugging pathing issues, stuck bot, or route optimization.
allowed-tools: Read, Edit, Grep, Glob, Bash
trigger-keywords: pathfinding, navigation, stuck, ppather, triangle, mesh, waypoint, route
---

# Pathfinding Analyzer (WowClassicGrindBot)

Expert pathfinding and navigation specialist for WowClassicGrindBot's PPather triangle mesh navigation system, stuck detection, and movement optimization.

## When to Use

- 🗺️ **Path generation** — Bot not finding path to target
- 🚫 **Stuck detection** — Bot stuck on obstacles
- 📍 **Waypoint issues** — Skipping waypoints or wrong order
- 🏃 **Movement optimization** — Inefficient routes
- 🧭 **Triangle mesh** — Missing navmesh data
- 🔄 **Backtracking** — Bot going in circles
- 🛑 **Collision detection** — Walking through walls

## PPather Architecture

### Core Components

**Location:** `PPather/`

```
PPather/
  ├── Triangles/
  │   ├── TriangleCollection.cs  - Triangle mesh storage
  │   ├── TrianglePath.cs        - A* pathfinding
  │   └── ChunkedTriangleMatrix.cs - Chunked mesh loading
  ├── Graph/
  │   ├── PathGraph.cs           - High-level path graph
  │   └── GraphChunk.cs          - Graph chunking
  ├── PathFinder.cs              - Main pathfinding coordinator
  └── Data/
      └── world/                  - Triangle mesh data files
          ├── Azeroth/
          ├── Kalimdor/
          └── ...
```

### How Pathfinding Works

**1. Triangle Mesh Loading:**
```
- World divided into chunks (256x256 yards)
- Each chunk contains triangles
- Triangles form walkable surface
- Loaded on-demand from .mesh files
```

**2. A* Pathfinding:**
```csharp
// TrianglePath.cs - A* search
public List<Location> FindPath(Location start, Location end)
{
    var openSet = new PriorityQueue<Triangle>();
    var cameFrom = new Dictionary<Triangle, Triangle>();
    
    var startTriangle = GetTriangleAt(start);
    var endTriangle = GetTriangleAt(end);
    
    openSet.Enqueue(startTriangle, Heuristic(start, end));
    
    while (openSet.Count > 0)
    {
        var current = openSet.Dequeue();
        
        if (current == endTriangle)
            return ReconstructPath(cameFrom, current);
        
        foreach (var neighbor in current.Neighbors)
        {
            var tentativeG = gScore[current] + Distance(current, neighbor);
            
            if (tentativeG < gScore[neighbor])
            {
                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                fScore[neighbor] = tentativeG + Heuristic(neighbor.Center, end);
                openSet.Enqueue(neighbor, fScore[neighbor]);
            }
        }
    }
    
    return null; // No path found
}
```

**3. Path Smoothing:**
```
- Raw path follows triangle edges (jagged)
- Smoothing removes unnecessary waypoints
- Line-of-sight checks between waypoints
- Result: Smooth, natural-looking path
```

## Common Issues

### Issue 1: No Path Found

**Symptom:**
```
[PathFinder       ] Failed to find path from {Start} to {End}
```

**Causes:**
1. **Missing triangle mesh data** — Chunk not loaded
2. **Unreachable destination** — Island, cliff, instance portal
3. **Corrupted mesh file** — Bad data in .mesh file

**Debugging:**
```bash
# Check if mesh data exists for area
ls PPather/Data/world/Kalimdor/

# Check chunk loading
grep "LoadChunk" logs/app.txt

# Verify start/end triangles exist
# Add logging to PathFinder.cs:
Log.Debug("[PathFinder] Start triangle: {Triangle}, End triangle: {EndTriangle}", 
    startTriangle?.Id, endTriangle?.Id);
```

**Solution:**
```csharp
// Fallback to direct movement if no path
if (path == null || path.Count == 0)
{
    Log.Warning("[PathFinder] No path found, moving directly");
    return [start, end]; // Simple straight line
}
```

### Issue 2: Bot Stuck on Obstacle

**Symptom:** Bot runs into wall/tree repeatedly

**Stuck Detection:** `Core/Goals/StuckRecoveryV2.cs`

```csharp
public class StuckRecoveryV2(ILogger logger, IPlayerReader player)
{
    private readonly Queue<Vector3> recentPositions = new(capacity: 60); // 60 frames
    
    public bool IsStuck()
    {
        recentPositions.Enqueue(player.Position);
        
        if (recentPositions.Count < 60)
            return false; // Not enough data
        
        // Calculate total distance moved in last 60 frames
        var totalDistance = 0f;
        var previous = recentPositions.First();
        foreach (var pos in recentPositions.Skip(1))
        {
            totalDistance += Vector3.Distance(previous, pos);
            previous = pos;
        }
        
        var avgDistancePerFrame = totalDistance / 60f;
        
        // If moving < 0.1 yards/frame = stuck
        if (avgDistancePerFrame < 0.1f)
        {
            Log.Warning("[StuckRecovery  ] Bot stuck - avg movement: {Dist:F2} yards/frame", 
                avgDistancePerFrame);
            return true;
        }
        
        return false;
    }
    
    public void RecoverFromStuck()
    {
        Log.Information("[StuckRecovery  ] Attempting unstuck maneuver");
        
        // Try: Jump
        input.PressKey(ConsoleKey.Spacebar);
        Wait.For(500);
        
        // Try: Backtrack
        input.PressKey(ConsoleKey.S); // Move backward
        Wait.For(2000);
        input.ReleaseKey(ConsoleKey.S);
        
        // Try: Strafe
        input.PressKey(ConsoleKey.A); // Strafe left
        Wait.For(1000);
        input.ReleaseKey(ConsoleKey.A);
        
        // Clear position history
        recentPositions.Clear();
    }
}
```

**Integration:**
```csharp
// In PathGoal.cs
public override void Update()
{
    if (stuckRecovery.IsStuck())
    {
        stuckRecovery.RecoverFromStuck();
        
        // Recalculate path after unstuck
        currentPath = pathFinder.FindPath(player.Position, destination);
        return;
    }
    
    // Normal pathfinding logic
    FollowPath(currentPath);
}
```

### Issue 3: Inefficient Paths

**Symptom:** Bot takes long, winding route instead of direct path

**Causes:**
1. **Mesh resolution** — Coarse triangles create suboptimal paths
2. **No path smoothing** — Following raw triangle edges
3. **Elevation changes** — Avoiding slopes unnecessarily

**Solution:**
```csharp
// Path smoothing algorithm
public List<Location> SmoothPath(List<Location> rawPath)
{
    var smoothed = new List<Location> { rawPath[0] };
    var current = 0;
    
    while (current < rawPath.Count - 1)
    {
        var next = current + 1;
        
        // Try to skip waypoints with line-of-sight check
        while (next < rawPath.Count && HasLineOfSight(rawPath[current], rawPath[next]))
        {
            next++;
        }
        
        smoothed.Add(rawPath[next - 1]);
        current = next - 1;
    }
    
    smoothed.Add(rawPath[^1]); // Add final destination
    return smoothed;
}

private bool HasLineOfSight(Location from, Location to)
{
    // Raycast between points
    // Check if any triangles block the path
    var ray = to - from;
    var steps = (int)(ray.Length / 0.5f); // Check every 0.5 yards
    
    for (int i = 0; i < steps; i++)
    {
        var point = from + (ray * (i / (float)steps));
        var triangle = GetTriangleAt(point);
        
        if (triangle == null)
            return false; // No walkable surface = blocked
    }
    
    return true;
}
```

### Issue 4: Missing Navmesh Data

**Symptom:** Specific areas have no paths

**Check:**
```bash
# List available mesh files
ls PPather/Data/world/Azeroth/
ls PPather/Data/world/Kalimdor/

# Check file size (0 bytes = corrupt)
ls -lh PPather/Data/world/Azeroth/*.mesh
```

**Generate Missing Mesh:**
```
1. Use PPatherEditor (separate tool)
2. Load WoW client
3. Walk around missing area
4. Export mesh data
5. Copy .mesh files to PPather/Data/world/
```

## Pathfinding Optimization

### Strategy 1: Chunk Caching

```csharp
// Cache loaded chunks in memory
private readonly Dictionary<Vector2i, GraphChunk> chunkCache = new();

public GraphChunk GetChunk(int chunkX, int chunkY)
{
    var key = new Vector2i(chunkX, chunkY);
    
    if (chunkCache.TryGetValue(key, out var chunk))
        return chunk;
    
    chunk = LoadChunkFromDisk(chunkX, chunkY);
    chunkCache[key] = chunk;
    
    // Evict old chunks if cache > 100MB
    if (GetCacheSize() > 100_000_000)
        EvictLRUChunk();
    
    return chunk;
}
```

### Strategy 2: Path Caching

```csharp
// Cache recently calculated paths
private readonly Dictionary<(Location, Location), List<Location>> pathCache = new();

public List<Location> FindPath(Location start, Location end)
{
    var key = (start.RoundTo(1f), end.RoundTo(1f)); // Round to 1-yard precision
    
    if (pathCache.TryGetValue(key, out var cached))
    {
        Log.Debug("[PathFinder] Using cached path");
        return cached;
    }
    
    var path = CalculatePath(start, end);
    pathCache[key] = path;
    
    return path;
}
```

### Strategy 3: Async Pathfinding

```csharp
// Don't block main thread during pathfinding
public async Task<List<Location>> FindPathAsync(Location start, Location end)
{
    return await Task.Run(() => FindPath(start, end));
}

// Usage in PathGoal
public override async void Update()
{
    if (needsNewPath)
    {
        currentPath = await pathFinder.FindPathAsync(player.Position, destination);
        needsNewPath = false;
    }
    
    FollowPath(currentPath);
}
```

## Best Practices

### ✅ Do This

- **Stuck detection** — Monitor position history
- **Path smoothing** — Line-of-sight optimization
- **Chunk caching** — Keep frequently used meshes in memory
- **Fallback paths** — Direct movement if pathfinding fails
- **Async pathfinding** — Don't block main thread
- **Log path failures** — Debug missing mesh data
- **Waypoint tolerance** — 2-5 yard radius (not exact)

### ❌ Avoid This

- **Synchronous pathfinding** — Freezes bot during calculation
- **No stuck recovery** — Bot stuck permanently
- **Exact waypoint matching** — Never reaches destination
- **Ignoring path failures** — Silent errors
- **No mesh data validation** — Crashes on corrupt files
- **Pathfinding every frame** — Expensive operation

## Integration with Other Skills

**→ context-scout** — Find pathfinding code locations
**→ performance-profiler** — Profile pathfinding performance
**→ goap-designer** — Integrate with PathGoal
**→ code-reviewer** — Review pathfinding logic

---

**Remember:** Pathfinding is complex. Start with simple direct movement, then add sophistication (triangle mesh, smoothing, stuck recovery) as needed.
