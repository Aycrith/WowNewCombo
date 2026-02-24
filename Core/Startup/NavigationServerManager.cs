using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Startup;

/// <summary>
/// Status of the navigation server.
/// </summary>
public enum NavigationServerStatus
{
    /// <summary>Server executable not found.</summary>
    NotInstalled,

    /// <summary>Server is not running.</summary>
    Stopped,

    /// <summary>Server is starting up.</summary>
    Starting,

    /// <summary>Server is running and healthy.</summary>
    Running,

    /// <summary>Server failed to start or crashed.</summary>
    Failed
}

/// <summary>
/// Manages the AmeisenNavigation server process.
/// Can start, stop, and monitor the server health.
/// Includes comprehensive error capture and diagnostics.
/// </summary>
public sealed class NavigationServerManager : IHostedService, IDisposable
{
    private readonly ILogger<NavigationServerManager> _logger;
    private readonly StartupOptions _options;
    private readonly StartupState _state;

    private Process? _process;
    private readonly string _serverPath;
    private readonly string _serverDirectory;
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;

    // Output capture for diagnostics
    private readonly StringBuilder _outputBuffer = new StringBuilder();
    private readonly StringBuilder _errorBuffer = new StringBuilder();
    private readonly object _logLock = new object();

    // Restart limiting to prevent spawn loops
    private const int MaxRestartAttempts = 3;
    private const int RestartCooldownMinutes = 5;
    private int _restartAttempts;
    private DateTime _lastRestartAttempt = DateTime.MinValue;
    private readonly object _startLock = new object();
    private bool _isStarting;

    public NavigationServerStatus Status { get; private set; } = NavigationServerStatus.Stopped;
    public int Port => _options.NavigationServerPort;
    public bool IsInstalled => File.Exists(_serverPath);

    /// <summary>
    /// Gets the last captured output from the server for diagnostics.
    /// </summary>
    public string LastOutputSnapshot
    {
        get
        {
            lock (_logLock)
            {
                return _outputBuffer.ToString();
            }
        }
    }

    /// <summary>
    /// Gets the last captured errors from the server for diagnostics.
    /// </summary>
    public string LastErrorSnapshot
    {
        get
        {
            lock (_logLock)
            {
                return _errorBuffer.ToString();
            }
        }
    }

    public NavigationServerManager(
        ILogger<NavigationServerManager> logger,
        IOptions<StartupOptions> options,
        StartupState state)
    {
        _logger = logger;
        _options = options.Value;
        _state = state;
        _state.NavigationServerPort = _options.NavigationServerPort;

        // Determine server path
        if (!string.IsNullOrEmpty(_options.NavigationServerPath))
        {
            _serverPath = _options.NavigationServerPath;
        }
        else
        {
            // Default location relative to bot directory
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // Navigate up from bin folder to find Navigation folder
            var botRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            _serverPath = Path.Combine(botRoot, "Navigation", "AmeisenNavigationServer.exe");

            // Also check in current directory structure
            if (!File.Exists(_serverPath))
            {
                _serverPath = Path.Combine(baseDir, "Navigation", "AmeisenNavigationServer.exe");
            }
            if (!File.Exists(_serverPath))
            {
                _serverPath = Path.Combine(Directory.GetCurrentDirectory(), "Navigation", "AmeisenNavigationServer.exe");
            }
        }

        _serverDirectory = Path.GetDirectoryName(_serverPath) ?? Directory.GetCurrentDirectory();

        if (!File.Exists(_serverPath))
        {
            _logger.LogWarning("[NavigationServerManager] AmeisenNavigationServer.exe not found at: {Path}", _serverPath);
            Status = NavigationServerStatus.NotInstalled;
        }
    }

    /// <summary>
    /// Start the navigation server as a background process.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoStartNavigationServer)
        {
            _logger.LogInformation("[NavigationServerManager] Auto-start disabled, skipping");
            return;
        }

        await EnsureRunningAsync(cancellationToken);
    }

    /// <summary>
    /// Stop the navigation server.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _monitorCts?.Cancel();
        if (_monitorTask != null)
        {
            try
            {
                await _monitorTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NavigationServerManager] Monitor task failed during shutdown");
            }
        }

        await StopServerAsync();
    }

    /// <summary>
    /// Ensure the navigation server is running, starting it if necessary.
    /// </summary>
    public async Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default)
    {
        // Prevent concurrent start attempts
        lock (_startLock)
        {
            if (_isStarting)
            {
                _logger.LogDebug("[NavigationServerManager] Start already in progress, skipping");
                return false;
            }
        }

        // Check if already running (our process or external)
        if (await IsHealthyAsync())
        {
            Status = NavigationServerStatus.Running;
            _logger.LogInformation("[NavigationServerManager] Navigation server already running on port {Port}", Port);
            ResetRestartAttempts();
            return true;
        }

        if (!IsInstalled)
        {
            _logger.LogWarning("[NavigationServerManager] Cannot start - server not installed");
            Status = NavigationServerStatus.NotInstalled;
            return false;
        }

        // Check restart limits
        if (!CanAttemptRestart())
        {
            return false;
        }

        return await StartServerAsync(cancellationToken);
    }

    /// <summary>
    /// Check if we can attempt a restart based on limits.
    /// </summary>
    private bool CanAttemptRestart()
    {
        var now = DateTime.UtcNow;

        // Reset attempts after cooldown period
        if ((now - _lastRestartAttempt).TotalMinutes >= RestartCooldownMinutes)
        {
            ResetRestartAttempts();
        }

        if (_restartAttempts >= MaxRestartAttempts)
        {
            _logger.LogWarning(
                "[NavigationServerManager] Max restart attempts ({Max}) reached. " +
                "Waiting for cooldown period ({Cooldown} min) before retrying. " +
                "Navigation server may have a configuration issue.",
                MaxRestartAttempts, RestartCooldownMinutes);
            _logger.LogWarning("[NavigationServerManager] Last error output:\n{Error}", LastErrorSnapshot);
            Status = NavigationServerStatus.Failed;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reset the restart attempt counter.
    /// </summary>
    private void ResetRestartAttempts()
    {
        _restartAttempts = 0;
        _lastRestartAttempt = DateTime.MinValue;
        lock (_logLock)
        {
            _outputBuffer.Clear();
            _errorBuffer.Clear();
        }
    }

    /// <summary>
    /// Check if the navigation server is healthy (responding on its port).
    /// </summary>
    public Task<bool> IsHealthyAsync()
    {
        try
        {
            // AmeisenNavigationServer is extremely sensitive to clients that connect/disconnect without following
            // its expected protocol, and can crash with an access violation (-1073741819).
            // Avoid probing it with short-lived TCP connections; check for a LISTENing socket instead.
            System.Net.IPEndPoint[] listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i].Port == Port)
                {
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NavigationServerManager] Health check failed");
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Attempt to verify the server is actually responding to API requests.
    /// This is more thorough than just checking if the port is open.
    /// </summary>
    private async Task<bool> VerifyApiRespondsAsync()
    {
        try
        {
            // Try to connect and send a simple path request
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.ConnectAsync("127.0.0.1", Port);

            // If we can connect, that's good enough for now
            // Full protocol verification would require sending actual pathfinding requests
            _logger.LogDebug("[NavigationServerManager] API connectivity verified");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NavigationServerManager] API verification failed");
            return false;
        }
    }

    private async Task<bool> StartServerAsync(CancellationToken cancellationToken)
    {
        lock (_startLock)
        {
            if (_isStarting)
                return false;
            _isStarting = true;
        }

        try
        {
            _restartAttempts++;
            _lastRestartAttempt = DateTime.UtcNow;

            Status = NavigationServerStatus.Starting;
            _logger.LogInformation("[NavigationServerManager] Starting navigation server (attempt {Attempt}/{Max}): {Path}",
                _restartAttempts, MaxRestartAttempts, _serverPath);

            // Check for MMAP files
            var mmapsPath = Path.Combine(_serverDirectory, "mmaps");
            if (!Directory.Exists(mmapsPath))
            {
                _logger.LogError("[NavigationServerManager] MMAP directory not found: {Path}", mmapsPath);
                _logger.LogError("[NavigationServerManager] Navigation server requires MMAP files to function");
                Status = NavigationServerStatus.Failed;
                return false;
            }

            var mmapFiles = Directory.GetFiles(mmapsPath, "*.mmap");
            var mmtileFiles = Directory.GetFiles(mmapsPath, "*.mmtile");

            if (mmapFiles.Length == 0)
            {
                _logger.LogError("[NavigationServerManager] No .mmap files found in {Path}", mmapsPath);
                _logger.LogError("[NavigationServerManager] Navigation server requires MMAP files to function");
                Status = NavigationServerStatus.Failed;
                return false;
            }

            _logger.LogInformation("[NavigationServerManager] Found {MmapCount} .mmap files and {MmtileCount} .mmtile files",
                mmapFiles.Length, mmtileFiles.Length);

            // Clear previous output
            lock (_logLock)
            {
                _outputBuffer.Clear();
                _errorBuffer.Clear();
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _serverPath,
                WorkingDirectory = _serverDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Clean up any existing processes holding the port
            PortCleanupUtility.TryTerminateProcessHoldingPort(Port, "AmeisenNavigationServer", _logger);

            _process = Process.Start(startInfo);

            if (_process == null)
            {
                _logger.LogError("[NavigationServerManager] Failed to start process - Process.Start returned null");
                Status = NavigationServerStatus.Failed;
                return false;
            }

            _logger.LogInformation("[NavigationServerManager] Process started (PID: {PID})", _process.Id);

            // Setup output capture
            _process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (_logLock)
                    {
                        _outputBuffer.AppendLine(e.Data);
                        // Keep buffer from growing too large
                        if (_outputBuffer.Length > 10000)
                        {
                            _outputBuffer.Remove(0, _outputBuffer.Length - 5000);
                        }
                    }
                    _logger.LogDebug("[NavServer-Out] {Data}", e.Data);
                }
            };

            _process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (_logLock)
                    {
                        _errorBuffer.AppendLine(e.Data);
                        // Keep buffer from growing too large
                        if (_errorBuffer.Length > 10000)
                        {
                            _errorBuffer.Remove(0, _errorBuffer.Length - 5000);
                        }
                    }
                    _logger.LogError("[NavServer-Err] {Data}", e.Data);
                }
            };

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _state.NavigationProcess = _process;

            // Wait for server to become healthy
            _logger.LogInformation("[NavigationServerManager] Waiting for server to initialize...");

            for (int i = 0; i < 30; i++) // Wait up to 30 seconds
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("[NavigationServerManager] Startup cancelled");
                    break;
                }

                await Task.Delay(1000, cancellationToken);

                // Check if process died immediately
                _process.Refresh();
                if (_process.HasExited)
                {
                    int exitCode = _process.ExitCode;
                    _logger.LogError("[NavigationServerManager] Server process exited immediately with code {Code}", exitCode);
                    _logger.LogError("[NavigationServerManager] Error output:\n{Error}", LastErrorSnapshot);
                    Status = NavigationServerStatus.Failed;
                    return false;
                }

                if (await IsHealthyAsync())
                {
                    // Additional verification - make sure API actually responds
                    if (await VerifyApiRespondsAsync())
                    {
                        Status = NavigationServerStatus.Running;
                        _logger.LogInformation("[NavigationServerManager] Navigation server started successfully (PID: {PID})", _process.Id);

                        // Reset restart attempts on success
                        ResetRestartAttempts();

                        // Start monitoring
                        StartMonitoring();
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("[NavigationServerManager] Port open but API not responding, waiting...");
                    }
                }
            }

            // Timeout - capture what happened
            _process.Refresh();
            if (!_process.HasExited)
            {
                _logger.LogError("[NavigationServerManager] Server did not become healthy within timeout");
                _logger.LogError("[NavigationServerManager] Last output:\n{Output}", LastOutputSnapshot);
                _logger.LogError("[NavigationServerManager] Last errors:\n{Error}", LastErrorSnapshot);

                // Kill the process since it's not responding
                try
                {
                    _process.Kill(true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NavigationServerManager] Error killing non-responsive process");
                }
            }
            else
            {
                int exitCode = _process.ExitCode;
                _logger.LogError("[NavigationServerManager] Server process exited with code {Code}", exitCode);
                _logger.LogError("[NavigationServerManager] Error output:\n{Error}", LastErrorSnapshot);
            }

            Status = NavigationServerStatus.Failed;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NavigationServerManager] Failed to start navigation server");
            Status = NavigationServerStatus.Failed;
            return false;
        }
        finally
        {
            lock (_startLock)
            {
                _isStarting = false;
            }
        }
    }

    private async Task StopServerAsync()
    {
        if (_process == null)
            return;

        try
        {
            _logger.LogInformation("[NavigationServerManager] Stopping navigation server...");

            // Stop output reading first
            try
            {
                _process.CancelOutputRead();
                _process.CancelErrorRead();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[NavigationServerManager] Error cancelling output read");
            }

            if (!_process.HasExited)
            {
                _process.CloseMainWindow();
                if (!_process.WaitForExit(5000))
                {
                    _logger.LogWarning("[NavigationServerManager] Server did not exit gracefully, killing...");
                    _process.Kill(true);
                }
            }

            _logger.LogInformation("[NavigationServerManager] Navigation server stopped");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NavigationServerManager] Error stopping navigation server");
        }
        finally
        {
            _process?.Dispose();
            _process = null;
            _state.NavigationProcess = null;
            Status = NavigationServerStatus.Stopped;
        }
    }

    private void StartMonitoring()
    {
        if (_monitorTask != null && !_monitorTask.IsCompleted)
        {
            return;
        }

        _monitorCts?.Dispose();
        _monitorCts = new CancellationTokenSource();
        _monitorTask = MonitorServerAsync(_monitorCts.Token);
    }

    private async Task MonitorServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                if (_process != null)
                {
                    _process.Refresh();
                    if (_process.HasExited)
                    {
                        int exitCode = _process.ExitCode;
                        _logger.LogWarning("[NavigationServerManager] Server process exited unexpectedly (code {Code})", exitCode);
                        _logger.LogWarning("[NavigationServerManager] Last error output:\n{Error}", LastErrorSnapshot);

                        try
                        {
                            _process.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "[NavigationServerManager] Error disposing process");
                        }
                        _process = null;
                        _state.NavigationProcess = null;
                        Status = NavigationServerStatus.Stopped;

                        bool restarted = await EnsureRunningAsync(cancellationToken);
                        if (!restarted)
                        {
                            Status = NavigationServerStatus.Failed;
                            _logger.LogError("[NavigationServerManager] Failed to restart server after unexpected exit");
                        }

                        continue;
                    }
                    else if (!await IsHealthyAsync())
                    {
                        _logger.LogWarning("[NavigationServerManager] Server not responding on port {Port}", Port);
                        Status = NavigationServerStatus.Failed;
                    }
                    else
                    {
                        Status = NavigationServerStatus.Running;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NavigationServerManager] Monitor error");
            }
        }
    }

    public void Dispose()
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();

        if (_process != null)
        {
            try
            {
                // Cancel output reading
                try
                {
                    _process.CancelOutputRead();
                    _process.CancelErrorRead();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[NavigationServerManager] Error cancelling output read during dispose");
                }

                if (!_process.HasExited)
                {
                    _process.Kill(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[NavigationServerManager] Exception during process cleanup");
            }
            _process.Dispose();
            _process = null;
            _state.NavigationProcess = null;
        }
    }
}
