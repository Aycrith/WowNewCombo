using Core;

using Game;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Threading;
using System.Threading.Tasks;

using static WinAPI.NativeMethods;

namespace BlazorServer;

/// <summary>
/// Global emergency stop hotkeys that work while WoW is focused.
/// Polls GetAsyncKeyState to avoid needing a UI message loop.
/// </summary>
public sealed class GlobalHotkeyKillSwitchService : BackgroundService
{
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt
    private const int VK_F12 = 0x7B;

    private readonly ILogger<GlobalHotkeyKillSwitchService> logger;
    private readonly IServiceProvider serviceProvider;
    private readonly IHostApplicationLifetime appLifetime;

    private bool softChordLatched;
    private bool hardChordLatched;

    public GlobalHotkeyKillSwitchService(
        ILogger<GlobalHotkeyKillSwitchService> logger,
        IServiceProvider serviceProvider,
        IHostApplicationLifetime appLifetime)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
        this.appLifetime = appLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogWarning(
            "[KillSwitch       ] Hotkeys active: Soft=Ctrl+Shift+F12, Hard=Ctrl+Alt+Shift+F12");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Focus guard: only process kill-switch hotkeys when WoW or
                // the bot's own window is in the foreground. Prevents false
                // triggers caused by external scripts/tools sending matching
                // key combinations while other apps have focus.
                if (!IsExpectedWindowFocused())
                {
                    softChordLatched = false;
                    hardChordLatched = false;
                    await Task.Delay(50, stoppingToken);
                    continue;
                }

                bool ctrl = IsKeyDown(VK_CONTROL);
                bool shift = IsKeyDown(VK_SHIFT);
                bool alt = IsKeyDown(VK_MENU);
                bool f12 = IsKeyDown(VK_F12);

                bool hardChord = ctrl && shift && alt && f12;
                bool softChord = ctrl && shift && !alt && f12;

                if (hardChord)
                {
                    if (!hardChordLatched)
                    {
                        hardChordLatched = true;
                        softChordLatched = true;
                        TriggerHardStop();
                    }
                }
                else
                {
                    hardChordLatched = false;
                }

                if (softChord)
                {
                    if (!softChordLatched)
                    {
                        softChordLatched = true;
                        TriggerSoftStop();
                    }
                }
                else if (!hardChord)
                {
                    softChordLatched = false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[KillSwitch       ] Polling loop error");
            }

            await Task.Delay(50, stoppingToken);
        }
    }

    private void TriggerSoftStop()
    {
        logger.LogWarning("[KillSwitch       ] Soft stop hotkey detected");
        EmergencyReleaseInputs();

        IBotController? botController = serviceProvider.GetService<IBotController>();
        if (botController == null)
        {
            logger.LogWarning("[KillSwitch       ] IBotController unavailable during soft stop");
            return;
        }

        if (botController.IsBotActive)
        {
            botController.ToggleBotStatus();
            logger.LogWarning("[KillSwitch       ] Bot stopped");
        }
    }

    private void TriggerHardStop()
    {
        logger.LogError("[KillSwitch       ] HARD STOP hotkey detected");
        EmergencyReleaseInputs();

        IBotController? botController = serviceProvider.GetService<IBotController>();
        if (botController != null && botController.IsBotActive)
        {
            botController.ToggleBotStatus();
            logger.LogError("[KillSwitch       ] Bot stopped (hard stop)");
        }

        appLifetime.StopApplication();
    }

    private void EmergencyReleaseInputs()
    {
        try
        {
            WowProcessInput? wowInput = serviceProvider.GetService<WowProcessInput>();
            if (wowInput == null)
            {
                return;
            }

            wowInput.EmergencyReleaseAllKeys();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[KillSwitch       ] Failed to release inputs");
        }
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & unchecked((short)0x8000)) != 0;
    }

    /// <summary>
    /// Returns true when the foreground window belongs to the WoW process
    /// or is the current (BlazorServer) process. This prevents false kill-switch
    /// triggers from external key combinations when other apps have focus.
    /// </summary>
    private bool IsExpectedWindowFocused()
    {
        try
        {
            nint foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
                return false;

            // Check against our own process window
            nint selfHandle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (selfHandle != IntPtr.Zero && foreground == selfHandle)
                return true;

            // Check against the WoW process window
            WowProcess? wowProcess = serviceProvider.GetService<WowProcess>();
            if (wowProcess != null && wowProcess.MainWindowHandle != IntPtr.Zero)
                return foreground == wowProcess.MainWindowHandle;

            // If we can't determine either handle, allow the hotkey through
            // to avoid blocking legitimate stop requests during startup.
            return true;
        }
        catch
        {
            return true;
        }
    }
}
