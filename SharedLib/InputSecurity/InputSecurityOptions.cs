namespace SharedLib.InputSecurity;

/// <summary>
/// Input security hardening to reduce detection vectors.
/// Addresses structural fingerprints in Win32 message stream.
/// </summary>
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

    /// <summary>Stagger delay between modifier press and key press (ms).</summary>
    public int ModifierStaggerMinMs { get; set; } = 4;

    /// <summary>Maximum stagger delay between modifier press and key press (ms).</summary>
    public int ModifierStaggerMaxMs { get; set; } = 12;

    /// <summary>Delay before releasing modifier after key up (ms).</summary>
    public int ModifierReleaseStaggerMinMs { get; set; } = 2;

    /// <summary>Maximum delay before releasing modifier after key up (ms).</summary>
    public int ModifierReleaseStaggerMaxMs { get; set; } = 8;

    /// <summary>Milliseconds to wait for focus restoration.</summary>
    public int FocusGuardSettleMs { get; set; } = 50;

    /// <summary>Initial delay before first key repeat (ms).</summary>
    public int KeyRepeatInitialDelayMs { get; set; } = 250;

    /// <summary>Interval between key repeats (ms).</summary>
    public int KeyRepeatIntervalMs { get; set; } = 33;

    /// <summary>Maximum actions per second before burst dampening triggers.</summary>
    public double BurstDampenerMaxActionsPerSecond { get; set; } = 12.0;

    /// <summary>Number of recent timestamps to track for burst detection.</summary>
    public int BurstDampenerWindowSize { get; set; } = 8;
}
