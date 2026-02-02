namespace Core.Startup;

/// <summary>
/// Configuration options for the startup orchestrator.
/// Can be bound from appsettings.json "Startup" section.
/// </summary>
public sealed class StartupOptions
{
    /// <summary>
    /// Whether to automatically launch WoW if not running.
    /// </summary>
    public bool AutoLaunchWoW { get; set; } = true;

    /// <summary>
    /// Whether to automatically start the navigation server.
    /// </summary>
    public bool AutoStartNavigationServer { get; set; } = true;

    /// <summary>
    /// Whether to automatically configure frames when character enters world.
    /// </summary>
    public bool AutoConfigureFrames { get; set; } = true;

    /// <summary>
    /// Whether to automatically open the browser to the WebUI.
    /// </summary>
    public bool AutoOpenBrowser { get; set; } = true;

    /// <summary>
    /// Whether to enable health monitoring and auto-restart of crashed services.
    /// </summary>
    public bool EnableHealthMonitoring { get; set; } = true;

    /// <summary>
    /// Interval in seconds between health checks.
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum time to wait for WoW to start (seconds).
    /// </summary>
    public int WoWLaunchTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum time to wait for character to enter world (seconds).
    /// Set to -1 to wait indefinitely.
    /// </summary>
    public int WaitForCharacterTimeoutSeconds { get; set; } = -1;

    /// <summary>
    /// Maximum number of retries for frame configuration.
    /// </summary>
    public int FrameConfigMaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between frame config retries (seconds).
    /// </summary>
    public int FrameConfigRetryDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Path to the navigation server executable.
    /// If empty, will look in default location (Navigation/AmeisenNavigationServer.exe).
    /// </summary>
    public string NavigationServerPath { get; set; } = string.Empty;

    /// <summary>
    /// Port for the navigation server.
    /// </summary>
    public int NavigationServerPort { get; set; } = 47111;

    /// <summary>
    /// Explicit WoW installation path. If empty, will auto-detect.
    /// </summary>
    public string WoWPath { get; set; } = string.Empty;

    /// <summary>
    /// Name of the WoW executable to launch.
    /// </summary>
    public string WoWExecutableName { get; set; } = "WowClassic.exe";

    /// <summary>
    /// Web UI port for auto-opening browser.
    /// </summary>
    public int WebUIPort { get; set; } = 5000;

    /// <summary>
    /// Skip startup orchestration entirely (for debugging).
    /// </summary>
    public bool SkipStartupOrchestration { get; set; } = false;
}
