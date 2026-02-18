namespace Core;

/// <summary>
/// Runtime-configurable rules for when mounting is allowed.
/// 
/// This is intentionally bound from configuration (e.g. appsettings.json + runtime overrides)
/// so it can be changed live from the Web UI without restarts.
/// </summary>
public sealed class MountUnlockOptions
{
    public const string Position = "MountUnlock";

    /// <summary>
    /// When enabled and the client is TBC Classic, mounting will be blocked until
    /// <see cref="TbcMountUnlockLevel"/>.
    /// </summary>
    public bool EnforceTbcMountLevelRequirement { get; set; } = true;

    /// <summary>
    /// Mount unlock level for TBC Classic.
    /// </summary>
    public int TbcMountUnlockLevel { get; set; } = 30;

    /// <summary>
    /// When enabled, automatically cancel stealth during long-distance travel (40+ yards)
    /// when mounting is not available (pre-mount levels or indoor/swimming locations).
    /// Optimizes travel speed for stealth classes (Rogue, Druid) by moving at normal speed
    /// instead of slow stealth speed. Re-stealth happens via Pull sequence when approaching enemies.
    /// </summary>
    public bool AutoUnstealthForTravel { get; set; } = true;
}
