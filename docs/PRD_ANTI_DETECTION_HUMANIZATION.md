# PRD: Anti-Detection & Humanization System

**Version:** 1.0  
**Date:** February 2026  
**Author:** Planning Agent  
**Reference Research:**  
- [warden_technical_analysis.html](ImportedResearch/warden/warden_technical_analysis.html)  
- [warden_scholar.csv](ImportedResearch/warden/warden_scholar.csv)  

---

## Executive Summary

This PRD defines a comprehensive Anti-Detection & Humanization System designed to minimize false positive detection risks from Warden and similar anti-cheat systems. The system focuses on behavioral humanization rather than evasion techniques, leveraging the bot's inherently safe external-process architecture while adding human-like behavioral patterns.

### Key Principles

1. **No Memory Manipulation** - Bot never touches game process memory
2. **Legitimate Data Channels** - Pixel reading via official Addon API
3. **Behavioral Humanization** - Make automated inputs indistinguishable from human
4. **Statistical Authenticity** - Match human behavioral distributions, not just add randomness

---

## 1. Threat Model Analysis

### 1.1 Warden Detection Vectors

Based on technical research, Warden employs these detection methods:

| Detection Type | Description | Risk Level for This Bot |
|----------------|-------------|------------------------|
| Memory Scanning | Scans for known cheat signatures in process memory | **NONE** - External process |
| DLL/Module Audit | Detects injected DLLs in game process | **NONE** - No injection |
| Lua Call Stack | Detects calls from outside WoW.exe | **NONE** - Uses standard addon |
| Window Enumeration | Scans window titles of running apps | **LOW** - Configurable window titles |
| Input Pattern Analysis | Detects robotic input timing | **MEDIUM** - Requires humanization |
| Behavioral Telemetry | Server-side gameplay pattern analysis | **MEDIUM** - Requires variation |
| Signature Hashing | Hash-based detection of known bots | **LOW** - Source available, unique builds |

### 1.2 Current Architecture Safety Assessment

**Why This Bot Is Inherently Safe:**

```
┌─────────────────────────────────────────────────────────────────────┐
│                         BOT PROCESS (External)                       │
│  ┌─────────────┐   ┌─────────────┐   ┌─────────────────────┐       │
│  │ Screen Read │   │ Input Send  │   │ Decision Engine     │       │
│  │ (Pixels)    │   │ (PostMsg)   │   │ (GOAP)              │       │
│  └──────┬──────┘   └──────┬──────┘   └─────────────────────┘       │
└─────────┼─────────────────┼─────────────────────────────────────────┘
          │                 │
          │ Screen pixels   │ Window messages (keystrokes)
          ▼                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      WOW PROCESS (Unmodified)                        │
│  ┌─────────────────────┐                                            │
│  │ DataToColor Addon   │  ← Standard Lua addon using official APIs  │
│  │ (Pixel encoder)     │                                            │
│  └─────────────────────┘                                            │
└─────────────────────────────────────────────────────────────────────┘
```

**Safe Properties:**
- ✅ No memory reading from game process
- ✅ No code injection or hooking
- ✅ No DLL injection
- ✅ Addon uses only official WoW Lua API
- ✅ Input via standard Windows messaging (same as human keyboard/mouse)

### 1.3 Remaining Risk Areas

| Risk Area | Current State | Required Mitigation |
|-----------|---------------|---------------------|
| Input Timing | Basic random delay (±maxDelay ms) | Gaussian/human distribution |
| Mouse Movement | Direct SetCursorPos (instant) | Bezier curves with micro-corrections |
| Action Patterns | GOAP deterministic | Add probabilistic variation |
| Session Length | Infinite possible | Fatigue simulation, breaks |
| Reaction Times | Near-instant | Human latency modeling |

---

## 2. User Stories

| ID | Story | Acceptance Criteria |
|----|-------|---------------------|
| US-1 | As a user, I want input timing to mimic human keystroke patterns so behavior appears natural | Input timing follows Gaussian distribution with configurable mean/variance |
| US-2 | As a user, I want mouse movements to look human with curves and micro-corrections | Mouse paths use Bezier interpolation with jitter |
| US-3 | As a user, I want the bot to take natural breaks to simulate human fatigue | Configurable micro-pauses, breaks, and session limits |
| US-4 | As a user, I want reaction times to appear human | GCD reactions delayed with human-like latency distribution |
| US-5 | As a user, I want to configure humanization aggressiveness based on my risk tolerance | Low/Medium/High presets plus custom tuning |
| US-6 | As a user, I want metrics showing my behavior patterns match human baselines | Dashboard with statistical comparison |

---

## 3. Functional Requirements

### 3.1 Input Timing Humanization

| ID | Requirement | Priority | Implementation Notes |
|----|-------------|----------|---------------------|
| FR-1.1 | Replace uniform random with Gaussian distribution | P0 | `HumanizedRandom.NextGaussian(mean, stdDev)` |
| FR-1.2 | Model human reaction latency (180-400ms baseline + fatigue) | P0 | Fatigue accumulator affects mean |
| FR-1.3 | Add "thinking pauses" before complex actions | P1 | 0-500ms pause before combat rotation start |
| FR-1.4 | Implement typing delays for chat/commands | P1 | Per-character delay with variance |
| FR-1.5 | Model key press-release duration naturally | P0 | 40-120ms with Gaussian distribution |

**Technical Specification - Gaussian Timing:**

```csharp
/// <summary>
/// Human-like timing using Box-Muller transform for Gaussian distribution.
/// </summary>
public static class HumanizedRandom
{
    private static readonly Random _random = Random.Shared;
    
    /// <summary>
    /// Generate Gaussian-distributed delay in milliseconds.
    /// </summary>
    /// <param name="mean">Target center time in ms</param>
    /// <param name="stdDev">Standard deviation in ms</param>
    /// <param name="min">Floor value to prevent negative/zero</param>
    /// <param name="max">Ceiling to prevent extreme outliers</param>
    public static int NextGaussian(double mean, double stdDev, int min = 10, int max = 2000)
    {
        // Box-Muller transform
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        
        int result = (int)(mean + stdDev * normal);
        return Math.Clamp(result, min, max);
    }
}
```

**Human Reaction Time Distribution (Research-Based):**

| Action Type | Mean (ms) | StdDev (ms) | Source |
|-------------|-----------|-------------|--------|
| Simple reaction (expected stimulus) | 250 | 50 | Human Benchmark data |
| Choice reaction (1 of N options) | 350 | 80 | Hick's Law research |
| Complex decision (combat rotation) | 500 | 150 | Gaming studies |
| Interrupt/emergency reaction | 300 | 100 | Alert response data |

### 3.2 Mouse Movement Humanization

| ID | Requirement | Priority | Implementation Notes |
|----|-------------|----------|---------------------|
| FR-2.1 | Replace instant SetCursorPos with interpolated paths | P0 | Bezier curve generator |
| FR-2.2 | Add micro-corrections during path | P1 | Small deviations mid-path |
| FR-2.3 | Vary movement speed (acceleration/deceleration) | P1 | Ease-in-out curves |
| FR-2.4 | Occasional overshoot and correction | P2 | 5-10% of movements overshoot |
| FR-2.5 | Add natural "hesitation" jitter near targets | P2 | Slow down approaching click target |

**Technical Specification - Bezier Mouse Path:**

```csharp
/// <summary>
/// Generates human-like mouse movement paths using cubic Bezier curves.
/// </summary>
public sealed class HumanizedMousePath
{
    /// <summary>
    /// Generate points along a human-like path from start to end.
    /// </summary>
    public static IEnumerable<Point> GeneratePath(Point start, Point end, int steps = 20)
    {
        // Add randomized control points for natural curve
        double distance = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
        double deviation = Math.Clamp(distance * 0.3, 10, 100); // 30% of distance, clamped
        
        Point control1 = new(
            start.X + (int)(Random.Shared.NextDouble() * deviation * RandomSign()),
            start.Y + (int)(Random.Shared.NextDouble() * deviation * RandomSign()));
            
        Point control2 = new(
            end.X + (int)(Random.Shared.NextDouble() * deviation * RandomSign()),
            end.Y + (int)(Random.Shared.NextDouble() * deviation * RandomSign()));
        
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            // Apply ease-in-out for natural acceleration
            double easedT = t < 0.5 
                ? 2 * t * t 
                : 1 - Math.Pow(-2 * t + 2, 2) / 2;
                
            yield return CubicBezier(start, control1, control2, end, easedT);
        }
    }
    
    private static Point CubicBezier(Point p0, Point p1, Point p2, Point p3, double t)
    {
        double u = 1 - t;
        double tt = t * t;
        double uu = u * u;
        double uuu = uu * u;
        double ttt = tt * t;
        
        double x = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
        double y = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;
        
        // Add micro-jitter
        x += Random.Shared.NextDouble() * 2 - 1;
        y += Random.Shared.NextDouble() * 2 - 1;
        
        return new Point((int)x, (int)y);
    }
    
    private static int RandomSign() => Random.Shared.Next(2) * 2 - 1;
}
```

### 3.3 Behavioral Pattern Variation

| ID | Requirement | Priority | Implementation Notes |
|----|-------------|----------|---------------------|
| FR-3.1 | Add micro-pauses during extended actions | P0 | 0.5-2s pause every 30-120s |
| FR-3.2 | Simulate "distraction" events | P1 | Occasional random target switch or path deviation |
| FR-3.3 | Vary combat rotation order (within DPS-neutral range) | P1 | Shuffle filler spells |
| FR-3.4 | Model session fatigue (increasing reaction times) | P1 | +10% latency per hour |
| FR-3.5 | Implement scheduled AFK breaks | P1 | 1-5 min break every 30-60 min |
| FR-3.6 | Randomize grinding path variations | P2 | Slight waypoint deviations |

**Session Fatigue Model:**

```csharp
/// <summary>
/// Models human fatigue during extended sessions.
/// </summary>
public sealed class FatigueSimulator
{
    private readonly Stopwatch _sessionTimer = Stopwatch.StartNew();
    private readonly TimeSpan _breakInterval;
    private DateTime _lastBreak = DateTime.UtcNow;
    
    /// <summary>
    /// Gets fatigue multiplier (1.0 = fresh, increases over time).
    /// </summary>
    public double FatigueMultiplier
    {
        get
        {
            double hoursPlayed = _sessionTimer.Elapsed.TotalHours;
            // 10% slower per hour, max 50% slower
            return Math.Min(1.0 + (hoursPlayed * 0.10), 1.5);
        }
    }
    
    /// <summary>
    /// Calculates if a break is due.
    /// </summary>
    public bool ShouldTakeBreak()
    {
        TimeSpan sinceLastBreak = DateTime.UtcNow - _lastBreak;
        // Randomize break timing ±20%
        double jitter = 0.8 + Random.Shared.NextDouble() * 0.4;
        return sinceLastBreak > _breakInterval * jitter;
    }
    
    /// <summary>
    /// Gets recommended break duration.
    /// </summary>
    public TimeSpan GetBreakDuration()
    {
        // 1-5 minutes, longer as fatigue increases
        double baseMinutes = 1 + Random.Shared.NextDouble() * 2;
        double fatigueBonus = (FatigueMultiplier - 1.0) * 5; // Up to 2.5 more min
        return TimeSpan.FromMinutes(baseMinutes + fatigueBonus);
    }
}
```

### 3.4 Process Concealment

| ID | Requirement | Priority | Implementation Notes |
|----|-------------|----------|---------------------|
| FR-4.1 | Configurable window title | P0 | Default to generic name |
| FR-4.2 | Configurable process name (via build) | P2 | Rename output executable |
| FR-4.3 | Minimize memory footprint patterns | P2 | Avoid identifiable allocations |

---

## 4. Non-Functional Requirements

| ID | Requirement | Target | Validation Method |
|----|-------------|--------|------------------|
| NFR-1 | Humanization overhead < 5% CPU | < 5% | Benchmark comparison |
| NFR-2 | Configuration hot-reload | < 1s | Runtime flag test |
| NFR-3 | No blocking of main loop | 0 blocked frames | Trace logging |
| NFR-4 | Memory overhead < 10MB | < 10MB | Memory profiling |

---

## 5. Technical Architecture

### 5.1 Component Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Humanization Layer                               │
│ ┌─────────────────┐ ┌──────────────────┐ ┌────────────────────────┐    │
│ │ HumanizedRandom │ │ MousePathGen     │ │ FatigueSimulator       │    │
│ │ - Gaussian      │ │ - Bezier curves  │ │ - Session time track   │    │
│ │ - Fatigue adj   │ │ - Micro-jitter   │ │ - Break scheduling     │    │
│ └────────┬────────┘ └────────┬─────────┘ └────────────┬───────────┘    │
│          │                   │                        │                 │
│          └───────────────────┴────────────────────────┘                 │
│                              │                                          │
│                    ┌─────────▼─────────┐                               │
│                    │ HumanizationService│  ← IHostedService             │
│                    │ - Coordinates all  │                               │
│                    │ - Config loading   │                               │
│                    └─────────┬──────────┘                               │
└──────────────────────────────┼──────────────────────────────────────────┘
                               │
          ┌────────────────────┼────────────────────┐
          ▼                    ▼                    ▼
┌─────────────────┐ ┌─────────────────┐  ┌─────────────────┐
│ ConfigurableInput│ │ WowProcessInput │  │ GOAP Goals      │
│ - Uses timing   │ │ - Uses mouse    │  │ - Uses fatigue  │
│   from service  │ │   paths         │  │   checks        │
└─────────────────┘ └─────────────────┘  └─────────────────┘
```

### 5.2 Configuration Schema

```json
{
  "Humanization": {
    "Enabled": true,
    "Preset": "Medium",
    "InputTiming": {
      "BaseMeanMs": 50,
      "BaseStdDevMs": 15,
      "ReactionMeanMs": 280,
      "ReactionStdDevMs": 60
    },
    "MouseMovement": {
      "Enabled": true,
      "StepsPerMovement": 15,
      "CurveIntensity": 0.3,
      "MicroJitterPixels": 2
    },
    "Fatigue": {
      "Enabled": true,
      "BreakIntervalMinutes": 45,
      "BreakDurationMinMinutes": 1,
      "BreakDurationMaxMinutes": 5,
      "FatigueRatePerHour": 0.10
    },
    "Behavior": {
      "MicroPauseEnabled": true,
      "MicroPauseIntervalSeconds": 60,
      "RotationVariationEnabled": true,
      "PathDeviationEnabled": false
    }
  }
}
```

### 5.3 Preset Definitions

| Preset | Input Variance | Mouse Humanize | Fatigue | Breaks | Use Case |
|--------|---------------|----------------|---------|--------|----------|
| **Minimal** | ±10ms uniform | No | No | No | Testing only |
| **Low** | Gaussian σ=10ms | Basic curve | No | No | Low-risk private server |
| **Medium** | Gaussian σ=20ms | Full curve | Yes (5%/hr) | 45 min | Default for retail |
| **High** | Gaussian σ=40ms | Full + overshoot | Yes (10%/hr) | 30 min | Maximum safety |
| **Custom** | Configurable | Configurable | Configurable | Configurable | Advanced users |

---

## 6. Implementation Phases

### Phase 1: Core Timing Humanization (8 hours)

| Task | Description | Files | Effort |
|------|-------------|-------|--------|
| 1.1 | Create HumanizedRandom utility class | `Core/Humanization/HumanizedRandom.cs` | 2h |
| 1.2 | Create FatigueSimulator service | `Core/Humanization/FatigueSimulator.cs` | 2h |
| 1.3 | Integrate with InputWindowsNative.PressRandom | `Game/Input/InputWindowsNative.cs` | 2h |
| 1.4 | Add configuration schema and loading | `BlazorServer/humanization_config.json` | 1h |
| 1.5 | Unit tests | `CoreTests/Humanization/` | 1h |

### Phase 2: Mouse Movement Humanization (6 hours)

| Task | Description | Files | Effort |
|------|-------------|-------|--------|
| 2.1 | Create HumanizedMousePath generator | `Core/Humanization/HumanizedMousePath.cs` | 3h |
| 2.2 | Integrate with InputWindowsNative mouse methods | `Game/Input/InputWindowsNative.cs` | 2h |
| 2.3 | Benchmark and optimize path generation | `Benchmarks/Humanization/` | 1h |

### Phase 3: Behavioral Patterns (6 hours)

| Task | Description | Files | Effort |
|------|-------------|-------|--------|
| 3.1 | Create MicroPauseService (IHostedService) | `Core/Humanization/MicroPauseService.cs` | 2h |
| 3.2 | Create ScheduledBreakService | `Core/Humanization/ScheduledBreakService.cs` | 2h |
| 3.3 | Integrate fatigue with GOAP action delays | `Core/GOAP/GoapAgent.cs` | 2h |

### Phase 4: Configuration & Monitoring (4 hours)

| Task | Description | Files | Effort |
|------|-------------|-------|--------|
| 4.1 | Create HumanizationConfigService | `Core/Humanization/HumanizationConfigService.cs` | 1h |
| 4.2 | Add DI registration | `BlazorServer/DependencyInjection.cs` | 0.5h |
| 4.3 | Create Blazor settings panel | `Frontend/Pages/HumanizationSettings.razor` | 2h |
| 4.4 | Add behavioral metrics dashboard | `Frontend/Components/HumanizationMetrics.razor` | 0.5h |

**Total Estimated Effort: 24 hours**

---

## 7. Verification & Testing

### 7.1 Unit Tests

```csharp
[Fact]
public void HumanizedRandom_NextGaussian_DistributionMatchesExpected()
{
    // Arrange
    const int samples = 10000;
    const double expectedMean = 100;
    const double expectedStdDev = 20;
    var results = new List<int>();
    
    // Act
    for (int i = 0; i < samples; i++)
        results.Add(HumanizedRandom.NextGaussian(expectedMean, expectedStdDev));
    
    // Assert
    double actualMean = results.Average();
    double actualStdDev = Math.Sqrt(results.Average(x => Math.Pow(x - actualMean, 2)));
    
    Assert.InRange(actualMean, expectedMean - 5, expectedMean + 5);
    Assert.InRange(actualStdDev, expectedStdDev - 3, expectedStdDev + 3);
}

[Fact]
public void HumanizedMousePath_GeneratePath_ProducesValidCurve()
{
    // Arrange
    var start = new Point(100, 100);
    var end = new Point(500, 300);
    
    // Act
    var path = HumanizedMousePath.GeneratePath(start, end).ToList();
    
    // Assert
    Assert.True(path.Count >= 10);
    Assert.Equal(start.X, path.First().X, tolerance: 5);
    Assert.Equal(end.X, path.Last().X, tolerance: 5);
    // Verify path isn't perfectly straight
    var midpoint = path[path.Count / 2];
    var linearMidX = (start.X + end.X) / 2;
    Assert.NotEqual(linearMidX, midpoint.X); // Some curve deviation
}
```

### 7.2 Integration Tests

**Automated verification (Feb 5, 2026):**

- [x] Timing distribution approximately Gaussian (`CoreUnitTests/Humanization/HumanizedRandomTests.cs`)
- [x] Mouse paths are fast and allocation-free (`dotnet run --project Benchmarks -c Release -- --filter "*MousePath*"`)
- [x] Fatigue multiplier reaches ~1.3x after 3 hours (`CoreUnitTests/Humanization/FatigueSimulatorTests.cs`)
- [x] Scheduled breaks occur within ±10% of configured interval (`CoreUnitTests/Humanization/FatigueSimulatorTests.cs`)
- [x] Startup/config smoke tests pass (`Scripts/Validate-BlazorLaunch.ps1`, `Scripts/Preflight-OperationReadiness.ps1`)

### 7.3 Manual Verification Checklist

**Optional manual verification (recommended before long unattended runs):**

- Record a 10-minute session, visually confirm natural-looking input
- Compare timing histogram to human baseline
- Verify mouse movements have visible curves (screen recording)
- Confirm breaks occur and resume correctly

---

## 8. Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Humanization causes DPS loss | Medium | Low | Tune timing to stay within GCD constraints |
| Overly aggressive settings cause stuttering | Low | Medium | Performance benchmarks, default safe presets |
| Research outdated, Warden evolved | Medium | High | Architecture inherently safe; humanization is extra layer |
| Configuration complexity confuses users | Medium | Low | Provide well-tuned presets |

---

## 9. Out of Scope

- Kernel-level evasion (not needed with external architecture)
- Memory scanning counter-measures (not needed)
- Signature obfuscation (source available, unique builds)
- Network traffic manipulation
- Active anti-debugging

---

## 10. References

1. Warden Technical Analysis (2026) - Internal Research Report
2. Lehtonen, S. (2020) - Comparative Study of Anti-Cheat Methods in Video Games
3. Yang, J. (2020) - Reverse Engineering of an Obfuscated Binary
4. Human Benchmark (2024) - Reaction Time Statistics
5. Hick's Law - Choice Reaction Time Research

---

## Appendix A: Detection Risk Checklist

Use this checklist to assess current setup safety:

| Check | Status | Notes |
|-------|--------|-------|
| Bot runs as separate process (not injected) | ✅ | External architecture |
| No DLLs loaded into WoW process | ✅ | Uses pixel reading |
| Addon uses only official Lua API | ✅ | DataToColor addon |
| Input sent via standard Windows messages | ✅ | PostMessage API |
| Window title doesn't reveal bot identity | ⚠️ | Configurable |
| Input timing uses Gaussian distribution | ✅ | `Core/Humanization/HumanizedRandom.cs` |
| Mouse movements humanized | ✅ | `Core/Humanization/HumanizedMousePath.cs` |
| Session has scheduled breaks | ✅ | `Core/Humanization/ScheduledBreakService.cs` |
| Reaction times match human distributions | ✅ | `Core/Humanization/HumanizationProvider.cs` + fatigue scaling |

---

## Appendix B: Comparison with Detected Approaches

| Approach | Detection Method | Why We're Safe |
|----------|-----------------|----------------|
| Memory reading bots | Signature scan, handle audit | No memory access |
| Injected DLL bots | Module enumeration, hash check | No injection |
| Lua unlocking bots | Lua call stack origin check | Standard addon |
| Pixel bots (old) | Input timing patterns | Humanization fixes this |
| Hardware cheats (DMA) | Hardware ID tracking | Software only |

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-05 | Initial PRD based on Warden research synthesis |
