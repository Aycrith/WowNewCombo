using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Startup;

/// <summary>
/// Main orchestrator that coordinates the complete startup sequence.
/// Executes stages in order: Discover WoW → Install Addons → Start Nav Server → Launch WoW → Configure Frames.
/// </summary>
public sealed class StartupOrchestrator
{
    private readonly ILogger<StartupOrchestrator> _logger;
    private readonly StartupOptions _options;
    private readonly StartupState _state;
    private readonly WoWPathFinder _pathFinder;
    private readonly AddonInstaller _addonInstaller;
    private readonly AddonValidator _addonValidator;
    private readonly NavigationServerManager _navManager;
    private readonly WoWProcessLauncher _wowLauncher;

    private readonly List<(StartupStage Stage, StageResult Result)> _stageResults = [];

    /// <summary>Event raised when a stage completes.</summary>
    public event EventHandler<StageCompletedEventArgs>? StageCompleted;

    /// <summary>Event raised when startup is fully complete.</summary>
    public event EventHandler<StartupResult>? StartupComplete;

    /// <summary>Current startup state (for UI binding).</summary>
    public StartupState State => _state;

    public StartupOrchestrator(
        ILogger<StartupOrchestrator> logger,
        IOptions<StartupOptions> options,
        StartupState state,
        WoWPathFinder pathFinder,
        AddonInstaller addonInstaller,
        AddonValidator addonValidator,
        NavigationServerManager navManager,
        WoWProcessLauncher wowLauncher)
    {
        _logger = logger;
        _options = options.Value;
        _state = state;
        _pathFinder = pathFinder;
        _addonInstaller = addonInstaller;
        _addonValidator = addonValidator;
        _navManager = navManager;
        _wowLauncher = wowLauncher;
    }

    /// <summary>
    /// Run the complete startup sequence.
    /// </summary>
    public async Task<StartupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (_options.SkipStartupOrchestration)
        {
            _logger.LogInformation("[StartupOrchestrator] Startup orchestration disabled, skipping");
            _state.IsReady = true;
            return StartupResult.CreateSuccess(TimeSpan.Zero, []);
        }

        _logger.LogInformation("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");
        _logger.LogInformation("[StartupOrchestrator]              STARTUP ORCHESTRATION BEGINNING");
        _logger.LogInformation("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");

        _state.Reset();
        _state.StartTime = DateTime.UtcNow;
        _stageResults.Clear();

        var overallStopwatch = Stopwatch.StartNew();

        try
        {
            // Stage 1: Initialize
            if (!await ExecuteStageAsync(StartupStage.Initializing, InitializeAsync, cancellationToken))
                return CreateFailureResult(StartupStage.Initializing, overallStopwatch.Elapsed);

            // Stage 2: Discover WoW
            if (!await ExecuteStageAsync(StartupStage.DiscoveringWoW, DiscoverWoWAsync, cancellationToken))
                return CreateFailureResult(StartupStage.DiscoveringWoW, overallStopwatch.Elapsed);

            // Stage 3: Validate/Install Addons
            if (!await ExecuteStageAsync(StartupStage.ValidatingAddons, ValidateAddonsAsync, cancellationToken))
                return CreateFailureResult(StartupStage.ValidatingAddons, overallStopwatch.Elapsed);

            // Stage 4: Start Navigation Server
            if (!await ExecuteStageAsync(StartupStage.StartingNavigationServer, StartNavigationServerAsync, cancellationToken))
            {
                // Navigation server is optional, log warning but continue
                _logger.LogWarning("[StartupOrchestrator] Navigation server failed to start, continuing without it");
            }

            // Stage 5: Launch/Detect WoW
            if (!await ExecuteStageAsync(StartupStage.LaunchingWoW, LaunchWoWAsync, cancellationToken))
                return CreateFailureResult(StartupStage.LaunchingWoW, overallStopwatch.Elapsed);

            // Stage 6: Wait for Character (user must log in manually)
            // This stage is handled differently - it waits indefinitely or times out
            await ExecuteStageAsync(StartupStage.WaitingForCharacter, WaitForCharacterAsync, cancellationToken);

            // Stage 7: Configure Frames (if needed)
            if (!await ExecuteStageAsync(StartupStage.ConfiguringFrames, ConfigureFramesAsync, cancellationToken))
            {
                // Frame config might need user action, but we don't fail startup for it
                _logger.LogWarning("[StartupOrchestrator] Frame configuration incomplete - may need manual setup");
            }

            // Stage 8: Final Validation
            if (!await ExecuteStageAsync(StartupStage.FinalValidation, FinalValidationAsync, cancellationToken))
                return CreateFailureResult(StartupStage.FinalValidation, overallStopwatch.Elapsed);

            // Success!
            overallStopwatch.Stop();
            _state.CurrentStage = StartupStage.Ready;
            _state.StatusMessage = "Startup complete! Bot is ready.";
            _state.IsReady = true;

            _logger.LogInformation("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");
            _logger.LogInformation("[StartupOrchestrator]              STARTUP COMPLETE - SYSTEM READY");
            _logger.LogInformation("[StartupOrchestrator]              Total Time: {Elapsed:mm\\:ss\\.fff}", overallStopwatch.Elapsed);
            _logger.LogInformation("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");

            var result = StartupResult.CreateSuccess(overallStopwatch.Elapsed, _stageResults);
            StartupComplete?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[StartupOrchestrator] Startup cancelled");
            return CreateFailureResult(_state.CurrentStage, overallStopwatch.Elapsed, "Startup cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StartupOrchestrator] Unexpected error during startup");
            return CreateFailureResult(_state.CurrentStage, overallStopwatch.Elapsed, ex.Message);
        }
    }

    private async Task<bool> ExecuteStageAsync(
        StartupStage stage,
        Func<CancellationToken, Task<StageResult>> stageFunc,
        CancellationToken cancellationToken)
    {
        _state.CurrentStage = stage;
        _logger.LogInformation("[StartupOrchestrator] ──────────────────────────────────────────────────────────────");
        _logger.LogInformation("[StartupOrchestrator] Stage: {Stage}", stage);

        var stopwatch = Stopwatch.StartNew();
        StageResult result;

        try
        {
            result = await stageFunc(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StartupOrchestrator] Stage {Stage} threw exception", stage);
            result = StageResult.Failed(ex.Message, ex);
        }

        stopwatch.Stop();
        _stageResults.Add((stage, result));

        _logger.LogInformation("[StartupOrchestrator] Stage {Stage}: {Result} ({Elapsed:mm\\:ss\\.fff})",
            stage, result.Type, stopwatch.Elapsed);

        StageCompleted?.Invoke(this, new StageCompletedEventArgs(stage, result, stopwatch.Elapsed));

        return result.CanContinue;
    }

    private StartupResult CreateFailureResult(StartupStage stage, TimeSpan elapsed, string? message = null)
    {
        _state.CurrentStage = StartupStage.Failed;
        _state.StatusMessage = message ?? $"Startup failed at stage: {stage}";
        _state.IsReady = false;

        _logger.LogError("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");
        _logger.LogError("[StartupOrchestrator]              STARTUP FAILED AT STAGE: {Stage}", stage);
        _logger.LogError("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");

        var result = StartupResult.CreateFailure(stage, _state.StatusMessage, elapsed, _stageResults);
        StartupComplete?.Invoke(this, result);
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // STAGE IMPLEMENTATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    private Task<StageResult> InitializeAsync(CancellationToken ct)
    {
        _state.StatusMessage = "Initializing startup orchestration...";
        _logger.LogInformation("[StartupOrchestrator] Options: AutoLaunchWoW={AutoLaunch}, AutoConfigFrames={AutoConfig}, NavServer={NavServer}",
            _options.AutoLaunchWoW, _options.AutoConfigureFrames, _options.AutoStartNavigationServer);

        return Task.FromResult(StageResult.Success("Initialization complete"));
    }

    private Task<StageResult> DiscoverWoWAsync(CancellationToken ct)
    {
        _state.StatusMessage = "Searching for World of Warcraft installation...";

        var installation = _pathFinder.FindInstallation();
        if (installation == null)
        {
            return Task.FromResult(StageResult.Failed(
                "Could not find WoW installation. Please configure WoWPath in appsettings.json or ensure WoW is installed in a standard location."));
        }

        _state.WoWInstallation = installation;
        _state.StatusMessage = $"Found WoW at: {installation.Path}";

        _logger.LogInformation("[StartupOrchestrator] WoW Installation: {Path}", installation.Path);
        _logger.LogInformation("[StartupOrchestrator]   Executable: {Exe}", installation.ExecutableName);
        _logger.LogInformation("[StartupOrchestrator]   Version: {Version}", installation.Version);
        _logger.LogInformation("[StartupOrchestrator]   DataToColor: {HasAddon}", installation.HasDataToColorAddon);
        _logger.LogInformation("[StartupOrchestrator]   SecureButtons.xml: {HasSecure}", installation.HasSecureButtonsXml);

        return Task.FromResult(StageResult.Success($"Found WoW: {installation.Path}"));
    }

    private async Task<StageResult> ValidateAddonsAsync(CancellationToken ct)
    {
        _state.StatusMessage = "Validating and installing addons...";

        var wowPath = _state.WoWInstallation?.Path;
        if (string.IsNullOrEmpty(wowPath))
        {
            return StageResult.Failed("WoW path not set");
        }

        try
        {
            // Check if this is a first-run scenario (AddonConfig doesn't exist)
            if (!AddonConfig.Exists())
            {
                _logger.LogWarning("[StartupOrchestrator] AddonConfig not found - first-run setup required");
                _state.StatusMessage = "First-run: Please configure addon in WebUI";
                return StageResult.Warning("Addon configuration required - please complete setup in WebUI at /AddonConfiguration");
            }

            // Run addon installer maintenance
            _addonInstaller.PerformMaintenance();

            // Validate addons
            var validation = _addonValidator.Validate();

            if (validation.IsValid)
            {
                _state.AddonsValidated = true;
                return StageResult.Success("All addons validated");
            }

            // Log errors
            foreach (var error in validation.Errors)
            {
                _logger.LogError("[StartupOrchestrator] Addon Error: {Title}: {Desc}", error.Title, error.Description);
            }

            foreach (var warning in validation.Warnings)
            {
                _logger.LogWarning("[StartupOrchestrator] Addon Warning: {Title}: {Desc}", warning.Title, warning.Description);
            }

            // If there are only warnings, we can continue
            if (validation.Errors.Count == 0)
            {
                _state.AddonsValidated = true;
                return StageResult.Warning("Addons validated with warnings");
            }

            // Check if the error is about addon not being configured (first-run)
            bool isFirstRunError = validation.Errors.Any(e => 
                e.Title.Contains("not configured", StringComparison.OrdinalIgnoreCase) ||
                e.Description.Contains("configuration is missing", StringComparison.OrdinalIgnoreCase));

            if (isFirstRunError)
            {
                _logger.LogWarning("[StartupOrchestrator] Addon not yet configured - first-run setup required");
                _state.StatusMessage = "First-run: Please configure addon in WebUI";
                return StageResult.Warning("Addon configuration required - please complete setup in WebUI at /AddonConfiguration");
            }

            return StageResult.Failed($"Addon validation failed: {validation.Errors[0].Title}");
        }
        catch (Exception ex)
        {
            return StageResult.Failed($"Addon validation error: {ex.Message}", ex);
        }
    }

    private async Task<StageResult> StartNavigationServerAsync(CancellationToken ct)
    {
        if (!_options.AutoStartNavigationServer)
        {
            return StageResult.Skipped("Navigation server auto-start disabled");
        }

        _state.StatusMessage = "Starting navigation server...";

        try
        {
            var success = await _navManager.EnsureRunningAsync(ct);

            if (success)
            {
                return StageResult.Success($"Navigation server running on port {_navManager.Port}");
            }

            if (_navManager.Status == NavigationServerStatus.NotInstalled)
            {
                return StageResult.Warning("Navigation server not installed - pathfinding will use local fallback");
            }

            return StageResult.Warning("Navigation server failed to start - pathfinding will use local fallback");
        }
        catch (Exception ex)
        {
            return StageResult.Warning($"Navigation server error: {ex.Message}");
        }
    }

    private async Task<StageResult> LaunchWoWAsync(CancellationToken ct)
    {
        _state.StatusMessage = "Checking for WoW process...";

        // First check if WoW is already running
        var existing = _wowLauncher.FindExistingProcess();
        if (existing != null)
        {
            return StageResult.Success($"WoW already running (PID: {existing.Id})");
        }

        if (!_options.AutoLaunchWoW)
        {
            _state.StatusMessage = "Please start World of Warcraft...";
            return StageResult.Waiting("Waiting for user to launch WoW");
        }

        // Launch WoW
        _state.StatusMessage = "Launching World of Warcraft...";

        var installation = _state.WoWInstallation;
        if (installation == null)
        {
            return StageResult.Failed("No WoW installation available");
        }

        var success = await _wowLauncher.LaunchAsync(installation, ct);

        if (success)
        {
            return StageResult.Success("WoW launched successfully");
        }

        return StageResult.Failed("Failed to launch WoW");
    }

    private async Task<StageResult> WaitForCharacterAsync(CancellationToken ct)
    {
        _state.StatusMessage = "Please log in and enter the game world...";

        // Check if we already have frame config (character was previously configured)
        if (FrameConfig.Exists() && AddonConfig.Exists())
        {
            _logger.LogInformation("[StartupOrchestrator] Frame config exists, assuming character ready");
            return StageResult.Skipped("Configuration already exists");
        }

        _logger.LogInformation("[StartupOrchestrator] Waiting for user to log in...");
        _logger.LogInformation("[StartupOrchestrator] ┌────────────────────────────────────────────────────────────┐");
        _logger.LogInformation("[StartupOrchestrator] │  Please log in to World of Warcraft and enter the world   │");
        _logger.LogInformation("[StartupOrchestrator] │  with your character. The bot will continue automatically  │");
        _logger.LogInformation("[StartupOrchestrator] │  once your character is in-game.                           │");
        _logger.LogInformation("[StartupOrchestrator] └────────────────────────────────────────────────────────────┘");

        // For now, we just wait for a reasonable time and let the frame configurator handle detection
        // In a more sophisticated implementation, we could poll pixel data to detect in-world state

        // Wait up to 5 minutes for user to log in (checking every 5 seconds)
        var timeout = _options.WaitForCharacterTimeoutSeconds > 0
            ? TimeSpan.FromSeconds(_options.WaitForCharacterTimeoutSeconds)
            : TimeSpan.FromMinutes(10); // Default max wait

        var startTime = DateTime.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            var elapsed = DateTime.UtcNow - startTime;
            if (elapsed > timeout)
            {
                _logger.LogWarning("[StartupOrchestrator] Timeout waiting for character, proceeding anyway");
                return StageResult.Warning("Timeout waiting for character");
            }

            // Check if WoW is still running
            if (!_wowLauncher.IsRunning())
            {
                return StageResult.Failed("WoW process exited");
            }

            // Update status message with elapsed time
            var remaining = timeout - elapsed;
            _state.StatusMessage = $"Waiting for character to enter world... ({remaining:mm\\:ss} remaining)";

            await Task.Delay(5000, ct);
        }

        return StageResult.Warning("Wait cancelled");
    }

    private async Task<StageResult> ConfigureFramesAsync(CancellationToken ct)
    {
        if (!_options.AutoConfigureFrames)
        {
            return StageResult.Skipped("Auto frame configuration disabled");
        }

        // Check if frame config already exists and is valid
        if (FrameConfig.Exists() && AddonConfig.Exists())
        {
            _state.FramesConfigured = true;
            return StageResult.Skipped("Frame configuration already exists");
        }

        _state.StatusMessage = "Configuring pixel reading frames...";

        _logger.LogInformation("[StartupOrchestrator] Frame configuration needed");
        _logger.LogInformation("[StartupOrchestrator] This will be handled by the FrameConfiguration page");

        // The actual frame configuration is complex and requires the full
        // FrameConfigurator with screen capture. We'll mark this as needing
        // attention and let the WebUI handle it.
        _state.StatusMessage = "Frame configuration required - please use the WebUI";

        return StageResult.Warning("Frame configuration pending - complete in WebUI");
    }

    private Task<StageResult> FinalValidationAsync(CancellationToken ct)
    {
        _state.StatusMessage = "Performing final validation...";

        var issues = new List<string>();

        // Check WoW is running
        if (!_wowLauncher.IsRunning())
        {
            issues.Add("WoW is not running");
        }

        // Check frame config
        if (!FrameConfig.Exists())
        {
            issues.Add("Frame configuration not complete");
        }

        // Check addon config
        if (!AddonConfig.Exists())
        {
            issues.Add("Addon configuration not complete");
        }

        if (issues.Count > 0)
        {
            _logger.LogWarning("[StartupOrchestrator] Validation issues: {Issues}", string.Join(", ", issues));
            return Task.FromResult(StageResult.Warning($"Ready with issues: {string.Join(", ", issues)}"));
        }

        _state.FramesConfigured = true;
        return Task.FromResult(StageResult.Success("All validations passed"));
    }
}
