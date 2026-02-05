namespace Core.Launch;

/// <summary>
/// Options for launch readiness checks and wizard behavior.
/// Bind from configuration section "Launch".
/// </summary>
public sealed class LaunchOptions
{
    /// <summary>
    /// Maximum time (ms) allowed for a full readiness evaluation. Any subsystem check that does not
    /// complete within this window will be marked as timed out so status endpoints never hang.
    /// Set to 0 or less to disable time-limited evaluation (not recommended).
    /// </summary>
    public int EvaluateTimeoutMs { get; set; } = 1500;

    /// <summary>
    /// Maximum age (ms) of addon GlobalTime before we consider addon data "stale".
    /// </summary>
    public int AddonHandshakeMaxStalenessMs { get; set; } = 2500;

    /// <summary>
    /// Timeout (seconds) for explicit addon handshake waits.
    /// </summary>
    public int AddonHandshakeTimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// Timeout (seconds) for explicit navigation/pathing handshake waits.
    /// </summary>
    public int NavigationHandshakeTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Timeout (seconds) for waiting on WoW + add-on validation.
    /// </summary>
    public int WoWAndAddonsTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Timeout (seconds) for waiting on a valid frame configuration.
    /// </summary>
    public int FramesTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Timeout (seconds) for waiting on a loaded class profile.
    /// </summary>
    public int ProfileTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout (seconds) for waiting on a loaded/validated route.
    /// </summary>
    public int RouteTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout (seconds) for waiting on keybinds + action bar synchronization.
    /// </summary>
    public int KeybindsTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Log a warning when a readiness check takes longer than this.
    /// </summary>
    public int SlowCheckThresholdMs { get; set; } = 250;
}
