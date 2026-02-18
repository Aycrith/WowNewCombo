using System;
using System.Numerics;

namespace Core.GoalsComponent.Blacklist;

/// <summary>
/// Severity level for blacklist entries.
/// </summary>
public enum BlacklistSeverity
{
    /// <summary>Evade, tagged - short duration (2-5 min).</summary>
    Temporary = 1,

    /// <summary>Player death, repeated failures - medium duration (30 min).</summary>
    Medium = 2,

    /// <summary>Config-based, manual - permanent.</summary>
    Permanent = 3
}

/// <summary>
/// A single blacklist entry with metadata for smart blacklisting.
/// </summary>
public sealed class SmartBlacklistEntry
{
    /// <summary>Target GUID (unique identifier).</summary>
    public int TargetGuid { get; init; }

    /// <summary>Target name (for debugging).</summary>
    public string TargetName { get; init; } = string.Empty;

    /// <summary>Severity tier determining TTL.</summary>
    public BlacklistSeverity Severity { get; init; }

    /// <summary>Reason for blacklisting.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Position where blacklisted (for analytics).</summary>
    public Vector3 Position { get; init; }

    /// <summary>Map ID where blacklisted.</summary>
    public int MapId { get; init; }

    /// <summary>When the entry was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the entry expires (null for permanent).</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Number of times this target was blacklisted.</summary>
    public int HitCount { get; set; }

    /// <summary>Last time this entry was accessed/updated.</summary>
    public DateTime LastAccessedAt { get; set; }

    /// <summary>
    /// Creates a new blacklist entry.
    /// </summary>
    public SmartBlacklistEntry(
        int targetGuid,
        string targetName,
        BlacklistSeverity severity,
        string reason,
        Vector3 position,
        int mapId,
        TimeSpan? customTtl = null)
    {
        TargetGuid = targetGuid;
        TargetName = targetName;
        Severity = severity;
        Reason = reason;
        Position = position;
        MapId = mapId;
        CreatedAt = DateTime.UtcNow;
        LastAccessedAt = DateTime.UtcNow;
        HitCount = 1;

        if (severity != BlacklistSeverity.Permanent)
        {
            TimeSpan ttl = customTtl ?? GetDefaultTtl(severity);
            ExpiresAt = CreatedAt.Add(ttl);
        }
    }

    /// <summary>
    /// Checks if this entry has expired.
    /// </summary>
    public bool IsExpired() =>
        ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;

    /// <summary>
    /// Gets the remaining TTL.
    /// </summary>
    public TimeSpan? GetRemainingTtl() =>
        ExpiresAt.HasValue ? ExpiresAt.Value - DateTime.UtcNow : null;

    /// <summary>
    /// Gets default TTL for a severity level.
    /// </summary>
    public static TimeSpan GetDefaultTtl(BlacklistSeverity severity) =>
        severity switch
        {
            BlacklistSeverity.Temporary => TimeSpan.FromMinutes(5),
            BlacklistSeverity.Medium => TimeSpan.FromMinutes(30),
            BlacklistSeverity.Permanent => TimeSpan.MaxValue,
            _ => TimeSpan.FromMinutes(5)
        };
}

/// <summary>
/// Serializable data transfer object for blacklist persistence.
/// </summary>
public sealed class SmartBlacklistDto
{
    public int TargetGuid { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public int Severity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public int MapId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int HitCount { get; set; }
    public DateTime LastAccessedAt { get; set; }
}
