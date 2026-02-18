# PRD: Input Security Interceptor — Anti-Cheat Hardening

**Version:** 2.0  
**Date:** February 5, 2026  
**Status:** Implemented (Phase 1+2 inline in `InputWindowsNative`)  
**Implementation Note (Feb 6, 2026):** Hybrid modifiers, WM_CHAR emission, focus guard, key-repeat, and burst dampening are implemented in `Game/Input/InputWindowsNative.cs`. Phase 3 extractor (`InputSecurityInterceptor`) remains an optional refactor, not a functional blocker.  
**Companion:** [PRD_ANTI_DETECTION_HUMANIZATION.md](PRD_ANTI_DETECTION_HUMANIZATION.md) (behavioral layer)  

---

## 1. Problem Statement

The bot delivers synthetic keyboard and mouse input to WoW Classic via two Windows mechanisms:

| Mechanism | Used For | Files |
|-----------|----------|-------|
| `PostMessage` (WM_KEYDOWN/WM_KEYUP) | All regular key presses, clicks | `InputWindowsNative.cs` |
| `SendInput` (KEYEVENTF_SCANCODE) | Modifier+key combos via `SendKeyWithModifiers` | `NativeMethods.cs` |

While the companion PRD covers *behavioral* humanization (Gaussian timing, Bézier mouse, fatigue), this document addresses a distinct problem: **structural fingerprints in the Win32 message stream itself** that Warden/anti-cheat can detect regardless of how human-like the timing is.

A perfectly human-timed `PostMessage` is still detectable if:
- It arrives when the window doesn't have keyboard focus
- It lacks the `WM_CHAR` that `TranslateMessage` would produce
- The modifier key state visible to `GetAsyncKeyState` doesn't match what the message claims
- Long-held keys produce no auto-repeat messages

These are **protocol-level** defects — the messages are structurally malformed compared to real hardware input. No amount of timing humanization fixes them.

---

## 2. Current Input Pipeline

```
┌─────────────────────────────────────────────────────────────────────────┐
│  ConfigurableInput           High-level game actions (PressRandom,     │
│  (Core/Input/)               mount, interact, etc.)                    │
│                              Adds pre-action reaction delay            │
│                              Delegates to WowProcessInput              │
├─────────────────────────────────────────────────────────────────────────┤
│  WowProcessInput             Key state tracking (BitArray keysDown)    │
│  (Game/Input/)               Modifier-aware pressing                   │
│                              Creates InputWindowsNative internally     │
│                              in its constructor with `new`             │
├─────────────────────────────────────────────────────────────────────────┤
│  InputWindowsNative          Actual Win32 dispatch                     │
│  (Game/Input/)               Implements IInput interface               │
│                              PostMessage for all keys                  │
│                              Layout translation via VkKeyScanExW       │
│                              Humanized mouse paths if provider exists  │
├─────────────────────────────────────────────────────────────────────────┤
│  NativeMethods               P/Invoke declarations                     │
│  (WinAPI/)                   PostMessage, SendInput, SendKeyWithMods   │
│                              lParam builders (MakeKeyDownLParam, etc.) │
│                              Scan code resolution (MapVirtualKeyA)     │
└─────────────────────────────────────────────────────────────────────────┘
```

**Critical architectural fact:** `WowProcessInput` instantiates `InputWindowsNative` directly via `new` in its constructor (line 52 of WowProcessInput.cs). There is no DI indirection between these two layers. This constrains where an interceptor can be placed.

---

## 3. Detection Fingerprint Catalogue

### 3.1 Summary Table

| ID | Fingerprint | Risk | Fixable? | Phase |
|----|-------------|------|----------|-------|
| F1 | PostMessage modifiers invisible to `GetAsyncKeyState` | **CRITICAL** | Yes — hybrid SendInput for modifiers | 1 |
| F2 | No `WM_CHAR` after `WM_KEYDOWN` for printable keys | **HIGH** | Yes — emit matching WM_CHAR | 1 |
| F3 | Input delivered to unfocused window | **HIGH** | Yes — focus guard | 1 |
| F4 | No auto-repeat `WM_KEYDOWN` for long-held keys | **MEDIUM** | Yes — repeat emitter for movement keys | 2 |
| F5 | `LLKHF_INJECTED` flag on `SendInput` events | **LOW** | No — set by kernel; mitigated by restricting SendInput to modifiers only | N/A |
| F6 | `dwExtraInfo = 0` in `SendInput` | **NEGLIGIBLE** | Not recommended — wrong spoofed value is worse than zero | N/A |
| F7 | Burst regularity in combat rotations | **LOW** | Yes — burst dampener | 2 |

### 3.2 Detailed Analysis

#### F1: GetAsyncKeyState Mismatch (CRITICAL)

**How it works today:**

```
WowProcessInput.PressRandomWithModifier(ConsoleKey.D1, ModifierKey.Shift, 40):
  1. nativeInput.KeyDown(VK_SHIFT)      →  PostMessage(hWnd, WM_KEYDOWN, 0x10, ...) 
  2. nativeInput.PressRandom(VK_1, 40)  →  PostMessage(hWnd, WM_KEYDOWN, 0x31, ...)
  3. Thread.Sleep(~40ms)
  4. PostMessage(hWnd, WM_KEYUP, 0x31, ...)
  5. nativeInput.KeyUp(VK_SHIFT)        →  PostMessage(hWnd, WM_KEYUP, 0x10, ...)
```

**The defect:** `PostMessage` places messages in the window's *message queue* but does **not** update the *asynchronous key state table*. The code itself documents this:

> *"WoW uses GetAsyncKeyState() which only works at system level"* — [NativeMethods.cs line 349](WinAPI/NativeMethods.cs#L349)

When WoW processes step 2's `WM_KEYDOWN` for '1' and calls `GetAsyncKeyState(VK_SHIFT)`, it returns **FALSE** — Shift is not *really* held down at the system level. WoW sees an unmodified '1' press.

**The existing `SendKeyWithModifiers` in `NativeMethods` already solves this** using `SendInput`, but the primary input path (`WowProcessInput.PressRandomWithModifier`) uses `PostMessage` instead.

**Why this is critical:** This isn't just a detection vector — it's a *functional bug*. Shift+1 abilities may simply not fire, which forces the user to put all abilities on unmodified keys.

#### F2: Missing WM_CHAR (HIGH)

When a human presses the '1' key, the real message sequence is:

```
1. WM_KEYDOWN (VK_1, scanCode=0x02)
2. WM_CHAR    ('1' = 0x31, same lParam)      ← generated by TranslateMessage()
3. WM_KEYUP   (VK_1, scanCode=0x02)
```

The bot sends only steps 1 and 3. WoW's DirectInput layer primarily uses `WM_KEYDOWN` scan codes, so WM_CHAR omission doesn't break functionality. But Warden could trivially check: "received WM_KEYDOWN for a character key but no WM_CHAR followed within the same message pump cycle."

**Which keys produce WM_CHAR:** Only keys where `TranslateMessage` would call the keyboard layout's `ToUnicode` and get a result: A-Z, 0-9, Space, and OEM symbol keys (`;`, `=`, `,`, `-`, `.`, `/`, `[`, `]`, `\`, `'`, `` ` ``). NOT F-keys, arrows, Escape, Tab, Enter, modifiers.

#### F3: Input While Unfocused (HIGH)

`PostMessage` successfully delivers messages to WoW's window even when it's behind another window. Real keyboard input only arrives at the window with keyboard focus (as documented by Microsoft: *"The system posts keyboard messages to the message queue of the foreground thread that created the window with the keyboard focus"*).

If Warden calls `GetForegroundWindow()` at any point and finds it's NOT the WoW window, yet keyboard messages are arriving, this is directly detectable and has zero false-positive risk for Warden — no legitimate scenario produces this condition.

#### F4: Missing Auto-Repeat (MEDIUM)

When a human holds a movement key (W, A, S, D) for 500ms+, Windows generates:
```
t=0ms:    WM_KEYDOWN (repeatCount=1, prevState=0)  ← initial press
t=250ms:  WM_KEYDOWN (repeatCount=1, prevState=1)  ← first repeat (bit 30 = 1)
t=283ms:  WM_KEYDOWN (repeatCount=1, prevState=1)  ← second repeat
...repeats every ~33ms...
t=end:    WM_KEYUP   (repeatCount=1, prevState=1)
```

The bot sends ONE `WM_KEYDOWN` then silence until `WM_KEYUP`. For short ability presses (30-55ms), this is irrelevant — the key isn't held long enough to trigger auto-repeat. But for movement keys held via `KeyDown`/`KeyUp` pairs (often 500ms-3s), the absence of repeat messages is detectable.

#### F5: LLKHF_INJECTED (LOW — Accept the Risk)

Windows kernel (`win32k.sys`) automatically sets `LLKHF_INJECTED` (bit 4) in the flags of `KBDLLHOOKSTRUCT` for any input injected via `SendInput`. This is unforgeable from user-mode. Any low-level keyboard hook can detect it.

**Why it's LOW risk:** (a) `PostMessage` completely avoids this flag since it never passes through the input pipe; (b) in the proposed design, `SendInput` is only used for modifier keys (Shift/Ctrl/Alt), which would have extremely high false-positive rates if flagged — accessibility software, remote desktop, and gaming peripherals all inject modifier events; (c) Warden in WoW Classic has not historically installed `WH_KEYBOARD_LL` hooks due to the performance cost.

#### F6: dwExtraInfo = 0 (NEGLIGIBLE — Don't Fix)

Currently `input.ki.dwExtraInfo = 0`. Real HID drivers *may* set non-zero values via `SetMessageExtraInfo`, but the specific values vary by hardware vendor and driver version. Setting a **wrong** spoofed value is more suspicious than zero. Many legitimate applications (AutoHotkey, remote desktop, accessibility tools) use `dwExtraInfo = 0`.

---

## 4. Architecture: Where the Interceptor Lives

### 4.1 Constraint Analysis

The ideal interceptor location is between `WowProcessInput` and `InputWindowsNative`, wrapping the `IInput` interface. However, `WowProcessInput` constructs `InputWindowsNative` directly:

```csharp
// WowProcessInput.cs line 52
nativeInput = new(process, cts, InputDuration.FastPress, humanization);
```

There is no DI indirection here — `InputWindowsNative` is not injected. We have three options:

| Option | Approach | Invasiveness |
|--------|----------|-------------|
| **A** | Modify `InputWindowsNative` directly | Lowest — changes one existing file |
| **B** | Refactor `WowProcessInput` to accept `IInput` via constructor, inject interceptor as decorator | Medium — changes constructor + DI wiring |
| **C** | Make `InputWindowsNative` create the interceptor internally | Low — interceptor is a composition detail |

**Recommendation: Option A** for Phase 1 (modify `InputWindowsNative` directly). The interceptor concerns — WM_CHAR emission, focus guard, modifier hybrid — are tightly coupled to the Win32 dispatch logic already in this file. Extracting them into a separate decorator that reimplements all 8 `IInput` methods just to add a `PostMessage(WM_CHAR)` call is over-engineering.

Option B should be deferred until the interceptor logic grows complex enough to warrant its own class (Phase 2+).

### 4.2 Phase 1 Target Architecture

```
 ConfigurableInput ──► WowProcessInput ──► InputWindowsNative (MODIFIED)
                                                    │
                                           ┌────────┴────────────────┐
                                           │ Existing dispatch logic  │
                                           │ + Focus guard (F3)       │
                                           │ + WM_CHAR emission (F2)  │
                                           │ + Hybrid modifiers (F1)  │
                                           └─────────────────────────┘
```

### 4.3 Phase 2 Target Architecture (if complexity warrants extraction)

```
 ConfigurableInput ──► WowProcessInput ──► InputSecurityInterceptor (NEW, IInput)
                                                    │
                                                    │  wraps
                                                    ▼
                                              InputWindowsNative (original)
```

---

## 5. Implementation Specifications

### 5.1 F1 Fix: Hybrid Modifier Strategy

**File:** `Game/Input/InputWindowsNative.cs` — methods `PressModifiersDown` and `ReleaseModifiersUp`

**Current code:**
```csharp
private void PressModifiersDown(bool shift, bool ctrl, bool alt)
{
    if (shift)
        PostMessage(process.MainWindowHandle, WM_KEYDOWN, VK_SHIFT, MakeKeyDownLParam(VK_SHIFT));
    if (ctrl)
        PostMessage(process.MainWindowHandle, WM_KEYDOWN, VK_CONTROL, MakeKeyDownLParam(VK_CONTROL));
    if (alt)
        PostMessage(process.MainWindowHandle, WM_KEYDOWN, VK_MENU, MakeKeyDownLParam(VK_MENU));
}
```

**Proposed change:**
```csharp
private void PressModifiersDown(bool shift, bool ctrl, bool alt)
{
    // Use SendInput for modifiers so GetAsyncKeyState() returns TRUE.
    // PostMessage only puts messages in the window queue — it does NOT update 
    // the async key state table. WoW calls GetAsyncKeyState(VK_SHIFT) to detect
    // modifier state, so modifiers sent via PostMessage are invisible to the game.
    //
    // SendInput gets the LLKHF_INJECTED flag, but only on the modifier key —
    // the action key (sent via PostMessage) remains unflagged. Flagging
    // modifier keys has extremely high false-positive rates (accessibility
    // software, remote desktop, gaming peripherals) so anti-cheat systems
    // do not flag modifiers.
    if (shift) SendModifierKey(VK_SHIFT, keyUp: false);
    if (ctrl)  SendModifierKey(VK_CONTROL, keyUp: false);
    if (alt)   SendModifierKey(VK_MENU, keyUp: false);

    // Human finger stagger: real humans cannot press modifier + key simultaneously.
    // Add 4-12ms delay (measured human range for two-key combos).
    if (shift || ctrl || alt)
    {
        int stagger = 4 + Random.Shared.Next(9); // 4-12ms
        token.WaitHandle.WaitOne(stagger);
    }
}

private void ReleaseModifiersUp(bool shift, bool ctrl, bool alt)
{
    // Stagger release: humans lift modifier finger slightly after main key
    if (shift || ctrl || alt)
    {
        int stagger = 2 + Random.Shared.Next(7); // 2-8ms
        token.WaitHandle.WaitOne(stagger);
    }

    // Release in reverse order (LIFO — natural finger sequence)
    if (alt)   SendModifierKey(VK_MENU, keyUp: true);
    if (ctrl)  SendModifierKey(VK_CONTROL, keyUp: true);
    if (shift) SendModifierKey(VK_SHIFT, keyUp: true);
}

private static void SendModifierKey(int virtualKey, bool keyUp)
{
    INPUT input = CreateModifierInput(virtualKey, keyUp);
    SendInput(1, [input], Marshal.SizeOf<INPUT>());
}

/// <summary>
/// Creates an INPUT struct for a modifier key using scan codes.
/// Separate from CreateKeyInput in NativeMethods to keep that method internal.
/// </summary>
private static INPUT CreateModifierInput(int virtualKey, bool keyUp)
{
    uint flags = KEYEVENTF_SCANCODE;
    if (keyUp) flags |= KEYEVENTF_KEYUP;

    INPUT input = new();
    input.type = INPUT_KEYBOARD;
    input.ki.wVk = 0;
    input.ki.wScan = (ushort)MapVirtualKeyA((uint)virtualKey, MAPVK_VK_TO_VSC);
    input.ki.dwFlags = flags;
    input.ki.time = 0;
    input.ki.dwExtraInfo = 0;
    return input;
}
```

**Why the hybrid approach works:**

```
BEFORE (all PostMessage — broken modifiers):
  PostMessage(SHIFT down) → queue only, GetAsyncKeyState = FALSE
  PostMessage(1 down)     → WoW reads GetAsyncKeyState(SHIFT) = FALSE → wrong ability

AFTER (hybrid — working modifiers):
  SendInput(SHIFT down)   → updates async key state, GetAsyncKeyState = TRUE
  Sleep(4-12ms)           → human finger stagger
  PostMessage(1 down)     → WoW reads GetAsyncKeyState(SHIFT) = TRUE → correct ability!
  Sleep(~40ms)            → humanized key hold
  PostMessage(1 up)
  Sleep(2-8ms)            → human release stagger
  SendInput(SHIFT up)     → clears async key state
```

The `LLKHF_INJECTED` flag is only set on the modifier key (Shift/Ctrl/Alt), never on the action key (1, 2, etc.). Warden inspecting action keys sees no injection flag.

### 5.2 F2 Fix: WM_CHAR Emission

**File:** `Game/Input/InputWindowsNative.cs`

Add a helper method and call it after every `PostMessage(WM_KEYDOWN)` for a printable key:

```csharp
/// <summary>
/// Emits WM_CHAR matching what TranslateMessage() would produce after WM_KEYDOWN.
/// Only for character-producing keys — not F-keys, arrows, modifiers, etc.
/// </summary>
private void EmitWmCharIfPrintable(int virtualKey, int keyDownLParam)
{
    char? ch = VirtualKeyToChar(virtualKey);
    if (ch.HasValue)
    {
        // WM_CHAR lParam matches the preceding WM_KEYDOWN lParam
        PostMessage(process.MainWindowHandle, WM_CHAR, ch.Value, keyDownLParam);
    }
}

private static char? VirtualKeyToChar(int vk)
{
    return vk switch
    {
        >= 0x30 and <= 0x39 => (char)vk,       // '0'-'9'
        >= 0x41 and <= 0x5A => (char)(vk + 32), // 'A'-'Z' → 'a'-'z' (unshifted)
        0x20 => ' ',                             // Space
        0xBA => ';',   // VK_OEM_1
        0xBB => '=',   // VK_OEM_PLUS
        0xBC => ',',   // VK_OEM_COMMA
        0xBD => '-',   // VK_OEM_MINUS
        0xBE => '.',   // VK_OEM_PERIOD
        0xBF => '/',   // VK_OEM_2
        0xC0 => '`',   // VK_OEM_3
        0xDB => '[',   // VK_OEM_4
        0xDC => '\\',  // VK_OEM_5
        0xDD => ']',   // VK_OEM_6
        0xDE => '\'',  // VK_OEM_7
        _ => null       // F-keys, arrows, modifiers, Escape, Tab, Enter → no WM_CHAR
    };
}
```

**Integration points:** Add `EmitWmCharIfPrintable(actualKey, downLParam)` after each `PostMessage(WM_KEYDOWN)` call in `PressRandom`, `PressFixed`, and `KeyDown`. Example for `PressRandom`:

```csharp
public int PressRandom(int key, int milliseconds, CancellationToken token)
{
    var (actualKey, shift, ctrl, alt) = TranslateKeyForLayout(key);
    bool extended = IsExtendedKey(actualKey);
    int downLParam = MakeKeyDownLParam(actualKey, extended);
    int upLParam = MakeKeyUpLParam(actualKey, extended);

    PressModifiersDown(shift, ctrl, alt);

    PostMessage(process.MainWindowHandle, WM_KEYDOWN, actualKey, downLParam);
    EmitWmCharIfPrintable(actualKey, downLParam);  // ← NEW

    int delay = DelayTime(milliseconds);
    token.WaitHandle.WaitOne(delay);

    PostMessage(process.MainWindowHandle, WM_KEYUP, actualKey, upLParam);
    ReleaseModifiersUp(shift, ctrl, alt);

    return delay;
}
```

### 5.3 F3 Fix: Focus Guard

**File:** `Game/Input/InputWindowsNative.cs`

Add a focus validation check that runs before key dispatch:

```csharp
/// <summary>
/// Ensures WoW window is the foreground window before sending input.
/// PostMessage delivers messages to unfocused windows — real keyboards don't.
/// Returns true if WoW has focus (or focus was successfully restored).
/// </summary>
private bool EnsureForegroundFocus()
{
    nint foreground = GetForegroundWindow();
    if (foreground == process.MainWindowHandle)
        return true;

    // Attempt to bring WoW to foreground
    SetForegroundWindow(process.MainWindowHandle);
    token.WaitHandle.WaitOne(50); // let window activation settle

    // Verify it actually took
    return GetForegroundWindow() == process.MainWindowHandle;
}
```

**Integration:** Call `EnsureForegroundFocus()` at the start of `PressRandom`, `PressFixed`, `KeyDown`, `LeftClick`, and `RightClick`. If it returns false, the input should still proceed (to avoid breaking movement mid-combat) but log a warning.

**Note:** This guard should NOT prevent input when it fails — it should be advisory/best-effort. Blocking input because of a brief focus loss would cause the character to stop mid-combat, which is worse than the detection risk. The purpose is to *usually* ensure focus is correct, not to strictly gate all input.

### 5.4 F4 Fix: Auto-Repeat for Held Keys (Phase 2)

This only matters for keys held via `KeyDown`/`KeyUp` pairs (movement keys), not for `PressRandom`/`PressFixed` (hold times 30-200ms, below Windows' 250ms repeat threshold).

**Approach:** When `KeyDown` is called for a movement key, start a background timer. After ~250ms, begin emitting `WM_KEYDOWN` with `previousState=1` (bit 30) at ~33ms intervals until `KeyUp` is called.

```csharp
/// <summary>
/// Emits Windows auto-repeat WM_KEYDOWN messages for held keys.
/// Real keyboards generate repeats after ~250ms, then every ~33ms.
/// </summary>
private sealed class KeyRepeatTimer : IDisposable
{
    private readonly nint windowHandle;
    private Timer? timer;
    private int activeKey;
    private int repeatLParam;

    public KeyRepeatTimer(nint windowHandle)
    {
        this.windowHandle = windowHandle;
    }

    public void Start(int virtualKey, bool extended)
    {
        Stop();
        activeKey = virtualKey;

        uint scanCode = MapVirtualKeyA((uint)virtualKey, MAPVK_VK_TO_VSC);
        // Repeat lParam: bit 30 = 1 (key was already down)
        repeatLParam = 1;                         // repeat count = 1
        repeatLParam |= (int)(scanCode << 16);    // scan code
        if (extended) repeatLParam |= (1 << 24);  // extended flag
        repeatLParam |= (1 << 30);                // previous key state = 1 (repeat)

        // Delay first repeat by ~250ms (±20ms jitter), then repeat every ~33ms (±4ms)
        int initialDelay = 230 + Random.Shared.Next(40);
        timer = new Timer(EmitRepeat, null, initialDelay, Timeout.Infinite);
    }

    private void EmitRepeat(object? state)
    {
        PostMessage(windowHandle, WM_KEYDOWN, activeKey, repeatLParam);

        // Schedule next repeat with jitter
        int interval = 29 + Random.Shared.Next(9); // 29-37ms
        timer?.Change(interval, Timeout.Infinite);
    }

    public void Stop()
    {
        timer?.Dispose();
        timer = null;
        activeKey = 0;
    }

    public void Dispose() => Stop();
}
```

**Integration:** Create one `KeyRepeatTimer` per `InputWindowsNative` instance (lazy). Start it in `KeyDown`, stop it in `KeyUp`.

### 5.5 F7 Fix: Burst Dampener (Phase 2)

Track recent action timestamps in a ring buffer. If actions cluster with too-regular spacing (low variance), inject a small random delay.

```csharp
/// <summary>
/// Tracks recent input event timestamps and dampens bursts that are too regular.
/// Combat rotations can produce GCD-locked sequences with mechanical precision.
/// </summary>
private sealed class BurstDampener
{
    private readonly long[] timestamps;
    private int index;
    private readonly double maxActionsPerSecond;

    public BurstDampener(int windowSize = 8, double maxActionsPerSecond = 12.0)
    {
        timestamps = new long[windowSize];
        this.maxActionsPerSecond = maxActionsPerSecond;
    }

    /// <summary>
    /// Call before dispatching an input event. May sleep briefly to dampen bursts.
    /// </summary>
    public void CheckAndDampen(WaitHandle waitHandle)
    {
        long now = Stopwatch.GetTimestamp();
        int slot = index % timestamps.Length;
        long oldest = timestamps[slot];
        timestamps[slot] = now;
        index++;

        if (oldest == 0) return; // ring not full yet

        double windowSec = (double)(now - oldest) / Stopwatch.Frequency;
        double rate = timestamps.Length / windowSec;

        if (rate > maxActionsPerSecond)
        {
            int dampMs = 20 + Random.Shared.Next(80); // 20-99ms
            waitHandle.WaitOne(dampMs);
        }
    }
}
```

---

## 6. NativeMethods Changes

Minimal exposure needed. `InputWindowsNative` already has access to all the `NativeMethods` statics it needs (`PostMessage`, `SendInput`, `MapVirtualKeyA`, the `INPUT`/`KEYBDINPUT` structs, and the `KEYEVENTF_*` constants). These are all `public static`.

**Only change needed:** The `CreateKeyInput` method in `NativeMethods` is `private`. Either:
- (a) Make it `internal` so `InputWindowsNative` can reuse it, or
- (b) Define the modifier input creation locally in `InputWindowsNative` (shown in §5.1 above)

**Recommendation:** Option (b) — keeps the change to `NativeMethods.cs` at zero lines. The modifier input struct is trivial to construct locally.

---

## 7. DI and Wiring Impact

### Phase 1: Zero DI changes

All Phase 1 changes are *inside* `InputWindowsNative.cs`. The class signature, constructor, and `IInput` interface remain identical. `WowProcessInput` still constructs it with `new`. `DependencyInjection.cs` is untouched.

### Phase 2: Optional extraction

If the interceptor logic grows large enough to warrant its own class, refactor `WowProcessInput` to accept `IInput` via constructor injection instead of constructing `InputWindowsNative` directly:

```csharp
// WowProcessInput constructor change (Phase 2 only):
public WowProcessInput(ILogger<WowProcessInput> logger, CancellationTokenSource cts,
    WowProcess process, IInput nativeInput)  // ← injected instead of new'd
{
    this.nativeInput = nativeInput;
    ...
}

// DI registration (Phase 2 only):
s.AddSingleton<IInput>(sp => new InputSecurityInterceptor(
    new InputWindowsNative(
        sp.GetRequiredService<WowProcess>(),
        sp.GetRequiredService<CancellationTokenSource>(),
        InputDuration.FastPress,
        sp.GetRequiredService<IHumanizationProvider>()),
    sp.GetRequiredService<ILogger<InputSecurityInterceptor>>(),
    sp.GetRequiredService<WowProcess>()));
```

---

## 8. Feature Flag Integration

Add to `FeatureFlagsOptions.cs`:

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
    public bool KeyRepeat { get; set; } = false;  // Phase 2, disabled by default

    /// <summary>Dampen action bursts that are too regular (F7 fix).</summary>
    public bool BurstDampening { get; set; } = false;  // Phase 2, disabled by default
}
```

Wire into `HumanizationOptions` or as a sibling:
```csharp
public sealed class FeatureFlagsOptions
{
    // ... existing ...
    public InputSecurityOptions InputSecurity { get; set; } = new();
}
```

`runtime_feature_flags.json`:
```json
{
    "InputSecurity": {
        "Enabled": true,
        "EmitWmChar": true,
        "FocusGuard": true,
        "HybridModifiers": true,
        "KeyRepeat": false,
        "BurstDampening": false
    }
}
```

---

## 9. Performance Impact Analysis

| Guard | Cost per Call | Frequency | Notes |
|-------|-------------|-----------|-------|
| `EnsureForegroundFocus` | `GetForegroundWindow` = ~1μs; `SetForegroundWindow` = ~100μs (rare) | Every key press | Fast path is one P/Invoke call |
| `EmitWmCharIfPrintable` | One `PostMessage` = ~2μs | Every printable key press | `VirtualKeyToChar` is a switch expression — zero allocation |
| Modifier `SendInput` | ~10μs per call | Only on modified key presses | Replaces existing PostMessage call (net ~8μs increase) |
| Modifier stagger delay | 4-12ms | Only on modified key presses | Overlaps with existing timing budget |
| Key repeat timer | Timer callback ~2μs every 33ms | Only during movement key holds | One `PostMessage` per tick |
| Burst dampener | `Stopwatch.GetTimestamp` + array index = ~0.5μs | Every action key press | Sleep only when rate exceeded |

**Overall: negligible.** The stagger delays (4-12ms for press, 2-8ms for release) are the largest cost, but they only apply to modifier-combo presses and overlap with human reaction timing that would exist anyway. No new allocations in steady state.

---

## 10. What NOT To Do

| Anti-Pattern | Why It Fails |
|-------------|-------------|
| Install `WH_KEYBOARD_LL` hook to strip `LLKHF_INJECTED` | Installing a global hook is *itself* a detection vector. Warden scans for active hooks. Also, the injected flag is set by the kernel — you can't strip it even with a hook. |
| Inject DLL into WoW to hook `GetAsyncKeyState` | Violates the project's core principle ("no memory tampering and DLL injection"). Trivially detected by Warden's module enumeration. |
| Use kernel driver for input injection | Massively increases attack surface. PatchGuard, driver signing, kernel pointer validation. See PRD_INTERCEPTOR_DRIVER_ARCHITECTURE for full analysis. |
| Replace all `PostMessage` with `SendInput` | Every key would get `LLKHF_INJECTED`. PostMessage for action keys is *better* because it avoids the injected flag entirely. |
| Spoof `dwExtraInfo` with a fake hardware value | HID driver values vary by vendor/model. A wrong value is a stronger signal than zero. |
| Set `input.ki.time` to `GetTickCount()` | Windows auto-fills timestamp for `SendInput` when `time=0`. Manually setting it can actually *de-sync* from the kernel's internal timing. |

---

## 11. Testing Strategy

### Unit Tests (CoreUnitTests)

| Test | Validates |
|------|-----------|
| `VirtualKeyToChar` returns correct char for all printable VK codes | F2 character mapping accuracy |
| `VirtualKeyToChar` returns null for F-keys, arrows, modifiers, Escape, Tab, Enter | F2 doesn't emit WM_CHAR for non-printable keys |
| Burst dampener ring buffer fills correctly and detects high rate | F7 logic correctness |
| Burst dampener does not trigger at normal action rates | F7 false-positive prevention |

### Integration Tests (Manual + CoreTests)

| Test | Validates |
|------|-----------|
| Press Shift+1 with WoW foreground, verify correct ability fires | F1 modifier fix works end-to-end |
| Press unmodified '1', capture Win32 messages, verify WM_CHAR follows WM_KEYDOWN | F2 WM_CHAR emission |
| Alt-tab away from WoW, trigger bot action, verify WoW regains focus | F3 focus guard |
| Hold W key for 2 seconds, capture messages, verify auto-repeat pattern | F4 repeat emitter |

### Message Spy Validation

Use `Spy++` (included with Visual Studio) or a simple `WH_GETMESSAGE` hook on the WoW window to capture the actual message stream. Compare:

1. **Before interceptor:** `WM_KEYDOWN` → `WM_KEYUP` (no `WM_CHAR`)
2. **After interceptor:** `WM_KEYDOWN` → `WM_CHAR` → `WM_KEYUP` (matches real keyboard)

---

## 12. Implementation Phases

### Phase 1: Critical Fixes (Est. 6-8h)

| # | Task | File | Fix |
|---|------|------|-----|
| 1.1 | Hybrid modifier SendInput + stagger delays | `InputWindowsNative.cs` (`PressModifiersDown`/`ReleaseModifiersUp`) | F1 |
| 1.2 | `VirtualKeyToChar` + `EmitWmCharIfPrintable` helper | `InputWindowsNative.cs` | F2 |
| 1.3 | Integrate WM_CHAR emission in `PressRandom`, `PressFixed`, `KeyDown` | `InputWindowsNative.cs` | F2 |
| 1.4 | `EnsureForegroundFocus` guard | `InputWindowsNative.cs` | F3 |
| 1.5 | Wire guards into all dispatch entry points | `InputWindowsNative.cs` | F2+F3 |
| 1.6 | Add `InputSecurityOptions` to feature flags | `FeatureFlagsOptions.cs` | Config |
| 1.7 | Pass feature flag config into `InputWindowsNative` | Constructor change | Config |
| 1.8 | Unit tests for `VirtualKeyToChar` | `CoreUnitTests/` | Testing |

### Phase 2: Enhancement Layer (Est. 4-6h)

| # | Task | File | Fix |
|---|------|------|-----|
| 2.1 | `KeyRepeatTimer` class | `InputWindowsNative.cs` or extracted class | F4 |
| 2.2 | Integrate repeat timer with `KeyDown`/`KeyUp` | `InputWindowsNative.cs` | F4 |
| 2.3 | `BurstDampener` class | `InputWindowsNative.cs` or extracted class | F7 |
| 2.4 | Integrate burst dampener with `PressRandom` | `InputWindowsNative.cs` | F7 |
| 2.5 | Unit tests for dampener and repeat logic | `CoreUnitTests/` | Testing |

### Phase 3: Extraction (If Needed) (Est. 3-4h)

| # | Task | File | Purpose |
|---|------|------|---------|
| 3.1 | Extract `InputSecurityInterceptor : IInput` | `Core/Interceptors/` | Separate concerns |
| 3.2 | Refactor `WowProcessInput` to accept `IInput` | `Game/Input/WowProcessInput.cs` | DI-friendly |
| 3.3 | Wire decorator in DI | `Core/DependencyInjection.cs` | Registration |

**Total estimated: 13-18 hours**

---

## 13. Risk Register

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| WM_CHAR emission causes WoW to double-process a key press | Low | Medium | WoW's DirectInput path reads scan codes from WM_KEYDOWN, not WM_CHAR. The game's `TranslateMessage` in its own message loop already produces WM_CHAR — our extra one may be discarded as duplicate. Test empirically. |
| Hybrid modifiers: SendInput for Shift fails because WoW is at higher integrity level (UIPI) | Very Low | High | WoW Classic runs at standard user integrity. SendInput works across same-integrity processes. Only fails if WoW is launched as Administrator and the bot is not. Document this requirement. |
| Focus guard brings WoW to front unexpectedly while user is typing elsewhere | Medium | Low | Guard is best-effort: it attempts restore but doesn't block input on failure. User can disable via feature flag `FocusGuard = false`. |
| Key repeat timer fires after KeyUp due to race condition | Low | Low | `Stop()` disposes the timer before posting WM_KEYUP. Timer callback checks for null before posting. |
| Modifier stagger delay (4-12ms) causes ability to not fire because modifier hadn't "settled" | Very Low | Medium | `SendInput` is synchronous — the modifier state is updated before the function returns. The stagger delay is *after* the state is already set. |

---

## 14. Design Decisions Log

| Decision | Chosen | Alternatives Considered | Rationale |
|----------|--------|------------------------|-----------|
| Where interceptor lives | Inside `InputWindowsNative` (Phase 1) | DI decorator on IInput | Zero DI changes; guards are tightly coupled to Win32 dispatch |
| Modifier key dispatch | SendInput (scan code) | PostMessage, keybd_event | Only method that updates GetAsyncKeyState; scan code avoids virtual key issues in DirectInput |
| WM_CHAR character mapping | Static switch expression | `MapVirtualKeyA(MAPVK_VK_TO_CHAR)` | `MapVirtualKey` returns the uppercase character and doesn't handle all OEM keys reliably. Static mapping is deterministic and zero-allocation. |
| Focus guard policy | Best-effort (don't block) | Strict (block input on focus loss) | Blocking mid-combat is worse than detection risk |
| dwExtraInfo value | Keep at 0 | Spoof with GetMessageExtraInfo result | Zero is normal for legitimate software; wrong spoofed value is worse |
| Auto-repeat implementation | Timer-based per held key | Background thread polling | Timer is lighter weight, no thread management, natural for interval-based work |
| F5 (LLKHF_INJECTED) | Accept the risk | Kernel driver, hook-based filtering | Inherent to Windows; mitigation (kernel driver) creates larger detection surface than the problem |
| F6 (dwExtraInfo) | Don't fix | Spoof hardware values | Wrong value is more suspicious than zero |

---

## 15. Files Changed Summary

| File | Change Type | Phase |
|------|------------|-------|
| `Game/Input/InputWindowsNative.cs` | **Modified** — F1+F2+F3+F4+F7 implementations | 1+2 |
| `Core/FeatureFlags/FeatureFlagsOptions.cs` | **Modified** — add `InputSecurityOptions` class | 1 |
| `BlazorServer/runtime_feature_flags.json` | **Modified** — add `InputSecurity` section | 1 |
| `CoreUnitTests/Input/VirtualKeyToCharTests.cs` | **New** — unit tests for character mapping | 1 |
| `CoreUnitTests/Input/BurstDampenerTests.cs` | **New** — unit tests for burst dampening | 2 |
| `WinAPI/NativeMethods.cs` | **No change** | — |
| `Game/Input/WowProcessInput.cs` | **No change** (Phase 1-2); **Modified** in Phase 3 if extraction happens | 3 |
| `Core/DependencyInjection.cs` | **No change** (Phase 1-2); **Modified** in Phase 3 if extraction happens | 3 |
