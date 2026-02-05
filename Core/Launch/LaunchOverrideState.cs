using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Core.Launch;

/// <summary>
/// In-memory overrides for advanced users (not persisted as active state).
/// These overrides affect launch readiness gating and bot start permissions.
/// </summary>
public sealed class LaunchOverrideState
{
    private const int MaxAuditEntries = 200;

    private readonly object gate = new();
    private readonly ILogger<LaunchOverrideState> logger;
    private readonly string auditFilePath;

    private readonly Dictionary<LaunchSubsystem, LaunchSubsystemBypass> bypasses = new();
    private readonly List<LaunchOverrideAuditEntry> audit = new();

    public event Action? Changed;

    public bool AllowStartWithWarnings { get; private set; }

    public bool EmergencyBypassAll { get; private set; }

    public LaunchOverrideState(ILogger<LaunchOverrideState> logger)
    {
        this.logger = logger;

        string logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDir);
        auditFilePath = Path.Combine(logsDir, "launch-override-audit.jsonl");
    }

    public LaunchOverrideSnapshot Snapshot()
    {
        lock (gate)
        {
            return new LaunchOverrideSnapshot(
                AllowStartWithWarnings,
                EmergencyBypassAll,
                new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>(bypasses));
        }
    }

    public IReadOnlyList<LaunchOverrideAuditEntry> GetAudit()
    {
        lock (gate)
        {
            return audit.ToArray();
        }
    }

    public bool IsBypassed(LaunchSubsystem subsystem)
    {
        lock (gate)
        {
            return EmergencyBypassAll || (bypasses.TryGetValue(subsystem, out LaunchSubsystemBypass? bypass) && bypass != null && bypass.Enabled);
        }
    }

    public bool TryGetBypass(LaunchSubsystem subsystem, out LaunchSubsystemBypass bypass)
    {
        lock (gate)
        {
            return bypasses.TryGetValue(subsystem, out bypass!);
        }
    }

    public void Reset(string reason = "Reset", string source = "System")
    {
        bool changed;
        lock (gate)
        {
            changed = AllowStartWithWarnings || EmergencyBypassAll || bypasses.Count > 0;
            AllowStartWithWarnings = false;
            EmergencyBypassAll = false;
            bypasses.Clear();
            AppendAuditLocked(new LaunchOverrideAuditEntry(DateTimeOffset.UtcNow, null, "Reset", false, reason, source));
        }

        if (changed)
        {
            logger.LogWarning("[LaunchOverride     ] Reset all overrides (reason={Reason}, source={Source})", reason, source);
            Changed?.Invoke();
        }
    }

    public void SetAllowStartWithWarnings(bool value, string reason = "Manual", string source = "Wizard")
    {
        bool changed;
        lock (gate)
        {
            changed = AllowStartWithWarnings != value;
            if (changed)
            {
                AllowStartWithWarnings = value;
                AppendAuditLocked(new LaunchOverrideAuditEntry(DateTimeOffset.UtcNow, null, "AllowStartWithWarnings", value, reason, source));
            }
        }

        if (changed)
        {
            logger.LogWarning("[LaunchOverride     ] AllowStartWithWarnings={Value} (reason={Reason}, source={Source})", value, reason, source);
            Changed?.Invoke();
        }
    }

    public void SetEmergencyBypassAll(bool value, string reason = "Manual", string source = "Wizard")
    {
        bool changed;
        lock (gate)
        {
            changed = EmergencyBypassAll != value;
            if (changed)
            {
                EmergencyBypassAll = value;
                AppendAuditLocked(new LaunchOverrideAuditEntry(DateTimeOffset.UtcNow, null, "EmergencyBypassAll", value, reason, source));
            }
        }

        if (changed)
        {
            logger.LogError("[LaunchOverride     ] EmergencyBypassAll={Value} (reason={Reason}, source={Source})", value, reason, source);
            Changed?.Invoke();
        }
    }

    public void SetBypass(LaunchSubsystem subsystem, bool value, string reason = "Manual", string source = "Wizard")
    {
        bool changed;
        lock (gate)
        {
            bypasses.TryGetValue(subsystem, out LaunchSubsystemBypass? existing);

            if (!value)
            {
                changed = bypasses.Remove(subsystem);
            }
            else
            {
                LaunchSubsystemBypass bypass = new(
                    Enabled: true,
                    Reason: string.IsNullOrWhiteSpace(reason) ? "Manual override" : reason.Trim(),
                    TimestampUtc: DateTimeOffset.UtcNow,
                    Source: string.IsNullOrWhiteSpace(source) ? "Unknown" : source.Trim());

                bypasses[subsystem] = bypass;
                changed = existing == null || !Equals(existing, bypass);
            }

            if (changed)
            {
                AppendAuditLocked(new LaunchOverrideAuditEntry(DateTimeOffset.UtcNow, subsystem, "Bypass", value, reason, source));
            }
        }

        if (changed)
        {
            logger.LogWarning("[LaunchOverride     ] Bypass {Subsystem}={Value} (reason={Reason}, source={Source})", subsystem, value, reason, source);
            Changed?.Invoke();
        }
    }

    private void AppendAuditLocked(LaunchOverrideAuditEntry entry)
    {
        audit.Add(entry);
        if (audit.Count > MaxAuditEntries)
        {
            audit.RemoveAt(0);
        }

        TryAppendAuditFileLocked(entry);
    }

    private void TryAppendAuditFileLocked(LaunchOverrideAuditEntry entry)
    {
        try
        {
            string json = JsonSerializer.Serialize(entry);
            File.AppendAllText(auditFilePath, json + "\n");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[LaunchOverride     ] Failed to write audit file");
        }
    }
}

public sealed record LaunchOverrideAuditEntry(
    DateTimeOffset TimestampUtc,
    LaunchSubsystem? Subsystem,
    string Action,
    bool Enabled,
    string Reason,
    string Source);
