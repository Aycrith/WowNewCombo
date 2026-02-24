using Core;
using Core.Diagnostics;
using Core.Goals;
using Core.Startup;
using Core.Testing;
using Game;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedLib;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

public record InputSecurityModeInfo(
    string Mode,
    bool BackgroundCompatible,
    bool Enabled,
    bool FocusGuard,
    bool HybridModifiers,
    bool EmitWmChar);

public record SetInputSecurityModeRequest(bool BackgroundCompatible);

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

public record BagMetaDto(
    int Index,
    int BagType,
    int SlotCount,
    int FreeSlots,
    int ItemId,
    string ItemName);

public record BagItemDto(
    int Bag,
    int Slot,
    int ItemId,
    string Name,
    int Quality,
    int Count,
    bool Tradable,
    bool Soulbound,
    bool Locked,
    bool NoValue);

public record BagDiagnostics(
    int TotalSlots,
    int TotalFreeSlots,
    int TotalFreeSlotsGeneral,
    bool BagsFull,
    bool AnyGreyItem,
    int ItemCount,
    IReadOnlyCollection<BagMetaDto> Bags,
    IReadOnlyCollection<BagItemDto> SampleItems,
    DateTime Timestamp);

public record MailboxInteractDiagnostics(
    bool MailFrameShown,
    bool CursorFound,
    string CursorType,
    int CursorX,
    int CursorY,
    string InteractionStep,
    int Attempts,
    long ElapsedMs);

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
    private readonly CursorScan cursorScan;
    private readonly AddonBits addonBits;
    private readonly BagReader bagReader;
    private readonly EquipmentReader equipmentReader;
    private readonly ILoggerFactory loggerFactory;
    private readonly SystemDiagnostics systemDiagnostics;

    private readonly StartupOptions startupOptions;

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
        CursorScan cursorScan,
        AddonBits addonBits,
        BagReader bagReader,
        EquipmentReader equipmentReader,
        ILoggerFactory loggerFactory,
        SystemDiagnostics systemDiagnostics,
        IOptions<StartupOptions> startupOptions)
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
        this.cursorScan = cursorScan;
        this.addonBits = addonBits;
        this.bagReader = bagReader;
        this.equipmentReader = equipmentReader;
        this.loggerFactory = loggerFactory;
        this.systemDiagnostics = systemDiagnostics;
        this.startupOptions = startupOptions.Value;
    }

    #region Diagnostic Endpoints

    /// <summary>
    /// GET /api/diagnostics/bags?take=20
    /// Returns current bag meta + computed bag-full signals
    /// </summary>
    [HttpGet("bags")]
    public IActionResult GetBags([FromQuery] int take = 20)
    {
        try
        {
            take = Math.Clamp(take, 0, 200);

            List<BagMetaDto> bags = [];
            for (int i = 0; i < bagReader.Bags.Length; i++)
            {
                Bag b = bagReader.Bags[i];
                bags.Add(new BagMetaDto(
                    i,
                    (int)b.BagType,
                    b.SlotCount,
                    b.FreeSlot,
                    b.Item.Entry,
                    b.Item.Name ?? string.Empty));
            }

            int totalFreeSlots = bagReader.TotalFreeSlotCount();
            int totalFreeSlotsGeneral = bagReader.TotalFreeGeneralSlotCount();

            List<BagItemDto> sampleItems = bagReader.BagItems
                .OrderBy(x => x.Item.Quality)
                .ThenBy(x => x.Item.Name, StringComparer.OrdinalIgnoreCase)
                .Take(take)
                .Select(x => new BagItemDto(
                    x.Bag,
                    x.Slot,
                    x.Item.Entry,
                    x.Item.Name ?? string.Empty,
                    x.Item.Quality,
                    x.Count,
                    x.IsTradable,
                    x.IsSoulbound,
                    x.IsLocked,
                    x.HasNoValue))
                .ToList();

            BagDiagnostics result = new(
                bagReader.SlotCount,
                totalFreeSlots,
                totalFreeSlotsGeneral,
                bagReader.BagsFull(),
                bagReader.AnyGreyItem(),
                bagReader.BagItems.Count,
                bags,
                sampleItems,
                DateTime.UtcNow);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bag diagnostics failed");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

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

    /// <summary>
    /// GET /api/diagnostics/slot/{slotNumber}
    /// Returns the raw integer value from a specific addon frame slot
    /// </summary>
    [HttpGet("slot/{slotNumber:int}")]
    public IActionResult GetSlotValue(int slotNumber)
    {
        try
        {
            if (slotNumber < 0 || slotNumber >= 324)
            {
                return BadRequest(new { Error = $"Slot number must be between 0 and 323 (got {slotNumber})" });
            }

            IAddonReader reader = addonReader;
            IAddonDataProvider dataProvider = ((AddonReader)reader).DataProvider;
            int value = dataProvider.GetInt(slotNumber);

            return Ok(new
            {
                Slot = slotNumber,
                Value = value,
                Hex = $"0x{value:X8}",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read slot {Slot}", slotNumber);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/diagnostics/slots/range?start=0&end=10
    /// Returns raw values from a range of slots
    /// </summary>
    [HttpGet("slots/range")]
    public IActionResult GetSlotRange([FromQuery] int start = 0, [FromQuery] int end = 10)
    {
        try
        {
            if (start < 0 || start >= 324)
                return BadRequest(new { Error = $"Start must be between 0 and 323 (got {start})" });
            if (end < 0 || end >= 324)
                return BadRequest(new { Error = $"End must be between 0 and 323 (got {end})" });
            if (end < start)
                return BadRequest(new { Error = $"End ({end}) must be >= start ({start})" });
            if ((end - start) > 50)
                return BadRequest(new { Error = $"Range too large. Max 50 slots at once (requested {end - start + 1})" });

            IAddonReader reader = addonReader;
            IAddonDataProvider dataProvider = ((AddonReader)reader).DataProvider;
            List<object> slots = [];

            for (int i = start; i <= end; i++)
            {
                int value = dataProvider.GetInt(i);
                slots.Add(new
                {
                    Slot = i,
                    Value = value,
                    Hex = $"0x{value:X8}"
                });
            }

            return Ok(new
            {
                Start = start,
                End = end,
                Count = slots.Count,
                Slots = slots,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read slot range {Start}-{End}", start, end);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/diagnostics/keybindings/stats
    /// Returns statistics about keybinding slot reads
    /// </summary>
    [HttpGet("keybindings/stats")]
    public IActionResult GetKeybindingStats()
    {
        try
        {
            (int totalReads, int nonZeroReads, int consecutiveZeros) = keyBindingsReader.GetReadStats();

            return Ok(new
            {
                TotalReads = totalReads,
                NonZeroReads = nonZeroReads,
                ConsecutiveZeros = consecutiveZeros,
                IsInitialized = keyBindingsReader.IsInitialized,
                BindingCount = keyBindingsReader.Count,
                PercentageNonZero = totalReads > 0 ? (nonZeroReads * 100.0 / totalReads) : 0.0,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get keybinding stats");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/diagnostics/monitor/slot106?duration=5
    /// Monitors slot 106 for a specified duration (in seconds) and returns all values seen
    /// </summary>
    [HttpGet("monitor/slot106")]
    public async Task<IActionResult> MonitorSlot106([FromQuery] int duration = 5)
    {
        try
        {
            if (duration < 1 || duration > 30)
                return BadRequest(new { Error = "Duration must be between 1 and 30 seconds" });

            IAddonReader reader = addonReader;
            IAddonDataProvider dataProvider = ((AddonReader)reader).DataProvider;
            List<object> readings = [];
            DateTime startTime = DateTime.UtcNow;
            DateTime endTime = startTime.AddSeconds(duration);
            int lastValue = -1;
            int sameValueCount = 0;

            while (DateTime.UtcNow < endTime)
            {
                int value = dataProvider.GetInt(106);

                if (value != lastValue)
                {
                    if (lastValue != -1)
                    {
                        // Record the previous value's duration
                        readings.Add(new
                        {
                            Value = lastValue,
                            Hex = $"0x{lastValue:X8}",
                            Count = sameValueCount,
                            Timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff")
                        });
                    }
                    lastValue = value;
                    sameValueCount = 1;
                }
                else
                {
                    sameValueCount++;
                }

                await Task.Delay(10); // 10ms between reads (~100 reads/sec)
            }

            // Add the last value
            if (lastValue != -1)
            {
                readings.Add(new
                {
                    Value = lastValue,
                    Hex = $"0x{lastValue:X8}",
                    Count = sameValueCount,
                    Timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff")
                });
            }

            return Ok(new
            {
                DurationSeconds = duration,
                TotalReadings = readings.Count,
                NonZeroCount = readings.Count(r => ((dynamic)r).Value != 0),
                Values = readings,
                StartTime = startTime.ToString("HH:mm:ss.fff"),
                EndTime = DateTime.UtcNow.ToString("HH:mm:ss.fff")
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to monitor slot 106");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/diagnostics/bot/state
    /// Returns current bot state including goals, profile, and system health
    /// </summary>
    [HttpGet("bot/state")]
    public IActionResult GetBotState()
    {
        try
        {
            Core.GOAP.GoapAgent? goapAgent = botController.GoapAgent;
            string currentGoal = goapAgent?.CurrentGoal?.GetType().Name ?? "None";
            List<string> goalStack = goapAgent?.Plan?.Select(g => g.GetType().Name).ToList() ?? [];

            return Ok(new
            {
                BotActive = botController.IsBotActive,
                CurrentGoal = currentGoal,
                GoalStackDepth = goalStack.Count,
                GoalStack = goalStack,
                Profile = new
                {
                    FileName = botController.SelectedClassFilename,
                    Mode = botController.ClassConfig?.Mode.ToString(),
                    PathCount = botController.SelectedPathFilename?.Count ?? 0
                },
                System = new
                {
                    AvgScreenLatency = botController.AvgScreenLatency,
                    AvgNPCLatency = botController.AvgNPCLatency,
                    KeybindingsInitialized = keyBindingsReader.IsInitialized,
                    ActionBarInitialized = textureReader.IsInitialized
                },
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get bot state");
            return StatusCode(500, new { Error = ex.Message, Stack = ex.StackTrace });
        }
    }

    /// <summary>
    /// GET /api/diagnostics/bot/combat-log
    /// Returns recent combat log entries
    /// </summary>
    [HttpGet("bot/combat-log")]
    public IActionResult GetCombatLog([FromQuery] int count = 20)
    {
        try
        {
            if (count < 1 || count > 100)
                return BadRequest(new { Error = "Count must be between 1 and 100" });

            // Try to get combat log via reflection
            Type addonReaderType = addonReader.GetType();
            var combatLogField = addonReaderType.GetField("combatLog",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (combatLogField == null)
            {
                return Ok(new { Message = "Combat log not available", Entries = Array.Empty<object>() });
            }

            dynamic? combatLog = combatLogField.GetValue(addonReader);
            if (combatLog == null)
            {
                return Ok(new { Message = "Combat log is null", Entries = Array.Empty<object>() });
            }

            return Ok(new
            {
                Count = count,
                Entries = new { Message = "Combat log entries available but format unknown - implement based on CombatLog class" },
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get combat log");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/diagnostics/system
    /// Returns comprehensive system diagnostics using SystemDiagnostics service.
    /// Checks: WoW process, navigation server, addon status.
    /// </summary>
    [HttpGet("system")]
    public async Task<IActionResult> GetSystemDiagnostics()
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Build server path
            string serverPath = string.IsNullOrEmpty(startupOptions.NavigationServerPath)
                ? System.IO.Path.Combine(AppContext.BaseDirectory, "Navigation", "AmeisenNavigationServer.exe")
                : startupOptions.NavigationServerPath;

            // Run individual checks
            var navCheck = await systemDiagnostics.CheckNavigationServerAsync(serverPath, startupOptions.NavigationServerPort);
            var wowCheck = systemDiagnostics.CheckWoWProcess();

            // Build response
            var response = new
            {
                Timestamp = DateTime.UtcNow,
                OverallStatus = DetermineOverallStatus(new[] { navCheck.Status, wowCheck.Status }),
                Checks = new[]
                {
                    new
                    {
                        navCheck.Name,
                        Status = navCheck.Status.ToString(),
                        navCheck.Message,
                        navCheck.Recommendation,
                        navCheck.Details
                    },
                    new
                    {
                        wowCheck.Name,
                        Status = wowCheck.Status.ToString(),
                        wowCheck.Message,
                        wowCheck.Recommendation,
                        wowCheck.Details
                    }
                }
            };

            sw.Stop();
            logger.LogInformation("System diagnostics completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "System diagnostics failed");
            sw.Stop();
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    private static string DetermineOverallStatus(IEnumerable<DiagnosticStatus> statuses)
    {
        if (statuses.Any(s => s == DiagnosticStatus.Error))
            return "Error";
        if (statuses.Any(s => s == DiagnosticStatus.Warning))
            return "Warning";
        return "Healthy";
    }

    #endregion

    #region Fix Endpoints

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

    #endregion
}
