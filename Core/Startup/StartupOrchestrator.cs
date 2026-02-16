using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Game;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SharedLib;

namespace Core.Startup;

/// <summary>
/// Main orchestrator that coordinates the complete startup sequence.
/// Executes stages in order: Discover WoW → Install Addons → Start Nav Server → Launch WoW → Configure Frames.
/// </summary>
public sealed class StartupOrchestrator
{
    private readonly ILogger<StartupOrchestrator> logger;
    private readonly StartupOptions options;
    private readonly StartupState state;
    private readonly StartupConfigPathing pathing;
    private readonly WoWPathFinder pathFinder;
    private readonly AddonInstaller addonInstaller;
    private readonly AddonValidator addonValidator;
    private readonly NavigationServerManager navManager;
    private readonly WoWProcessLauncher wowLauncher;
    private readonly FrameConfigurator? frameConfigurator;
    private readonly WowProcess? wowProcess;

    private readonly List<(StartupStage Stage, StageResult Result)> _stageResults = [];

    // Constants

    /// <summary>
    /// Delay in milliseconds between startup stages.
    /// Allows UI updates and prevents rapid-fire stage transitions.
    /// </summary>
    private const int StageTransitionDelayMs = 2000;

    /// <summary>
    /// Default timeout in minutes for waiting for character login.
    /// Used when WaitForCharacterTimeoutSeconds option is not configured.
    /// </summary>
    private const int DefaultCharacterWaitTimeoutMinutes = 10;

    /// <summary>
    /// Polling interval in milliseconds when waiting for character.
    /// Controls how often we check if the character is in-world.
    /// </summary>
    private const int CharacterWaitPollIntervalMs = 5000;

    /// <summary>
    /// Default port for RemoteV1 pathing API.
    /// Used when portv1 is not configured or is invalid.
    /// </summary>
    private const int DefaultPathingApiPort = 5001;

    /// <summary>
    /// HTTP timeout in seconds for pathing API health checks.
    /// Prevents indefinite blocking when checking if pathing API is responsive.
    /// </summary>
    private const int PathingApiHealthCheckTimeoutSeconds = 2;

    /// <summary>
    /// Default localhost IP address for pathing API.
    /// Used when hostv1 is not configured or is empty.
    /// </summary>
    private const string DefaultPathingApiHost = "127.0.0.1";

    /// <summary>Event raised when a stage completes.</summary>
    public event EventHandler<StageCompletedEventArgs>? StageCompleted;

    /// <summary>Event raised when startup is fully complete.</summary>
    public event EventHandler<StartupResult>? StartupComplete;

    /// <summary>Current startup state (for UI binding).</summary>
    public StartupState State => state;

    public StartupOrchestrator(
        ILogger<StartupOrchestrator> loggerParam,
        IOptions<StartupOptions> optionsParam,
        IOptions<StartupConfigPathing> pathingParam,
        StartupState stateParam,
        WoWPathFinder pathFinderParam,
        AddonInstaller addonInstallerParam,
        AddonValidator addonValidatorParam,
        NavigationServerManager navManagerParam,
        WoWProcessLauncher wowLauncherParam,
        FrameConfigurator? frameConfiguratorParam = null,
        WowProcess? wowProcessParam = null)
    {
        logger = loggerParam;
        options = optionsParam.Value;
        pathing = pathingParam.Value;
        state = stateParam;
        pathFinder = pathFinderParam;
        addonInstaller = addonInstallerParam;
        addonValidator = addonValidatorParam;
        navManager = navManagerParam;
        wowLauncher = wowLauncherParam;
        frameConfigurator = frameConfiguratorParam;
        wowProcess = wowProcessParam;
    }

    /// <summary>
    /// Run the complete startup sequence.
    /// </summary>
    public async Task<StartupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (options.SkipStartupOrchestration)
        {
            logger.LogWarning("[StartupOrchestrator] Startup orchestration disabled via config (SkipStartupOrchestration=true). " +
                "Marking ready WITHOUT verifying WoW, addons, frames, or navigation server.");
            state.IsReady = true;
            return StartupResult.CreateSuccess(TimeSpan.Zero, []);
        }

        logger.LogInformation("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");
        logger.LogInformation("[StartupOrchestrator]              STARTUP ORCHESTRATION BEGINNING");
        logger.LogInformation("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");

        state.Reset();
        state.StartTime = DateTime.UtcNow;
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
                logger.LogWarning("[StartupOrchestrator] Navigation server failed to start, continuing without it");
            }

            // Stage 5: Launch/Detect WoW
            if (!await ExecuteStageAsync(StartupStage.LaunchingWoW, LaunchWoWAsync, cancellationToken))
                return CreateFailureResult(StartupStage.LaunchingWoW, overallStopwatch.Elapsed);

            // Stage 6: Wait for Character (user must log in manually)
            // This stage is handled differently - it waits indefinitely or times out
            if (!await ExecuteStageAsync(StartupStage.WaitingForCharacter, WaitForCharacterAsync, cancellationToken))
            {
                logger.LogWarning("[StartupOrchestrator] Character detection failed or timed out - continuing but frame config may fail");
            }

            // Stage 7: Configure Frames (if needed)
            if (!await ExecuteStageAsync(StartupStage.ConfiguringFrames, ConfigureFramesAsync, cancellationToken))
            {
                // Frame config might need user action, but we don't fail startup for it
                logger.LogWarning("[StartupOrchestrator] Frame configuration incomplete - may need manual setup");
            }

            // Stage 8: Final Validation
            if (!await ExecuteStageAsync(StartupStage.FinalValidation, FinalValidationAsync, cancellationToken))
                return CreateFailureResult(StartupStage.FinalValidation, overallStopwatch.Elapsed);

            // Success - but log which optional stages failed
            overallStopwatch.Stop();

            bool navFailed = _stageResults.Any(s => s.Stage == StartupStage.StartingNavigationServer && !s.Result.CanContinue);
            bool framesFailed = !state.FramesConfigured;

            if (navFailed || framesFailed)
            {
                logger.LogWarning("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");
                logger.LogWarning("[StartupOrchestrator]   STARTUP COMPLETE WITH DEGRADED SUBSYSTEMS:");
                if (navFailed)
                    logger.LogWarning("[StartupOrchestrator]     ⚠ Navigation server is NOT running");
                if (framesFailed)
                    logger.LogWarning("[StartupOrchestrator]     ⚠ Frame configuration is NOT complete");
                logger.LogWarning("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");
            }

            state.CurrentStage = StartupStage.Ready;
            state.StatusMessage = navFailed || framesFailed
                ? "Startup complete with warnings - some subsystems need attention"
                : "Startup complete! Bot is ready.";
            state.IsReady = true;

            logger.LogInformation("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");
            logger.LogInformation("[StartupOrchestrator]              STARTUP COMPLETE - SYSTEM READY");
            logger.LogInformation("[StartupOrchestrator]              Total Time: {Elapsed:mm\\:ss\\.fff}", overallStopwatch.Elapsed);
            logger.LogInformation("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");

            var result = StartupResult.CreateSuccess(overallStopwatch.Elapsed, _stageResults);
            StartupComplete?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("[StartupOrchestrator] Startup cancelled");
            return CreateFailureResult(state.CurrentStage, overallStopwatch.Elapsed, "Startup cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[StartupOrchestrator] Unexpected error during startup");
            return CreateFailureResult(state.CurrentStage, overallStopwatch.Elapsed, ex.Message);
        }
    }

    private async Task<bool> ExecuteStageAsync(
        StartupStage stage,
        Func<CancellationToken, Task<StageResult>> stageFunc,
        CancellationToken cancellationToken)
    {
        state.CurrentStage = stage;
        logger.LogInformation("[StartupOrchestrator] ──────────────────────────────────────────────────────────────");
        logger.LogInformation("[StartupOrchestrator] Stage: {Stage}", stage);

        var stopwatch = Stopwatch.StartNew();
        StageResult result;

        try
        {
            result = await stageFunc(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[StartupOrchestrator] Stage {Stage} threw exception", stage);
            result = StageResult.Failed(ex.Message, ex);
        }

        stopwatch.Stop();
        _stageResults.Add((stage, result));

        logger.LogInformation("[StartupOrchestrator] Stage {Stage}: {Result} ({Elapsed:mm\\:ss\\.fff})",
            stage, result.Type, stopwatch.Elapsed);

        StageCompleted?.Invoke(this, new StageCompletedEventArgs(stage, result, stopwatch.Elapsed));

        return result.CanContinue;
    }

    private StartupResult CreateFailureResult(StartupStage stage, TimeSpan elapsed, string? message = null)
    {
        state.CurrentStage = StartupStage.Failed;
        state.StatusMessage = message ?? $"Startup failed at stage: {stage}";
        state.IsReady = false;

        logger.LogError("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");
        logger.LogError("[StartupOrchestrator]              STARTUP FAILED AT STAGE: {Stage}", stage);
        logger.LogError("[StartupOrchestrator] ═══════════════════════════════════════════════════════════════");

        var result = StartupResult.CreateFailure(stage, state.StatusMessage, elapsed, _stageResults);
        StartupComplete?.Invoke(this, result);
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // STAGE IMPLEMENTATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task<StageResult> InitializeAsync(CancellationToken ct)
    {
        state.StatusMessage = "Initializing startup orchestration...";

        await CleanupPathingApiPortIfStaleAsync(ct);

        logger.LogInformation("[StartupOrchestrator] Options: AutoLaunchWoW={AutoLaunch}, AutoConfigFrames={AutoConfig}, NavServer={NavServer}",
            options.AutoLaunchWoW, options.AutoConfigureFrames, options.AutoStartNavigationServer);

        return StageResult.Success("Initialization complete");
    }

    private Task<StageResult> DiscoverWoWAsync(CancellationToken ct)
    {
        state.StatusMessage = "Searching for World of Warcraft installation...";

        var installation = pathFinder.FindInstallation();
        if (installation == null)
        {
            return Task.FromResult(StageResult.Failed(
                "Could not find WoW installation. Please configure WoWPath in appsettings.json or ensure WoW is installed in a standard location."));
        }

        state.WoWInstallation = installation;
        state.StatusMessage = $"Found WoW at: {installation.Path}";

        logger.LogInformation("[StartupOrchestrator] WoW Installation: {Path}", installation.Path);
        logger.LogInformation("[StartupOrchestrator]   Executable: {Exe}", installation.ExecutableName);
        logger.LogInformation("[StartupOrchestrator]   Version: {Version}", installation.Version);
        logger.LogInformation("[StartupOrchestrator]   DataToColor: {HasAddon}", installation.HasDataToColorAddon);
        logger.LogInformation("[StartupOrchestrator]   SecureButtons.xml: {HasSecure}", installation.HasSecureButtonsXml);

        return Task.FromResult(StageResult.Success($"Found WoW: {installation.Path}"));
    }

    private async Task<StageResult> ValidateAddonsAsync(CancellationToken ct)
    {
        state.StatusMessage = "Validating and installing addons...";

        var wowPath = state.WoWInstallation?.Path;
        if (string.IsNullOrEmpty(wowPath))
        {
            return StageResult.Failed("WoW path not set");
        }

        try
        {
            // Check if this is a first-run scenario (AddonConfig doesn't exist)
            if (!AddonConfig.Exists())
            {
                logger.LogWarning("[StartupOrchestrator] AddonConfig not found - first-run setup required");
                state.StatusMessage = "First-run: Please configure addon in WebUI";
                return StageResult.Warning("Addon configuration required - please complete setup in WebUI at /AddonConfiguration");
            }

            // Run addon installer maintenance
            addonInstaller.PerformMaintenance();

            // Validate addons
            var validation = addonValidator.Validate();

            if (validation.IsValid)
            {
                state.AddonsValidated = true;
                return StageResult.Success("All addons validated");
            }

            // Log errors
            foreach (var error in validation.Errors)
            {
                logger.LogError("[StartupOrchestrator] Addon Error: {Title}: {Desc}", error.Title, error.Description);
            }

            foreach (var warning in validation.Warnings)
            {
                logger.LogWarning("[StartupOrchestrator] Addon Warning: {Title}: {Desc}", warning.Title, warning.Description);
            }

            // If there are only warnings, we can continue
            if (validation.Errors.Count == 0)
            {
                state.AddonsValidated = true;
                return StageResult.Warning("Addons validated with warnings");
            }

            // Check if the error is about addon not being configured (first-run)
            bool isFirstRunError = validation.Errors.Any(e => 
                e.Title.Contains("not configured", StringComparison.OrdinalIgnoreCase) ||
                e.Description.Contains("configuration is missing", StringComparison.OrdinalIgnoreCase));

            if (isFirstRunError)
            {
                logger.LogWarning("[StartupOrchestrator] Addon not yet configured - first-run setup required");
                state.StatusMessage = "First-run: Please configure addon in WebUI";
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
        if (!options.AutoStartNavigationServer)
        {
            return StageResult.Skipped("Navigation server auto-start disabled");
        }

        if (pathing.Type != StartupConfigPathing.Types.RemoteV3)
        {
            return StageResult.Skipped($"Navigation server skipped (Pathing.Mode={pathing.Type})");
        }

        state.StatusMessage = "Starting navigation server...";

        try
        {
            var success = await navManager.EnsureRunningAsync(ct);

            if (success)
            {
                return StageResult.Success($"Navigation server running on port {navManager.Port}");
            }

            if (navManager.Status == NavigationServerStatus.NotInstalled)
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
        state.StatusMessage = "Checking for WoW process...";

        // First check if WoW is already running
        var existing = wowLauncher.FindExistingProcess();
        if (existing != null)
        {
            return StageResult.Success($"WoW already running (PID: {existing.Id})");
        }

        if (!options.AutoLaunchWoW)
        {
            state.StatusMessage = "Please start World of Warcraft...";
            return StageResult.Waiting("Waiting for user to launch WoW");
        }

        // Launch WoW
        state.StatusMessage = "Launching World of Warcraft...";

        var installation = state.WoWInstallation;
        if (installation == null)
        {
            return StageResult.Failed("No WoW installation available");
        }

        var success = await wowLauncher.LaunchAsync(installation, ct);

        if (success)
        {
            return StageResult.Success("WoW launched successfully");
        }

        return StageResult.Failed("Failed to launch WoW");
    }

    private async Task<StageResult> WaitForCharacterAsync(CancellationToken ct)
    {
        state.StatusMessage = "Please log in and enter the game world...";

        // Check if we already have frame config (character was previously configured)
        if (FrameConfig.Exists() && AddonConfig.Exists())
        {
            logger.LogInformation("[StartupOrchestrator] Frame config exists, assuming character ready");
            return StageResult.Skipped("Configuration already exists");
        }

        // If WoW is running and we have the FrameConfigurator, we can try to detect character
        // by attempting frame configuration (it will fail if character isn't in-world)
        if (frameConfigurator != null && wowProcess != null && wowProcess.IsRunning)
        {
            logger.LogInformation("[StartupOrchestrator] WoW is running, proceeding to frame configuration");
            logger.LogInformation("[StartupOrchestrator] If character is not in-world, frame config will fail");
            
            // Give a short delay for any loading screens to complete
            state.StatusMessage = "WoW detected, preparing for frame configuration...";
            await Task.Delay(StageTransitionDelayMs, ct);
            
            return StageResult.Success("WoW process detected, proceeding to frame configuration");
        }

        logger.LogInformation("[StartupOrchestrator] Waiting for user to log in...");
        logger.LogInformation("[StartupOrchestrator] ┌────────────────────────────────────────────────────────────┐");
        logger.LogInformation("[StartupOrchestrator] │  Please log in to World of Warcraft and enter the world   │");
        logger.LogInformation("[StartupOrchestrator] │  with your character. The bot will continue automatically  │");
        logger.LogInformation("[StartupOrchestrator] │  once your character is in-game.                           │");
        logger.LogInformation("[StartupOrchestrator] └────────────────────────────────────────────────────────────┘");

        // For now, we just wait for a reasonable time and let the frame configurator handle detection
        // In a more sophisticated implementation, we could poll pixel data to detect in-world state

            // Wait up to 5 minutes for user to log in (checking every 5 seconds)
            var timeout = options.WaitForCharacterTimeoutSeconds > 0
                ? TimeSpan.FromSeconds(options.WaitForCharacterTimeoutSeconds)
                : TimeSpan.FromMinutes(DefaultCharacterWaitTimeoutMinutes);

        var startTime = DateTime.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            var elapsed = DateTime.UtcNow - startTime;
            if (elapsed > timeout)
            {
                logger.LogWarning("[StartupOrchestrator] Timeout waiting for character, proceeding anyway");
                return StageResult.Warning("Timeout waiting for character");
            }

            // Check if WoW is still running
            if (!wowLauncher.IsRunning())
            {
                return StageResult.Failed("WoW process exited");
            }

            // Update status message with elapsed time
            var remaining = timeout - elapsed;
            state.StatusMessage = $"Waiting for character to enter world... ({remaining:mm\\:ss} remaining)";

            await Task.Delay(CharacterWaitPollIntervalMs, ct);
        }

        return StageResult.Warning("Wait cancelled");
    }

    private async Task<StageResult> ConfigureFramesAsync(CancellationToken ct)
    {
        if (!options.AutoConfigureFrames)
        {
            return StageResult.Skipped("Auto frame configuration disabled");
        }

        // Check if frame config already exists and is valid
        if (FrameConfig.Exists() && AddonConfig.Exists())
        {
            state.FramesConfigured = true;
            return StageResult.Skipped("Frame configuration already exists");
        }

        state.StatusMessage = "Configuring pixel reading frames...";

        // Check if we have the FrameConfigurator available
        if (frameConfigurator == null)
        {
            logger.LogWarning("[StartupOrchestrator] FrameConfigurator not available - please configure manually");
            state.StatusMessage = "Frame configuration required - please use the WebUI";
            return StageResult.Warning("Frame configuration pending - complete in WebUI");
        }

        logger.LogInformation("[StartupOrchestrator] Starting automatic frame configuration...");
        logger.LogInformation("[StartupOrchestrator] This will type '/{Command}' in WoW chat to toggle config mode", GetAddonCommand());
        
        try
        {
            // Use the async version with retries
            var success = await frameConfigurator.StartAutoConfigWithRetriesAsync(
                maxRetries: options.FrameConfigMaxRetries,
                retryDelaySeconds: options.FrameConfigRetryDelaySeconds,
                cancellationToken: ct);

            if (success)
            {
                state.FramesConfigured = true;
                logger.LogInformation("[StartupOrchestrator] Frame configuration completed successfully!");
                return StageResult.Success("Frames configured successfully");
            }
            else
            {
                // Check what went wrong
                if (frameConfigurator.PreFlightFailed)
                {
                    logger.LogError("[StartupOrchestrator] Pre-flight checks failed: {Status}", 
                        frameConfigurator.StatusMessage);
                    return StageResult.Failed($"Pre-flight failed: {frameConfigurator.StatusMessage}");
                }
                
                if (frameConfigurator.AddonNotVisible)
                {
                    string command = GetAddonCommand();
                    logger.LogWarning("[StartupOrchestrator] Addon not visible - ensure character is in-world");
                    logger.LogWarning("[StartupOrchestrator] Run '/{Command}actions' in WoW to setup keybindings", command);
                    return StageResult.Warning($"Addon not visible - run /{command}actions in WoW");
                }
                
                logger.LogWarning("[StartupOrchestrator] Frame configuration failed: {Status}", 
                    frameConfigurator.StatusMessage);
                return StageResult.Warning($"Frame config incomplete: {frameConfigurator.StatusMessage}");
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("[StartupOrchestrator] Frame configuration cancelled");
            return StageResult.Warning("Frame configuration cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[StartupOrchestrator] Frame configuration threw exception");
            return StageResult.Failed($"Frame configuration error: {ex.Message}", ex);
        }
    }

    private Task<StageResult> FinalValidationAsync(CancellationToken ct)
    {
        state.StatusMessage = "Performing final validation...";

        var issues = new List<string>();

        // Check WoW is running
        if (!wowLauncher.IsRunning())
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
            logger.LogWarning("[StartupOrchestrator] Validation issues: {Issues}", string.Join(", ", issues));
            return Task.FromResult(StageResult.Warning($"Ready with issues: {string.Join(", ", issues)}"));
        }

        state.FramesConfigured = true;
        return Task.FromResult(StageResult.Success("All validations passed"));
    }

    private static string GetAddonCommand()
    {
        try
        {
            string cmd = AddonConfig.Load().Command?.Trim() ?? string.Empty;
            if (cmd.StartsWith('/'))
            {
                cmd = cmd.TrimStart('/');
            }

            return string.IsNullOrWhiteSpace(cmd) ? "dc" : cmd;
        }
        catch (Exception)
        {
            // Static method - cannot log. AddonConfig not available is expected during first-run.
            return "dc";
        }
    }

    private async Task CleanupPathingApiPortIfStaleAsync(CancellationToken cancellationToken)
    {
        if (pathing.Type != StartupConfigPathing.Types.RemoteV1)
        {
            return;
        }

            int port = pathing.portv1 > 0 ? pathing.portv1 : DefaultPathingApiPort;
            string host = string.IsNullOrWhiteSpace(pathing.hostv1) ? DefaultPathingApiHost : pathing.hostv1;

        if (!IsLocalHost(host))
        {
            return;
        }

        bool healthy = await IsPathingApiHealthyAsync(host, port, cancellationToken);
        if (healthy)
        {
            return;
        }

        PortCleanupUtility.TryTerminateProcessHoldingPort(port, "PathingAPI", logger);
    }

    private static async Task<bool> IsPathingApiHealthyAsync(string host, int port, CancellationToken cancellationToken)
    {
        string url = $"http://{host}:{port}/api/PPather/SelfTest";

        try
        {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(PathingApiHealthCheckTimeoutSeconds) };
            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            string payload = await response.Content.ReadAsStringAsync(cancellationToken);
            return payload.Contains("true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // Static method - cannot log. Connection failure is expected when PathingAPI is not running.
            return false;
        }
    }

    private static bool IsLocalHost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}
