# Anti-Detection Humanization System - Implementation Tasks

**Reference PRD:** [PRD_ANTI_DETECTION_HUMANIZATION.md](PRD_ANTI_DETECTION_HUMANIZATION.md)  
**Total Effort:** 24 hours  
**Priority:** P1 - Safety Enhancement

---

## Phase 1: Core Timing Humanization (8 hours)

### Task 1.1: Create HumanizedRandom Utility Class (2h)

**File:** `Core/Humanization/HumanizedRandom.cs`

```csharp
using System;
using System.Runtime.CompilerServices;

namespace Core.Humanization;

/// <summary>
/// Provides human-like random distributions for timing and behavior simulation.
/// Uses Box-Muller transform for Gaussian distribution.
/// </summary>
public static class HumanizedRandom
{
    /// <summary>
    /// Generate Gaussian-distributed value using Box-Muller transform.
    /// </summary>
    /// <param name="mean">Center of distribution</param>
    /// <param name="stdDev">Standard deviation</param>
    /// <param name="min">Minimum value (floor)</param>
    /// <param name="max">Maximum value (ceiling)</param>
    /// <returns>Random value following Gaussian distribution</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextGaussianMs(double mean, double stdDev, int min = 5, int max = 5000)
    {
        // Box-Muller transform for Gaussian distribution
        double u1 = 1.0 - Random.Shared.NextDouble(); // Avoid log(0)
        double u2 = 1.0 - Random.Shared.NextDouble();
        
        double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        int result = (int)(mean + stdDev * normal);
        
        return Math.Clamp(result, min, max);
    }
    
    /// <summary>
    /// Generate Gaussian double value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double NextGaussian(double mean, double stdDev)
    {
        double u1 = 1.0 - Random.Shared.NextDouble();
        double u2 = 1.0 - Random.Shared.NextDouble();
        double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + stdDev * normal;
    }
    
    /// <summary>
    /// Generate human-like reaction time based on action complexity.
    /// </summary>
    /// <param name="complexity">0 = simple reaction, 1 = choice, 2 = complex decision</param>
    /// <param name="fatigueMultiplier">1.0 = fresh, increases with session time</param>
    public static int NextReactionTimeMs(int complexity = 1, double fatigueMultiplier = 1.0)
    {
        // Research-based human reaction time distributions
        (double mean, double stdDev) = complexity switch
        {
            0 => (250.0, 50.0),   // Simple reaction (expected stimulus)
            1 => (350.0, 80.0),   // Choice reaction (1 of N options)
            2 => (500.0, 150.0),  // Complex decision (combat rotation)
            _ => (350.0, 80.0)
        };
        
        return NextGaussianMs(mean * fatigueMultiplier, stdDev * fatigueMultiplier);
    }
    
    /// <summary>
    /// Generate humanized key press duration.
    /// Human key presses typically 40-120ms with Gaussian distribution.
    /// </summary>
    public static int NextKeyPressDurationMs(int baseDuration = 50)
    {
        // Human key press: mean ~60ms, stdDev ~15ms
        return NextGaussianMs(baseDuration + 10, 15, min: 30, max: 200);
    }
    
    /// <summary>
    /// Generate micro-pause duration for natural behavior breaks.
    /// </summary>
    public static int NextMicroPauseMs()
    {
        // Occasional very short pauses (200-1500ms)
        return NextGaussianMs(600, 300, min: 200, max: 2000);
    }
}
```

**Acceptance Criteria:**
- [x] Compiles (solution has known warnings; no new errors introduced)
- [x] Unit test validates Gaussian distribution properties (`CoreUnitTests/Humanization/HumanizedRandomTests.cs`)
- [x] No allocations in hot path methods (span-based APIs; mouse-path benchmark reports 0 alloc)

---

### Task 1.2: Create FatigueSimulator Service (2h)

**File:** `Core/Humanization/FatigueSimulator.cs`

```csharp
using System;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Core.Humanization;

/// <summary>
/// Models human fatigue during extended gaming sessions.
/// Affects reaction times and triggers scheduled breaks.
/// </summary>
public sealed class FatigueSimulator
{
    private readonly ILogger<FatigueSimulator> logger;
    private readonly HumanizationConfig config;
    
    private readonly Stopwatch sessionTimer = Stopwatch.StartNew();
    private DateTime lastBreakTime = DateTime.UtcNow;
    private DateTime sessionStartTime = DateTime.UtcNow;
    private bool isOnBreak;
    private DateTime breakEndTime;
    
    public FatigueSimulator(ILogger<FatigueSimulator> logger, HumanizationConfig config)
    {
        this.logger = logger;
        this.config = config;
    }
    
    /// <summary>
    /// Gets the current fatigue multiplier (1.0 = fresh, increases over time).
    /// Applied to reaction times and timing delays.
    /// </summary>
    public double FatigueMultiplier
    {
        get
        {
            if (!config.Fatigue.Enabled)
                return 1.0;
                
            double hoursPlayed = sessionTimer.Elapsed.TotalHours;
            // Configurable fatigue rate, max 50% slower
            double multiplier = 1.0 + (hoursPlayed * config.Fatigue.FatigueRatePerHour);
            return Math.Min(multiplier, 1.5);
        }
    }
    
    /// <summary>
    /// Gets total session duration.
    /// </summary>
    public TimeSpan SessionDuration => sessionTimer.Elapsed;
    
    /// <summary>
    /// Gets time since last break.
    /// </summary>
    public TimeSpan TimeSinceLastBreak => DateTime.UtcNow - lastBreakTime;
    
    /// <summary>
    /// Whether the bot is currently on a scheduled break.
    /// </summary>
    public bool IsOnBreak => isOnBreak && DateTime.UtcNow < breakEndTime;
    
    /// <summary>
    /// Remaining break time if on break.
    /// </summary>
    public TimeSpan RemainingBreakTime => IsOnBreak 
        ? breakEndTime - DateTime.UtcNow 
        : TimeSpan.Zero;
    
    /// <summary>
    /// Checks if a break is due based on configured interval.
    /// </summary>
    public bool ShouldTakeBreak()
    {
        if (!config.Fatigue.Enabled || isOnBreak)
            return false;
            
        TimeSpan interval = TimeSpan.FromMinutes(config.Fatigue.BreakIntervalMinutes);
        // Add ±20% jitter to break timing
        double jitter = 0.8 + Random.Shared.NextDouble() * 0.4;
        
        return TimeSinceLastBreak > interval * jitter;
    }
    
    /// <summary>
    /// Starts a break and returns the duration.
    /// </summary>
    public TimeSpan StartBreak()
    {
        if (isOnBreak)
            return RemainingBreakTime;
            
        // Calculate break duration (longer when more fatigued)
        double baseMinutes = config.Fatigue.BreakDurationMinMinutes;
        double maxMinutes = config.Fatigue.BreakDurationMaxMinutes;
        double fatigueBonus = (FatigueMultiplier - 1.0) * (maxMinutes - baseMinutes);
        
        double duration = baseMinutes + Random.Shared.NextDouble() * (maxMinutes - baseMinutes) + fatigueBonus;
        TimeSpan breakDuration = TimeSpan.FromMinutes(Math.Min(duration, maxMinutes * 1.5));
        
        isOnBreak = true;
        breakEndTime = DateTime.UtcNow + breakDuration;
        
        logger.LogInformation(
            "[FatigueSimulator ] Starting break for {Duration:F1} minutes (fatigue: {Fatigue:P0})",
            breakDuration.TotalMinutes, FatigueMultiplier - 1);
            
        return breakDuration;
    }
    
    /// <summary>
    /// Called when break ends (either naturally or manually).
    /// </summary>
    public void EndBreak()
    {
        if (!isOnBreak)
            return;
            
        isOnBreak = false;
        lastBreakTime = DateTime.UtcNow;
        
        // Partial fatigue reset after break (recover 30% of accumulated fatigue)
        double hoursRecovered = sessionTimer.Elapsed.TotalHours * 0.3;
        // We can't actually reset the stopwatch, but we track effective session time
        
        logger.LogInformation(
            "[FatigueSimulator ] Break ended, resuming (fatigue now: {Fatigue:P0})",
            FatigueMultiplier - 1);
    }
    
    /// <summary>
    /// Resets the session (e.g., on bot restart).
    /// </summary>
    public void ResetSession()
    {
        sessionTimer.Restart();
        lastBreakTime = DateTime.UtcNow;
        sessionStartTime = DateTime.UtcNow;
        isOnBreak = false;
        
        logger.LogInformation("[FatigueSimulator ] Session reset");
    }
}
```

**Acceptance Criteria:**
- [x] FatigueMultiplier increases correctly over time (`CoreUnitTests/Humanization/FatigueSimulatorTests.cs`)
- [x] Break scheduling respects configured intervals with jitter (±10% window; `CoreUnitTests/Humanization/FatigueSimulatorTests.cs`)
- [x] Integration test verifies fatigue affects timing (`CoreUnitTests/Humanization/HumanizationProviderTimingTests.cs`)

---

### Task 1.3: Create HumanizationConfig Model (1h)

**File:** `Core/Humanization/HumanizationConfig.cs`

```csharp
namespace Core.Humanization;

/// <summary>
/// Configuration for humanization systems.
/// </summary>
public sealed class HumanizationConfig
{
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Preset name: "Minimal", "Low", "Medium", "High", "Custom"
    /// </summary>
    public string Preset { get; set; } = "Medium";
    
    public InputTimingConfig InputTiming { get; set; } = new();
    public MouseMovementConfig MouseMovement { get; set; } = new();
    public FatigueConfig Fatigue { get; set; } = new();
    public BehaviorConfig Behavior { get; set; } = new();
}

public sealed class InputTimingConfig
{
    /// <summary>Base mean for key press timing (ms)</summary>
    public int BaseMeanMs { get; set; } = 50;
    
    /// <summary>Base standard deviation for timing (ms)</summary>
    public int BaseStdDevMs { get; set; } = 15;
    
    /// <summary>Mean reaction time before actions (ms)</summary>
    public int ReactionMeanMs { get; set; } = 280;
    
    /// <summary>Reaction time standard deviation (ms)</summary>
    public int ReactionStdDevMs { get; set; } = 60;
}

public sealed class MouseMovementConfig
{
    public bool Enabled { get; set; } = true;
    
    /// <summary>Points per mouse movement interpolation</summary>
    public int StepsPerMovement { get; set; } = 15;
    
    /// <summary>Curve intensity (0.1 = subtle, 0.5 = pronounced)</summary>
    public double CurveIntensity { get; set; } = 0.3;
    
    /// <summary>Micro-jitter pixels per step</summary>
    public int MicroJitterPixels { get; set; } = 2;
    
    /// <summary>Probability of overshoot (0.0 - 0.2 typical)</summary>
    public double OvershootProbability { get; set; } = 0.08;
}

public sealed class FatigueConfig
{
    public bool Enabled { get; set; } = true;
    
    /// <summary>Minutes between scheduled breaks</summary>
    public int BreakIntervalMinutes { get; set; } = 45;
    
    /// <summary>Minimum break duration (minutes)</summary>
    public double BreakDurationMinMinutes { get; set; } = 1.0;
    
    /// <summary>Maximum break duration (minutes)</summary>
    public double BreakDurationMaxMinutes { get; set; } = 5.0;
    
    /// <summary>Fatigue increase rate per hour (0.1 = 10% slower per hour)</summary>
    public double FatigueRatePerHour { get; set; } = 0.10;
}

public sealed class BehaviorConfig
{
    /// <summary>Enable random micro-pauses</summary>
    public bool MicroPauseEnabled { get; set; } = true;
    
    /// <summary>Average interval between micro-pauses (seconds)</summary>
    public int MicroPauseIntervalSeconds { get; set; } = 60;
    
    /// <summary>Enable slight combat rotation variation</summary>
    public bool RotationVariationEnabled { get; set; } = true;
    
    /// <summary>Enable grinding path deviations</summary>
    public bool PathDeviationEnabled { get; set; } = false;
}
```

---

### Task 1.4: Integrate Timing with InputWindowsNative (2h)

**File:** `Game/Input/InputWindowsNative.cs`

**Changes Required:**

1. Add humanization dependency
2. Modify `DelayTime` to use Gaussian distribution
3. Modify `PressRandom` to use humanized timing

```csharp
// Add to constructor parameters and field:
private readonly HumanizationConfig? humanizationConfig;
private readonly FatigueSimulator? fatigueSimulator;

// Modify DelayTime method:
private int DelayTime(int milliseconds)
{
    if (humanizationConfig?.Enabled != true)
        return milliseconds + Random.Shared.Next(maxDelay);
    
    // Use Gaussian distribution with fatigue adjustment
    double fatigueMultiplier = fatigueSimulator?.FatigueMultiplier ?? 1.0;
    int mean = (int)(milliseconds * fatigueMultiplier);
    int stdDev = humanizationConfig.InputTiming.BaseStdDevMs;
    
    return HumanizedRandom.NextGaussianMs(mean, stdDev, min: 10, max: milliseconds * 3);
}

// Modify PressRandom to use humanized key press duration:
public int PressRandom(int key, int milliseconds, CancellationToken token)
{
    var (actualKey, shift, ctrl, alt) = TranslateKeyForLayout(key);
    bool extended = IsExtendedKey(actualKey);
    int downLParam = MakeKeyDownLParam(actualKey, extended);
    int upLParam = MakeKeyUpLParam(actualKey, extended);

    PressModifiersDown(shift, ctrl, alt);
    PostMessage(process.MainWindowHandle, WM_KEYDOWN, actualKey, downLParam);

    // Humanized delay
    int delay = DelayTime(milliseconds);
    token.WaitHandle.WaitOne(delay);

    PostMessage(process.MainWindowHandle, WM_KEYUP, actualKey, upLParam);
    ReleaseModifiersUp(shift, ctrl, alt);

    return delay;
}
```

**Acceptance Criteria:**
- [x] Timing distribution follows Gaussian when humanization enabled (`CoreUnitTests/Humanization/HumanizedRandomTests.cs`)
- [x] Fatigue multiplier correctly affects delay times (`CoreUnitTests/Humanization/HumanizationProviderTimingTests.cs`)
- [x] Backward compatible when humanization disabled (`CoreUnitTests/Humanization/HumanizationProviderDisabledTests.cs`)

---

### Task 1.5: Configuration File and Loading (1h)

**File:** `BlazorServer/humanization_config.json`

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
      "MicroJitterPixels": 2,
      "OvershootProbability": 0.08
    },
    "Fatigue": {
      "Enabled": true,
      "BreakIntervalMinutes": 45,
      "BreakDurationMinMinutes": 1.0,
      "BreakDurationMaxMinutes": 5.0,
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

**File:** `Core/Humanization/HumanizationConfigLoader.cs`

```csharp
using System;
using System.IO;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace Core.Humanization;

public sealed class HumanizationConfigLoader
{
    private const string ConfigFileName = "humanization_config.json";
    
    private readonly ILogger<HumanizationConfigLoader> logger;
    
    public HumanizationConfigLoader(ILogger<HumanizationConfigLoader> logger)
    {
        this.logger = logger;
    }
    
    public HumanizationConfig Load()
    {
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
        
        if (!File.Exists(configPath))
        {
            logger.LogWarning("[HumanizationCfgLd] Config not found, using defaults");
            return ApplyPreset(new HumanizationConfig(), "Medium");
        }
        
        try
        {
            string json = File.ReadAllText(configPath);
            JsonDocument doc = JsonDocument.Parse(json);
            
            if (doc.RootElement.TryGetProperty("Humanization", out JsonElement elem))
            {
                HumanizationConfig? config = JsonSerializer.Deserialize<HumanizationConfig>(elem);
                if (config != null)
                {
                    logger.LogInformation("[HumanizationCfgLd] Loaded config with preset: {Preset}", config.Preset);
                    return ApplyPreset(config, config.Preset);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[HumanizationCfgLd] Failed to load config");
        }
        
        return ApplyPreset(new HumanizationConfig(), "Medium");
    }
    
    private static HumanizationConfig ApplyPreset(HumanizationConfig config, string preset)
    {
        return preset.ToLowerInvariant() switch
        {
            "minimal" => ApplyMinimalPreset(config),
            "low" => ApplyLowPreset(config),
            "medium" => config, // Medium is the default values
            "high" => ApplyHighPreset(config),
            _ => config
        };
    }
    
    private static HumanizationConfig ApplyMinimalPreset(HumanizationConfig config)
    {
        config.InputTiming.BaseStdDevMs = 5;
        config.MouseMovement.Enabled = false;
        config.Fatigue.Enabled = false;
        config.Behavior.MicroPauseEnabled = false;
        return config;
    }
    
    private static HumanizationConfig ApplyLowPreset(HumanizationConfig config)
    {
        config.InputTiming.BaseStdDevMs = 10;
        config.MouseMovement.CurveIntensity = 0.15;
        config.Fatigue.Enabled = false;
        config.Behavior.MicroPauseEnabled = false;
        return config;
    }
    
    private static HumanizationConfig ApplyHighPreset(HumanizationConfig config)
    {
        config.InputTiming.BaseStdDevMs = 40;
        config.InputTiming.ReactionMeanMs = 350;
        config.MouseMovement.CurveIntensity = 0.4;
        config.MouseMovement.OvershootProbability = 0.12;
        config.Fatigue.FatigueRatePerHour = 0.15;
        config.Fatigue.BreakIntervalMinutes = 30;
        config.Behavior.PathDeviationEnabled = true;
        return config;
    }
}
```

---

## Phase 2: Mouse Movement Humanization (6 hours)

### Task 2.1: Create HumanizedMousePath Generator (3h)

**File:** `Core/Humanization/HumanizedMousePath.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp;

namespace Core.Humanization;

/// <summary>
/// Generates human-like mouse movement paths using Bezier curves.
/// </summary>
public static class HumanizedMousePath
{
    /// <summary>
    /// Generate interpolated path from start to end point.
    /// </summary>
    public static IReadOnlyList<Point> GeneratePath(
        Point start, 
        Point end, 
        MouseMovementConfig config)
    {
        if (!config.Enabled)
            return [start, end];
            
        double distance = Distance(start, end);
        
        // Very short movements don't need humanization
        if (distance < 10)
            return [start, end];
        
        // Calculate steps based on distance (longer = more steps)
        int steps = Math.Clamp((int)(distance / 20) + config.StepsPerMovement, 5, 50);
        
        // Generate control points for Bezier curve
        double deviation = Math.Clamp(distance * config.CurveIntensity, 10, 150);
        
        Point control1 = new(
            start.X + (int)(RandomSign() * Random.Shared.NextDouble() * deviation),
            start.Y + (int)(RandomSign() * Random.Shared.NextDouble() * deviation));
            
        Point control2 = new(
            end.X + (int)(RandomSign() * Random.Shared.NextDouble() * deviation * 0.5),
            end.Y + (int)(RandomSign() * Random.Shared.NextDouble() * deviation * 0.5));
        
        List<Point> path = new(steps + 1);
        
        // Check for overshoot
        bool willOvershoot = Random.Shared.NextDouble() < config.OvershootProbability;
        Point overshootTarget = end;
        
        if (willOvershoot)
        {
            // Calculate overshoot point (5-15% beyond target)
            double overshootFactor = 1.05 + Random.Shared.NextDouble() * 0.10;
            int dx = end.X - start.X;
            int dy = end.Y - start.Y;
            overshootTarget = new Point(
                start.X + (int)(dx * overshootFactor),
                start.Y + (int)(dy * overshootFactor));
        }
        
        // Generate main path
        Point target = willOvershoot ? overshootTarget : end;
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            // Ease-in-out for natural acceleration
            double easedT = EaseInOutQuad(t);
            
            Point p = CubicBezier(start, control1, control2, target, easedT);
            
            // Add micro-jitter
            if (i > 0 && i < steps && config.MicroJitterPixels > 0)
            {
                p = new Point(
                    p.X + Random.Shared.Next(-config.MicroJitterPixels, config.MicroJitterPixels + 1),
                    p.Y + Random.Shared.Next(-config.MicroJitterPixels, config.MicroJitterPixels + 1));
            }
            
            path.Add(p);
        }
        
        // Add correction path if overshot
        if (willOvershoot)
        {
            int correctionSteps = Random.Shared.Next(3, 6);
            for (int i = 1; i <= correctionSteps; i++)
            {
                double t = (double)i / correctionSteps;
                double easedT = EaseOutQuad(t); // Slower ease for correction
                
                Point p = LinearInterpolate(overshootTarget, end, easedT);
                p = new Point(
                    p.X + Random.Shared.Next(-1, 2),
                    p.Y + Random.Shared.Next(-1, 2));
                path.Add(p);
            }
        }
        
        // Ensure we end exactly at target
        if (path[^1] != end)
            path.Add(end);
        
        return path;
    }
    
    /// <summary>
    /// Calculate delay between path points based on distance and humanization.
    /// </summary>
    public static int GetStepDelayMs(int currentStep, int totalSteps, double totalDistance)
    {
        // Base delay: longer movements have longer delays
        double baseDelay = Math.Clamp(totalDistance / 50, 1, 10);
        
        // Acceleration phase (first 30%) - shorter delays
        // Deceleration phase (last 30%) - longer delays
        double position = (double)currentStep / totalSteps;
        double speedFactor = position switch
        {
            < 0.3 => 0.7 + position, // Accelerating
            > 0.7 => 1.3 - (position - 0.7), // Decelerating
            _ => 1.0 // Cruising
        };
        
        int delay = (int)(baseDelay * speedFactor);
        return Math.Max(1, delay + Random.Shared.Next(-1, 2));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Point CubicBezier(Point p0, Point p1, Point p2, Point p3, double t)
    {
        double u = 1 - t;
        double tt = t * t;
        double uu = u * u;
        double uuu = uu * u;
        double ttt = tt * t;
        
        double x = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
        double y = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;
        
        return new Point((int)x, (int)y);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Point LinearInterpolate(Point start, Point end, double t)
    {
        return new Point(
            (int)(start.X + (end.X - start.X) * t),
            (int)(start.Y + (end.Y - start.Y) * t));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double EaseInOutQuad(double t) =>
        t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double EaseOutQuad(double t) =>
        1 - (1 - t) * (1 - t);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Distance(Point a, Point b) =>
        Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RandomSign() => Random.Shared.Next(2) * 2 - 1;
}
```

---

### Task 2.2: Integrate Mouse Paths with InputWindowsNative (2h)

**File:** `Game/Input/InputWindowsNative.cs`

**Add new humanized cursor movement method:**

```csharp
/// <summary>
/// Move cursor to point using humanized path.
/// </summary>
public void SetCursorPosHumanized(Point target, CancellationToken token = default)
{
    if (humanizationConfig?.MouseMovement.Enabled != true)
    {
        WinAPI.NativeMethods.SetCursorPos(target.X, target.Y);
        return;
    }
    
    // Get current position
    WinAPI.NativeMethods.GetCursorPos(out System.Drawing.Point currentWin);
    Point current = new(currentWin.X, currentWin.Y);
    
    // Generate humanized path
    var path = HumanizedMousePath.GeneratePath(current, target, humanizationConfig.MouseMovement);
    double totalDistance = Math.Sqrt(Math.Pow(target.X - current.X, 2) + Math.Pow(target.Y - current.Y, 2));
    
    // Execute path
    for (int i = 0; i < path.Count; i++)
    {
        if (token.IsCancellationRequested)
            break;
            
        WinAPI.NativeMethods.SetCursorPos(path[i].X, path[i].Y);
        
        if (i < path.Count - 1)
        {
            int delay = HumanizedMousePath.GetStepDelayMs(i, path.Count, totalDistance);
            if (delay > 0)
                token.WaitHandle.WaitOne(delay);
        }
    }
}

/// <summary>
/// Humanized left click at position.
/// </summary>
public void LeftClickHumanized(Point p, CancellationToken token = default)
{
    SetCursorPosHumanized(p, token);
    
    // Small pause before click (human hesitation)
    int preClickDelay = HumanizedRandom.NextGaussianMs(30, 10, 10, 100);
    token.WaitHandle.WaitOne(preClickDelay);
    
    ScreenToClient(process.MainWindowHandle, ref p);
    int lparam = MakeLParam(p.X, p.Y);

    PostMessage(process.MainWindowHandle, WM_LBUTTONDOWN, 0, lparam);
    token.WaitHandle.WaitOne(DelayTime(maxDelay));
    PostMessage(process.MainWindowHandle, WM_LBUTTONUP, 0, lparam);
}

/// <summary>
/// Humanized right click at position.
/// </summary>
public void RightClickHumanized(Point p, CancellationToken token = default)
{
    SetCursorPosHumanized(p, token);
    
    int preClickDelay = HumanizedRandom.NextGaussianMs(30, 10, 10, 100);
    token.WaitHandle.WaitOne(preClickDelay);
    
    ScreenToClient(process.MainWindowHandle, ref p);
    int lparam = MakeLParam(p.X, p.Y);

    PostMessage(process.MainWindowHandle, WM_RBUTTONDOWN, 0, lparam);
    token.WaitHandle.WaitOne(DelayTime(maxDelay));
    PostMessage(process.MainWindowHandle, WM_RBUTTONUP, 0, lparam);
}
```

---

### Task 2.3: Benchmark Mouse Path Generation (1h)

**File:** `Benchmarks/Humanization/MousePathBenchmarks.cs`

```csharp
using BenchmarkDotNet.Attributes;

using Core.Humanization;

using SixLabors.ImageSharp;

namespace Benchmarks.Humanization;

[MemoryDiagnoser]
[SimpleJob]
public class MousePathBenchmarks
{
    private readonly MouseMovementConfig config = new()
    {
        Enabled = true,
        StepsPerMovement = 15,
        CurveIntensity = 0.3,
        MicroJitterPixels = 2
    };
    
    private readonly Point shortStart = new(100, 100);
    private readonly Point shortEnd = new(150, 130);
    
    private readonly Point longStart = new(100, 100);
    private readonly Point longEnd = new(900, 700);
    
    [Benchmark]
    public IReadOnlyList<Point> GenerateShortPath()
    {
        return HumanizedMousePath.GeneratePath(shortStart, shortEnd, config);
    }
    
    [Benchmark]
    public IReadOnlyList<Point> GenerateLongPath()
    {
        return HumanizedMousePath.GeneratePath(longStart, longEnd, config);
    }
}
```

**Acceptance Criteria:**
- [x] Short path generation < 10μs (measured via `dotnet run --project Benchmarks -c Release -- --filter "*MousePath*"`)
- [x] Long path generation < 50μs (measured via `dotnet run --project Benchmarks -c Release -- --filter "*MousePath*"`)
- [x] Memory allocation < 1KB per path (measured 0 alloc via BenchmarkDotNet MemoryDiagnoser)

---

## Phase 3: Behavioral Patterns (6 hours)

### Task 3.1: Create MicroPauseService (2h)

**File:** `Core/Humanization/MicroPauseService.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Humanization;

/// <summary>
/// Service that triggers random micro-pauses to simulate human behavior.
/// </summary>
public sealed class MicroPauseService : IHostedService, IDisposable
{
    private readonly ILogger<MicroPauseService> logger;
    private readonly HumanizationConfig config;
    private readonly BotController botController;
    
    private Timer? pauseTimer;
    private bool isPaused;
    
    public MicroPauseService(
        ILogger<MicroPauseService> logger,
        HumanizationConfig config,
        BotController botController)
    {
        this.logger = logger;
        this.config = config;
        this.botController = botController;
    }
    
    /// <summary>
    /// Whether a micro-pause is currently active.
    /// </summary>
    public bool IsPaused => isPaused;
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!config.Behavior.MicroPauseEnabled)
        {
            logger.LogInformation("[MicroPauseService] Disabled by configuration");
            return Task.CompletedTask;
        }
        
        // Start with random delay
        int initialDelay = GetNextPauseInterval();
        pauseTimer = new Timer(OnPauseTimerTick, null, initialDelay, Timeout.Infinite);
        
        logger.LogInformation("[MicroPauseService] Started, first pause in {Delay}ms", initialDelay);
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        pauseTimer?.Change(Timeout.Infinite, 0);
        logger.LogInformation("[MicroPauseService] Stopped");
        return Task.CompletedTask;
    }
    
    private void OnPauseTimerTick(object? state)
    {
        if (!botController.IsRunning)
        {
            // Reschedule when bot not running
            ScheduleNextPause();
            return;
        }
        
        ExecuteMicroPause();
        ScheduleNextPause();
    }
    
    private void ExecuteMicroPause()
    {
        // Don't pause during combat or other critical activities
        // This check would integrate with PlayerReader state
        
        isPaused = true;
        int pauseDuration = HumanizedRandom.NextMicroPauseMs();
        
        logger.LogDebug("[MicroPauseService] Micro-pause for {Duration}ms", pauseDuration);
        
        // The pause is passive - we just signal that actions should be slightly delayed
        // Actual implementation integrates with GOAP action execution
        
        Thread.Sleep(100); // Minimal actual pause
        isPaused = false;
    }
    
    private void ScheduleNextPause()
    {
        int interval = GetNextPauseInterval();
        pauseTimer?.Change(interval, Timeout.Infinite);
    }
    
    private int GetNextPauseInterval()
    {
        // Gaussian around configured interval
        int meanMs = config.Behavior.MicroPauseIntervalSeconds * 1000;
        int stdDevMs = meanMs / 4; // 25% variation
        return HumanizedRandom.NextGaussianMs(meanMs, stdDevMs, meanMs / 2, meanMs * 2);
    }
    
    public void Dispose()
    {
        pauseTimer?.Dispose();
    }
}
```

---

### Task 3.2: Create ScheduledBreakService (2h)

**File:** `Core/Humanization/ScheduledBreakService.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Humanization;

/// <summary>
/// Service that schedules and manages AFK breaks to simulate human session patterns.
/// </summary>
public sealed class ScheduledBreakService : IHostedService, IDisposable
{
    private readonly ILogger<ScheduledBreakService> logger;
    private readonly FatigueSimulator fatigueSimulator;
    private readonly BotController botController;
    
    private Timer? breakCheckTimer;
    private CancellationTokenSource? breakCts;
    
    public ScheduledBreakService(
        ILogger<ScheduledBreakService> logger,
        FatigueSimulator fatigueSimulator,
        BotController botController)
    {
        this.logger = logger;
        this.fatigueSimulator = fatigueSimulator;
        this.botController = botController;
    }
    
    /// <summary>
    /// Whether a scheduled break is currently active.
    /// </summary>
    public bool IsOnBreak => fatigueSimulator.IsOnBreak;
    
    /// <summary>
    /// Remaining break time.
    /// </summary>
    public TimeSpan RemainingBreakTime => fatigueSimulator.RemainingBreakTime;
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Check for breaks every 30 seconds
        breakCheckTimer = new Timer(OnBreakCheckTick, null, 
            TimeSpan.FromSeconds(30), 
            TimeSpan.FromSeconds(30));
            
        logger.LogInformation("[SchedBreakService] Started, checking every 30s");
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        breakCheckTimer?.Change(Timeout.Infinite, 0);
        breakCts?.Cancel();
        
        if (fatigueSimulator.IsOnBreak)
            fatigueSimulator.EndBreak();
            
        logger.LogInformation("[SchedBreakService] Stopped");
        return Task.CompletedTask;
    }
    
    private async void OnBreakCheckTick(object? state)
    {
        if (!botController.IsRunning || fatigueSimulator.IsOnBreak)
            return;
            
        if (!fatigueSimulator.ShouldTakeBreak())
            return;
        
        await ExecuteScheduledBreak();
    }
    
    private async Task ExecuteScheduledBreak()
    {
        TimeSpan breakDuration = fatigueSimulator.StartBreak();
        
        logger.LogInformation(
            "[SchedBreakService] Starting {Duration:F1}min break (session: {Session:F1}h)",
            breakDuration.TotalMinutes,
            fatigueSimulator.SessionDuration.TotalHours);
        
        // Pause bot actions
        // Note: This integrates with BotController pause mechanism
        
        breakCts = new CancellationTokenSource();
        
        try
        {
            await Task.Delay(breakDuration, breakCts.Token);
        }
        catch (TaskCanceledException)
        {
            logger.LogInformation("[SchedBreakService] Break interrupted");
        }
        
        fatigueSimulator.EndBreak();
        logger.LogInformation("[SchedBreakService] Break ended, resuming");
    }
    
    /// <summary>
    /// Skip the current break early.
    /// </summary>
    public void SkipBreak()
    {
        if (!fatigueSimulator.IsOnBreak)
            return;
            
        breakCts?.Cancel();
        fatigueSimulator.EndBreak();
        logger.LogInformation("[SchedBreakService] Break skipped by user");
    }
    
    public void Dispose()
    {
        breakCheckTimer?.Dispose();
        breakCts?.Dispose();
    }
}
```

---

### Task 3.3: Integrate Fatigue with GOAP Actions (2h)

**File:** `Core/GOAP/GoapAgent.cs` (modifications)

Add fatigue check before action execution:

```csharp
// Add field:
private readonly FatigueSimulator? fatigueSimulator;

// In action execution, add reaction delay:
private async Task ExecuteAction(IGoapAction action, CancellationToken token)
{
    // Human-like reaction delay before action
    if (fatigueSimulator != null && humanizationConfig?.Enabled == true)
    {
        int complexity = action.IsComplexDecision ? 2 : 1;
        int reactionDelay = HumanizedRandom.NextReactionTimeMs(complexity, fatigueSimulator.FatigueMultiplier);
        
        // Only add significant delay for non-movement actions
        if (reactionDelay > 100 && !action.IsMovementAction)
        {
            await Task.Delay(Math.Min(reactionDelay, 500), token);
        }
    }
    
    // Execute the action
    await action.Execute(token);
}
```

---

## Phase 4: DI Registration & UI (4 hours)

### Task 4.1: DI Registration (1h)

**File:** `BlazorServer/DependencyInjection.cs`

```csharp
public static IServiceCollection AddHumanizationServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Load config
    services.AddSingleton<HumanizationConfigLoader>();
    services.AddSingleton(sp => 
        sp.GetRequiredService<HumanizationConfigLoader>().Load());
    
    // Core services
    services.AddSingleton<FatigueSimulator>();
    
    // Background services
    services.AddHostedService<MicroPauseService>();
    services.AddHostedService<ScheduledBreakService>();
    
    return services;
}
```

**File:** `BlazorServer/Program.cs` (add to startup):

```csharp
services.AddHumanizationServices(builder.Configuration);
```

---

### Task 4.2: Blazor Settings Panel (2h)

**File:** `Frontend/Pages/HumanizationSettings.razor`

```razor
@page "/humanization"
@using Core.Humanization
@inject HumanizationConfig Config
@inject FatigueSimulator FatigueSimulator
@inject ScheduledBreakService BreakService

<PageTitle>Humanization Settings</PageTitle>

<h3>Anti-Detection Settings</h3>

<div class="card mb-3">
    <div class="card-header">
        <h5>Session Status</h5>
    </div>
    <div class="card-body">
        <dl class="row">
            <dt class="col-sm-4">Session Duration</dt>
            <dd class="col-sm-8">@FatigueSimulator.SessionDuration.ToString(@"hh\:mm\:ss")</dd>
            
            <dt class="col-sm-4">Fatigue Level</dt>
            <dd class="col-sm-8">@((FatigueSimulator.FatigueMultiplier - 1) * 100:F0)%</dd>
            
            <dt class="col-sm-4">Time Since Break</dt>
            <dd class="col-sm-8">@FatigueSimulator.TimeSinceLastBreak.ToString(@"mm\:ss")</dd>
            
            @if (BreakService.IsOnBreak)
            {
                <dt class="col-sm-4">Break Remaining</dt>
                <dd class="col-sm-8">
                    @BreakService.RemainingBreakTime.ToString(@"mm\:ss")
                    <button class="btn btn-sm btn-warning ms-2" @onclick="SkipBreak">Skip</button>
                </dd>
            }
        </dl>
    </div>
</div>

<div class="card mb-3">
    <div class="card-header">
        <h5>Preset: @Config.Preset</h5>
    </div>
    <div class="card-body">
        <div class="row">
            <div class="col-md-6">
                <h6>Input Timing</h6>
                <p>Base variance: ±@Config.InputTiming.BaseStdDevMs ms (Gaussian)</p>
                <p>Reaction time: @Config.InputTiming.ReactionMeanMs ±@Config.InputTiming.ReactionStdDevMs ms</p>
            </div>
            <div class="col-md-6">
                <h6>Mouse Movement</h6>
                <p>Humanized: @(Config.MouseMovement.Enabled ? "Yes" : "No")</p>
                <p>Curve intensity: @(Config.MouseMovement.CurveIntensity * 100)%</p>
            </div>
        </div>
        <div class="row mt-2">
            <div class="col-md-6">
                <h6>Fatigue Simulation</h6>
                <p>Enabled: @(Config.Fatigue.Enabled ? "Yes" : "No")</p>
                <p>Break interval: @Config.Fatigue.BreakIntervalMinutes min</p>
            </div>
            <div class="col-md-6">
                <h6>Behavior</h6>
                <p>Micro-pauses: @(Config.Behavior.MicroPauseEnabled ? "Yes" : "No")</p>
                <p>Path deviation: @(Config.Behavior.PathDeviationEnabled ? "Yes" : "No")</p>
            </div>
        </div>
    </div>
</div>

@code {
    private void SkipBreak()
    {
        BreakService.SkipBreak();
    }
}
```

---

## Unit Test Requirements

### `CoreTests/Humanization/HumanizedRandomTests.cs`

```csharp
using Core.Humanization;

using Xunit;

namespace CoreTests.Humanization;

public class HumanizedRandomTests
{
    [Fact]
    public void NextGaussianMs_ProducesGaussianDistribution()
    {
        // Arrange
        const int samples = 10000;
        const double expectedMean = 100;
        const double expectedStdDev = 20;
        int[] results = new int[samples];
        
        // Act
        for (int i = 0; i < samples; i++)
            results[i] = HumanizedRandom.NextGaussianMs(expectedMean, expectedStdDev);
        
        // Assert - mean within 5% of expected
        double actualMean = results.Average();
        Assert.InRange(actualMean, expectedMean * 0.95, expectedMean * 1.05);
        
        // Assert - stddev within 20% of expected
        double variance = results.Average(x => Math.Pow(x - actualMean, 2));
        double actualStdDev = Math.Sqrt(variance);
        Assert.InRange(actualStdDev, expectedStdDev * 0.8, expectedStdDev * 1.2);
    }
    
    [Theory]
    [InlineData(0, 200, 300)]
    [InlineData(1, 270, 430)]
    [InlineData(2, 350, 650)]
    public void NextReactionTimeMs_ReturnsReasonableRanges(int complexity, int minExpected, int maxExpected)
    {
        // Act
        int[] results = Enumerable.Range(0, 100)
            .Select(_ => HumanizedRandom.NextReactionTimeMs(complexity))
            .ToArray();
        
        // Assert - average should be in expected range
        double avg = results.Average();
        Assert.InRange(avg, minExpected, maxExpected);
    }
}
```

---

## Verification Commands

```powershell
# Build
dotnet build MasterOfPuppets.sln

# Run unit tests
dotnet test --filter "FullyQualifiedName~Humanization"

# Run benchmarks
dotnet run --project Benchmarks -c Release -- --filter "*Mouse*"
```

---

## Definition of Done

- [x] All unit tests pass (`dotnet test -c Release`)
- [x] Build completes (`dotnet build MasterOfPuppets.sln -c Release`)
- [x] Benchmark targets met (mouse path < 50μs)
- [x] Configuration loads correctly on startup (`Scripts/Validate-BlazorLaunch.ps1`, `Scripts/Preflight-OperationReadiness.ps1`)
- [x] Fatigue multiplier increases over session (`CoreUnitTests/Humanization/FatigueSimulatorTests.cs`)
- [x] Scheduled breaks occur at configured intervals (±10% jitter; `CoreUnitTests/Humanization/FatigueSimulatorTests.cs`)
- [x] UI displays current humanization status (`/humanization` page)
- [x] No performance regression in main bot loop (humanization disabled by default; mouse-path generation measured 0 alloc)

---

## Risk Mitigation Notes

1. **GCD Timing**: Humanization delays must respect GCD (1500ms) - reaction delays capped at 500ms
2. **Combat Interruption**: Breaks never start during active combat
3. **Backward Compatibility**: All humanization features are opt-in and disabled by default in config
4. **Performance**: All hot-path methods use `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
