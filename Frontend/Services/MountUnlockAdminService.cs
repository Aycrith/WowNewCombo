using Core;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Frontend;

/// <summary>
/// Persists runtime feature flags to <c>runtime_feature_flags.json</c>.
/// 
/// The host loads that file with <c>reloadOnChange: true</c> so changes apply live
/// without application restarts.
/// </summary>
public sealed class MountUnlockAdminService
{
    private static readonly object FileGate = new();

    private readonly ILogger<MountUnlockAdminService> logger;
    private readonly IWebHostEnvironment env;

    public MountUnlockAdminService(
        ILogger<MountUnlockAdminService> logger,
        IWebHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
    }

    public string RuntimeFlagsFilePath => Path.Combine(env.ContentRootPath, "runtime_feature_flags.json");

    public void Save(MountUnlockOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // Basic guardrails: keep values in a sane range.
        int level = options.TbcMountUnlockLevel;
        if (level <= 0)
        {
            level = 30;
        }
        level = Math.Clamp(level, 1, 80);

        lock (FileGate)
        {
            JsonObject root = LoadOrCreateRootObject(RuntimeFlagsFilePath);

            var mountUnlock = new JsonObject
            {
                [nameof(MountUnlockOptions.EnforceTbcMountLevelRequirement)] = options.EnforceTbcMountLevelRequirement,
                [nameof(MountUnlockOptions.TbcMountUnlockLevel)] = level,
                [nameof(MountUnlockOptions.AutoUnstealthForTravel)] = options.AutoUnstealthForTravel
            };

            root[MountUnlockOptions.Position] = mountUnlock;

            string json = root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            });

            WriteFileAtomic(RuntimeFlagsFilePath, json);
        }

        logger.LogInformation(
            "[MountUnlockAdmin  ] Saved {File} {Flag}={Enabled}, {LevelKey}={Level}, {UnstealthKey}={Unstealth}",
            Path.GetFileName(RuntimeFlagsFilePath),
            nameof(MountUnlockOptions.EnforceTbcMountLevelRequirement), options.EnforceTbcMountLevelRequirement,
            nameof(MountUnlockOptions.TbcMountUnlockLevel), level,
            nameof(MountUnlockOptions.AutoUnstealthForTravel), options.AutoUnstealthForTravel);
    }

    private static JsonObject LoadOrCreateRootObject(string path)
    {
        if (!File.Exists(path))
        {
            return new JsonObject();
        }

        try
        {
            string existing = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(existing))
            {
                return new JsonObject();
            }

            JsonNode? node = JsonNode.Parse(existing);
            return node as JsonObject ?? new JsonObject();
        }
        catch
        {
            // Corrupted/malformed JSON should not crash the server.
            return new JsonObject();
        }
    }

    private static void WriteFileAtomic(string path, string content)
    {
        string dir = Path.GetDirectoryName(path) ?? string.Empty;
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
