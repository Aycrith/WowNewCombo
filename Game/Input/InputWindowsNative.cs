#nullable enable

using SixLabors.ImageSharp;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using Game.Input.Security;

using Microsoft.Extensions.Logging;

using SharedLib.Humanization;
using SharedLib.InputSecurity;

using static WinAPI.NativeMethods;

namespace Game;

/// <summary>
/// Windows-native input implementation with optional security hardening.
/// Addresses detection vectors F1-F4 and F7 through configurable mitigations.
/// </summary>
public sealed class InputWindowsNative : IInput, IDisposable
{
    private readonly int maxDelay;
    private readonly WowProcess process;
    private readonly CancellationToken token;
    private readonly IHumanizationProvider? humanization;
    private readonly InputSecurityOptions securityOptions;
    private readonly ILogger<InputWindowsNative>? logger;

    // Security components (lazy-initialized)
    private readonly KeyRepeatTimer? keyRepeatTimer;
    private readonly BurstDampener? burstDampener;

    // Modifier key constants
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt key

    // Movement keys that should have auto-repeat
    private const int VK_W = 0x57;
    private const int VK_A = 0x41;
    private const int VK_S = 0x53;
    private const int VK_D = 0x44;

    /// <summary>
    /// Creates a new InputWindowsNative instance.
    /// </summary>
    public InputWindowsNative(
        WowProcess process,
        CancellationTokenSource cts,
        int maxDelay,
        IHumanizationProvider? humanization = null,
        InputSecurityOptions? securityOptions = null,
        ILogger<InputWindowsNative>? logger = null)
    {
        this.process = process;
        token = cts.Token;
        this.maxDelay = maxDelay;
        this.humanization = humanization;
        this.securityOptions = securityOptions ?? new InputSecurityOptions();
        this.logger = logger;

        // Initialize security components if enabled
        if (this.securityOptions.Enabled)
        {
            if (this.securityOptions.KeyRepeat)
            {
                keyRepeatTimer = new KeyRepeatTimer(
                    process.MainWindowHandle,
                    this.securityOptions.KeyRepeatInitialDelayMs,
                    this.securityOptions.KeyRepeatIntervalMs,
                    null); // Logger is optional, pass null for now
            }

            if (this.securityOptions.BurstDampening)
            {
                burstDampener = new BurstDampener(
                    this.securityOptions.BurstDampenerWindowSize,
                    this.securityOptions.BurstDampenerMaxActionsPerSecond,
                    null); // Logger is optional, pass null for now
            }
        }
    }

    private int DelayTime(int milliseconds)
    {
        if (humanization?.Enabled == true)
        {
            int holdMs = humanization.GetKeyHoldDurationMs(milliseconds);
            int extra = maxDelay > 0 ? Random.Shared.Next(maxDelay) : 0;
            return holdMs + extra;
        }

        return milliseconds + Random.Shared.Next(maxDelay);
    }

    /// <summary>
    /// Translates a virtual key code to the correct key for the current keyboard layout.
    /// </summary>
    private (int key, bool shift, bool ctrl, bool alt) TranslateKeyForLayout(int key)
    {
        if (!IsLayoutDependentKey(key))
            return (key, false, false, false);

        char? character = GetCharacterForUSKey(key);
        if (!character.HasValue)
            return (key, false, false, false);

        if (GetVirtualKeyForCharacter(character.Value, process.MainWindowHandle,
            out int translatedKey, out bool needsShift, out bool needsCtrl, out bool needsAlt))
        {
            return (translatedKey, needsShift, needsCtrl, needsAlt);
        }

        return (key, false, false, false);
    }

    #region Modifier Handling

    /// <summary>
    /// Presses modifier keys using PostMessage (legacy behavior).
    /// </summary>
    private void PressModifiersDownPostMessage(bool shift, bool ctrl, bool alt)
    {
        if (shift)
            PostMessage(process.MainWindowHandle, WM_KEYDOWN, VK_SHIFT, MakeKeyDownLParam(VK_SHIFT));
        if (ctrl)
            PostMessage(process.MainWindowHandle, WM_KEYDOWN, VK_CONTROL, MakeKeyDownLParam(VK_CONTROL));
        if (alt)
            PostMessage(process.MainWindowHandle, WM_KEYDOWN, VK_MENU, MakeKeyDownLParam(VK_MENU));
    }

    /// <summary>
    /// Releases modifier keys using PostMessage (legacy behavior).
    /// </summary>
    private void ReleaseModifiersUpPostMessage(bool shift, bool ctrl, bool alt)
    {
        if (alt)
            PostMessage(process.MainWindowHandle, WM_KEYUP, VK_MENU, MakeKeyUpLParam(VK_MENU));
        if (ctrl)
            PostMessage(process.MainWindowHandle, WM_KEYUP, VK_CONTROL, MakeKeyUpLParam(VK_CONTROL));
        if (shift)
            PostMessage(process.MainWindowHandle, WM_KEYUP, VK_SHIFT, MakeKeyUpLParam(VK_SHIFT));
    }

    /// <summary>
    /// Presses modifier keys using SendInput (F1 fix).
    /// This updates GetAsyncKeyState() so WoW sees the modifier as truly held.
    /// </summary>
    private void PressModifiersDownHybrid(bool shift, bool ctrl, bool alt)
    {
        // Use SendInput for modifiers so GetAsyncKeyState() returns TRUE
        if (shift) SendModifierKey(VK_SHIFT, keyUp: false);
        if (ctrl) SendModifierKey(VK_CONTROL, keyUp: false);
        if (alt) SendModifierKey(VK_MENU, keyUp: false);

        // Human finger stagger: real humans cannot press modifier + key simultaneously
        if (shift || ctrl || alt)
        {
            int stagger = securityOptions.ModifierStaggerMinMs +
                Random.Shared.Next(securityOptions.ModifierStaggerMaxMs - securityOptions.ModifierStaggerMinMs + 1);
            token.WaitHandle.WaitOne(stagger);
        }
    }

    /// <summary>
    /// Releases modifier keys using SendInput (F1 fix).
    /// </summary>
    private void ReleaseModifiersUpHybrid(bool shift, bool ctrl, bool alt)
    {
        // Stagger release: humans lift modifier finger slightly after main key
        if (shift || ctrl || alt)
        {
            int stagger = securityOptions.ModifierReleaseStaggerMinMs +
                Random.Shared.Next(securityOptions.ModifierReleaseStaggerMaxMs - securityOptions.ModifierReleaseStaggerMinMs + 1);
            token.WaitHandle.WaitOne(stagger);
        }

        // Release in reverse order (LIFO - natural finger sequence)
        if (alt) SendModifierKey(VK_MENU, keyUp: true);
        if (ctrl) SendModifierKey(VK_CONTROL, keyUp: true);
        if (shift) SendModifierKey(VK_SHIFT, keyUp: true);
    }

    /// <summary>
    /// Sends a modifier key using SendInput to update system-level key state.
    /// </summary>
    private static void SendModifierKey(int virtualKey, bool keyUp)
    {
        INPUT input = CreateModifierInput(virtualKey, keyUp);
        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Creates an INPUT struct for a modifier key using scan codes.
    /// </summary>
    private static INPUT CreateModifierInput(int virtualKey, bool keyUp)
    {
        uint flags = KEYEVENTF_SCANCODE;
        if (keyUp) flags |= KEYEVENTF_KEYUP;
        if (IsExtendedKey(virtualKey)) flags |= KEYEVENTF_EXTENDEDKEY;

        INPUT input = new();
        input.type = INPUT_KEYBOARD;
        input.ki.wVk = 0;
        input.ki.wScan = (ushort)MapVirtualKeyA((uint)virtualKey, MAPVK_VK_TO_VSC);
        input.ki.dwFlags = flags;
        input.ki.time = 0;
        input.ki.dwExtraInfo = 0;
        return input;
    }

    #endregion

    #region Security Features

    /// <summary>
    /// F3 Fix: Ensures WoW window is the foreground window before sending input.
    /// PostMessage delivers messages to unfocused windows — real keyboards don't.
    /// </summary>
    private bool EnsureForegroundFocus()
    {
        if (!securityOptions.FocusGuard)
            return true;

        nint foreground = GetForegroundWindow();
        if (foreground == process.MainWindowHandle)
            return true;

        // Attempt to bring WoW to foreground
        SetForegroundWindow(process.MainWindowHandle);
        token.WaitHandle.WaitOne(securityOptions.FocusGuardSettleMs);

        // Verify it actually took
        bool success = GetForegroundWindow() == process.MainWindowHandle;

        if (!success)
        {
            logger?.LogWarning("[InputSecurity] Failed to restore WoW focus (foreground={Foreground:X}, wow={Wow:X})",
                foreground, process.MainWindowHandle);
        }

        return success;
    }

    /// <summary>
    /// F2 Fix: Emits WM_CHAR matching what TranslateMessage() would produce after WM_KEYDOWN.
    /// Only for character-producing keys — not F-keys, arrows, modifiers, etc.
    /// </summary>
    private void EmitWmCharIfPrintable(int virtualKey, int keyDownLParam)
    {
        if (!securityOptions.EmitWmChar)
            return;

        char? ch = VirtualKeyToChar.Map(virtualKey);
        if (ch.HasValue)
        {
            // WM_CHAR lParam matches the preceding WM_KEYDOWN lParam
            PostMessage(process.MainWindowHandle, WM_CHAR, ch.Value, keyDownLParam);

            logger?.LogTrace("[InputSecurity] Emitted WM_CHAR for '{Char}' (VK_{Key:X})",
                ch.Value, virtualKey);
        }
    }

    /// <summary>
    /// F4 Fix: Checks if a key should have auto-repeat enabled.
    /// </summary>
    private static bool IsRepeatableKey(int virtualKey)
    {
        return virtualKey switch
        {
            VK_W or VK_A or VK_S or VK_D => true, // Movement keys
            _ => false
        };
    }

    /// <summary>
    /// F7 Fix: Applies burst dampening if enabled.
    /// </summary>
    private void ApplyBurstDampening()
    {
        if (securityOptions.BurstDampening && burstDampener != null)
        {
            burstDampener.CheckAndDampen(token.WaitHandle);
        }
    }

    #endregion

    #region IInput Implementation

    public void KeyDown(int key)
    {
        var (actualKey, shift, ctrl, alt) = TranslateKeyForLayout(key);

        // F3: Focus guard
        if (securityOptions.Enabled)
            EnsureForegroundFocus();

        // F1: Use hybrid modifiers if enabled
        if (securityOptions.Enabled && securityOptions.HybridModifiers)
            PressModifiersDownHybrid(shift, ctrl, alt);
        else
            PressModifiersDownPostMessage(shift, ctrl, alt);

        bool extended = IsExtendedKey(actualKey);
        int lParam = MakeKeyDownLParam(actualKey, extended);

        PostMessage(process.MainWindowHandle, WM_KEYDOWN, actualKey, lParam);

        // F2: Emit WM_CHAR for printable keys
        if (securityOptions.Enabled)
            EmitWmCharIfPrintable(actualKey, lParam);

        // F4: Start key repeat timer for movement keys
        if (securityOptions.Enabled && securityOptions.KeyRepeat && IsRepeatableKey(actualKey))
            keyRepeatTimer?.Start(actualKey, extended);
    }

    public void KeyUp(int key)
    {
        var (actualKey, shift, ctrl, alt) = TranslateKeyForLayout(key);

        // F4: Stop key repeat timer
        if (securityOptions.Enabled && securityOptions.KeyRepeat)
            keyRepeatTimer?.Stop();

        bool extended = IsExtendedKey(actualKey);
        int lParam = MakeKeyUpLParam(actualKey, extended);
        PostMessage(process.MainWindowHandle, WM_KEYUP, actualKey, lParam);

        // F1: Use hybrid modifiers if enabled
        if (securityOptions.Enabled && securityOptions.HybridModifiers)
            ReleaseModifiersUpHybrid(shift, ctrl, alt);
        else
            ReleaseModifiersUpPostMessage(shift, ctrl, alt);
    }

    public int PressRandom(int key, int milliseconds)
    {
        return PressRandom(key, milliseconds, token);
    }

    public int PressRandom(int key, int milliseconds, CancellationToken cancellationToken)
    {
        var (actualKey, shift, ctrl, alt) = TranslateKeyForLayout(key);
        bool extended = IsExtendedKey(actualKey);
        int downLParam = MakeKeyDownLParam(actualKey, extended);
        int upLParam = MakeKeyUpLParam(actualKey, extended);

        // F3: Focus guard
        if (securityOptions.Enabled)
            EnsureForegroundFocus();

        // F7: Burst dampening
        if (securityOptions.Enabled)
            ApplyBurstDampening();

        // F1: Press modifiers (hybrid or legacy)
        if (securityOptions.Enabled && securityOptions.HybridModifiers)
            PressModifiersDownHybrid(shift, ctrl, alt);
        else
            PressModifiersDownPostMessage(shift, ctrl, alt);

        // Send key down
        PostMessage(process.MainWindowHandle, WM_KEYDOWN, actualKey, downLParam);

        // F2: Emit WM_CHAR for printable keys
        if (securityOptions.Enabled)
            EmitWmCharIfPrintable(actualKey, downLParam);

        // Wait for key duration
        int delay = DelayTime(milliseconds);
        cancellationToken.WaitHandle.WaitOne(delay);

        // Send key up
        PostMessage(process.MainWindowHandle, WM_KEYUP, actualKey, upLParam);

        // F1: Release modifiers (hybrid or legacy)
        if (securityOptions.Enabled && securityOptions.HybridModifiers)
            ReleaseModifiersUpHybrid(shift, ctrl, alt);
        else
            ReleaseModifiersUpPostMessage(shift, ctrl, alt);

        return delay;
    }

    public void PressFixed(int key, int milliseconds, CancellationToken cancellationToken)
    {
        var (actualKey, shift, ctrl, alt) = TranslateKeyForLayout(key);
        bool extended = IsExtendedKey(actualKey);
        int downLParam = MakeKeyDownLParam(actualKey, extended);
        int upLParam = MakeKeyUpLParam(actualKey, extended);

        // F3: Focus guard
        if (securityOptions.Enabled)
            EnsureForegroundFocus();

        // F7: Burst dampening
        if (securityOptions.Enabled)
            ApplyBurstDampening();

        // F1: Press modifiers
        if (securityOptions.Enabled && securityOptions.HybridModifiers)
            PressModifiersDownHybrid(shift, ctrl, alt);
        else
            PressModifiersDownPostMessage(shift, ctrl, alt);

        PostMessage(process.MainWindowHandle, WM_KEYDOWN, actualKey, downLParam);

        // F2: Emit WM_CHAR
        if (securityOptions.Enabled)
            EmitWmCharIfPrintable(actualKey, downLParam);

        cancellationToken.WaitHandle.WaitOne(milliseconds);

        PostMessage(process.MainWindowHandle, WM_KEYUP, actualKey, upLParam);

        // F1: Release modifiers
        if (securityOptions.Enabled && securityOptions.HybridModifiers)
            ReleaseModifiersUpHybrid(shift, ctrl, alt);
        else
            ReleaseModifiersUpPostMessage(shift, ctrl, alt);
    }

    public void LeftClick(Point p)
    {
        MoveCursorTo(p);

        Point client = p;
        ScreenToClient(process.MainWindowHandle, ref client);
        int lparam = MakeLParam(client.X, client.Y);

        // F3: Focus guard for clicks
        if (securityOptions.Enabled)
            EnsureForegroundFocus();

        PostMessage(process.MainWindowHandle, WM_LBUTTONDOWN, 0, lparam);
        token.WaitHandle.WaitOne(DelayTime(maxDelay));
        PostMessage(process.MainWindowHandle, WM_LBUTTONUP, 0, lparam);
    }

    public void RightClick(Point p)
    {
        MoveCursorTo(p);

        Point client = p;
        ScreenToClient(process.MainWindowHandle, ref client);
        int lparam = MakeLParam(client.X, client.Y);

        // F3: Focus guard for clicks
        if (securityOptions.Enabled)
            EnsureForegroundFocus();

        PostMessage(process.MainWindowHandle, WM_RBUTTONDOWN, 0, lparam);
        token.WaitHandle.WaitOne(DelayTime(maxDelay));
        PostMessage(process.MainWindowHandle, WM_RBUTTONUP, 0, lparam);
    }

    public void SetCursorPos(Point p)
    {
        WinAPI.NativeMethods.SetCursorPos(p.X, p.Y);
    }

    private void MoveCursorTo(Point target)
    {
        if (humanization?.Enabled != true)
        {
            SetCursorPos(target);
            return;
        }

        if (!GetCursorPos(out Point current))
        {
            SetCursorPos(target);
            return;
        }

        Span<Point> points = stackalloc Point[128];
        int count = humanization.BuildMousePath(current, target, points);
        if (count < 2)
        {
            SetCursorPos(target);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            SetCursorPos(points[i]);

            if (i == count - 1)
            {
                break;
            }

            int delay = humanization.GetInterKeyDelayMs(0);
            if (delay > 0)
            {
                token.WaitHandle.WaitOne(delay);
            }
        }
    }

    public void SendText(string text)
    {
        foreach (char c in text)
        {
            PostMessage(process.MainWindowHandle, WM_CHAR, c, 0);
        }
    }

    #endregion

    /// <summary>
    /// Disposes resources used by the input security features.
    /// </summary>
    public void Dispose()
    {
        keyRepeatTimer?.Dispose();
    }
}
