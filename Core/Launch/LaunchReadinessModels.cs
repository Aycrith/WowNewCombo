using System;
using System.Collections.Generic;

namespace Core.Launch;

public enum LaunchSubsystem
{
    Navigation = 0,
    WoWProcess = 1,
    Addons = 2,
    Frames = 3,
    AddonHandshake = 4,
    Profile = 5,
    Route = 6,
    KeyBindings = 7,
    ActionBar = 8
}

public enum LaunchStatus
{
    Unknown = 0,
    Pending = 1,
    Ok = 2,
    Warning = 3,
    Error = 4,
    Skipped = 5
}

public sealed record LaunchSubsystemCheck(
    LaunchSubsystem Subsystem,
    LaunchStatus Status,
    string Title,
    string Message,
    bool IsRequired,
    bool IsBlocking,
    DateTimeOffset TimestampUtc,
    string? FixHint = null,
    string? NavigateTo = null,
    bool IsOverridden = false);

public sealed record LaunchSubsystemBypass(
    bool Enabled,
    string Reason,
    DateTimeOffset TimestampUtc,
    string Source);

public sealed record LaunchOverrideSnapshot(
    bool AllowStartWithWarnings,
    bool EmergencyBypassAll,
    IReadOnlyDictionary<LaunchSubsystem, LaunchSubsystemBypass> Bypasses);

public sealed record LaunchReadinessSnapshot(
    bool IsLaunchReady,
    bool CanStartBot,
    DateTimeOffset TimestampUtc,
    IReadOnlyList<LaunchSubsystemCheck> Checks,
    LaunchOverrideSnapshot Overrides);
