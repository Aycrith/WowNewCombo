using Core;
using Core.Testing;
using Game;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SharedLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Frontend.Controllers;

#region Response DTOs

public record BindingInfo(string BindingId, string Key, string Modifier);

public record MismatchInfo(
    string BindingId,
    string ExpectedKey,
    string ExpectedModifier,
    string ActualKey,
    string ActualModifier);

public record KeybindDiagnostics(
    int TotalBindings,
    int MismatchCount,
    bool IsInitialized,
    IReadOnlyCollection<BindingInfo> Bindings,
    IReadOnlyCollection<MismatchInfo> Mismatches);

public record ActionBarIssueDto(
    string SpellName,
    int Slot,
    string Status,
    bool CanResolve);

public record ActionBarDiagnostics(
    int IssueCount,
    bool IsTextureInitialized,
    IReadOnlyCollection<ActionBarIssueDto> Issues);

public record ProfileDiagnostics(
    string? FileName,
    string? Mode,
    int KeybindMismatches,
    int ActionBarIssues,
    bool IsReady);

public record DiagnosticsSummary(
    bool SystemHealthy,
    string Message,
    KeybindDiagnostics? Keybindings,
    ActionBarDiagnostics? ActionBar,
    ProfileDiagnostics? Profile);

public record PlaceRequest(int Slot, string Name);

public record FixResult(bool Success, string Message, int ChangesApplied = 0);

#endregion

/// <summary>
/// API controller for diagnostic operations and automated fixes
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class DiagnosticsController : ControllerBase
{
    private readonly ILogger<DiagnosticsController> logger;
    private readonly KeyBindingsReader keyBindingsReader;
    private readonly ActionBarSlotValidator slotValidator;
    private readonly ActionBarTextureReader textureReader;
    private readonly IBotController botController;
    private readonly ExecGameCommand exec;
    private readonly AddonConfigurator addonConfigurator;
    private readonly IAddonReader addonReader;
    private readonly WowProcessInput wowInput;
    private readonly BagReader bagReader;
    private readonly EquipmentReader equipmentReader;
    private readonly ILoggerFactory loggerFactory;

    public DiagnosticsController(
        ILogger<DiagnosticsController> logger,
        KeyBindingsReader keyBindingsReader,
        ActionBarSlotValidator slotValidator,
        ActionBarTextureReader textureReader,
        IBotController botController,
        ExecGameCommand exec,
        AddonConfigurator addonConfigurator,
        IAddonReader addonReader,
        WowProcessInput wowInput,
        BagReader bagReader,
        EquipmentReader equipmentReader,
        ILoggerFactory loggerFactory)
    {
        this.logger = logger;
        this.keyBindingsReader = keyBindingsReader;
        this.slotValidator = slotValidator;
        this.textureReader = textureReader;
        this.botController = botController;
        this.exec = exec;
        this.addonConfigurator = addonConfigurator;
        this.addonReader = addonReader;
        this.wowInput = wowInput;
        this.bagReader = bagReader;
        this.equipmentReader = equipmentReader;
        this.loggerFactory = loggerFactory;
    }

    #region Diagnostic Endpoints

    /// <summary>
    /// GET /api/diagnostics/keybindings
    /// Returns current keybinding state and mismatches against profile
    /// </summary>
    [HttpGet("keybindings")]
    public IActionResult GetKeybindings()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            List<BindingInfo> bindings = [];
            List<MismatchInfo> mismatches = [];

            // Build list of current bindings
            foreach (KeyValuePair<BindingID, (ConsoleKey Key, ModifierKey Modifier)> kvp in keyBindingsReader.Bindings)
            {
                bindings.Add(new BindingInfo(
                    kvp.Key.ToStringF(),
                    kvp.Value.Key.ToString(),
                    kvp.Value.Modifier.ToPrefix()));
            }

            // Get mismatches if we have a profile loaded
            ClassConfiguration? classConfig = botController.ClassConfig;
            if (classConfig != null)
            {
                List<KeyAction> allKeyActions = [];
                foreach ((string _, KeyActions keyActions) in classConfig.GetByType<KeyActions>())
                {
                    allKeyActions.AddRange(keyActions.Sequence);
                }

                List<BindingMismatch> mismatchList = keyBindingsReader.GetMismatches(allKeyActions);
                foreach (BindingMismatch m in mismatchList)
                {
                    mismatches.Add(new MismatchInfo(
                        m.BindingId.ToStringF(),
                        m.ExpectedKey.ToString(),
                        m.ExpectedModifier.ToPrefix(),
                        m.ActualKey.ToString(),
                        m.ActualModifier.ToPrefix()));
                }
            }

            KeybindDiagnostics result = new(
                keyBindingsReader.Count,
                mismatches.Count,
                keyBindingsReader.IsInitialized,
                bindings,
                mismatches);

            sw.Stop();
            logger.LogInformation("Keybind diagnostics: {MismatchCount} mismatches, {TotalBindings} bindings ({ElapsedMs}ms)",
                result.MismatchCount, result.TotalBindings, sw.ElapsedMilliseconds);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keybind diagnostics failed");
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/diagnostics/actionbar
    /// Returns action bar validation issues
    /// </summary>
    [HttpGet("actionbar")]
    public IActionResult GetActionBar()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            ClassConfiguration? classConfig = botController.ClassConfig;
            if (classConfig == null)
            {
                return Ok(new ActionBarDiagnostics(0, false, Array.Empty<ActionBarIssueDto>()));
            }

            List<ActionBarIssue> issues = slotValidator.GetIssues(classConfig);
            List<ActionBarIssueDto> issueDtos = issues.Select(i => new ActionBarIssueDto(
                i.SpellName,
                i.Slot,
                i.Status.ToString(),
                i.CanResolve)).ToList();

            ActionBarDiagnostics result = new(
                issues.Count,
                textureReader.IsInitialized,
                issueDtos);

            sw.Stop();
            logger.LogInformation("Action bar diagnostics: {IssueCount} issues, initialized={IsInitialized} ({ElapsedMs}ms)",
                result.IssueCount, result.IsTextureInitialized, sw.ElapsedMilliseconds);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Action bar diagnostics failed");
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/diagnostics/profile
    /// Returns current profile status
    /// </summary>
    [HttpGet("profile")]
    public IActionResult GetProfile()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            ClassConfiguration? classConfig = botController.ClassConfig;
            if (classConfig == null)
            {
                return Ok(new ProfileDiagnostics(null, null, 0, 0, false));
            }

            // Count keybind mismatches
            List<KeyAction> allKeyActions = [];
            foreach ((string _, KeyActions keyActions) in classConfig.GetByType<KeyActions>())
            {
                allKeyActions.AddRange(keyActions.Sequence);
            }
            int keybindMismatches = keyBindingsReader.GetMismatches(allKeyActions).Count;

            // Count action bar issues
            int actionBarIssues = slotValidator.GetIssueCount(classConfig);

            ProfileDiagnostics result = new(
                botController.SelectedClassFilename,
                classConfig.Mode.ToString(),
                keybindMismatches,
                actionBarIssues,
                keybindMismatches == 0 && actionBarIssues == 0);

            sw.Stop();
            logger.LogInformation("Profile diagnostics: {FileName}, ready={IsReady} ({ElapsedMs}ms)",
                result.FileName, result.IsReady, sw.ElapsedMilliseconds);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Profile diagnostics failed");
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/diagnostics/summary
    /// Returns combined diagnostic summary
    /// </summary>
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            ClassConfiguration? classConfig = botController.ClassConfig;
            
            // Get all diagnostics
            KeybindDiagnostics? keybinds = null;
            ActionBarDiagnostics? actionBar = null;
            ProfileDiagnostics? profile = null;

            if (classConfig != null)
            {
                // Keybinds
                List<KeyAction> allKeyActions = [];
                foreach ((string _, KeyActions keyActions) in classConfig.GetByType<KeyActions>())
                {
                    allKeyActions.AddRange(keyActions.Sequence);
                }
                List<BindingMismatch> mismatchList = keyBindingsReader.GetMismatches(allKeyActions);
                keybinds = new KeybindDiagnostics(
                    keyBindingsReader.Count,
                    mismatchList.Count,
                    keyBindingsReader.IsInitialized,
                    Array.Empty<BindingInfo>(),
                    Array.Empty<MismatchInfo>());

                // Action Bar
                List<ActionBarIssue> issues = slotValidator.GetIssues(classConfig);
                actionBar = new ActionBarDiagnostics(
                    issues.Count,
                    textureReader.IsInitialized,
                    Array.Empty<ActionBarIssueDto>());

                // Profile
                profile = new ProfileDiagnostics(
                    botController.SelectedClassFilename,
                    classConfig.Mode.ToString(),
                    mismatchList.Count,
                    issues.Count,
                    mismatchList.Count == 0 && issues.Count == 0);
            }

            bool healthy = profile?.IsReady ?? false;
            string message = healthy
                ? "✅ System ready - no issues detected"
                : $"⚠️ Issues detected - {keybinds?.MismatchCount ?? 0} keybind mismatches, {actionBar?.IssueCount ?? 0} action bar issues";

            DiagnosticsSummary result = new(healthy, message, keybinds, actionBar, profile);

            sw.Stop();
            logger.LogInformation("Diagnostic summary: healthy={Healthy} ({ElapsedMs}ms)", healthy, sw.ElapsedMilliseconds);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Diagnostic summary failed");
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    #endregion

    #region Fix Endpoints

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
    /// POST /api/diagnostics/fix/initstate
    /// Resets addon state and flushes data
    /// </summary>
    [HttpPost("fix/initstate")]
    public IActionResult FixInitState()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Resetting addon state");
            addonReader.FullReset();
            wowInput.PressFlushKey();

            sw.Stop();
            return Ok(new FixResult(true, "Addon state reset and flushed", 1));
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

    #endregion
}
