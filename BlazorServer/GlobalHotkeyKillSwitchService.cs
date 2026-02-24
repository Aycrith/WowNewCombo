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
}
