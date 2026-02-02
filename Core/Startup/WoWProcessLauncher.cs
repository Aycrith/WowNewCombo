using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Startup;

/// <summary>
/// Status of the WoW process.
/// </summary>
public enum WoWProcessStatus
{
    /// <summary>WoW is not running.</summary>
    NotRunning,

    /// <summary>WoW is starting up.</summary>
    Starting,

    /// <summary>WoW is running but state is unknown.</summary>
    Running,

    /// <summary>WoW is at the login screen.</summary>
    AtLoginScreen,

    /// <summary>WoW is at character selection.</summary>
    AtCharacterSelect,

    /// <summary>WoW is loading into the world.</summary>
    Loading,

    /// <summary>Character is in the game world.</summary>
    InWorld
}

/// <summary>
/// Manages launching and detecting the WoW process.
/// </summary>
public sealed class WoWProcessLauncher
{
    private readonly ILogger<WoWProcessLauncher> _logger;
    private readonly StartupOptions _options;
    private readonly StartupState _state;

    // Known WoW process names to look for
    private static readonly string[] WoWProcessNames =
    [
        "Wow",
        "WowClassic",
        "WowClassicT",
        "WowClassicB",
        "Wow-64"
    ];

    public WoWProcessStatus Status { get; private set; } = WoWProcessStatus.NotRunning;
    public Process? CurrentProcess { get; private set; }

    public WoWProcessLauncher(
        ILogger<WoWProcessLauncher> logger,
        IOptions<StartupOptions> options,
        StartupState state)
    {
        _logger = logger;
        _options = options.Value;
        _state = state;
    }

    /// <summary>
    /// Find an existing WoW process.
    /// </summary>
    public Process? FindExistingProcess()
    {
        foreach (var processName in WoWProcessNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    var process = processes[0];
                    _logger.LogInformation("[WoWProcessLauncher] Found existing WoW process: {Name} (PID: {PID})",
                        processName, process.Id);
                    CurrentProcess = process;
                    _state.WoWProcess = process;
                    Status = WoWProcessStatus.Running;
                    return process;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("[WoWProcessLauncher] Error checking for {Name}: {Error}",
                    processName, ex.Message);
            }
        }

        return null;
    }

    /// <summary>
    /// Launch WoW from the specified installation.
    /// </summary>
    public async Task<bool> LaunchAsync(WoWInstallation installation, CancellationToken cancellationToken = default)
    {
        if (installation == null)
        {
            _logger.LogError("[WoWProcessLauncher] Cannot launch - no installation provided");
            return false;
        }

        // Check if already running
        var existing = FindExistingProcess();
        if (existing != null)
        {
            _logger.LogInformation("[WoWProcessLauncher] WoW is already running");
            return true;
        }

        try
        {
            Status = WoWProcessStatus.Starting;
            _logger.LogInformation("[WoWProcessLauncher] Launching WoW: {Path}", installation.ExecutablePath);

            var startInfo = new ProcessStartInfo
            {
                FileName = installation.ExecutablePath,
                WorkingDirectory = installation.Path,
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);

            if (process == null)
            {
                _logger.LogError("[WoWProcessLauncher] Failed to start WoW process");
                Status = WoWProcessStatus.NotRunning;
                return false;
            }

            CurrentProcess = process;
            _state.WoWProcess = process;

            _logger.LogInformation("[WoWProcessLauncher] WoW launched (PID: {PID})", process.Id);

            // Wait for process to be responsive
            return await WaitForProcessAsync(TimeSpan.FromSeconds(_options.WoWLaunchTimeoutSeconds), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WoWProcessLauncher] Error launching WoW");
            Status = WoWProcessStatus.NotRunning;
            return false;
        }
    }

    /// <summary>
    /// Wait for WoW process to exist and be responsive.
    /// </summary>
    public async Task<bool> WaitForProcessAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            var process = FindExistingProcess();
            if (process != null)
            {
                // Wait a moment for the window to be ready
                await Task.Delay(2000, cancellationToken);

                try
                {
                    process.Refresh();
                    if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
                    {
                        Status = WoWProcessStatus.Running;
                        _logger.LogInformation("[WoWProcessLauncher] WoW process is ready");
                        return true;
                    }
                }
                catch
                {
                    // Process may have exited
                }
            }

            await Task.Delay(1000, cancellationToken);
        }

        _logger.LogWarning("[WoWProcessLauncher] Timeout waiting for WoW process");
        return false;
    }

    /// <summary>
    /// Wait indefinitely for WoW to be running with a character in-world.
    /// This polls the existing WowProcess service for status.
    /// </summary>
    public async Task<bool> WaitForCharacterInWorldAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[WoWProcessLauncher] Waiting for character to enter world...");
        _state.StatusMessage = "Waiting for you to log in and enter the game world...";

        var timeout = _options.WaitForCharacterTimeoutSeconds;
        var startTime = DateTime.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Check timeout (if not infinite)
            if (timeout > 0)
            {
                var elapsed = DateTime.UtcNow - startTime;
                if (elapsed.TotalSeconds > timeout)
                {
                    _logger.LogWarning("[WoWProcessLauncher] Timeout waiting for character in world");
                    return false;
                }
            }

            // Check if process is still running
            if (CurrentProcess != null)
            {
                try
                {
                    CurrentProcess.Refresh();
                    if (CurrentProcess.HasExited)
                    {
                        _logger.LogWarning("[WoWProcessLauncher] WoW process exited while waiting");
                        Status = WoWProcessStatus.NotRunning;
                        CurrentProcess = null;
                        _state.WoWProcess = null;
                        return false;
                    }
                }
                catch
                {
                    // Process access error
                }
            }

            // TODO: In the full implementation, we would check if the addon is responding
            // by looking at pixel data. For now, we rely on the FrameConfigurator to
            // detect when the character is in-world during frame configuration.

            await Task.Delay(1000, cancellationToken);
        }

        return false;
    }

    /// <summary>
    /// Check if WoW is currently running.
    /// </summary>
    public bool IsRunning()
    {
        if (CurrentProcess == null)
        {
            // Try to find an existing process
            return FindExistingProcess() != null;
        }

        try
        {
            CurrentProcess.Refresh();
            return !CurrentProcess.HasExited;
        }
        catch
        {
            CurrentProcess = null;
            _state.WoWProcess = null;
            Status = WoWProcessStatus.NotRunning;
            return false;
        }
    }
}
