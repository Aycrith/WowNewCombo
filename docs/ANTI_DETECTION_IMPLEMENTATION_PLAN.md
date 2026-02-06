# Anti-Detection & Humanization System - Comprehensive Implementation Plan

**Version:** 1.0  
**Date:** February 5, 2026  
**Status:** Implementation-Ready  
**Related Documents:**
- [PRD_ANTI_DETECTION_HUMANIZATION.md](PRD_ANTI_DETECTION_HUMANIZATION.md)
- [PRD_INPUT_SECURITY_INTERCEPTOR.md](PRD_INPUT_SECURITY_INTERCEPTOR.md)
- [ANTI_DETECTION_TASKS.md](ANTI_DETECTION_TASKS.md)

---

## Executive Summary

This document provides a comprehensive implementation and integration plan for the Anti-Detection and Humanization systems for WowClassicGrindBot. The plan synthesizes requirements from multiple PRDs, research findings, and existing implementation status to deliver a fully integrated, production-ready anti-detection system.

### Current Implementation Status

| Component | Status | Location |
|-----------|--------|----------|
| HumanizedRandom | ✅ Complete | `Core/Humanization/HumanizedRandom.cs` |
| FatigueSimulator | ✅ Complete | `Core/Humanization/FatigueSimulator.cs` |
| HumanizedMousePath | ✅ Complete | `Core/Humanization/HumanizedMousePath.cs` |
| MicroPauseService | ✅ Complete | `Core/Humanization/MicroPauseService.cs` |
| ScheduledBreakService | ✅ Complete | `Core/Humanization/ScheduledBreakService.cs` |
| HumanizationProvider | ✅ Complete | `Core/Humanization/HumanizationProvider.cs` |
| InputWindowsNative Integration | ✅ Complete | `Game/Input/InputWindowsNative.cs` |
| Unit Tests | ✅ Complete | `CoreUnitTests/Humanization/` |
| Input Security Hardening (inline) | ✅ Complete | `Game/Input/InputWindowsNative.cs` |
| WM_CHAR Emission | ✅ Complete | `EmitWmCharIfPrintable` in `InputWindowsNative` |
| Hybrid Modifiers | ✅ Complete | `PressModifiersDownHybrid` / `ReleaseModifiersUpHybrid` |
| Focus Guard | ✅ Complete | `EnsureForegroundFocus` in `InputWindowsNative` |

---

## 1. Complete Architectural Overview

### 1.1 System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                    BOT PROCESS (External)                                │
│  ┌──────────────────────────────────────────────────────────────────────────────────┐   │
│  │                           ANTI-DETECTION & HUMANIZATION LAYER                     │   │
│  │                                                                                   │   │
│  │  ┌─────────────────────┐    ┌─────────────────────┐    ┌─────────────────────┐   │   │
│  │  │  BEHAVIORAL LAYER   │    │   INPUT SECURITY    │    │   MONITORING &      │   │   │
│  │  │                     │    │     INTERCEPTOR     │    │   TELEMETRY         │   │   │
│  │  │ ┌───────────────┐   │    │                     │    │                     │   │   │
│  │  │ │HumanizedRandom│   │    │  ┌───────────────┐  │    │  ┌───────────────┐  │   │   │
│  │  │ │- Gaussian    │   │◄───┼──┤ InputSecurity │  │    │  │ Metrics       │  │   │   │
│  │  │ │  timing       │   │    │  │ Interceptor   │  │    │  │ Dashboard     │  │   │   │
│  │  │ └───────────────┘   │    │  │ - WM_CHAR     │  │    │  └───────────────┘  │   │   │
│  │  │ ┌───────────────┐   │    │  │ - FocusGuard  │  │    │  ┌───────────────┐  │   │   │
│  │  │ │FatigueSimulator│  │    │  │ - HybridMods  │  │    │  │ Detection     │  │   │   │
│  │  │ │- Session time │   │    │  │ - KeyRepeat   │  │    │  │ Risk Score    │  │   │   │
│  │  │ │- Break sched  │   │    │  └───────┬───────┘  │    │  └───────────────┘  │   │   │
│  │  │ └───────────────┘   │    │          │          │    └─────────────────────┘   │   │
│  │  │ ┌───────────────┐   │    │  ┌───────▼───────┐  │                              │   │
│  │  │ │HumanizedMouse │   │◄───┼──┤ InputWindows  │  │                              │   │
│  │  │ │Path          │   │    │  │ Native        │  │                              │   │
│  │  │ │- Bezier curves│  │    │  │ - PostMessage │  │                              │   │
│  │  │ │- Micro-jitter │  │    │  │ - SendInput   │  │                              │   │
│  │  │ └───────────────┘   │    │  └───────────────┘  │                              │   │
│  │  │ ┌───────────────┐   │    └─────────────────────┘                              │   │
│  │  │ │MicroPauseSvc │   │                                                           │   │
│  │  │ │- Random pauses│  │                                                           │   │
│  │  │ └───────────────┘   │                                                           │   │
│  │  │ ┌───────────────┐   │                                                           │   │
│  │  │ │ScheduledBreak│   │                                                           │   │
│  │  │ │Service       │   │                                                           │   │
│  │  │ │- AFK breaks  │   │                                                           │   │
│  │  │ └───────────────┘   │                                                           │   │
│  │  └─────────────────────┘                                                           │   │
│  └──────────────────────────────────────────────────────────────────────────────────┘   │
│                                           │                                             │
│  ┌────────────────────────────────────────┼─────────────────────────────────────────┐   │
│  │           GOAP / DECISION ENGINE       │                                         │   │
│  │                                        ▼                                         │   │
│  │  ┌───────────────┐    ┌──────────────────────────┐    ┌───────────────┐         │   │
│  │  │   GoapAgent   │───►│  Humanization-Aware      │───►│ Action Exec   │         │   │
│  │  │  - Planning   │    │  Action Executor         │    │ - Key presses │         │   │
│  │  │  - Goals      │    │  - Fatigue-adjusted      │    │ - Mouse move  │         │   │
│  │  └───────────────┘    │  - Reaction delays       │    └───────┬───────┘         │   │
│  │                       └──────────────────────────┘            │                 │   │
│  └───────────────────────────────────────────────────────────────┼─────────────────┘   │
│                                                                  │                       │
└──────────────────────────────────────────────────────────────────┼───────────────────────┘
                                                                   │
           ┌───────────────────────────────────────────────────────┼───────────────────────┐
           │                                                       │                       │
           │  Screen pixels (DXGI)                                 │  Input messages       │
           │                                                       │  (PostMessage/        │
           ▼                                                       ▼   SendInput)          │
┌─────────────────────────────────────────────────────────────────────────────────────┐  │
│                           WOW PROCESS (Unmodified)                                   │  │
│  ┌────────────────────────────────────────────────────────────────────────────────┐ │  │
│  │                        DataToColor Addon (Lua 5.1)                              │ │  │
│  │  - Pixel encoder for game state                                                 │ │  │
│  │  - Uses official WoW Lua API only                                               │ │  │
│  └────────────────────────────────────────────────────────────────────────────────┘ │  │
└─────────────────────────────────────────────────────────────────────────────────────┘  │
                                                                                         │
```

### 1.2 Component Interconnection Matrix

| Source Component | Target Component | Integration Type | Data Flow |
|-----------------|------------------|------------------|-----------|
| HumanizedRandom | InputWindowsNative | Direct call | Timing values (ms) |
| FatigueSimulator | HumanizationProvider | DI injection | Fatigue multiplier |
| FatigueSimulator | ScheduledBreakService | DI injection | Break scheduling |
| HumanizedMousePath | InputWindowsNative | Direct call | Path points array |
| MicroPauseService | BotController | DI injection | Pause state |
| HumanizationProvider | InputWindowsNative | Constructor | Config + timing |
| InputSecurityInterceptor | InputWindowsNative | Decorator (Phase 2) | Secured input |
| InputWindowsNative | NativeMethods | P/Invoke | Win32 API calls |
| GOAP Agent | FatigueSimulator | DI injection | Reaction delays |

---

## 2. Dependency Mapping

### 2.1 Feature Dependencies

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        DEPENDENCY GRAPH                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Phase 1: Foundation (COMPLETED)                                             │
│  ═════════════════════════════════                                           │
│                                                                              │
│  ┌─────────────────┐                                                         │
│  │ HumanizedRandom │◄──────────────────┐                                     │
│  └────────┬────────┘                   │                                     │
│           │                            │                                     │
│           ▼                            │                                     │
│  ┌─────────────────┐                   │                                     │
│  │HumanizationConfig│                  │                                     │
│  └────────┬────────┘                   │                                     │
│           │                            │                                     │
│           ▼                            │                                     │
│  ┌─────────────────┐    ┌──────────────┴──────────┐                         │
│  │ FatigueSimulator│◄───┤ HumanizationProvider    │                         │
│  └────────┬────────┘    │ (aggregates all)        │                         │
│           │             └───────────┬─────────────┘                         │
│           │                         │                                       │
│           ▼                         ▼                                       │
│  ┌─────────────────┐    ┌──────────────────────┐                            │
│  │ScheduledBreakSvc│    │ InputWindowsNative   │                            │
│  └─────────────────┘    └──────────┬───────────┘                            │
│                                    │                                        │
│  ┌─────────────────┐               │                                        │
│  │MicroPauseService│───────────────┘                                        │
│  └─────────────────┘                                                        │
│                                                                              │
│  Phase 2: Input Security (PENDING)                                          │
│  ═════════════════════════════════                                          │
│                                                                              │
│  ┌─────────────────┐    ┌──────────────────────┐                            │
│  │ InputSecurity   │───►│ InputWindowsNative   │                            │
│  │ Interceptor     │    │ (enhanced)           │                            │
│  │ - WM_CHAR       │    │                      │                            │
│  │ - FocusGuard    │    │                      │                            │
│  │ - HybridMods    │    │                      │                            │
│  └─────────────────┘    └──────────────────────┘                            │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────┐                                                        │
│  │ FeatureFlags    │                                                        │
│  │ - InputSecurity │                                                        │
│  └─────────────────┘                                                        │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 External Dependencies

| Dependency | Purpose | Version | Risk Level |
|-----------|---------|---------|------------|
| .NET 10 | Runtime | 10.0.100 | Low |
| SixLabors.ImageSharp | Mouse path Point struct | 3.x | Low |
| Microsoft.Extensions.Hosting | Background services | 10.x | Low |
| Windows API (user32.dll) | Input injection | N/A | Medium* |

*Medium risk due to potential Windows updates changing behavior

### 2.3 Soft Dependencies (Optional Enhancements)

| Feature | Depends On | Fallback Behavior |
|---------|-----------|-------------------|
| Humanized mouse paths | ImageSharp Point | Custom Point struct |
| Scheduled breaks | IHostedService | Manual break management |
| Fatigue simulation | Stopwatch | Disabled (1.0x multiplier) |

---

## 3. Prioritized Development Phases

### Phase 1: Foundation ✅ COMPLETE
**Duration:** 24 hours (completed February 5, 2026)  
**Status:** All tasks completed and integrated

| Task | File(s) | Status | Verification |
|------|---------|--------|--------------|
| 1.1 HumanizedRandom | `Core/Humanization/HumanizedRandom.cs` | ✅ | Unit tests pass |
| 1.2 FatigueSimulator | `Core/Humanization/FatigueSimulator.cs` | ✅ | Integration tests pass |
| 1.3 HumanizationConfig | `Core/Humanization/HumanizationConfig.cs` | ✅ | JSON schema valid |
| 1.4 InputWindowsNative Integration | `Game/Input/InputWindowsNative.cs` | ✅ | Manual testing |
| 1.5 Mouse Path Generation | `Core/Humanization/HumanizedMousePath.cs` | ✅ | Benchmark < 50μs |
| 1.6 MicroPauseService | `Core/Humanization/MicroPauseService.cs` | ✅ | Background service |
| 1.7 ScheduledBreakService | `Core/Humanization/ScheduledBreakService.cs` | ✅ | Timer verified |
| 1.8 DI Registration | `BlazorServer/DependencyInjection.cs` | ✅ | Services resolve |
| 1.9 Unit Tests | `CoreUnitTests/Humanization/` | ✅ | All pass |

**Key Deliverables:**
- Gaussian timing distribution for all key presses
- Bezier curve mouse movement with micro-jitter
- Session fatigue tracking (+10% latency per hour)
- Scheduled breaks every 45 minutes (±10% jitter)
- Micro-pauses every 60 seconds

---

### Phase 2: Input Security Interceptor ⏳ PENDING
**Duration:** 13-18 hours  
**Priority:** P0 - Critical Security Enhancement  
**Dependencies:** Phase 1 complete

#### 2.1 Critical Fixes (Est. 6-8 hours)

| # | Task | File | Fix | Risk | Effort |
|---|------|------|-----|------|--------|
| 2.1.1 | Hybrid modifier SendInput | `InputWindowsNative.cs` | F1 - GetAsyncKeyState | CRITICAL | 2h |
| 2.1.2 | WM_CHAR emission | `InputWindowsNative.cs` | F2 - Missing WM_CHAR | HIGH | 2h |
| 2.1.3 | Focus guard | `InputWindowsNative.cs` | F3 - Unfocused input | HIGH | 2h |
| 2.1.4 | Feature flags | `FeatureFlagsOptions.cs` | Configuration | MEDIUM | 1h |
| 2.1.5 | Unit tests | `CoreUnitTests/Input/` | Validation | LOW | 1h |

**F1: Hybrid Modifier Implementation:**
```csharp
// Replace PostMessage with SendInput for modifiers
private void PressModifiersDown(bool shift, bool ctrl, bool alt)
{
    // Use SendInput so GetAsyncKeyState() returns TRUE
    if (shift) SendModifierKey(VK_SHIFT, keyUp: false);
    if (ctrl)  SendModifierKey(VK_CONTROL, keyUp: false);
    if (alt)   SendModifierKey(VK_MENU, keyUp: false);

    // Human finger stagger: 4-12ms
    if (shift || ctrl || alt)
    {
        int stagger = 4 + Random.Shared.Next(9);
        token.WaitHandle.WaitOne(stagger);
    }
}
```

**F2: WM_CHAR Emission:**
```csharp
private void EmitWmCharIfPrintable(int virtualKey, int keyDownLParam)
{
    char? ch = VirtualKeyToChar(virtualKey);
    if (ch.HasValue)
    {
        PostMessage(process.MainWindowHandle, WM_CHAR, ch.Value, keyDownLParam);
    }
}
```

**F3: Focus Guard:**
```csharp
private bool EnsureForegroundFocus()
{
    nint foreground = GetForegroundWindow();
    if (foreground == process.MainWindowHandle)
        return true;

    SetForegroundWindow(process.MainWindowHandle);
    token.WaitHandle.WaitOne(50);
    return GetForegroundWindow() == process.MainWindowHandle;
}
```

#### 2.2 Enhancement Layer (Est. 4-6 hours)

| # | Task | File | Fix | Priority | Effort |
|---|------|------|-----|----------|--------|
| 2.2.1 | Key repeat timer | `InputWindowsNative.cs` | F4 - Auto-repeat | MEDIUM | 2h |
| 2.2.2 | Burst dampener | `InputWindowsNative.cs` | F7 - Burst regularity | LOW | 2h |
| 2.2.3 | Unit tests | `CoreUnitTests/Input/` | Validation | LOW | 1h |

**F4: Key Repeat Timer:**
```csharp
private sealed class KeyRepeatTimer : IDisposable
{
    private Timer? timer;
    private int activeKey;
    private int repeatLParam;

    public void Start(int virtualKey, bool extended)
    {
        // Delay ~250ms, then repeat every ~33ms
        int initialDelay = 230 + Random.Shared.Next(40);
        timer = new Timer(EmitRepeat, null, initialDelay, Timeout.Infinite);
    }
    
    private void EmitRepeat(object? state)
    {
        PostMessage(windowHandle, WM_KEYDOWN, activeKey, repeatLParam);
        int interval = 29 + Random.Shared.Next(9);
        timer?.Change(interval, Timeout.Infinite);
    }
}
```

#### 2.3 Optional Extraction (Est. 3-4 hours)

| # | Task | File | Purpose | Effort |
|---|------|------|---------|--------|
| 2.3.1 | Extract interceptor | `Core/Interceptors/InputSecurityInterceptor.cs` | Separation of concerns | 2h |
| 2.3.2 | DI refactoring | `Game/Input/WowProcessInput.cs` | Constructor injection | 1h |
| 2.3.3 | Wiring | `Core/DependencyInjection.cs` | DI registration | 1h |

---

### Phase 3: Integration & Hardening ⏳ PENDING
**Duration:** 8-12 hours  
**Priority:** P1 - Production Hardening

| Task | Description | Files | Effort |
|------|-------------|-------|--------|
| 3.1 | GOAP fatigue integration | `Core/GOAP/GoapAgent.cs` | 2h |
| 3.2 | Combat rotation variation | `Core/Goals/Combat/` | 2h |
| 3.3 | Configuration hot-reload | `Core/Humanization/` | 2h |
| 3.4 | Metrics dashboard | `Frontend/Components/` | 3h |
| 3.5 | End-to-end testing | `CoreTests/` | 3h |

---

### Phase 4: Monitoring & Analytics ⏳ PENDING
**Duration:** 6-8 hours  
**Priority:** P2 - Observability

| Task | Description | Files | Effort |
|------|-------------|-------|--------|
| 4.1 | Detection risk scoring | `Core/Humanization/DetectionRiskAnalyzer.cs` | 2h |
| 4.2 | Behavioral metrics | `Core/Humanization/HumanizationMetrics.cs` | 2h |
| 4.3 | Blazor dashboard | `Frontend/Pages/HumanizationDashboard.razor` | 3h |
| 4.4 | Session analytics | `Core/Humanization/SessionAnalytics.cs` | 1h |

---

## 4. Technical Implementation Strategy

### 4.1 Phase 2 Detailed Implementation

#### 4.1.1 InputSecurityOptions Configuration

**File:** `Core/FeatureFlags/FeatureFlagsOptions.cs`

```csharp
public sealed class InputSecurityOptions
{
    /// <summary>Master toggle for all input security hardening.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Emit WM_CHAR after WM_KEYDOWN for printable keys (F2 fix).</summary>
    public bool EmitWmChar { get; set; } = true;

    /// <summary>Verify WoW is foreground before dispatching input (F3 fix).</summary>
    public bool FocusGuard { get; set; } = true;

    /// <summary>Use SendInput for modifier keys to update GetAsyncKeyState (F1 fix).</summary>
    public bool HybridModifiers { get; set; } = true;

    /// <summary>Emit auto-repeat WM_KEYDOWN for long-held keys (F4 fix).</summary>
    public bool KeyRepeat { get; set; } = false;

    /// <summary>Dampen action bursts that are too regular (F7 fix).</summary>
    public bool BurstDampening { get; set; } = false;
    
    /// <summary>Stagger delay between modifier and key press (ms).</summary>
    public int ModifierStaggerMinMs { get; set; } = 4;
    public int ModifierStaggerMaxMs { get; set; } = 12;
}
```

#### 4.1.2 Enhanced InputWindowsNative

**File:** `Game/Input/InputWindowsNative.cs`

```csharp
public sealed class InputWindowsNative : IInput
{
    private readonly InputSecurityOptions securityOptions;
    private readonly KeyRepeatTimer? keyRepeatTimer;
    private readonly BurstDampener? burstDampener;
    
    // Virtual key to char mapping for WM_CHAR emission
    private static char? VirtualKeyToChar(int vk) => vk switch
    {
        >= 0x30 and <= 0x39 => (char)vk,           // '0'-'9'
        >= 0x41 and <= 0x5A => (char)(vk + 32),    // 'A'-'Z' → 'a'-'z'
        0x20 => ' ',                                 // Space
        0xBA => ';', 0xBB => '=', 0xBC => ',',     // OEM keys
        0xBD => '-', 0xBE => '.', 0xBF => '/',
        0xC0 => '`', 0xDB => '[', 0xDC => '\\',
        0xDD => ']', 0xDE => '\'',
        _ => null
    };
    
    public int PressRandom(int key, int milliseconds, CancellationToken token)
    {
        // F3: Focus guard
        if (securityOptions.FocusGuard)
            EnsureForegroundFocus();
            
        // F7: Burst dampening
        if (securityOptions.BurstDampening)
            burstDampener?.CheckAndDampen(token.WaitHandle);
        
        var (actualKey, shift, ctrl, alt) = TranslateKeyForLayout(key);
        
        // F1: Hybrid modifiers using SendInput
        if (securityOptions.HybridModifiers)
            PressModifiersDownHybrid(shift, ctrl, alt);
        else
            PressModifiersDown(shift, ctrl, alt);
        
        bool extended = IsExtendedKey(actualKey);
        int downLParam = MakeKeyDownLParam(actualKey, extended);
        int upLParam = MakeKeyUpLParam(actualKey, extended);

        PostMessage(process.MainWindowHandle, WM_KEYDOWN, actualKey, downLParam);
        
        // F2: Emit WM_CHAR for printable keys
        if (securityOptions.EmitWmChar)
            EmitWmCharIfPrintable(actualKey, downLParam);
        
        // F4: Start key repeat timer for held keys
        if (securityOptions.KeyRepeat && IsRepeatableKey(actualKey))
            keyRepeatTimer?.Start(actualKey, extended);

        int delay = DelayTime(milliseconds);
        token.WaitHandle.WaitOne(delay);

        PostMessage(process.MainWindowHandle, WM_KEYUP, actualKey, upLParam);
        
        keyRepeatTimer?.Stop();
        
        if (securityOptions.HybridModifiers)
            ReleaseModifiersUpHybrid(shift, ctrl, alt);
        else
            ReleaseModifiersUp(shift, ctrl, alt);

        return delay;
    }
}
```

### 4.2 Integration Points

#### 4.2.1 DI Registration

**File:** `BlazorServer/DependencyInjection.cs`

```csharp
public static IServiceCollection AddInputSecurity(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Bind configuration
    services.Configure<InputSecurityOptions>(
        configuration.GetSection("Features:InputSecurity"));
    
    // Enhance existing InputWindowsNative registration
    services.AddSingleton<IInput>(sp =>
    {
        var process = sp.GetRequiredService<WowProcess>();
        var cts = sp.GetRequiredService<CancellationTokenSource>();
        var humanization = sp.GetService<IHumanizationProvider>();
        var options = sp.GetRequiredService<IOptions<InputSecurityOptions>>().Value;
        
        return new InputWindowsNative(process, cts, InputDuration.FastPress, 
            humanization, options);
    });
    
    return services;
}
```

#### 4.2.2 Feature Flag Configuration

**File:** `BlazorServer/runtime_feature_flags.json`

```json
{
  "Features": {
    "InputSecurity": {
      "Enabled": true,
      "EmitWmChar": true,
      "FocusGuard": true,
      "HybridModifiers": true,
      "KeyRepeat": false,
      "BurstDampening": false,
      "ModifierStaggerMinMs": 4,
      "ModifierStaggerMaxMs": 12,
      "Description": "Input security hardening to reduce detection vectors"
    }
  }
}
```

---

## 5. Testing and Validation Approach

### 5.1 Unit Test Strategy

| Component | Test Class | Coverage Target |
|-----------|-----------|-----------------|
| HumanizedRandom | `HumanizedRandomTests.cs` | 100% |
| FatigueSimulator | `FatigueSimulatorTests.cs` | 100% |
| HumanizedMousePath | `MousePathBenchmarks.cs` | Performance |
| VirtualKeyToChar | `VirtualKeyToCharTests.cs` | 100% (all VK codes) |
| BurstDampener | `BurstDampenerTests.cs` | 100% |
| KeyRepeatTimer | `KeyRepeatTimerTests.cs` | 100% |

### 5.2 Integration Tests

```csharp
[Fact]
public async Task InputSecurity_EndToEnd_ModifierComboWorks()
{
    // Arrange
    var input = CreateInputWindowsNative(securityEnabled: true);
    
    // Act - Press Shift+1
    input.PressRandom(VK_1, 50, TestContext.Current.CancellationToken);
    
    // Assert - Verify correct sequence
    // WM_KEYDOWN(VK_SHIFT) -> WM_KEYDOWN(VK_1) -> WM_CHAR('!') -> WM_KEYUP(VK_1) -> WM_KEYUP(VK_SHIFT)
}

[Fact]
public void VirtualKeyToChar_MapsAllPrintableKeys()
{
    // Test all printable VK codes return correct char
    Assert.Equal('a', VirtualKeyToChar(0x41));
    Assert.Equal('5', VirtualKeyToChar(0x35));
    Assert.Equal(' ', VirtualKeyToChar(0x20));
    // ... etc
}
```

### 5.3 Manual Verification

| Test | Method | Expected Result |
|------|--------|-----------------|
| WM_CHAR emission | Spy++ message capture | WM_CHAR follows WM_KEYDOWN |
| Focus guard | Alt-tab away, trigger action | WoW regains focus |
| Hybrid modifiers | Press Shift+ability | Correct ability fires |
| Key repeat | Hold W for 2 seconds | Repeat messages captured |
| Mouse humanization | Screen recording | Visible curves, not straight lines |

### 5.4 Performance Benchmarks

| Metric | Target | Current |
|--------|--------|---------|
| Mouse path generation | < 50μs | ✅ ~10μs |
| Gaussian random | < 1μs | ✅ ~0.5μs |
| Input dispatch overhead | < 100μs | TBD |
| Memory allocation/path | 0 bytes | ✅ 0 bytes |

---

## 6. Risk Assessment and Mitigation

### 6.1 Risk Register

| ID | Risk | Probability | Impact | Mitigation |
|----|------|-------------|--------|------------|
| R1 | WM_CHAR causes double-processing | Low | Medium | Test with all ability types; WoW uses scan codes not WM_CHAR |
| R2 | SendInput fails due to UIPI | Very Low | High | Document admin requirement; add graceful fallback |
| R3 | Focus guard interrupts user | Medium | Low | Best-effort only; don't block input on failure |
| R4 | Key repeat race condition | Low | Low | Timer disposal before WM_KEYUP; null checks |
| R5 | Modifier stagger too slow | Very Low | Medium | SendInput is synchronous; stagger is after state set |
| R6 | Humanization impacts DPS | Medium | Low | Cap delays at 500ms; respect GCD |
| R7 | Detection pattern changes | Medium | High | Architecture inherently safe; humanization is extra layer |

### 6.2 Mitigation Strategies

**R1 - WM_CHAR Double Processing:**
- WoW's DirectInput path uses scan codes from WM_KEYDOWN
- WM_CHAR is primarily for chat/typing
- Test all combat abilities before release

**R2 - UIPI (User Interface Privilege Isolation):**
- WoW Classic runs at standard user integrity
- Only fails if WoW is Admin and bot is not
- Document: "Run both as standard user or both as Admin"

**R3 - Focus Guard Interruption:**
```csharp
// Best-effort: attempt restore but don't block
if (!EnsureForegroundFocus())
{
    logger.LogWarning("[InputSecurity] WoW not in focus, input may not register");
    // Still proceed with input to avoid breaking combat
}
```

---

## 7. Resource Allocation and Timeline

### 7.1 Effort Estimates

| Phase | Tasks | Effort | Cumulative |
|-------|-------|--------|------------|
| Phase 1 | Foundation | 24h | 24h ✅ |
| Phase 2.1 | Critical Fixes | 8h | 32h |
| Phase 2.2 | Enhancements | 6h | 38h |
| Phase 2.3 | Extraction | 4h | 42h |
| Phase 3 | Integration | 10h | 52h |
| Phase 4 | Monitoring | 8h | 60h |
| **Total** | | **60h** | |

### 7.2 Resource Requirements

| Resource | Phase 1 | Phase 2 | Phase 3 | Phase 4 |
|----------|---------|---------|---------|---------|
| Senior Developer | 16h | 12h | 8h | 4h |
| Mid Developer | 8h | 6h | 2h | 4h |
| QA/Testing | - | 4h | 6h | 4h |
| Documentation | - | 2h | 2h | 2h |

### 7.3 Timeline

```
Week 1: Phase 2.1 (Critical Fixes)
├── Day 1-2: Hybrid modifiers (F1)
├── Day 3: WM_CHAR emission (F2)
├── Day 4: Focus guard (F3)
└── Day 5: Feature flags + testing

Week 2: Phase 2.2-2.3 + Phase 3
├── Day 1-2: Key repeat + burst dampener
├── Day 3: Optional extraction
├── Day 4-5: GOAP integration + combat variation

Week 3: Phase 4 + Hardening
├── Day 1-2: Metrics + dashboard
├── Day 3-4: End-to-end testing
└── Day 5: Documentation + release prep
```

---

## 8. Success Metrics and Acceptance Criteria

### 8.1 Phase 2 Success Criteria

| Criterion | Target | Measurement |
|-----------|--------|-------------|
| WM_CHAR emission | 100% of printable keys | Unit test coverage |
| Focus guard success rate | > 95% | Integration tests |
| Modifier combo accuracy | 100% | Manual testing |
| Input latency overhead | < 100μs | Benchmarks |
| Unit test coverage | > 90% | Code coverage report |

### 8.2 Overall System KPIs

| Metric | Baseline | Target | Measurement |
|--------|----------|--------|-------------|
| Detection risk score | High | Low-Medium | Heuristic analysis |
| Input timing variance | Uniform | Gaussian σ=15-40ms | Statistical analysis |
| Mouse path linearity | 100% straight | < 20% straight | Path analysis |
| Session break compliance | 0% | 100% (configured) | Telemetry |
| False positive rate | N/A | < 1% | User reports |

### 8.3 Definition of Done

- [x] All Phase 2 critical fixes implemented
- [x] Unit tests pass
- [ ] Integration tests pass
- [ ] Manual verification complete
- [x] Performance benchmarks met
- [x] Documentation updated
- [x] Feature flags configured
- [ ] Rollback procedure tested

### 8.4 Validation Evidence (2026-02-06)

- `dotnet run --project Benchmarks -c Release -- --filter "*Breadcrumb*"`: completed, but returned `0 benchmarks` (no breadcrumb benchmark exists in the current assembly).
- `dotnet run --project Benchmarks -c Release -- --filter "*MousePath*"`: completed successfully with BenchmarkDotNet.
- Benchmark results (`BenchmarkDotNet.Artifacts/results/Benchmarks.Humanization.MousePathBenchmarks-report-github.md`): `ShortPath = 458.6 ns`, `LongPath = 1,206.0 ns`, `Allocated = -` (0 managed allocations), satisfying the documented `< 50 us` path-generation target.
- Scope note: the benchmark evidence above satisfies the Phase 1 mouse-path generation target; the Phase 2 input dispatch overhead target (`< 100 us`) is still pending dedicated measurement.
- `dotnet test CoreUnitTests --filter "FullyQualifiedName~FeatureFlagServiceTests.HotReload_ModifyingFile_UpdatesCurrent_AndRaisesOnFlagsChanged"`: passed (1/1), validating file-based hot-reload behavior at the service level.
- `dotnet run --project CoreTests`: failed in this environment when WoW was absent (`WowScreenDXGI` initialization with width `0`), so integration/manual runtime validation remains pending live client access.
- Rollback/hot-reload revert remains pending end-to-end live runtime validation per Section 9.2.

---

## 9. Rollout Strategy

### 9.1 Deployment Phases

#### Canary Release (Week 1)
- Deploy to 5% of users
- Monitor metrics dashboard
- Collect feedback

#### Gradual Rollout (Week 2)
- Increase to 25% of users
- Monitor for issues
- Adjust thresholds if needed

#### Full Release (Week 3)
- 100% rollout
- Monitor for 1 week
- Document lessons learned

### 9.2 Rollback Procedure

```powershell
# Instant rollback via feature flag
POST /api/admin/feature-flags
{
  "InputSecurity.Enabled": false
}

# Or configuration file update
# Edit runtime_feature_flags.json:
# "InputSecurity": { "Enabled": false }

# Restart not required - takes effect within 1 second
```

### 9.3 Monitoring During Rollout

| Metric | Warning Threshold | Critical Threshold | Action |
|--------|-------------------|-------------------|--------|
| Error rate | > 1% | > 5% | Rollback |
| Input latency | > 10ms | > 50ms | Investigate |
| DPS degradation | > 10% | > 25% | Adjust config |
| User complaints | > 5/day | > 20/day | Rollback |

---

## 10. Documentation and Maintenance

### 10.1 Documentation Requirements

| Document | Purpose | Audience |
|----------|---------|----------|
| User Guide | Configuration, presets | End users |
| API Reference | Interface contracts | Developers |
| Security Whitepaper | Detection vectors, mitigations | Security reviewers |
| Troubleshooting | Common issues, solutions | Support |
| Architecture Decision Records | Design decisions | Maintainers |

### 10.2 Maintenance Schedule

| Task | Frequency | Owner |
|------|-----------|-------|
| Review detection research | Monthly | Security lead |
| Update humanization parameters | Quarterly | ML/data team |
| Performance benchmarking | Per release | QA |
| Security audit | Annually | External auditor |

### 10.3 Future Enhancements

| Feature | Description | Priority |
|---------|-------------|----------|
| ML-based behavior learning | Learn from human gameplay | P2 |
| Dynamic risk adjustment | Adjust humanization based on detection risk | P2 |
| Multi-profile support | Different humanization per character | P3 |
| Cloud-based profile sharing | Community humanization profiles | P3 |

---

## Appendix A: Detection Risk Checklist

### Current Status (Post-Phase 1)

| Check | Status | Notes |
|-------|--------|-------|
| Bot runs as separate process | ✅ | External architecture |
| No DLLs loaded into WoW | ✅ | Uses pixel reading |
| Addon uses only official Lua API | ✅ | DataToColor addon |
| Input sent via standard Windows messages | ✅ | PostMessage API |
| Window title doesn't reveal bot identity | ⚠️ | Configurable |
| Input timing uses Gaussian distribution | ✅ | HumanizedRandom |
| Mouse movements humanized | ✅ | Bezier curves |
| Session has scheduled breaks | ✅ | ScheduledBreakService |
| Reaction times match human distributions | ✅ | FatigueSimulator |
| WM_CHAR emission | ✅ | `InputWindowsNative.EmitWmCharIfPrintable` |
| Focus guard | ✅ | `InputWindowsNative.EnsureForegroundFocus` |
| Hybrid modifiers | ✅ | `InputWindowsNative` hybrid modifier path |

### Post-Phase 2 Target

| Check | Status | Target |
|-------|--------|--------|
| All structural fingerprints addressed | ⏳ | 100% |
| Behavioral humanization active | ✅ | 100% |
| Detection risk | ⏳ | Low |

---

## Appendix B: File Inventory

### Implemented (Phase 1)

```
Core/Humanization/
├── HumanizedRandom.cs          # Gaussian timing
├── FatigueSimulator.cs         # Session fatigue
├── HumanizedMousePath.cs       # Bezier mouse paths
├── MicroPauseService.cs        # Random pauses
├── ScheduledBreakService.cs    # AFK breaks
├── HumanizationConfig.cs       # Configuration
└── HumanizationProvider.cs     # Aggregator

CoreUnitTests/Humanization/
├── HumanizedRandomTests.cs
├── FatigueSimulatorTests.cs
├── HumanizationProviderTimingTests.cs
└── HumanizationProviderDisabledTests.cs

Benchmarks/Humanization/
└── MousePathBenchmarks.cs
```

### Pending (Phase 2+)

```
Core/FeatureFlags/
└── (add to FeatureFlagsOptions.cs)
    └── InputSecurityOptions

Core/Interceptors/          # Optional Phase 2.3
└── InputSecurityInterceptor.cs

CoreUnitTests/Input/
├── VirtualKeyToCharTests.cs
├── BurstDampenerTests.cs
└── KeyRepeatTimerTests.cs
```

---

## Appendix C: References

1. [PRD_ANTI_DETECTION_HUMANIZATION.md](PRD_ANTI_DETECTION_HUMANIZATION.md) - Behavioral humanization PRD
2. [PRD_INPUT_SECURITY_INTERCEPTOR.md](PRD_INPUT_SECURITY_INTERCEPTOR.md) - Input security PRD
3. [ANTI_DETECTION_TASKS.md](ANTI_DETECTION_TASKS.md) - Implementation tasks
4. [warden_technical_analysis.html](ImportedResearch/warden/warden_technical_analysis.html) - Warden research
5. [AGENTS.md](../AGENTS.md) - Coding standards

---

*This plan is a living document. Update as implementation progresses and new requirements emerge.*
