# Research Features - Implementation Task Breakdown

## Actionable Tasks with File Paths, Code Changes, and Verification

**Version:** 1.0  
**Date:** February 5, 2026  
**Source:** RESEARCH_SYNTHESIS_IMPLEMENTATION_PLANS.md

---

## Task Organization

Tasks are grouped by PRD and ordered by implementation phase. Each task includes:
- Specific file paths
- Code changes with context
- Verification commands
- Acceptance criteria

---

## Phase A: Performance Foundation (Week 1)

### PRD-4: Object Caching

#### Task A-1: Create LRU Cache Implementation

**Files to Create:**
- `Core/Performance/LRUCache.cs`

**Code:**
```csharp
// Core/Performance/LRUCache.cs
namespace Core.Performance;

using System.Collections.Concurrent;

/// <summary>
/// LRU (Least Recently Used) cache with TTL support.
/// Thread-safe implementation for hot-path usage.
/// </summary>
public sealed class LRUCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly TimeSpan _defaultTtl;
    private readonly Dictionary<TKey, LinkedListNode<CacheEntry>> _cache;
    private readonly LinkedList<CacheEntry> _lruList;
    private readonly object _lock = new();
    
    private long _hits;
    private long _misses;
    
    public LRUCache(int capacity, TimeSpan defaultTtl)
    {
        _capacity = capacity;
        _defaultTtl = defaultTtl;
        _cache = new Dictionary<TKey, LinkedListNode<CacheEntry>>(capacity);
        _lruList = new LinkedList<CacheEntry>();
    }
    
    public TValue GetOrAdd(TKey key, Func<TValue> factory, TimeSpan? ttl = null)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out LinkedListNode<CacheEntry>? node))
            {
                if (DateTime.UtcNow < node.Value.ExpiresAt)
                {
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    Interlocked.Increment(ref _hits);
                    return node.Value.Value;
                }
                
                _lruList.Remove(node);
                _cache.Remove(key);
            }
            
            Interlocked.Increment(ref _misses);
            
            TValue value = factory();
            DateTime expiresAt = DateTime.UtcNow + (ttl ?? _defaultTtl);
            
            CacheEntry entry = new(key, value, expiresAt);
            LinkedListNode<CacheEntry> newNode = _lruList.AddFirst(entry);
            _cache[key] = newNode;
            
            while (_cache.Count > _capacity)
            {
                LinkedListNode<CacheEntry>? lru = _lruList.Last;
                if (lru != null)
                {
                    _lruList.RemoveLast();
                    _cache.Remove(lru.Value.Key);
                }
            }
            
            return value;
        }
    }
    
    public bool TryGet(TKey key, out TValue? value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out LinkedListNode<CacheEntry>? node) &&
                DateTime.UtcNow < node.Value.ExpiresAt)
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                Interlocked.Increment(ref _hits);
                value = node.Value.Value;
                return true;
            }
        }
        
        Interlocked.Increment(ref _misses);
        value = default;
        return false;
    }
    
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
        }
    }
    
    public int Count
    {
        get { lock (_lock) return _cache.Count; }
    }
    
    public double HitRate => _hits + _misses == 0 ? 0 : (double)_hits / (_hits + _misses);
    
    public (long Hits, long Misses) GetStatistics() => (_hits, _misses);
    
    private readonly record struct CacheEntry(TKey Key, TValue Value, DateTime ExpiresAt);
}
```

**Verification:**
```bash
dotnet build Core/Core.csproj
```

**Acceptance Criteria:**
- [ ] Compiles without errors
- [ ] Thread-safe operations verified via code review
- [ ] No allocations after cache warmup

---

#### Task A-2: Add Cache Service Extensions

**Files to Create:**
- `Core/Performance/CacheServiceExtensions.cs`

**Code:**
```csharp
// Core/Performance/CacheServiceExtensions.cs
namespace Core.Performance;

using Microsoft.Extensions.DependencyInjection;

public static class CacheServiceExtensions
{
    public static IServiceCollection AddCachingServices(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            IOptions<FeatureFlagsOptions> options = 
                sp.GetRequiredService<IOptions<FeatureFlagsOptions>>();
            return new LRUCache<string, object>(
                options.Value.ObjectCaching.MaxCacheSize,
                TimeSpan.FromMilliseconds(options.Value.ObjectCaching.DefaultTTLMs));
        });
        
        return services;
    }
}
```

**Verification:**
```bash
dotnet build Core/Core.csproj
```

---

### PRD-5: Screen Capture Optimization

#### Task A-3: Create Texture Pool

**Files to Create:**
- `Core/WoWScreen/TexturePool.cs`

**Note:** This requires Vortice.Direct3D11 references already present in the project.

**Verification:**
```bash
dotnet build Core/Core.csproj
```

---

## Phase B: Humanization System (Week 2-3)

### PRD-1: Humanization

#### Task B-1: Create FastNoise Perlin Implementation

**Files to Create:**
- `Core/Humanization/FastNoise.cs`

**Source:** MIT-licensed FastNoise implementation (https://github.com/Auburn/FastNoise)

**Code (simplified excerpt):**
```csharp
// Core/Humanization/FastNoise.cs
namespace Core.Humanization;

/// <summary>
/// Fast Perlin noise generator.
/// MIT License - Auburn/FastNoise
/// </summary>
public sealed class FastNoise
{
    private readonly int _seed;
    private float _frequency = 0.01f;
    
    public FastNoise(int seed = 1337)
    {
        _seed = seed;
    }
    
    public void SetFrequency(float frequency) => _frequency = frequency;
    
    public float GetNoise(float x, float y, float z)
    {
        x *= _frequency;
        y *= _frequency;
        z *= _frequency;
        
        return SinglePerlin(_seed, x, y, z);
    }
    
    private static float SinglePerlin(int seed, float x, float y, float z)
    {
        int x0 = FastFloor(x);
        int y0 = FastFloor(y);
        int z0 = FastFloor(z);
        
        float xd0 = x - x0;
        float yd0 = y - y0;
        float zd0 = z - z0;
        float xd1 = xd0 - 1;
        float yd1 = yd0 - 1;
        float zd1 = zd0 - 1;
        
        float xs = InterpQuintic(xd0);
        float ys = InterpQuintic(yd0);
        float zs = InterpQuintic(zd0);
        
        x0 *= PrimeX;
        y0 *= PrimeY;
        z0 *= PrimeZ;
        int x1 = x0 + PrimeX;
        int y1 = y0 + PrimeY;
        int z1 = z0 + PrimeZ;
        
        float xf00 = Lerp(GradCoord(seed, x0, y0, z0, xd0, yd0, zd0), 
                         GradCoord(seed, x1, y0, z0, xd1, yd0, zd0), xs);
        float xf10 = Lerp(GradCoord(seed, x0, y1, z0, xd0, yd1, zd0), 
                         GradCoord(seed, x1, y1, z0, xd1, yd1, zd0), xs);
        float xf01 = Lerp(GradCoord(seed, x0, y0, z1, xd0, yd0, zd1), 
                         GradCoord(seed, x1, y0, z1, xd1, yd0, zd1), xs);
        float xf11 = Lerp(GradCoord(seed, x0, y1, z1, xd0, yd1, zd1), 
                         GradCoord(seed, x1, y1, z1, xd1, yd1, zd1), xs);
        
        float yf0 = Lerp(xf00, xf10, ys);
        float yf1 = Lerp(xf01, xf11, ys);
        
        return Lerp(yf0, yf1, zs);
    }
    
    private const int PrimeX = 501125321;
    private const int PrimeY = 1136930381;
    private const int PrimeZ = 1720413743;
    
    private static int FastFloor(float f) => f >= 0 ? (int)f : (int)f - 1;
    private static float Lerp(float a, float b, float t) => a + t * (b - a);
    private static float InterpQuintic(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    
    private static float GradCoord(int seed, int xPrimed, int yPrimed, int zPrimed, 
        float xd, float yd, float zd)
    {
        int hash = Hash(seed, xPrimed, yPrimed, zPrimed);
        hash ^= hash >> 15;
        hash &= 15;
        
        float u = hash < 8 ? xd : yd;
        float v = hash < 4 ? yd : (hash == 12 || hash == 14 ? xd : zd);
        
        return ((hash & 1) == 0 ? u : -u) + ((hash & 2) == 0 ? v : -v);
    }
    
    private static int Hash(int seed, int xPrimed, int yPrimed, int zPrimed)
    {
        int hash = seed ^ xPrimed ^ yPrimed ^ zPrimed;
        hash *= 0x27d4eb2d;
        return hash;
    }
}
```

**Verification:**
```bash
dotnet build Core/Core.csproj
```

---

#### Task B-2: Create Movement Humanizer

**Files to Create:**
- `Core/Humanization/MovementHumanizer.cs`

**Implementation:** See PRD-1 Section 3.5.1 in RESEARCH_SYNTHESIS_IMPLEMENTATION_PLANS.md

**Verification:**
```bash
dotnet build Core/Core.csproj
dotnet run --project Benchmarks -c Release -- --filter "*MovementHumanizer*"
```

---

#### Task B-3: Create Timing Humanizer

**Files to Create:**
- `Core/Humanization/TimingHumanizer.cs`

**Implementation:** See PRD-1 Section 3.5.2

**Verification:**
```bash
dotnet build Core/Core.csproj
```

---

#### Task B-4: Create Idle Behavior Injector

**Files to Create:**
- `Core/Humanization/IdleBehaviorInjector.cs`

**Implementation:** See PRD-1 Section 3.5.3

**Verification:**
```bash
dotnet build Core/Core.csproj
```

---

#### Task B-5: Create Mouse Humanizer

**Files to Create:**
- `Core/Humanization/MouseHumanizer.cs`

**Implementation:** See PRD-1 Section 3.5.4

**Verification:**
```bash
dotnet build Core/Core.csproj
```

---

#### Task B-6: Create Humanization Service Extensions

**Files to Create:**
- `Core/Humanization/HumanizationServiceExtensions.cs`

**Code:**
```csharp
// Core/Humanization/HumanizationServiceExtensions.cs
namespace Core.Humanization;

using Microsoft.Extensions.DependencyInjection;

public static class HumanizationServiceExtensions
{
    public static IServiceCollection AddHumanizationServices(this IServiceCollection services)
    {
        services.AddSingleton<MovementHumanizer>();
        services.AddSingleton<TimingHumanizer>();
        services.AddSingleton<IdleBehaviorInjector>();
        
        return services;
    }
}
```

---

#### Task B-7: Integrate Humanization into Navigation

**Files to Modify:**
- `Core/GoalsComponent/Navigation.cs`

**Change Location:** Method that sets waypoint destination

**Change:**
```csharp
// Before:
public void SetWaypoint(Vector3 waypoint)
{
    _currentWaypoint = waypoint;
    // ... existing code
}

// After:
public void SetWaypoint(Vector3 waypoint)
{
    if (_featureFlags.Humanization.Enabled)
    {
        waypoint = _movementHumanizer.HumanizePosition(waypoint, _deltaTime);
    }
    
    _currentWaypoint = waypoint;
    // ... existing code
}
```

**Verification:**
```bash
dotnet build Core/Core.csproj
```

---

#### Task B-8: Integrate Humanization into Input

**Files to Modify:**
- `Core/Input/ConfigurableInput.cs`

**Change:** Add timing delays before key presses

```csharp
// Add to constructor:
private readonly TimingHumanizer? _timingHumanizer;

// Add to key press method:
public async Task PressKeyAsync(ConsoleKey key, CancellationToken ct = default)
{
    if (_featureFlags.Humanization.Enabled && _timingHumanizer != null)
    {
        int delay = _timingHumanizer.GetActionDelay();
        await Task.Delay(delay, ct);
    }
    
    // ... existing key press logic
}
```

---

## Phase C: GOAP Enhancements (Week 4)

### PRD-2: GOAP Utility Scoring

#### Task C-1: Create Utility Scorer

**Files to Create:**
- `Core/GOAP/GoapUtilityScorer.cs`

**Implementation:** See PRD-2 Section 4.3.1

---

#### Task C-2: Create Plan Cache

**Files to Create:**
- `Core/GOAP/GoapPlanCache.cs`

**Implementation:** See PRD-2 Section 4.3.2

---

#### Task C-3: Create Execution History Tracker

**Files to Create:**
- `Core/GOAP/GoalExecutionHistory.cs`

**Code:**
```csharp
// Core/GOAP/GoalExecutionHistory.cs
namespace Core.GOAP;

public sealed class GoalExecutionHistory
{
    private readonly ConcurrentDictionary<string, DateTime> _lastExecution = new();
    
    public void RecordExecution(string goalName)
    {
        _lastExecution[goalName] = DateTime.UtcNow;
    }
    
    public float GetSecondsSince(string goalName)
    {
        if (_lastExecution.TryGetValue(goalName, out DateTime last))
        {
            return (float)(DateTime.UtcNow - last).TotalSeconds;
        }
        return float.MaxValue;
    }
}
```

---

#### Task C-4: Integrate with GoapAgent

**Files to Modify:**
- `Core/GOAP/GoapAgent.cs`

**Changes:**
1. Inject `GoapPlanCache` and `GoapUtilityScorer`
2. Check cache before planning
3. Apply utility scores before goal selection

---

## Phase D: Advanced Navigation (Week 5-6)

### PRD-7: Navigation Features

#### Task D-1: Create Path Cache

**Files to Create:**
- `Core/Navigation/PathCache.cs`

**Implementation:** See PRD-7 Section 9.2.1

---

#### Task D-2: Integrate Path Cache into Navigation

**Files to Modify:**
- `Core/GoalsComponent/Navigation.cs`

**Changes:**
1. Check cache before requesting path
2. Cache successful paths

---

## Phase E: Behavior Tree System (Week 7-9)

### PRD-3: GOBT Hybrid

#### Task E-1: Create Behavior Node Interface

**Files to Create:**
- `Core/BehaviorTree/IBehaviorNode.cs`

**Implementation:** See PRD-3 Section 5.3.1

---

#### Task E-2: Create Behavior Context

**Files to Create:**
- `Core/BehaviorTree/BehaviorContext.cs`

**Implementation:** See PRD-3 Section 5.3.1

---

#### Task E-3: Create Selector Node

**Files to Create:**
- `Core/BehaviorTree/Nodes/SelectorNode.cs`

**Implementation:** See PRD-3 Section 5.3.2

---

#### Task E-4: Create Sequence Node

**Files to Create:**
- `Core/BehaviorTree/Nodes/SequenceNode.cs`

**Implementation:** See PRD-3 Section 5.3.2

---

#### Task E-5: Create Condition Node

**Files to Create:**
- `Core/BehaviorTree/Nodes/ConditionNode.cs`

**Implementation:** See PRD-3 Section 5.3.3

---

#### Task E-6: Create Cast Spell Node

**Files to Create:**
- `Core/BehaviorTree/Nodes/CastSpellNode.cs`

**Implementation:** See PRD-3 Section 5.3.3

---

#### Task E-7: Create GOAP Planner Node

**Files to Create:**
- `Core/BehaviorTree/Nodes/GoapPlannerNode.cs`

**Implementation:** See PRD-3 Section 5.3.3

---

#### Task E-8: Create JSON to BT Converter

**Files to Create:**
- `Core/BehaviorTree/JsonToBehaviorTreeConverter.cs`

**Implementation:** See PRD-3 Section 5.3.4

---

#### Task E-9: Create Behavior Tree Executor

**Files to Create:**
- `Core/BehaviorTree/BehaviorTreeExecutor.cs`

**Code:**
```csharp
// Core/BehaviorTree/BehaviorTreeExecutor.cs
namespace Core.BehaviorTree;

public sealed class BehaviorTreeExecutor
{
    private readonly ILogger<BehaviorTreeExecutor> _logger;
    private readonly IBehaviorNode _root;
    private readonly BehaviorContext _context;
    
    public BehaviorTreeExecutor(
        ILogger<BehaviorTreeExecutor> logger,
        IBehaviorNode root,
        BehaviorContext context)
    {
        _logger = logger;
        _root = root;
        _context = context;
    }
    
    public NodeStatus Tick()
    {
        try
        {
            return _root.Execute(_context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BehaviorTree     ] Execution failed");
            return NodeStatus.Failure;
        }
    }
    
    public void Reset() => _root.Reset();
}
```

---

#### Task E-10: Integrate BT into CombatGoal (Optional)

**Files to Modify:**
- `Core/Goals/CombatGoal.cs`

**Changes:**
1. Add feature flag check for GOBT mode
2. If enabled, use BehaviorTreeExecutor instead of normal rotation

---

## Feature Flag Configuration Update

### Task FF-1: Update Feature Flags Options

**Files to Modify:**
- `Core/FeatureFlags/FeatureFlagsOptions.cs`

**Add the following option classes:**

```csharp
public sealed class HumanizationOptions
{
    public bool Enabled { get; set; }
    public float MovementNoiseAmplitude { get; set; } = 0.3f;
    public int ActionDelayMinMs { get; set; } = 50;
    public int ActionDelayMaxMs { get; set; } = 200;
    public float IdleInjectionChance { get; set; } = 0.05f;
    public int IdleMinDurationMs { get; set; } = 2000;
    public int IdleMaxDurationMs { get; set; } = 8000;
}

public sealed class GOAPUtilityScoringOptions
{
    public bool Enabled { get; set; }
    public int PlanCacheTTLSeconds { get; set; } = 5;
    public float UtilityDecayFactor { get; set; } = 0.95f;
}

public sealed class GOBTHybridOptions
{
    public bool Enabled { get; set; }
    public bool FallbackToGOAP { get; set; } = true;
    public int MaxTreeDepth { get; set; } = 20;
}

public sealed class ObjectCachingOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxCacheSize { get; set; } = 100;
    public int DefaultTTLMs { get; set; } = 500;
}

public sealed class AdvancedNavigationOptions
{
    public bool Enabled { get; set; }
    public bool DynamicObstacleAvoidance { get; set; }
    public bool TileStreaming { get; set; } = true;
    public int PathCacheMaxSize { get; set; } = 50;
}
```

---

## Verification Checklist

### Full Build Verification

```bash
# Build entire solution
dotnet build MasterOfPuppets.sln

# Run all tests
dotnet test

# Run benchmarks for new features
dotnet run --project Benchmarks -c Release -- --filter "*Cache*|*Humanization*|*GOAP*"
```

### Feature-Specific Verification

| Feature | Test Filter | Benchmark Filter |
|---------|-------------|------------------|
| LRU Cache | `*LRUCache*` | `*Cache*` |
| Humanization | `*Humanization*` | `*Movement*\|*Timing*` |
| GOAP Utility | `*GOAPUtility*` | `*GoapPlan*` |
| Path Cache | `*PathCache*` | `*Path*` |
| Behavior Tree | `*BehaviorTree*` | `*BT*` |

---

## Summary of Files to Create

```
Core/
├── Humanization/
│   ├── FastNoise.cs
│   ├── MovementHumanizer.cs
│   ├── TimingHumanizer.cs
│   ├── IdleBehaviorInjector.cs
│   ├── MouseHumanizer.cs
│   └── HumanizationServiceExtensions.cs
├── Performance/
│   ├── LRUCache.cs
│   └── CacheServiceExtensions.cs
├── GOAP/
│   ├── GoapUtilityScorer.cs
│   ├── GoapPlanCache.cs
│   └── GoalExecutionHistory.cs
├── Navigation/
│   └── PathCache.cs
├── BehaviorTree/
│   ├── IBehaviorNode.cs
│   ├── BehaviorContext.cs
│   ├── BehaviorTreeExecutor.cs
│   ├── JsonToBehaviorTreeConverter.cs
│   └── Nodes/
│       ├── SelectorNode.cs
│       ├── SequenceNode.cs
│       ├── ConditionNode.cs
│       ├── CastSpellNode.cs
│       ├── GoapPlannerNode.cs
│       └── WaitNode.cs
└── WoWScreen/
    └── TexturePool.cs
```

## Summary of Files to Modify

| File | Changes | Phase |
|------|---------|-------|
| `Core/FeatureFlags/FeatureFlagsOptions.cs` | Add new option classes | A |
| `Core/GoalsComponent/Navigation.cs` | Humanization + Path Cache | B, D |
| `Core/Input/ConfigurableInput.cs` | Timing humanization | B |
| `Core/GOAP/GoapAgent.cs` | Utility scoring + Plan cache | C |
| `Core/Goals/CombatGoal.cs` | Optional BT integration | E |
| `BlazorServer/runtime_feature_flags.json` | New feature flags | A |
| `BlazorServer/Program.cs` | Service registration | A-E |

---

**Document Status:** Complete Task Breakdown  
**Last Updated:** February 5, 2026  
**Total Tasks:** 28  
**Estimated Effort:** 297+ hours
