using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Core.FeatureFlags;

using Microsoft.Extensions.Logging;

namespace Core.GoalsComponent.Blacklist;

/// <summary>
/// Smart blacklist service with TTL tiers and disk persistence.
/// Extends the existing IBlacklist interface with temporal awareness.
/// </summary>
public sealed class SmartBlacklist : IBlacklist, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<SmartBlacklist> logger;
    private readonly SmartBlacklistOptions options;
    private readonly ReaderWriterLockSlim rwLock = new();

    // Primary storage: Guid -> Entry
    private readonly Dictionary<int, SmartBlacklistEntry> entries = new();

    private readonly string persistencePath;
    private Timer? autoSaveTimer;
    private long totalHits;
    private long totalSaves;

    /// <summary>
    /// Total number of blacklist hits since startup.
    /// </summary>
    public long TotalHits => Interlocked.Read(ref totalHits);

    /// <summary>
    /// Total number of disk saves.
    /// </summary>
    public long TotalSaves => Interlocked.Read(ref totalSaves);

    /// <summary>
    /// Current number of active entries.
    /// </summary>
    public int Count
    {
        get
        {
            rwLock.EnterReadLock();
            try
            {
                return entries.Count;
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }
    }

    public SmartBlacklist(
        ILogger<SmartBlacklist> logger,
        SmartBlacklistOptions options,
        string? customPersistencePath = null)
    {
        this.logger = logger;
        this.options = options;

        // Set persistence path (use custom if provided, otherwise default)
        if (!string.IsNullOrEmpty(customPersistencePath))
        {
            persistencePath = customPersistencePath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string botDir = Path.Combine(appData, "WowClassicGrindBot");
            Directory.CreateDirectory(botDir);
            persistencePath = Path.Combine(botDir, "smart_blacklist.json");
        }

        // Load existing data
        LoadFromDisk();

        // Start auto-save timer if enabled
        if (options.AutoSaveIntervalMinutes > 0)
        {
            int intervalMs = options.AutoSaveIntervalMinutes * 60 * 1000;
            autoSaveTimer = new Timer(_ => SaveToDisk(), null, intervalMs, intervalMs);
            logger.LogInformation("[SmartBlacklist] Auto-save enabled every {Minutes} minutes", options.AutoSaveIntervalMinutes);
        }

        logger.LogInformation("[SmartBlacklist] Initialized with {Count} entries from disk", Count);
    }

    /// <summary>
    /// Legacy IBlacklist interface implementation - checks current target.
    /// </summary>
    public bool Is()
    {
        // This is a compatibility method - the actual check should use Is(int targetGuid)
        // Returning false here since we don't have context about "current" target
        return false;
    }

    /// <summary>
    /// Checks if a specific target GUID is blacklisted.
    /// </summary>
    public bool Is(int targetGuid)
    {
        if (targetGuid == 0) return false;

        rwLock.EnterReadLock();
        try
        {
            if (!entries.TryGetValue(targetGuid, out SmartBlacklistEntry? entry))
            {
                return false;
            }

            // Check expiration
            if (entry.IsExpired())
            {
                // Expired - will be removed on next maintenance cycle
                return false;
            }

            // Update hit stats
            entry.HitCount++;
            entry.LastAccessedAt = DateTime.UtcNow;
            Interlocked.Increment(ref totalHits);

            if (options.LogBlacklistHits)
            {
                logger.LogDebug("[SmartBlacklist] Hit: {Name} (GUID: {Guid}, Severity: {Severity}, Reason: {Reason})",
                    entry.TargetName, targetGuid, entry.Severity, entry.Reason);
            }

            return true;
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Adds a target to the blacklist.
    /// </summary>
    public void Add(
        int targetGuid,
        string targetName,
        BlacklistSeverity severity,
        string reason,
        Vector3 position,
        int mapId,
        TimeSpan? customTtl = null)
    {
        if (targetGuid == 0) return;

        rwLock.EnterWriteLock();
        try
        {
            // Check if already exists
            if (entries.TryGetValue(targetGuid, out SmartBlacklistEntry? existing))
            {
                // Update existing entry
                existing.HitCount++;
                existing.LastAccessedAt = DateTime.UtcNow;

                // Upgrade severity if new one is higher
                if (severity > existing.Severity)
                {
                    // Remove old entry and create new with higher severity
                    entries.Remove(targetGuid);
                }
                else
                {
                    logger.LogDebug("[SmartBlacklist] Already exists: {Name} (updating hit count)", targetName);
                    return;
                }
            }

            // Create new entry
            var entry = new SmartBlacklistEntry(
                targetGuid, targetName, severity, reason, position, mapId, customTtl);

            entries[targetGuid] = entry;

            logger.LogInformation(
                "[SmartBlacklist] Added: {Name} (GUID: {Guid}, Severity: {Severity}, Reason: {Reason}, TTL: {Ttl})",
                targetName, targetGuid, severity, reason,
                entry.GetRemainingTtl()?.ToString("mm\\:ss") ?? "Permanent");

            // Prune if over capacity
            PruneIfNeeded();

            // Auto-save if enabled (synchronous for thread safety)
            if (options.AutoSaveOnChange)
            {
                SaveToDisk();
            }
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes a target from the blacklist.
    /// </summary>
    public bool Remove(int targetGuid)
    {
        rwLock.EnterWriteLock();
        try
        {
            bool removed = entries.Remove(targetGuid);
            if (removed)
            {
                logger.LogDebug("[SmartBlacklist] Removed: GUID {Guid}", targetGuid);
            }
            return removed;
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets all active entries (optionally filtered by severity).
    /// </summary>
    public List<SmartBlacklistEntry> GetEntries(BlacklistSeverity? minSeverity = null)
    {
        rwLock.EnterReadLock();
        try
        {
            var query = entries.Values.Where(e => !e.IsExpired());

            if (minSeverity.HasValue)
            {
                query = query.Where(e => e.Severity >= minSeverity.Value);
            }

            return query.OrderByDescending(e => e.LastAccessedAt).ToList();
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Removes all expired entries.
    /// </summary>
    public int PruneExpired()
    {
        rwLock.EnterWriteLock();
        try
        {
            var expired = entries.Where(e => e.Value.IsExpired()).Select(e => e.Key).ToList();
            foreach (int guid in expired)
            {
                entries.Remove(guid);
            }

            if (expired.Count > 0)
            {
                logger.LogInformation("[SmartBlacklist] Pruned {Count} expired entries", expired.Count);
            }

            return expired.Count;
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears all entries.
    /// </summary>
    public void Clear()
    {
        rwLock.EnterWriteLock();
        try
        {
            int count = entries.Count;
            entries.Clear();
            logger.LogInformation("[SmartBlacklist] Cleared all {Count} entries", count);
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Saves blacklist to disk.
    /// </summary>
    public void SaveToDisk()
    {
        try
        {
            // Prune expired first
            PruneExpired();

            rwLock.EnterReadLock();
            try
            {
                // Convert to DTOs
                var dtos = entries.Values.Select(e => new SmartBlacklistDto
                {
                    TargetGuid = e.TargetGuid,
                    TargetName = e.TargetName,
                    Severity = (int)e.Severity,
                    Reason = e.Reason,
                    PositionX = e.Position.X,
                    PositionY = e.Position.Y,
                    PositionZ = e.Position.Z,
                    MapId = e.MapId,
                    CreatedAt = e.CreatedAt,
                    ExpiresAt = e.ExpiresAt,
                    HitCount = e.HitCount,
                    LastAccessedAt = e.LastAccessedAt
                }).ToList();

                // Serialize
                string json = JsonSerializer.Serialize(dtos, JsonOptions);

                // Atomic write
                string tempPath = persistencePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, persistencePath, overwrite: true);

                Interlocked.Increment(ref totalSaves);
                logger.LogDebug("[SmartBlacklist] Saved {Count} entries to disk", dtos.Count);
            }
            finally
            {
                rwLock.ExitReadLock();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SmartBlacklist] Failed to save to disk");
        }
    }

    /// <summary>
    /// Loads blacklist from disk.
    /// </summary>
    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(persistencePath))
            {
                logger.LogInformation("[SmartBlacklist] No persistence file found, starting fresh");
                return;
            }

            string json = File.ReadAllText(persistencePath);
            var dtos = JsonSerializer.Deserialize<List<SmartBlacklistDto>>(json, JsonOptions);

            if (dtos == null)
            {
                logger.LogWarning("[SmartBlacklist] Failed to deserialize persistence file");
                return;
            }

            rwLock.EnterWriteLock();
            try
            {
                entries.Clear();

                foreach (var dto in dtos)
                {
                    // Skip expired entries on load
                    if (dto.ExpiresAt.HasValue && DateTime.UtcNow >= dto.ExpiresAt.Value)
                    {
                        continue;
                    }

                    var entry = new SmartBlacklistEntry(
                        dto.TargetGuid,
                        dto.TargetName,
                        (BlacklistSeverity)dto.Severity,
                        dto.Reason,
                        new Vector3(dto.PositionX, dto.PositionY, dto.PositionZ),
                        dto.MapId)
                    {
                        HitCount = dto.HitCount,
                        LastAccessedAt = dto.LastAccessedAt
                    };

                    entries[dto.TargetGuid] = entry;
                }

                logger.LogInformation("[SmartBlacklist] Loaded {Count} entries from disk", entries.Count);
            }
            finally
            {
                rwLock.ExitWriteLock();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[SmartBlacklist] Failed to load from disk");
        }
    }

    private void PruneIfNeeded()
    {
        if (entries.Count <= options.MaxEntries) return;

        // Remove oldest entries (by LastAccessedAt)
        var toRemove = entries
            .OrderBy(e => e.Value.LastAccessedAt)
            .Take(entries.Count - options.MaxEntries)
            .Select(e => e.Key)
            .ToList();

        foreach (int guid in toRemove)
        {
            entries.Remove(guid);
        }

        logger.LogInformation("[SmartBlacklist] Pruned {Count} old entries to stay under limit", toRemove.Count);
    }

    public void Dispose()
    {
        autoSaveTimer?.Dispose();
        SaveToDisk();
        rwLock.Dispose();
    }
}
