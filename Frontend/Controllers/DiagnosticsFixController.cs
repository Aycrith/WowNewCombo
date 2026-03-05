using Core;
using Core.Goals;
using Game;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using SixLabors.ImageSharp;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Frontend.Controllers;

/// <summary>
/// API controller for diagnostic fix and action operations.
/// Handles POST endpoints that mutate game state: keybindings, action bar sync,
/// slash commands, mailbox interaction, and input mode changes.
/// </summary>
[Route("api/diagnostics")]
[ApiController]
public sealed class DiagnosticsFixController : ControllerBase
{
    private readonly ILogger<DiagnosticsFixController> logger;
    private readonly IBotController botController;
    private readonly ExecGameCommand exec;
    private readonly AddonConfigurator addonConfigurator;
    private readonly IAddonReader addonReader;
    private readonly WowProcessInput wowInput;
    private readonly CursorScan cursorScan;
    private readonly AddonBits addonBits;
    private readonly BagReader bagReader;
    private readonly EquipmentReader equipmentReader;
    private readonly ILoggerFactory loggerFactory;

    public DiagnosticsFixController(
        ILogger<DiagnosticsFixController> logger,
        IBotController botController,
        ExecGameCommand exec,
        AddonConfigurator addonConfigurator,
        IAddonReader addonReader,
        WowProcessInput wowInput,
        CursorScan cursorScan,
        AddonBits addonBits,
        BagReader bagReader,
        EquipmentReader equipmentReader,
        ILoggerFactory loggerFactory)
    {
        this.logger = logger;
        this.botController = botController;
        this.exec = exec;
        this.addonConfigurator = addonConfigurator;
        this.addonReader = addonReader;
        this.wowInput = wowInput;
        this.cursorScan = cursorScan;
        this.addonBits = addonBits;
        this.bagReader = bagReader;
        this.equipmentReader = equipmentReader;
        this.loggerFactory = loggerFactory;
    }

    /// <summary>
    /// POST /api/diagnostics/mailbox/interact?attempts=2
    /// Attempts mailbox interaction via cursor scan + interact + click fallbacks.
    /// </summary>
    [HttpPost("mailbox/interact")]
    public async Task<IActionResult> TryInteractMailbox([FromQuery] int attempts = 2)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            attempts = Math.Clamp(attempts, 1, 5);

            if (addonBits.MailFrameShown())
            {
                sw.Stop();
                return Ok(new MailboxInteractDiagnostics(
                    MailFrameShown: true,
                    CursorFound: false,
                    CursorType: nameof(CursorType.None),
                    CursorX: 0,
                    CursorY: 0,
                    InteractionStep: "already_open",
                    Attempts: 0,
                    ElapsedMs: sw.ElapsedMilliseconds));
            }

            Point foundPoint = default;
            CursorType foundCursor = CursorType.None;
            int usedAttempt = 0;

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                if (cursorScan.FindAny([CursorType.Mail, CursorType.Speak], out foundCursor, out foundPoint))
                {
                    usedAttempt = attempt;
                    break;
                }

                await Task.Delay(100);
            }

            if (usedAttempt == 0)
            {
                sw.Stop();
                return Ok(new MailboxInteractDiagnostics(
                    MailFrameShown: addonBits.MailFrameShown(),
                    CursorFound: false,
                    CursorType: nameof(CursorType.None),
                    CursorX: 0,
                    CursorY: 0,
                    InteractionStep: "cursor_not_found",
                    Attempts: attempts,
                    ElapsedMs: sw.ElapsedMilliseconds));
            }

            wowInput.InteractMouseOver(CancellationToken.None);
            await Task.Delay(150);
            if (addonBits.MailFrameShown())
            {
                sw.Stop();
                return Ok(new MailboxInteractDiagnostics(
                    MailFrameShown: true,
                    CursorFound: true,
                    CursorType: foundCursor.ToStringF(),
                    CursorX: foundPoint.X,
                    CursorY: foundPoint.Y,
                    InteractionStep: "interact_mouseover",
                    Attempts: usedAttempt,
                    ElapsedMs: sw.ElapsedMilliseconds));
            }

            wowInput.RightClick(foundPoint);
            await Task.Delay(150);
            if (addonBits.MailFrameShown())
            {
                sw.Stop();
                return Ok(new MailboxInteractDiagnostics(
                    MailFrameShown: true,
                    CursorFound: true,
                    CursorType: foundCursor.ToStringF(),
                    CursorX: foundPoint.X,
                    CursorY: foundPoint.Y,
                    InteractionStep: "right_click",
                    Attempts: usedAttempt,
                    ElapsedMs: sw.ElapsedMilliseconds));
            }

            wowInput.LeftClick(foundPoint);
            await Task.Delay(150);

            sw.Stop();
            return Ok(new MailboxInteractDiagnostics(
                MailFrameShown: addonBits.MailFrameShown(),
                CursorFound: true,
                CursorType: foundCursor.ToStringF(),
                CursorX: foundPoint.X,
                CursorY: foundPoint.Y,
                InteractionStep: addonBits.MailFrameShown() ? "left_click" : "all_interactions_failed",
                Attempts: usedAttempt,
                ElapsedMs: sw.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Mailbox interact diagnostics failed");
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/diagnostics/fix/bindings
    /// Runs /{prefix}bindings command to set default action bar bindings (NumPad, F-keys)
    /// </summary>
    [HttpPost("fix/bindings")]
    public async Task<IActionResult> FixBindings()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            string command = $"/{addonConfigurator.Config.CommandBindings}";
            logger.LogInformation("Executing {Command}", command);

            exec.Run(command);
            await Task.Delay(500); // Give WoW time to apply bindings

            sw.Stop();
            return Ok(new FixResult(true, $"Executed {command}", 1));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fix bindings failed");
            sw.Stop();
            return StatusCode(500, new FixResult(false, ex.Message));
        }
    }

    /// <summary>
    /// POST /api/diagnostics/fix/numberkeys
    /// Runs /{prefix}numberkeys command to bind number row (1-9,0,-,=) to main action bar
    /// </summary>
    [HttpPost("fix/numberkeys")]
    public async Task<IActionResult> FixNumberKeys()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            string command = $"/{addonConfigurator.Config.CommandNumberKeys}";
            logger.LogInformation("Executing {Command}", command);

            exec.Run(command);
            await Task.Delay(500); // Give WoW time to apply bindings

            sw.Stop();
            return Ok(new FixResult(true, $"Executed {command}", 1));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fix number keys failed");
            sw.Stop();
            return StatusCode(500, new FixResult(false, ex.Message));
        }
    }

    /// <summary>
    /// POST /api/diagnostics/fix/actions
    /// Runs /{prefix}actions command to create custom action buttons
    /// </summary>
    [HttpPost("fix/actions")]
    public async Task<IActionResult> FixActions()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            string command = $"/{addonConfigurator.Config.CommandActions}";
            logger.LogInformation("Executing {Command}", command);

            exec.Run(command);
            await Task.Delay(500); // Give WoW time to create buttons

            sw.Stop();
            return Ok(new FixResult(true, $"Executed {command}", 1));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fix actions failed");
            sw.Stop();
            return StatusCode(500, new FixResult(false, ex.Message));
        }
    }

    /// <summary>
    /// POST /api/diagnostics/fix/syncbar
    /// Runs ActionBarPopulator.Execute() to place all configured abilities
    /// </summary>
    [HttpPost("fix/syncbar")]
    public IActionResult FixSyncBar()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            ClassConfiguration? classConfig = botController.ClassConfig;
            if (classConfig == null)
            {
                return BadRequest(new FixResult(false, "No profile loaded"));
            }

            ActionBarPopulator populator = new(
                loggerFactory.CreateLogger<ActionBarPopulator>(),
                classConfig,
                addonConfigurator,
                bagReader,
                equipmentReader,
                exec);

            logger.LogInformation("Syncing action bar with profile abilities");
            populator.Execute();

            sw.Stop();
            return Ok(new FixResult(true, "Action bar synced with profile", 1));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync action bar failed");
            sw.Stop();
            return StatusCode(500, new FixResult(false, ex.Message));
        }
    }

    /// <summary>
    /// POST /api/diagnostics/fix/reload
    /// Runs /reload in chat to recover a hung/frozen addon state.
    /// </summary>
    [HttpPost("fix/reload")]
    public async Task<IActionResult> FixReload()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            SlashCommandResult result = await ExecuteSlashCommandAsync(
                command: "/reload",
                useBackgroundCompatibleInput: false,
                preDelayMs: 200,
                postDelayMs: 500);

            sw.Stop();
            return Ok(new FixResult(result.Success, $"Executed {result.Command}", result.Success ? 1 : 0));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fix reload failed");
            sw.Stop();
            return StatusCode(500, new FixResult(false, ex.Message));
        }
    }

    /// <summary>
    /// POST /api/diagnostics/fix/slash
    /// Executes a safe-listed slash command via WowProcessInput.
    /// </summary>
    [HttpPost("fix/slash")]
    public async Task<IActionResult> FixSlash([FromBody] SlashCommandRequest request)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            if (!DiagnosticsController.TryNormalizeSupportedSlashCommand(request.Command, out string normalized, out string? error))
            {
                sw.Stop();
                return BadRequest(new SlashCommandResult(false, request.Command ?? string.Empty, "Rejected", sw.ElapsedMilliseconds, error));
            }

            SlashCommandResult result = await ExecuteSlashCommandAsync(
                command: normalized,
                useBackgroundCompatibleInput: request.UseBackgroundCompatibleInput,
                preDelayMs: request.PreDelayMs,
                postDelayMs: request.PostDelayMs);

            sw.Stop();
            return Ok(result with { ElapsedMs = sw.ElapsedMilliseconds });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fix slash command failed");
            sw.Stop();
            return StatusCode(500, new SlashCommandResult(false, request.Command ?? string.Empty, "Error", sw.ElapsedMilliseconds, ex.Message));
        }
    }

    /// <summary>
    /// POST /api/diagnostics/fix/flush
    /// Runs /{prefix}flush command in chat to force addon state refresh.
    /// </summary>
    [HttpPost("fix/flush")]
    public async Task<IActionResult> FixFlush()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            string command = $"/{addonConfigurator.Config.CommandFlush}";
            logger.LogInformation("Executing {Command}", command);

            exec.Run(command);
            await Task.Delay(500);

            sw.Stop();
            return Ok(new FixResult(true, $"Executed {command}", 1));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fix flush failed");
            sw.Stop();
            return StatusCode(500, new FixResult(false, ex.Message));
        }
    }

    /// <summary>
    /// POST /api/diagnostics/fix/initstate
    /// Resets addon state and flushes data
    /// </summary>
    [HttpPost("fix/initstate")]
    public async Task<IActionResult> FixInitState()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Resetting addon state");
            addonReader.FullReset();

            // Primary path: explicit slash command (does not depend on custom keybindings).
            string command = $"/{addonConfigurator.Config.CommandFlush}";
            exec.Run(command);
            await Task.Delay(350);

            // Fallback path: keep legacy keybind trigger for compatibility.
            wowInput.PressFlushKey();
            await Task.Delay(200);

            sw.Stop();
            return Ok(new FixResult(true, $"Addon state reset and flushed via {command}", 2));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Init state failed");
            sw.Stop();
            return StatusCode(500, new FixResult(false, ex.Message));
        }
    }

    /// <summary>
    /// POST /api/diagnostics/fix/place
    /// Places a specific ability on a slot
    /// Body: { "slot": 1, "name": "Sinister Strike" }
    /// </summary>
    [HttpPost("fix/place")]
    public IActionResult FixPlace([FromBody] PlaceRequest request)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            ClassConfiguration? classConfig = botController.ClassConfig;
            if (classConfig == null)
            {
                return BadRequest(new FixResult(false, "No profile loaded"));
            }

            // Find KeyAction matching the name
            KeyAction? keyAction = null;
            foreach ((string _, KeyActions keyActions) in classConfig.GetByType<KeyActions>())
            {
                keyAction = keyActions.Sequence.FirstOrDefault(ka => ka.Name == request.Name);
                if (keyAction != null)
                    break;
            }

            if (keyAction == null)
            {
                return NotFound(new FixResult(false, $"Ability '{request.Name}' not found in profile"));
            }

            ActionBarPopulator populator = new(
                loggerFactory.CreateLogger<ActionBarPopulator>(),
                classConfig,
                addonConfigurator,
                bagReader,
                equipmentReader,
                exec);

            logger.LogInformation("Placing {Name} on slot {Slot}", request.Name, request.Slot);
            bool success = populator.Place(keyAction);

            sw.Stop();
            return success
                ? Ok(new FixResult(true, $"Placed {request.Name} on slot {request.Slot}", 1))
                : StatusCode(500, new FixResult(false, $"Failed to place {request.Name}"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Place ability failed");
            sw.Stop();
            return StatusCode(500, new FixResult(false, ex.Message));
        }
    }

    /// <summary>
    /// POST /api/diagnostics/fix/all
    /// Applies all fixes in sequence: bindings → numberkeys → actions → syncbar
    /// </summary>
    [HttpPost("fix/all")]
    public async Task<IActionResult> FixAll()
    {
        Stopwatch sw = Stopwatch.StartNew();
        List<string> steps = [];

        try
        {
            logger.LogInformation("Starting auto-fix sequence");

            // Step 0: Force state flush (slash command, independent of keybinds).
            string flushCommand = $"/{addonConfigurator.Config.CommandFlush}";
            exec.Run(flushCommand);
            steps.Add($"Executed {flushCommand}");
            await Task.Delay(500);

            // Step 1: Default bindings (NumPad, F-keys)
            exec.Run($"/{addonConfigurator.Config.CommandBindings}");
            steps.Add("Default bindings applied");
            await Task.Delay(500);

            // Step 2: Number row bindings (1-9,0,-,=)
            exec.Run($"/{addonConfigurator.Config.CommandNumberKeys}");
            steps.Add("Number row bindings applied");
            await Task.Delay(500);

            // Step 3: Custom actions
            exec.Run($"/{addonConfigurator.Config.CommandActions}");
            steps.Add("Custom actions created");
            await Task.Delay(500);

            // Step 4: Sync action bar
            ClassConfiguration? classConfig = botController.ClassConfig;
            if (classConfig != null)
            {
                ActionBarPopulator populator = new(
                    loggerFactory.CreateLogger<ActionBarPopulator>(),
                    classConfig,
                    addonConfigurator,
                    bagReader,
                    equipmentReader,
                    exec);
                populator.Execute();
                steps.Add("Action bar synced");
            }

            sw.Stop();
            logger.LogInformation("Auto-fix sequence complete: {Steps}", string.Join(", ", steps));

            return Ok(new FixResult(true, string.Join(" → ", steps), steps.Count));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auto-fix sequence failed at step {StepCount}", steps.Count + 1);
            sw.Stop();
            return StatusCode(500, new FixResult(false, $"{ex.Message} (after {steps.Count} steps)"));
        }
    }

    /// <summary>
    /// GET /api/diagnostics/input-mode
    /// Returns current runtime input dispatch mode (foreground-safe vs background-compatible).
    /// </summary>
    [HttpGet("input-mode")]
    public IActionResult GetInputMode()
    {
        try
        {
            var state = wowInput.GetInputSecurityState();
            bool backgroundCompatible = !state.FocusGuard && !state.HybridModifiers;

            return Ok(new InputSecurityModeInfo(
                backgroundCompatible ? "BackgroundCompatible" : "ForegroundSafe",
                backgroundCompatible,
                state.Enabled,
                state.FocusGuard,
                state.HybridModifiers,
                state.EmitWmChar));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Get input mode failed");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/diagnostics/input-mode
    /// Body: { "backgroundCompatible": true|false }
    /// </summary>
    [HttpPost("input-mode")]
    public IActionResult SetInputMode([FromBody] SetInputSecurityModeRequest request)
    {
        try
        {
            wowInput.EmergencyReleaseAllKeys();
            wowInput.SetBackgroundCompatibleInputMode(request.BackgroundCompatible);

            var state = wowInput.GetInputSecurityState();
            bool backgroundCompatible = !state.FocusGuard && !state.HybridModifiers;

            logger.LogWarning(
                "[Diagnostics       ] Input mode set to {Mode} (FocusGuard={FocusGuard}, HybridModifiers={HybridModifiers})",
                backgroundCompatible ? "BackgroundCompatible" : "ForegroundSafe",
                state.FocusGuard,
                state.HybridModifiers);

            return Ok(new InputSecurityModeInfo(
                backgroundCompatible ? "BackgroundCompatible" : "ForegroundSafe",
                backgroundCompatible,
                state.Enabled,
                state.FocusGuard,
                state.HybridModifiers,
                state.EmitWmChar));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Set input mode failed");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    private async Task<SlashCommandResult> ExecuteSlashCommandAsync(
        string command,
        bool useBackgroundCompatibleInput,
        int preDelayMs,
        int postDelayMs)
    {
        Stopwatch sw = Stopwatch.StartNew();

        preDelayMs = Math.Clamp(preDelayMs, 0, 3000);
        postDelayMs = Math.Clamp(postDelayMs, 0, 5000);

        (bool Enabled, bool FocusGuard, bool HybridModifiers, bool EmitWmChar) stateBefore = wowInput.GetInputSecurityState();
        bool backgroundCompatibleBefore = !stateBefore.FocusGuard && !stateBefore.HybridModifiers;
        bool restoreInputMode = backgroundCompatibleBefore != useBackgroundCompatibleInput;

        try
        {
            if (restoreInputMode)
            {
                wowInput.EmergencyReleaseAllKeys();
                wowInput.SetBackgroundCompatibleInputMode(useBackgroundCompatibleInput);
                await Task.Delay(50);
            }

            logger.LogInformation(
                "Executing slash command {Command} via WowProcessInput (BackgroundCompatible={BackgroundCompatible})",
                command,
                useBackgroundCompatibleInput);

            wowInput.SetForegroundWindow();
            if (preDelayMs > 0)
            {
                await Task.Delay(preDelayMs);
            }

            wowInput.PressRandom(ConsoleKey.Escape, 50);
            await Task.Delay(300);
            wowInput.PressRandom(ConsoleKey.Escape, 50);
            await Task.Delay(300);
            wowInput.PressRandom(ConsoleKey.Enter, 50);
            await Task.Delay(200);
            wowInput.SendText(command);
            await Task.Delay(150);
            wowInput.PressRandom(ConsoleKey.Enter, 50);

            if (postDelayMs > 0)
            {
                await Task.Delay(postDelayMs);
            }

            return new SlashCommandResult(true, command, "WowProcessInput", sw.ElapsedMilliseconds);
        }
        finally
        {
            if (restoreInputMode)
            {
                try
                {
                    wowInput.EmergencyReleaseAllKeys();
                    wowInput.SetBackgroundCompatibleInputMode(backgroundCompatibleBefore);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to restore input mode after slash command dispatch");
                }
            }
        }
    }
}
