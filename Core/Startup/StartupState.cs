using System;
using System.Diagnostics;

namespace Core.Startup;

/// <summary>
/// Represents a discovered WoW installation.
/// </summary>
public sealed record WoWInstallation(
    string Path,
    string ExecutablePath,
    string ExecutableName,
    string Version,
    bool HasDataToColorAddon,
    bool HasSecureButtonsXml);

/// <summary>
/// Mutable state shared across the startup orchestration process.
/// Thread-safe for reading current state from UI.
/// </summary>
public sealed class StartupState
{
    private readonly object _lock = new();

    private StartupStage _currentStage = StartupStage.NotStarted;
    private string _statusMessage = "Waiting to start...";
    private WoWInstallation? _wowInstallation;
    private Process? _wowProcess;
    private Process? _navigationProcess;
    private bool _addonsValidated;
    private bool _framesConfigured;
    private bool _isReady;
    private DateTime _startTime;
    private DateTime? _readyTime;

    /// <summary>Current startup stage.</summary>
    public StartupStage CurrentStage
    {
        get { lock (_lock) return _currentStage; }
        set { lock (_lock) _currentStage = value; OnStateChanged(); }
    }

    /// <summary>Human-readable status message.</summary>
    public string StatusMessage
    {
        get { lock (_lock) return _statusMessage; }
        set { lock (_lock) _statusMessage = value; OnStateChanged(); }
    }

    /// <summary>Discovered WoW installation info.</summary>
    public WoWInstallation? WoWInstallation
    {
        get { lock (_lock) return _wowInstallation; }
        set { lock (_lock) _wowInstallation = value; }
    }

    /// <summary>Running WoW process (if detected).</summary>
    public Process? WoWProcess
    {
        get { lock (_lock) return _wowProcess; }
        set { lock (_lock) _wowProcess = value; }
    }

    /// <summary>Running navigation server process.</summary>
    public Process? NavigationProcess
    {
        get { lock (_lock) return _navigationProcess; }
        set { lock (_lock) _navigationProcess = value; }
    }

    /// <summary>Whether addons have been validated/installed.</summary>
    public bool AddonsValidated
    {
        get { lock (_lock) return _addonsValidated; }
        set { lock (_lock) _addonsValidated = value; }
    }

    /// <summary>Whether frames have been configured.</summary>
    public bool FramesConfigured
    {
        get { lock (_lock) return _framesConfigured; }
        set { lock (_lock) _framesConfigured = value; }
    }

    /// <summary>Whether startup is complete and system is ready.</summary>
    public bool IsReady
    {
        get { lock (_lock) return _isReady; }
        set
        {
            lock (_lock)
            {
                _isReady = value;
                if (value && !_readyTime.HasValue)
                    _readyTime = DateTime.UtcNow;
            }
            OnStateChanged();
        }
    }

    /// <summary>When startup began.</summary>
    public DateTime StartTime
    {
        get { lock (_lock) return _startTime; }
        set { lock (_lock) _startTime = value; }
    }

    /// <summary>When startup completed (null if not yet complete).</summary>
    public DateTime? ReadyTime
    {
        get { lock (_lock) return _readyTime; }
    }

    /// <summary>How long startup has been running.</summary>
    public TimeSpan ElapsedTime
    {
        get
        {
            lock (_lock)
            {
                if (_startTime == default) return TimeSpan.Zero;
                var endTime = _readyTime ?? DateTime.UtcNow;
                return endTime - _startTime;
            }
        }
    }

    /// <summary>Whether WoW process is currently running.</summary>
    public bool IsWoWRunning
    {
        get
        {
            lock (_lock)
            {
                if (_wowProcess == null) return false;
                try
                {
                    _wowProcess.Refresh();
                    return !_wowProcess.HasExited;
                }
                catch (InvalidOperationException)
                {
                    // Process handle is invalid or process exited between check and Refresh
                    return false;
                }
            }
        }
    }


    /// <summary>Whether navigation server is currently running.</summary>
    public bool IsNavigationServerRunning
    {
        get
        {
            lock (_lock)
            {
                if (_navigationProcess == null) return false;
                try
                {
                    _navigationProcess.Refresh();
                    return !_navigationProcess.HasExited;
                }
                catch (InvalidOperationException)
                {
                    // Process handle is invalid or process exited between check and Refresh
                    return false;
                }
            }
        }
    }

    /// <summary>Event raised when state changes.</summary>
    public event EventHandler? StateChanged;

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reset state for a fresh startup attempt.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _currentStage = StartupStage.NotStarted;
            _statusMessage = "Waiting to start...";
            _wowInstallation = null;
            _wowProcess = null;
            _navigationProcess = null;
            _addonsValidated = false;
            _framesConfigured = false;
            _isReady = false;
            _startTime = default;
            _readyTime = null;
        }
        OnStateChanged();
    }

    /// <summary>Get a snapshot of current state for display.</summary>
    public StartupStateSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new StartupStateSnapshot(
                CurrentStage: _currentStage,
                StatusMessage: _statusMessage,
                WoWPath: _wowInstallation?.Path,
                IsWoWRunning: IsWoWRunning,
                IsNavigationServerRunning: IsNavigationServerRunning,
                AddonsValidated: _addonsValidated,
                FramesConfigured: _framesConfigured,
                IsReady: _isReady,
                ElapsedTime: ElapsedTime
            );
        }
    }
}

/// <summary>
/// Immutable snapshot of startup state for UI display.
/// </summary>
public sealed record StartupStateSnapshot(
    StartupStage CurrentStage,
    string StatusMessage,
    string? WoWPath,
    bool IsWoWRunning,
    bool IsNavigationServerRunning,
    bool AddonsValidated,
    bool FramesConfigured,
    bool IsReady,
    TimeSpan ElapsedTime);
