#nullable enable

using System;
using System.Threading;

using Microsoft.Extensions.Logging;

using static WinAPI.NativeMethods;

namespace Game.Input.Security;

/// <summary>
/// Emits Windows auto-repeat WM_KEYDOWN messages for held keys.
/// Real keyboards generate repeats after ~250ms, then every ~33ms.
/// This addresses the F4 detection fingerprint (missing auto-repeat).
/// </summary>
public sealed class KeyRepeatTimer : IDisposable
{
    private readonly nint windowHandle;
    private readonly ILogger<KeyRepeatTimer>? logger;
    private readonly int initialDelayMs;
    private readonly int intervalMs;

    private Timer? timer;
    private int activeKey;
    private int repeatLParam;
    private bool isRunning;
    private readonly object lockObj = new();

    /// <summary>
    /// Creates a new KeyRepeatTimer.
    /// </summary>
    /// <param name="windowHandle">The window handle to post messages to</param>
    /// <param name="initialDelayMs">Initial delay before first repeat (default: 250ms)</param>
    /// <param name="intervalMs">Interval between repeats (default: 33ms)</param>
    /// <param name="logger">Optional logger</param>
    public KeyRepeatTimer(nint windowHandle, int initialDelayMs = 250, int intervalMs = 33, ILogger<KeyRepeatTimer>? logger = null)
    {
        this.windowHandle = windowHandle;
        this.initialDelayMs = initialDelayMs;
        this.intervalMs = intervalMs;
        this.logger = logger;
    }

    /// <summary>
    /// Whether the timer is currently emitting repeats.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (lockObj)
            {
                return isRunning;
            }
        }
    }

    /// <summary>
    /// Starts emitting repeat messages for the specified key.
    /// </summary>
    /// <param name="virtualKey">The virtual key code</param>
    /// <param name="extended">Whether this is an extended key</param>
    public void Start(int virtualKey, bool extended)
    {
        lock (lockObj)
        {
            StopInternal();

            activeKey = virtualKey;

            uint scanCode = MapVirtualKeyA((uint)virtualKey, MAPVK_VK_TO_VSC);

            // Build repeat lParam: bit 30 = 1 (key was already down)
            repeatLParam = 1;                          // repeat count = 1
            repeatLParam |= (int)(scanCode << 16);     // scan code
            if (extended) repeatLParam |= (1 << 24);   // extended flag
            repeatLParam |= (1 << 30);                 // previous key state = 1 (repeat)

            // Add jitter to initial delay (±20ms)
            int jitteredDelay = initialDelayMs + Random.Shared.Next(-20, 21);
            jitteredDelay = Math.Max(200, jitteredDelay); // Min 200ms

            timer = new Timer(EmitRepeat, null, jitteredDelay, Timeout.Infinite);
            isRunning = true;

            logger?.LogDebug("[KeyRepeatTimer] Started for VK_{VirtualKey:X} (scan={ScanCode}, delay={Delay}ms)",
                virtualKey, scanCode, jitteredDelay);
        }
    }

    /// <summary>
    /// Stops emitting repeat messages.
    /// </summary>
    public void Stop()
    {
        lock (lockObj)
        {
            StopInternal();
        }
    }

    private void StopInternal()
    {
        timer?.Dispose();
        timer = null;
        isRunning = false;
        activeKey = 0;
    }

    private void EmitRepeat(object? state)
    {
        lock (lockObj)
        {
            if (!isRunning || timer == null)
                return;

            try
            {
                // Post the repeat WM_KEYDOWN
                PostMessage(windowHandle, WM_KEYDOWN, activeKey, repeatLParam);

                logger?.LogTrace("[KeyRepeatTimer] Emitted repeat for VK_{VirtualKey:X}", activeKey);

                // Schedule next repeat with jitter (±4ms)
                int jitteredInterval = intervalMs + Random.Shared.Next(-4, 5);
                jitteredInterval = Math.Max(20, jitteredInterval); // Min 20ms

                timer.Change(jitteredInterval, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[KeyRepeatTimer] Error emitting repeat");
                StopInternal();
            }
        }
    }

    /// <summary>
    /// Disposes the timer and releases resources.
    /// </summary>
    public void Dispose()
    {
        lock (lockObj)
        {
            StopInternal();
        }
    }
}
