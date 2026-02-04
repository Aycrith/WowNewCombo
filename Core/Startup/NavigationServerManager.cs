using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
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

    public NavigationServerManager(
        ILogger<NavigationServerManager> logger,
        IOptions<StartupOptions> options,
        StartupState state)
    {
        _logger = logger;
        _options = options.Value;
        _state = state;

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
            try { await _monitorTask; } catch { }
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
    }

    /// <summary>
    /// Check if the navigation server is healthy (responding on its port).
    /// </summary>
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync("127.0.0.1", Port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(1000));

            if (completed == connectTask && client.Connected)
            {
                return true;
            }
        }
        catch
        {
            // Connection failed
        }

        return false;
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
            if (!Directory.Exists(mmapsPath) || Directory.GetFiles(mmapsPath, "*.mmap").Length == 0)
            {
                _logger.LogWarning("[NavigationServerManager] No MMAP files found in {Path}", mmapsPath);
                _logger.LogWarning("[NavigationServerManager] Navigation server may not work correctly without MMAPs");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _serverPath,
                WorkingDirectory = _serverDirectory,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            _process = Process.Start(startInfo);

            if (_process == null)
            {
                _logger.LogError("[NavigationServerManager] Failed to start process");
                Status = NavigationServerStatus.Failed;
                return false;
            }

            _state.NavigationProcess = _process;

            // Wait for server to become healthy
            _logger.LogInformation("[NavigationServerManager] Waiting for server to initialize...");

            for (int i = 0; i < 30; i++) // Wait up to 30 seconds
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await Task.Delay(1000, cancellationToken);

                if (await IsHealthyAsync())
                {
                    Status = NavigationServerStatus.Running;
                    _logger.LogInformation("[NavigationServerManager] Navigation server started successfully (PID: {PID})", _process.Id);

                    // Reset restart attempts on success
                    ResetRestartAttempts();

                    // Start monitoring
                    StartMonitoring();
                    return true;
                }

                // Check if process died
                _process.Refresh();
                if (_process.HasExited)
                {
                    _logger.LogError("[NavigationServerManager] Server process exited with code {Code}", _process.ExitCode);
                    Status = NavigationServerStatus.Failed;
                    return false;
                }
            }

            _logger.LogError("[NavigationServerManager] Server did not become healthy within timeout");
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
                        _logger.LogWarning("[NavigationServerManager] Server process exited unexpectedly");
                        Status = NavigationServerStatus.Stopped;
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
        _process?.Dispose();
    }
}
