# Research Synthesis: Production-Ready Implementation Plans

## Comprehensive Feature Enhancement Specifications

**Version:** 1.0  
**Date:** February 5, 2026  
**Status:** Production-Ready PRDs  
**Scope:** Features derived from cross-repository analysis and web research

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Cross-Cutting Guardrails](#2-cross-cutting-guardrails)
3. [PRD-1: Humanization & Anti-Detection System](#3-prd-1-humanization--anti-detection-system)
4. [PRD-2: Enhanced GOAP with Utility Scoring](#4-prd-2-enhanced-goap-with-utility-scoring)
5. [PRD-3: GOBT Hybrid Combat System](#5-prd-3-gobt-hybrid-combat-system)
6. [PRD-4: Object Caching & Performance Layer](#6-prd-4-object-caching--performance-layer)
7. [PRD-5: Screen Capture Optimization](#7-prd-5-screen-capture-optimization)
8. [PRD-6: Multi-Bot Coordination](#8-prd-6-multi-bot-coordination)
9. [PRD-7: Advanced Navigation Features](#9-prd-7-advanced-navigation-features)
10. [Integration Roadmap](#10-integration-roadmap)
11. [Compatibility Matrix](#11-compatibility-matrix)
12. [Security & Risk Matrix](#12-security--risk-matrix)
13. [Testing Protocols](#13-testing-protocols)
14. [Rollback Procedures](#14-rollback-procedures)

---

## 1. Executive Summary

This document synthesizes findings from comprehensive cross-repository analysis (AmeisenBotX family, LLM-driven variants) and web research (GOAP optimization, behavior trees, Recast/Detour, screen capture, bot detection) into production-ready implementation plans.

### Research Sources Analyzed

| Source | Category | Key Findings Extracted |
|--------|----------|----------------------|
| Jnnshschl/AmeisenBotX | Architecture | Behavior trees, memory caching, object pooling |
| noisiver/AmeisenBotX | Performance | LRU cache, threaded pathfinding, 30% CPU reduction |
| descention/AmeisenBotX | Multi-boxing | Formation movement, leader-follower system |
| bizkut/AmeisenBotX | LLM Integration | Hybrid decision engine, prompt patterns |
| Academic Papers | Bot Detection | Trajectory analysis (95% accuracy), behavioral patterns |
| Mountain GOAP / ReGoap | GOAP | Utility scoring, plan caching, performance benchmarks |
| D3DShot / DXGI Research | Screen Capture | Texture pooling, 60 FPS optimization |
| Red Blob Games | Pathfinding | A* with dynamic costs, priority queue patterns |

### Feature Priority Matrix

| PRD | Feature | Complexity | Impact | Risk | Detection Risk |
|-----|---------|------------|--------|------|----------------|
| **PRD-1** | Humanization System | Medium | High | Low | **Reduces** |
| **PRD-2** | GOAP Utility Scoring | Medium | Medium | Low | None |
| **PRD-3** | GOBT Hybrid Combat | High | Medium | Medium | None |
| **PRD-4** | Object Caching | Low | High | Low | None |
| **PRD-5** | Screen Capture Opt | Low | Medium | Low | None |
| **PRD-6** | Multi-Bot Coordination | Very High | Medium | High | Increases |
| **PRD-7** | Advanced Navigation | Medium | High | Low | None |

### Backwards Compatibility Commitment

All features implement:
- ✅ Feature flags with default disabled (except performance optimizations)
- ✅ Zero changes to existing JSON schemas
- ✅ Additive-only API extensions
- ✅ Automatic graceful degradation on failure
- ✅ Hot-reload capability without restart

---

## 2. Cross-Cutting Guardrails

### 2.1 Feature Flag Additions

**File:** `BlazorServer/runtime_feature_flags.json`

```json
{
  "Features": {
    "Humanization": {
      "Enabled": false,
      "MovementNoiseAmplitude": 0.3,
      "ActionDelayMinMs": 50,
      "ActionDelayMaxMs": 200,
      "IdleInjectionChance": 0.05,
      "IdleMinDurationMs": 2000,
      "IdleMaxDurationMs": 8000
    },
    "GOAPUtilityScoring": {
      "Enabled": false,
      "PlanCacheTTLSeconds": 5,
      "UtilityDecayFactor": 0.95
    },
    "GOBTHybrid": {
      "Enabled": false,
      "FallbackToGOAP": true,
      "MaxTreeDepth": 20
    },
    "ObjectCaching": {
      "Enabled": true,
      "MaxCacheSize": 100,
      "DefaultTTLMs": 500
    },
    "ScreenCaptureOptimization": {
      "Enabled": true,
      "TexturePoolSize": 3,
      "UseRegionCapture": false
    },
    "MultiBotCoordination": {
      "Enabled": false,
      "FormationType": "VFormation",
      "FormationSpacing": 5.0
    },
    "AdvancedNavigation": {
      "Enabled": false,
      "DynamicObstacleAvoidance": false,
      "TileStreaming": true,
      "PathCacheMaxSize": 50
    }
  }
}
```

### 2.2 Monitoring Thresholds

| Metric | Warning | Critical | Auto-Action |
|--------|---------|----------|-------------|
| `humanization_overhead_ms` | >50 | >100 | Reduce noise amplitude |
| `goap_plan_cache_hit_rate` | <50% | <20% | Increase TTL |
| `gobt_tree_depth_exceeded` | 15 | 20 | Force GOAP fallback |
| `object_cache_eviction_rate` | >100/s | >500/s | Increase cache size |
| `screen_capture_fps` | <30 | <15 | Disable optional features |
| `multibot_sync_latency_ms` | >500 | >1000 | Reduce formation complexity |

### 2.3 Validation Checkpoint Interface

```csharp
// Core/Validation/ResearchFeatureValidation.cs
namespace Core.Validation;

public interface IFeatureValidationCheckpoint
{
    string FeatureName { get; }
    ValidationSeverity Severity { get; }
    ValidationResult Validate(IServiceProvider services);
}

public enum ValidationSeverity { Info, Warning, Error, Critical }

public record ValidationResult(
    bool IsValid,
    string Message,
    Dictionary<string, object>? Diagnostics = null,
    ValidationSeverity OverrideSeverity = ValidationSeverity.Error);

// Example implementation for Humanization
public sealed class HumanizationValidation : IFeatureValidationCheckpoint
{
    public string FeatureName => "Humanization";
    public ValidationSeverity Severity => ValidationSeverity.Warning;
    
    public ValidationResult Validate(IServiceProvider services)
    {
        var flags = services.GetRequiredService<IOptions<FeatureFlagsOptions>>().Value;
        
        if (flags.Humanization.ActionDelayMaxMs > 500)
            return new(false, "Action delay too high - will impact DPS",
                new() { ["MaxDelay"] = flags.Humanization.ActionDelayMaxMs });
        
        if (flags.Humanization.MovementNoiseAmplitude > 1.0f)
            return new(false, "Movement noise too high - may cause path failures");
        
        return new(true, "Humanization parameters within safe limits");
    }
}
```

---

## 3. PRD-1: Humanization & Anti-Detection System

### 3.1 Executive Summary

Implement bot behavior humanization to reduce detection risk through trajectory noise injection, timing randomization, and idle behavior simulation. Based on academic research showing 95% bot detection accuracy via trajectory analysis (Sinica 2008) and 96% via behavioral patterns (Springer 2016).

### 3.2 User Stories

| ID | Story | Acceptance Criteria |
|----|-------|---------------------|
| US-H1 | As a user, I want movement to appear more natural | Movement includes Perlin noise with configurable amplitude |
| US-H2 | As a user, I want varied action timing | Key presses have 50-200ms random delay variance |
| US-H3 | As a user, I want occasional idle periods | 5% chance per decision of 2-8 second pause |
| US-H4 | As a user, I want mouse movements to follow curves | Bézier curve interpolation for NpcNameFinder clicks |
| US-H5 | As a user, I want humanization to be opt-in | Feature flag disabled by default |

### 3.3 Functional Requirements

| ID | Requirement | Priority | Notes |
|----|-------------|----------|-------|
| FR-H1 | Perlin noise injection for navigation waypoints | P0 | Amplitude 0-1 world units |
| FR-H2 | Random delay before key actions | P0 | Gaussian distribution 50-200ms |
| FR-H3 | Idle behavior injection | P1 | Configurable frequency |
| FR-H4 | Bézier curve mouse movement | P1 | Only for NpcNameFinder |
| FR-H5 | Key press duration variation | P1 | 40-120ms press duration |
| FR-H6 | Occasional camera movement | P2 | Simulate looking around |

### 3.4 Non-Functional Requirements

| ID | Requirement | Target | Validation |
|----|-------------|--------|------------|
| NFR-H1 | Overhead per frame | <5ms | Benchmark test |
| NFR-H2 | Memory footprint | <1MB | Profiler verification |
| NFR-H3 | No combat impact | DPS regression <2% | Combat simulation test |
| NFR-H4 | Thread-safe RNG | Lock-free | Code review |

### 3.5 Technical Specification

#### 3.5.1 Perlin Noise Movement

**Source:** Academic research on trajectory analysis (https://homepage.iis.sinica.edu.tw/~swc/pub/bot_detection_trajectory.html)

**Algorithm:**
```csharp
// Core/Humanization/MovementHumanizer.cs
namespace Core.Humanization;

/// <summary>
/// Applies Perlin noise to navigation waypoints to create natural movement patterns.
/// Based on trajectory analysis research showing bots follow unnaturally straight lines.
/// </summary>
public sealed class MovementHumanizer
{
    private readonly ILogger<MovementHumanizer> _logger;
    private readonly FastNoise _noise;
    private readonly float _amplitude;
    private double _timeAccumulator;
    
    public MovementHumanizer(
        ILogger<MovementHumanizer> logger,
        IOptions<FeatureFlagsOptions> options)
    {
        _logger = logger;
        _amplitude = options.Value.Humanization.MovementNoiseAmplitude;
        _noise = new FastNoise(seed: Environment.TickCount);
        _noise.SetNoiseType(FastNoise.NoiseType.Perlin);
        _noise.SetFrequency(0.5f); // Low frequency for smooth curves
    }
    
    /// <summary>
    /// Adds human-like drift to a target position.
    /// </summary>
    /// <param name="targetPosition">Original navigation target</param>
    /// <param name="deltaTime">Time since last call (for temporal coherence)</param>
    /// <returns>Humanized position with natural drift</returns>
    public Vector3 HumanizePosition(Vector3 targetPosition, float deltaTime)
    {
        if (_amplitude <= 0f) return targetPosition;
        
        _timeAccumulator += deltaTime;
        
        // Use time as Z-coordinate for temporal coherence
        float noiseX = _noise.GetNoise(
            targetPosition.X * 0.1f, 
            targetPosition.Z * 0.1f, 
            (float)_timeAccumulator);
        float noiseZ = _noise.GetNoise(
            targetPosition.Z * 0.1f, 
            targetPosition.X * 0.1f, 
            (float)_timeAccumulator + 1000f);
        
        return new Vector3(
            targetPosition.X + noiseX * _amplitude,
            targetPosition.Y, // Never modify Y (height)
            targetPosition.Z + noiseZ * _amplitude);
    }
}
```

#### 3.5.2 Action Timing Randomization

**Source:** Behavioral analysis research (https://link.springer.com/article/10.1186/s40064-016-2122-8)

```csharp
// Core/Humanization/TimingHumanizer.cs
namespace Core.Humanization;

/// <summary>
/// Provides humanized timing for actions to avoid detection through timing analysis.
/// Uses Gaussian distribution centered on human reaction time (200-300ms).
/// </summary>
public sealed class TimingHumanizer
{
    private readonly Random _random;
    private readonly int _minDelay;
    private readonly int _maxDelay;
    
    public TimingHumanizer(IOptions<FeatureFlagsOptions> options)
    {
        // Thread-local random for thread safety without locks
        _random = Random.Shared;
        _minDelay = options.Value.Humanization.ActionDelayMinMs;
        _maxDelay = options.Value.Humanization.ActionDelayMaxMs;
    }
    
    /// <summary>
    /// Gets a humanized delay using Gaussian distribution.
    /// </summary>
    /// <returns>Delay in milliseconds</returns>
    public int GetActionDelay()
    {
        // Box-Muller transform for Gaussian distribution
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        double gaussian = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        
        // Scale to desired range (mean at center, stddev = (max-min)/4)
        double mean = (_minDelay + _maxDelay) / 2.0;
        double stddev = (_maxDelay - _minDelay) / 4.0;
        int delay = (int)(mean + gaussian * stddev);
        
        return Math.Clamp(delay, _minDelay, _maxDelay);
    }
    
    /// <summary>
    /// Gets a randomized key press duration.
    /// Humans press keys for 40-120ms typically.
    /// </summary>
    public int GetKeyPressDuration()
    {
        return _random.Next(40, 120);
    }
}
```

#### 3.5.3 Idle Behavior Injection

```csharp
// Core/Humanization/IdleBehaviorInjector.cs
namespace Core.Humanization;

/// <summary>
/// Injects occasional idle periods to simulate human player behavior.
/// Bots are detected by continuous action patterns - humans take breaks.
/// </summary>
public sealed class IdleBehaviorInjector
{
    private readonly ILogger<IdleBehaviorInjector> _logger;
    private readonly float _injectionChance;
    private readonly int _minDuration;
    private readonly int _maxDuration;
    private readonly IConfigurableInput _input;
    
    private DateTime _lastIdle = DateTime.MinValue;
    private readonly TimeSpan _minIdleInterval = TimeSpan.FromMinutes(5);
    
    public IdleBehaviorInjector(
        ILogger<IdleBehaviorInjector> logger,
        IOptions<FeatureFlagsOptions> options,
        IConfigurableInput input)
    {
        _logger = logger;
        _injectionChance = options.Value.Humanization.IdleInjectionChance;
        _minDuration = options.Value.Humanization.IdleMinDurationMs;
        _maxDuration = options.Value.Humanization.IdleMaxDurationMs;
        _input = input;
    }
    
    /// <summary>
    /// Determines if idle behavior should be injected and executes it.
    /// </summary>
    /// <returns>True if idle pause occurred</returns>
    public async Task<bool> TryInjectIdleAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow - _lastIdle < _minIdleInterval)
            return false;
        
        if (Random.Shared.NextDouble() >= _injectionChance)
            return false;
        
        int duration = Random.Shared.Next(_minDuration, _maxDuration);
        
        _logger.LogDebug("[IdleBehavior     ] Injecting {Duration}ms idle pause", duration);
        
        // Optionally look around during idle
        if (Random.Shared.NextDouble() < 0.3)
        {
            await SimulateLookAroundAsync(ct);
        }
        
        await Task.Delay(duration, ct);
        _lastIdle = DateTime.UtcNow;
        
        return true;
    }
    
    private async Task SimulateLookAroundAsync(CancellationToken ct)
    {
        // Move mouse slightly to simulate looking around
        int deltaX = Random.Shared.Next(-100, 100);
        int deltaY = Random.Shared.Next(-50, 50);
        
        await _input.MoveMouseRelativeAsync(deltaX, deltaY, ct);
        await Task.Delay(Random.Shared.Next(200, 500), ct);
    }
}
```

#### 3.5.4 Bézier Curve Mouse Movement

```csharp
// Core/Humanization/MouseHumanizer.cs
namespace Core.Humanization;

/// <summary>
/// Provides humanized mouse movement using cubic Bézier curves.
/// Humans don't move mice in straight lines.
/// </summary>
public static class MouseHumanizer
{
    /// <summary>
    /// Generates a humanized mouse path between two points.
    /// </summary>
    /// <param name="start">Starting screen position</param>
    /// <param name="end">Target screen position</param>
    /// <param name="steps">Number of intermediate points</param>
    /// <returns>Sequence of points forming a natural curve</returns>
    public static IEnumerable<Point> GetBezierPath(Point start, Point end, int steps = 20)
    {
        // Random control points for natural curve
        Point control1 = new(
            start.X + Random.Shared.Next(-50, 50),
            start.Y + Random.Shared.Next(-50, 50));
        Point control2 = new(
            end.X + Random.Shared.Next(-50, 50),
            end.Y + Random.Shared.Next(-50, 50));
        
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            yield return CubicBezier(start, control1, control2, end, t);
        }
    }
    
    private static Point CubicBezier(Point p0, Point p1, Point p2, Point p3, double t)
    {
        double u = 1 - t;
        double tt = t * t;
        double uu = u * u;
        double uuu = uu * u;
        double ttt = tt * t;
        
        int x = (int)(uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X);
        int y = (int)(uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y);
        
        return new Point(x, y);
    }
}
```

### 3.6 Integration Points

| File | Change | Risk |
|------|--------|------|
| `Core/GoalsComponent/Navigation.cs` | Wrap `SetWaypoint()` with `MovementHumanizer` | Low |
| `Core/Input/ConfigurableInput.cs` | Add `TimingHumanizer` delays | Low |
| `SharedLib/NpcFinder/NpcNameTargeting.cs` | Use `MouseHumanizer` for clicks | Low |
| `Core/Goals/CombatGoal.cs` | Inject `IdleBehaviorInjector` between pulls | Low |

### 3.7 Files to Create

```
Core/Humanization/
├── MovementHumanizer.cs
├── TimingHumanizer.cs
├── IdleBehaviorInjector.cs
├── MouseHumanizer.cs
├── FastNoise.cs (MIT-licensed Perlin implementation)
└── HumanizationServiceExtensions.cs
```

### 3.8 Verification

```bash
# Build and verify no errors
dotnet build MasterOfPuppets.sln

# Run benchmarks
dotnet run --project Benchmarks -c Release -- --filter "*Humanization*"

# Acceptance criteria:
# - MovementHumanizer.HumanizePosition < 0.1ms per call
# - TimingHumanizer.GetActionDelay < 0.01ms per call
# - No allocations in hot paths
```

### 3.9 Rollback

**Immediate:** Set `Humanization.Enabled = false` in runtime config.

**Code Rollback:** All humanization is applied through optional wrapper calls; removing integration points restores original behavior.

**Data Migration:** None required - no persistent state.

---

## 4. PRD-2: Enhanced GOAP with Utility Scoring

### 4.1 Executive Summary

Enhance the existing GOAP implementation with utility scoring and plan caching, based on patterns from Mountain GOAP and F.E.A.R. implementation best practices. This improves decision quality in ambiguous situations and reduces CPU overhead through plan reuse.

### 4.2 User Stories

| ID | Story | Acceptance Criteria |
|----|-------|---------------------|
| US-G1 | As a user, I want smarter goal prioritization | Goals have utility scores based on context |
| US-G2 | As a user, I want faster planning | Plans are cached and reused when valid |
| US-G3 | As a user, I want tunable behavior | Utility weights configurable per profile |

### 4.3 Technical Specification

#### 4.3.1 Utility Scoring Extension

**Source:** Mountain GOAP (https://github.com/caesuric/mountain-goap), F.E.A.R. AI (GDC 2006)

```csharp
// Core/GOAP/GoapUtilityScorer.cs
namespace Core.GOAP;

/// <summary>
/// Extends GOAP with utility theory for better goal prioritization.
/// Instead of static costs, evaluates goals based on current game state.
/// </summary>
public sealed class GoapUtilityScorer
{
    private readonly IPlayerReader _player;
    private readonly IAddonReader _addon;
    
    /// <summary>
    /// Calculates dynamic utility score for a goal based on current state.
    /// Higher score = more urgent/valuable goal.
    /// </summary>
    public float CalculateUtility(GoapGoal goal, WorldState currentState)
    {
        float baseUtility = 1.0f / goal.Cost; // Invert cost as base utility
        
        // Apply context multipliers
        float contextMultiplier = goal.Key switch
        {
            GoapKey.dangercombat when _player.HealthPercent < 30 => 5.0f,
            GoapKey.shouldloot when _addon.BagsFree < 5 => 0.5f,
            GoapKey.incombat when HasHighValueTarget() => 1.5f,
            _ => 1.0f
        };
        
        // Apply temporal decay (recently executed goals are less valuable)
        float timeSinceExecution = GetTimeSinceLastExecution(goal);
        float decayMultiplier = 1.0f - MathF.Exp(-timeSinceExecution / 30f); // 30s half-life
        
        return baseUtility * contextMultiplier * decayMultiplier;
    }
    
    private bool HasHighValueTarget()
    {
        // Check for rare mobs, named NPCs, etc.
        return _addon.TargetClassification > 0; // Rare or better
    }
    
    private float GetTimeSinceLastExecution(GoapGoal goal)
    {
        // Track in GoalExecutionHistory
        return _executionHistory.GetSecondsSince(goal.GetType().Name);
    }
}
```

#### 4.3.2 Plan Caching

**Source:** F.E.A.R. AI implementation notes

```csharp
// Core/GOAP/GoapPlanCache.cs
namespace Core.GOAP;

/// <summary>
/// Caches GOAP plans with TTL-based invalidation.
/// Plans are reused when world state matches cache key.
/// </summary>
public sealed class GoapPlanCache
{
    private readonly ILogger<GoapPlanCache> _logger;
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<int, CachedPlan> _cache = new();
    
    public GoapPlanCache(
        ILogger<GoapPlanCache> logger,
        IOptions<FeatureFlagsOptions> options)
    {
        _logger = logger;
        _ttl = TimeSpan.FromSeconds(options.Value.GOAPUtilityScoring.PlanCacheTTLSeconds);
    }
    
    /// <summary>
    /// Gets a cached plan if valid, or returns null to trigger replanning.
    /// </summary>
    public IReadOnlyList<GoapGoal>? GetCachedPlan(WorldState state)
    {
        int key = ComputeStateHash(state);
        
        if (_cache.TryGetValue(key, out CachedPlan? cached))
        {
            if (DateTime.UtcNow - cached.CreatedAt < _ttl)
            {
                _logger.LogDebug("[GoapPlanCache    ] Cache hit for state hash {Hash}", key);
                return cached.Plan;
            }
            
            // Expired
            _cache.TryRemove(key, out _);
        }
        
        return null;
    }
    
    /// <summary>
    /// Stores a plan in the cache.
    /// </summary>
    public void CachePlan(WorldState state, IReadOnlyList<GoapGoal> plan)
    {
        int key = ComputeStateHash(state);
        _cache[key] = new CachedPlan(plan, DateTime.UtcNow);
        
        _logger.LogDebug(
            "[GoapPlanCache    ] Cached plan with {Count} goals for state hash {Hash}",
            plan.Count, key);
    }
    
    /// <summary>
    /// Invalidates all cached plans (e.g., when world state changes significantly).
    /// </summary>
    public void Invalidate()
    {
        _cache.Clear();
        _logger.LogDebug("[GoapPlanCache    ] Cache invalidated");
    }
    
    private static int ComputeStateHash(WorldState state)
    {
        // Hash relevant state variables for cache key
        HashCode hash = new();
        hash.Add(state.HasTarget);
        hash.Add(state.InCombat);
        hash.Add(state.HealthPercent / 10); // Bucket by 10%
        hash.Add(state.ManaPercent / 10);
        hash.Add(state.IsMounted);
        hash.Add(state.IsDead);
        return hash.ToHashCode();
    }
    
    private record CachedPlan(IReadOnlyList<GoapGoal> Plan, DateTime CreatedAt);
}
```

### 4.4 Integration Points

| File | Change | Backwards Compatible |
|------|--------|---------------------|
| `Core/GOAP/GoapAgent.cs` | Add utility scoring before planning | ✅ Additive |
| `Core/GOAP/GoapPlanner.cs` | Check cache before computing plan | ✅ Fallback to full plan |

### 4.5 Files to Create

```
Core/GOAP/
├── GoapUtilityScorer.cs
├── GoapPlanCache.cs
└── GoalExecutionHistory.cs
```

### 4.6 Verification

```bash
dotnet test --filter "FullyQualifiedName~GOAP"
dotnet run --project Benchmarks -c Release -- --filter "*GoapPlan*"

# Acceptance criteria:
# - Plan cache hit rate > 60% in typical grinding scenarios
# - Cache operations < 0.5ms
# - No regression in plan quality (measure via simulation)
```

---

## 5. PRD-3: GOBT Hybrid Combat System

### 5.1 Executive Summary

Implement a hybrid system combining Behavior Trees for high-level flow control with GOAP planners for complex sub-problems. Based on academic research (ICLR 2026) and production game patterns.

### 5.2 User Stories

| ID | Story | Acceptance Criteria |
|----|-------|---------------------|
| US-BT1 | As a user, I want more readable combat logic | Visual tree structure in UI |
| US-BT2 | As a user, I want GOAP for complex decisions | GOAP planner node in tree |
| US-BT3 | As a user, I want to import existing profiles | JSON converter from current format |

### 5.3 Technical Specification

#### 5.3.1 Behavior Tree Core

**Source:** Industry standard pattern (Unity, Unreal implementations)

```csharp
// Core/BehaviorTree/IBehaviorNode.cs
namespace Core.BehaviorTree;

public enum NodeStatus { Success, Failure, Running }

public interface IBehaviorNode
{
    string Name { get; }
    NodeStatus Execute(BehaviorContext context);
    void Reset();
}

// Core/BehaviorTree/BehaviorContext.cs
public sealed class BehaviorContext
{
    public IPlayerReader Player { get; init; } = null!;
    public IAddonReader Addon { get; init; } = null!;
    public ICastingHandler CastingHandler { get; init; } = null!;
    public ITargetFinder TargetFinder { get; init; } = null!;
    public ClassConfiguration ClassConfig { get; init; } = null!;
    
    // Shared blackboard for node communication
    public Dictionary<string, object> Blackboard { get; } = new();
}
```

#### 5.3.2 Composite Nodes

```csharp
// Core/BehaviorTree/Nodes/SelectorNode.cs
namespace Core.BehaviorTree.Nodes;

/// <summary>
/// Selector (OR) node - returns Success on first successful child.
/// Used for priority-based decisions (try heal, else try attack, else wait).
/// </summary>
public sealed class SelectorNode : IBehaviorNode
{
    public string Name { get; }
    public IReadOnlyList<IBehaviorNode> Children { get; }
    
    public SelectorNode(string name, IEnumerable<IBehaviorNode> children)
    {
        Name = name;
        Children = children.ToList();
    }
    
    public NodeStatus Execute(BehaviorContext context)
    {
        foreach (IBehaviorNode child in Children)
        {
            NodeStatus status = child.Execute(context);
            if (status is NodeStatus.Success or NodeStatus.Running)
                return status;
        }
        return NodeStatus.Failure;
    }
    
    public void Reset()
    {
        foreach (IBehaviorNode child in Children)
            child.Reset();
    }
}

// Core/BehaviorTree/Nodes/SequenceNode.cs
/// <summary>
/// Sequence (AND) node - returns Success only if all children succeed.
/// Used for multi-step actions (check condition, then execute ability).
/// </summary>
public sealed class SequenceNode : IBehaviorNode
{
    public string Name { get; }
    public IReadOnlyList<IBehaviorNode> Children { get; }
    private int _currentChild;
    
    public SequenceNode(string name, IEnumerable<IBehaviorNode> children)
    {
        Name = name;
        Children = children.ToList();
    }
    
    public NodeStatus Execute(BehaviorContext context)
    {
        while (_currentChild < Children.Count)
        {
            NodeStatus status = Children[_currentChild].Execute(context);
            
            if (status == NodeStatus.Failure)
            {
                Reset();
                return NodeStatus.Failure;
            }
            
            if (status == NodeStatus.Running)
                return NodeStatus.Running;
            
            _currentChild++;
        }
        
        Reset();
        return NodeStatus.Success;
    }
    
    public void Reset() => _currentChild = 0;
}
```

#### 5.3.3 Action Nodes

```csharp
// Core/BehaviorTree/Nodes/ConditionNode.cs
namespace Core.BehaviorTree.Nodes;

/// <summary>
/// Evaluates a condition - immediate Success or Failure.
/// </summary>
public sealed class ConditionNode : IBehaviorNode
{
    public string Name { get; }
    private readonly Func<BehaviorContext, bool> _condition;
    
    public ConditionNode(string name, Func<BehaviorContext, bool> condition)
    {
        Name = name;
        _condition = condition;
    }
    
    public NodeStatus Execute(BehaviorContext context)
    {
        return _condition(context) ? NodeStatus.Success : NodeStatus.Failure;
    }
    
    public void Reset() { }
}

// Core/BehaviorTree/Nodes/CastSpellNode.cs
/// <summary>
/// Executes a spell cast via CastingHandler.
/// </summary>
public sealed class CastSpellNode : IBehaviorNode
{
    public string Name { get; }
    private readonly string _spellName;
    
    public CastSpellNode(string spellName)
    {
        Name = $"Cast {spellName}";
        _spellName = spellName;
    }
    
    public NodeStatus Execute(BehaviorContext context)
    {
        KeyAction? action = context.ClassConfig.Combat.Sequence
            .FirstOrDefault(a => a.Name == _spellName);
        
        if (action == null)
            return NodeStatus.Failure;
        
        if (!context.CastingHandler.CanCast(action))
            return NodeStatus.Failure;
        
        context.CastingHandler.Cast(action);
        return NodeStatus.Success;
    }
    
    public void Reset() { }
}

// Core/BehaviorTree/Nodes/GoapPlannerNode.cs
/// <summary>
/// Delegates decision to GOAP planner for complex sub-problems.
/// This is the key integration point for GOBT hybrid architecture.
/// </summary>
public sealed class GoapPlannerNode : IBehaviorNode
{
    public string Name => "GOAP Combat Planner";
    private readonly GoapAgent _goapAgent;
    
    public GoapPlannerNode(GoapAgent goapAgent)
    {
        _goapAgent = goapAgent;
    }
    
    public NodeStatus Execute(BehaviorContext context)
    {
        // Use GOAP for complex combat rotation decisions
        _goapAgent.Update();
        
        // GOAP always handles the current frame
        return NodeStatus.Running;
    }
    
    public void Reset() { }
}
```

#### 5.3.4 JSON to Behavior Tree Converter

```csharp
// Core/BehaviorTree/JsonToBehaviorTreeConverter.cs
namespace Core.BehaviorTree;

/// <summary>
/// Converts existing JSON class profiles to behavior trees for backwards compatibility.
/// </summary>
public sealed class JsonToBehaviorTreeConverter
{
    private readonly ILogger<JsonToBehaviorTreeConverter> _logger;
    
    public IBehaviorNode Convert(ClassConfiguration config)
    {
        _logger.LogInformation(
            "[BTConverter      ] Converting {ClassName} profile to behavior tree",
            config.ClassName);
        
        // Build standard combat tree structure
        var combatTree = new SelectorNode("Combat", new IBehaviorNode[]
        {
            // Emergency survival
            BuildEmergencyBranch(config),
            
            // Normal combat rotation
            BuildCombatBranch(config),
            
            // Maintenance (buffs, consumables)
            BuildMaintenanceBranch(config),
            
            // Default: wait
            new WaitNode(100)
        });
        
        return combatTree;
    }
    
    private IBehaviorNode BuildEmergencyBranch(ClassConfiguration config)
    {
        // Convert high-priority survival abilities
        var emergencyActions = config.Combat.Sequence
            .Where(a => a.Requirements.Any(r => 
                r.Contains("Health%") && r.Contains("<") && 
                int.TryParse(ExtractNumber(r), out int v) && v < 30))
            .Select(a => new SequenceNode(a.Name, new IBehaviorNode[]
            {
                new ConditionNode($"Check {a.Name}", ctx => 
                    ctx.CastingHandler.CanCast(a)),
                new CastSpellNode(a.Name)
            }))
            .ToList();
        
        if (emergencyActions.Count == 0)
            return new ConditionNode("NoEmergency", _ => false);
        
        return new SelectorNode("Emergency", emergencyActions);
    }
    
    private IBehaviorNode BuildCombatBranch(ClassConfiguration config)
    {
        // Convert normal rotation abilities in priority order
        var rotationNodes = config.Combat.Sequence
            .Select(a => new SequenceNode(a.Name, new IBehaviorNode[]
            {
                new ConditionNode($"CanCast {a.Name}", ctx => 
                    ctx.CastingHandler.CanCast(a)),
                new CastSpellNode(a.Name)
            }))
            .ToArray();
        
        return new SelectorNode("Rotation", rotationNodes);
    }
    
    private IBehaviorNode BuildMaintenanceBranch(ClassConfiguration config)
    {
        // Convert Adhoc (buff/consumable) abilities
        var buffNodes = config.Adhoc?.Sequence?
            .Select(a => new SequenceNode(a.Name, new IBehaviorNode[]
            {
                new ConditionNode($"Need {a.Name}", ctx => 
                    ctx.CastingHandler.CanCast(a)),
                new CastSpellNode(a.Name)
            }))
            .ToArray() ?? Array.Empty<IBehaviorNode>();
        
        if (buffNodes.Length == 0)
            return new ConditionNode("NoMaintenance", _ => false);
        
        return new SelectorNode("Maintenance", buffNodes);
    }
    
    private static string ExtractNumber(string requirement)
    {
        return new string(requirement.Where(char.IsDigit).ToArray());
    }
}
```

### 5.4 Files to Create

```
Core/BehaviorTree/
├── IBehaviorNode.cs
├── BehaviorContext.cs
├── BehaviorTreeExecutor.cs
├── JsonToBehaviorTreeConverter.cs
└── Nodes/
    ├── SelectorNode.cs
    ├── SequenceNode.cs
    ├── ConditionNode.cs
    ├── CastSpellNode.cs
    ├── GoapPlannerNode.cs
    ├── WaitNode.cs
    └── DecoratorNodes.cs
```

### 5.5 Rollback

**Feature Flag Fallback:** When `GOBTHybrid.FallbackToGOAP = true`, any BT execution failure reverts to pure GOAP for that frame.

**Code Rollback:** BT system is isolated; removing references from `CombatGoal.cs` restores GOAP-only behavior.

---

## 6. PRD-4: Object Caching & Performance Layer

### 6.1 Executive Summary

Implement LRU object caching to reduce repeated computations and GC pressure. Based on patterns from noisiver/AmeisenBotX achieving 30% CPU reduction.

### 6.2 Technical Specification

```csharp
// Core/Performance/LRUCache.cs
namespace Core.Performance;

/// <summary>
/// LRU (Least Recently Used) cache with configurable TTL.
/// Adapted from noisiver/AmeisenBotX performance optimizations.
/// </summary>
public sealed class LRUCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly TimeSpan _defaultTtl;
    private readonly Dictionary<TKey, LinkedListNode<CacheEntry>> _cache;
    private readonly LinkedList<CacheEntry> _lruList;
    private readonly object _lock = new();
    
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
                    // Move to front (most recently used)
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    return node.Value.Value;
                }
                
                // Expired - remove
                _lruList.Remove(node);
                _cache.Remove(key);
            }
            
            // Create new entry
            TValue value = factory();
            DateTime expiresAt = DateTime.UtcNow + (ttl ?? _defaultTtl);
            
            var entry = new CacheEntry(key, value, expiresAt);
            var newNode = _lruList.AddFirst(entry);
            _cache[key] = newNode;
            
            // Evict if over capacity
            while (_cache.Count > _capacity)
            {
                var lru = _lruList.Last;
                if (lru != null)
                {
                    _lruList.RemoveLast();
                    _cache.Remove(lru.Value.Key);
                }
            }
            
            return value;
        }
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
        get
        {
            lock (_lock) return _cache.Count;
        }
    }
    
    private readonly record struct CacheEntry(TKey Key, TValue Value, DateTime ExpiresAt);
}
```

### 6.3 Integration Points

| Component | Cached Data | TTL | Expected Improvement |
|-----------|-------------|-----|---------------------|
| `AddonReader` | Parsed frame values | 100ms | Reduce parse calls |
| `NpcNameFinder` | Detection results | 500ms | Reduce image analysis |
| `PathGraph` | A* path results | 5000ms | Reduce pathfinding |
| `SpellDB` | Spell lookups | ∞ (static) | Reduce dictionary access |

### 6.4 Files to Create

```
Core/Performance/
├── LRUCache.cs
├── ObjectPool.cs (existing from Phase 1)
├── CacheServiceExtensions.cs
└── CacheStatistics.cs
```

---

## 7. PRD-5: Screen Capture Optimization

### 7.1 Executive Summary

Optimize DXGI screen capture with texture pooling based on D3DShot benchmarks showing potential for 60 FPS capture.

### 7.2 Technical Specification

```csharp
// Core/WoWScreen/TexturePool.cs
namespace Core;

/// <summary>
/// Pools D3D11 textures to avoid recreation overhead.
/// Based on D3DShot optimization patterns.
/// </summary>
public sealed class TexturePool : IDisposable
{
    private readonly ID3D11Device _device;
    private readonly ConcurrentBag<ID3D11Texture2D> _available = new();
    private readonly int _maxSize;
    private int _created;
    
    public TexturePool(ID3D11Device device, int maxSize = 3)
    {
        _device = device;
        _maxSize = maxSize;
    }
    
    public ID3D11Texture2D Rent(int width, int height)
    {
        if (_available.TryTake(out ID3D11Texture2D? texture))
        {
            // Verify dimensions match (or recreate)
            var desc = texture.Description;
            if (desc.Width == width && desc.Height == height)
                return texture;
            
            texture.Dispose();
        }
        
        // Create new texture
        Interlocked.Increment(ref _created);
        return CreateStagingTexture(width, height);
    }
    
    public void Return(ID3D11Texture2D texture)
    {
        if (_available.Count < _maxSize)
            _available.Add(texture);
        else
            texture.Dispose();
    }
    
    private ID3D11Texture2D CreateStagingTexture(int width, int height)
    {
        var desc = new Texture2DDescription
        {
            Width = width,
            Height = height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CpuAccessFlags = CpuAccessFlags.Read,
            OptionFlags = ResourceOptionFlags.None
        };
        
        return _device.CreateTexture2D(desc);
    }
    
    public void Dispose()
    {
        while (_available.TryTake(out ID3D11Texture2D? texture))
            texture.Dispose();
    }
}
```

### 7.3 Integration Point

Modify `Core/WoWScreen/WowScreenDXGI.cs` to use `TexturePool` instead of creating new textures per frame.

---

## 8. PRD-6: Multi-Bot Coordination

### 8.1 Executive Summary

**⚠️ HIGH RISK FEATURE** - Enables synchronized control of multiple bot instances with formation movement.

### 8.2 Risk Assessment

| Risk | Severity | Mitigation |
|------|----------|------------|
| Detection via synchronized behavior | Critical | Desync timing, independent decisions |
| Network overhead | Medium | Local IPC only, no internet traffic |
| Complexity | High | Extensive testing, gradual rollout |

### 8.3 Technical Specification (Research Phase)

```csharp
// Core/MultiBot/FormationManager.cs (RESEARCH ONLY)
namespace Core.MultiBot;

/// <summary>
/// Manages formation positioning for multi-bot setups.
/// Adapted from descention/AmeisenBotX formation patterns.
/// </summary>
public sealed class FormationManager
{
    public Vector3 GetFollowerPosition(Vector3 leaderPosition, int followerIndex, 
        FormationType formation)
    {
        return formation switch
        {
            FormationType.VFormation => GetVFormationPosition(leaderPosition, followerIndex),
            FormationType.Line => GetLinePosition(leaderPosition, followerIndex),
            FormationType.Spread => GetSpreadPosition(leaderPosition, followerIndex),
            _ => leaderPosition
        };
    }
    
    private Vector3 GetVFormationPosition(Vector3 leader, int index)
    {
        // V-formation: followers behind leader in chevron
        bool left = index % 2 == 0;
        int row = (index + 1) / 2;
        float angle = left ? 45f : -45f;
        float distance = 5f + row * 3f;
        
        return CalculateOffset(leader, angle, distance);
    }
    
    private static Vector3 CalculateOffset(Vector3 origin, float angleDegrees, float distance)
    {
        float radians = angleDegrees * MathF.PI / 180f;
        return new Vector3(
            origin.X + MathF.Sin(radians) * distance,
            origin.Y,
            origin.Z + MathF.Cos(radians) * distance);
    }
}

public enum FormationType { VFormation, Line, Spread, Circle }
```

### 8.4 Implementation Status

**Status:** Research/Planning Only - Not recommended for production until detection risk assessed.

---

## 9. PRD-7: Advanced Navigation Features

### 9.1 Executive Summary

Enhance navigation with tile streaming, path caching, and dynamic obstacle handling based on Recast/Detour patterns.

### 9.2 Technical Specification

#### 9.2.1 Path Caching

```csharp
// Core/Navigation/PathCache.cs
namespace Core.Navigation;

/// <summary>
/// Caches pathfinding results to avoid repeated expensive calculations.
/// </summary>
public sealed class PathCache
{
    private readonly LRUCache<PathCacheKey, List<Vector3>> _cache;
    
    public PathCache(IOptions<FeatureFlagsOptions> options)
    {
        _cache = new LRUCache<PathCacheKey, List<Vector3>>(
            capacity: options.Value.AdvancedNavigation.PathCacheMaxSize,
            defaultTtl: TimeSpan.FromSeconds(30));
    }
    
    public List<Vector3>? GetCachedPath(Vector3 start, Vector3 end, float mapId)
    {
        // Quantize positions to 2-unit grid for cache key
        var key = new PathCacheKey(
            QuantizePosition(start),
            QuantizePosition(end),
            (int)mapId);
        
        return _cache.TryGet(key);
    }
    
    public void CachePath(Vector3 start, Vector3 end, float mapId, List<Vector3> path)
    {
        var key = new PathCacheKey(
            QuantizePosition(start),
            QuantizePosition(end),
            (int)mapId);
        
        _cache.Add(key, path);
    }
    
    private static (int, int, int) QuantizePosition(Vector3 pos)
    {
        return ((int)(pos.X / 2), (int)(pos.Y / 2), (int)(pos.Z / 2));
    }
    
    private readonly record struct PathCacheKey(
        (int, int, int) Start,
        (int, int, int) End,
        int MapId);
}
```

#### 9.2.2 Tile Streaming (AmeisenNavigation Enhancement)

```csharp
// Integration with AmeisenNavigation for memory-efficient MMAP loading
// Configuration passed to AmeisenNavigation server

// appsettings.json addition:
{
  "AmeisenNavigation": {
    "TileStreamingEnabled": true,
    "MaxLoadedTiles": 50,
    "TileUnloadTimeoutSeconds": 300
  }
}
```

---

## 10. Integration Roadmap

### 10.1 Phased Delivery

| Phase | Features | Duration | Dependencies |
|-------|----------|----------|--------------|
| **A** | PRD-4 (Caching), PRD-5 (Screen Opt) | 1 week | None |
| **B** | PRD-1 (Humanization) | 2 weeks | Phase A |
| **C** | PRD-2 (GOAP Utility) | 1 week | None |
| **D** | PRD-7 (Adv Navigation) | 2 weeks | Phase A |
| **E** | PRD-3 (GOBT Hybrid) | 3 weeks | Phase C |
| **F** | PRD-6 (Multi-Bot) | 4+ weeks | Phase B, D, E (research) |

### 10.2 Milestone Definitions

**Phase A Complete:**
- All caching systems operational
- Screen capture optimization showing >20% improvement in benchmarks

**Phase B Complete:**
- Humanization active and measurable
- No DPS regression >2%

**Phase C Complete:**
- GOAP utility scoring improving decision quality
- Plan cache hit rate >50%

**Phase D Complete:**
- Path caching reducing pathfinding calls by >30%
- Navigation reliability metrics improved

**Phase E Complete:**
- Behavior trees functional with GOAP fallback
- JSON converter supporting all existing profiles

---

## 11. Compatibility Matrix

### 11.1 WoW Client Compatibility

| Feature | Classic Era | TBC | WotLK | Cata | MoP |
|---------|-------------|-----|-------|------|-----|
| Humanization | ✅ | ✅ | ✅ | ✅ | ✅ |
| GOAP Utility | ✅ | ✅ | ✅ | ✅ | ✅ |
| GOBT Hybrid | ✅ | ✅ | ✅ | ✅ | ✅ |
| Object Caching | ✅ | ✅ | ✅ | ✅ | ✅ |
| Screen Capture Opt | ✅ | ✅ | ✅ | ✅ | ✅ |
| Multi-Bot | ✅¹ | ✅¹ | ✅¹ | ✅¹ | ✅¹ |
| Adv Navigation | ✅ | ✅ | ✅ | ✅ | ✅ |

¹ High detection risk - not recommended for production

### 11.2 Existing Feature Compatibility

| New Feature | StuckRecovery | Hazard Avoidance | AI Profile Gen | Marketplace |
|-------------|---------------|------------------|----------------|-------------|
| Humanization | ✅ Complementary | ✅ Complementary | ✅ Independent | ✅ Independent |
| GOAP Utility | ✅ Uses | ✅ Enhances | ✅ Independent | ✅ Independent |
| GOBT Hybrid | ✅ Integrates | ✅ Integrates | ⚠️ Needs converter | ✅ Independent |
| Caching | ✅ Uses | ✅ Uses | ✅ Independent | ✅ Independent |

---

## 12. Security & Risk Matrix

### 12.1 Detection Risk Assessment

| Feature | Detection Vector | Risk Level | Mitigation |
|---------|-----------------|------------|------------|
| Humanization | None - reduces risk | Very Low | N/A |
| GOAP Utility | None | None | N/A |
| GOBT Hybrid | None | None | N/A |
| Object Caching | None | None | N/A |
| Screen Capture | None - pixel reading | Very Low | N/A |
| Multi-Bot | Synchronized behavior | High | Desync timing |
| Adv Navigation | None | None | N/A |

### 12.2 Technical Risk Assessment

| Feature | Complexity | Failure Mode | Fallback |
|---------|------------|--------------|----------|
| Humanization | Medium | Over-delay impacts DPS | Disable via flag |
| GOAP Utility | Medium | Bad scoring | Original GOAP |
| GOBT Hybrid | High | Tree execution failure | GOAP fallback |
| Object Caching | Low | Cache miss | Direct computation |
| Screen Capture | Low | Pool exhaustion | Recreate texture |
| Multi-Bot | Very High | Desync | Single-bot mode |
| Adv Navigation | Medium | Cache corruption | Direct pathfinding |

---

## 13. Testing Protocols

### 13.1 Unit Test Requirements

```csharp
// Each feature requires minimum 80% coverage

[Fact]
public void MovementHumanizer_AppliesNoise_WithinBounds()
{
    // Arrange
    var humanizer = new MovementHumanizer(logger, options);
    var original = new Vector3(100, 0, 100);
    
    // Act
    var humanized = humanizer.HumanizePosition(original, 0.1f);
    
    // Assert
    Assert.InRange(humanized.X, original.X - 1f, original.X + 1f);
    Assert.Equal(original.Y, humanized.Y); // Y unchanged
}
```

### 13.2 Integration Test Requirements

```csharp
[Fact]
public async Task Humanization_DoesNotImpactCombatPerformance()
{
    // Run combat simulation with and without humanization
    var withoutHuman = await RunCombatSimulation(humanizationEnabled: false);
    var withHuman = await RunCombatSimulation(humanizationEnabled: true);
    
    // DPS regression must be < 2%
    float regression = (withoutHuman.DPS - withHuman.DPS) / withoutHuman.DPS;
    Assert.InRange(regression, 0, 0.02f);
}
```

### 13.3 Benchmark Requirements

```csharp
[Benchmark]
public void LRUCache_GetOrAdd_HotPath()
{
    _cache.GetOrAdd("test_key", () => new ExpensiveObject());
}

// Acceptance: < 0.1ms per operation
// Allocation: 0 bytes after warmup
```

### 13.4 Test Commands

```bash
# Full test suite
dotnet test

# Feature-specific tests
dotnet test --filter "FullyQualifiedName~Humanization"
dotnet test --filter "FullyQualifiedName~GOAP"
dotnet test --filter "FullyQualifiedName~BehaviorTree"

# Performance benchmarks
dotnet run --project Benchmarks -c Release -- --filter "*Cache*"
dotnet run --project Benchmarks -c Release -- --filter "*Humanization*"
```

---

## 14. Rollback Procedures

### 14.1 Feature Flag Rollback (Immediate, <1 minute)

```bash
# Edit runtime config
notepad BlazorServer/runtime_feature_flags.json
# Set "Enabled": false for affected feature
# Config hot-reloads automatically
```

### 14.2 Code Rollback (Emergency, ~5 minutes)

```bash
# Identify breaking commit
git log --oneline -10

# Revert changes
git revert HEAD --no-commit
git commit -m "Revert: [Feature] due to [Issue]"

# Rebuild
dotnet build MasterOfPuppets.sln
```

### 14.3 Data Rollback

| Feature | Persistent Data | Rollback Procedure |
|---------|-----------------|-------------------|
| Humanization | None | N/A |
| GOAP Utility | Execution history (optional) | Delete `Json/goap_history.json` |
| GOBT Hybrid | None (uses existing profiles) | N/A |
| Object Caching | None (in-memory) | Restart clears cache |
| Path Caching | None (in-memory) | Restart clears cache |

---

## Appendix A: Files to Create

```
Core/
├── Humanization/
│   ├── MovementHumanizer.cs
│   ├── TimingHumanizer.cs
│   ├── IdleBehaviorInjector.cs
│   ├── MouseHumanizer.cs
│   ├── FastNoise.cs
│   └── HumanizationServiceExtensions.cs
├── GOAP/
│   ├── GoapUtilityScorer.cs
│   ├── GoapPlanCache.cs
│   └── GoalExecutionHistory.cs
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
├── Performance/
│   ├── LRUCache.cs
│   └── CacheStatistics.cs
├── Navigation/
│   └── PathCache.cs
├── WoWScreen/
│   └── TexturePool.cs
└── MultiBot/ (Research)
    └── FormationManager.cs
```

### Appendix B: Estimated Effort

| PRD | Development | Testing | Documentation | Total |
|-----|-------------|---------|---------------|-------|
| PRD-1 Humanization | 24h | 8h | 4h | 36h |
| PRD-2 GOAP Utility | 12h | 4h | 2h | 18h |
| PRD-3 GOBT Hybrid | 40h | 16h | 8h | 64h |
| PRD-4 Object Caching | 8h | 4h | 2h | 14h |
| PRD-5 Screen Capture | 6h | 2h | 1h | 9h |
| PRD-6 Multi-Bot | 80h+ | 40h | 8h | 128h+ |
| PRD-7 Adv Navigation | 16h | 8h | 4h | 28h |
| **Total** | **186h+** | **82h+** | **29h** | **297h+** |

---

**Document Status:** Complete - Production-Ready Specifications  
**Last Updated:** February 5, 2026  
**Source:** Cross-Repository Analysis & Web Research Synthesis  
**Review Required:** Before Phase E (GOBT) and Phase F (Multi-Bot) implementation
