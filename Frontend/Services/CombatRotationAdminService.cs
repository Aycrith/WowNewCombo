using Core.CombatRotation;
using Core.FeatureFlags;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Frontend;

/// <summary>
/// Persists combat rotation optimizer settings to <c>runtime_feature_flags.json</c>.
/// The FeatureFlagService hot-reloads that file so changes apply immediately.
/// </summary>
public sealed class CombatRotationAdminService
{
    private static readonly object FileGate = new();

    private readonly ILogger<CombatRotationAdminService> logger;
    private readonly IWebHostEnvironment env;

    public CombatRotationAdminService(
        ILogger<CombatRotationAdminService> logger,
        IWebHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
    }

    public string RuntimeFlagsFilePath =>
        Path.Combine(env.ContentRootPath, "runtime_feature_flags.json");

    public void Save(CombatRotationOptimizerOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        lock (FileGate)
        {
            JsonObject root = LoadOrCreateRootObject(RuntimeFlagsFilePath);
            JsonObject features = root["Features"] as JsonObject ?? new JsonObject();

            var optimizer = new JsonObject
            {
                [nameof(CombatRotationOptimizerOptions.Enabled)] = options.Enabled,
                [nameof(CombatRotationOptimizerOptions.FallbackToStaticPriority)] = options.FallbackToStaticPriority,
                [nameof(CombatRotationOptimizerOptions.BaseWeightMultiplier)] = options.BaseWeightMultiplier,
                [nameof(CombatRotationOptimizerOptions.EnableMetrics)] = options.EnableMetrics,
                [nameof(CombatRotationOptimizerOptions.EnableResourceForecasting)] = options.EnableResourceForecasting,
                [nameof(CombatRotationOptimizerOptions.EnableSwingTimerAlignment)] = options.EnableSwingTimerAlignment,
                [nameof(CombatRotationOptimizerOptions.MetricsFlushIntervalSeconds)] = options.MetricsFlushIntervalSeconds,
                [nameof(CombatRotationOptimizerOptions.MetricsOutputPath)] = options.MetricsOutputPath
            };

            features[nameof(FeatureFlagsOptions.CombatRotationOptimizer)] = optimizer;
            root["Features"] = features;
            root["LastModified"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            string json = root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            });

            WriteFileAtomic(RuntimeFlagsFilePath, json);
        }

        logger.LogInformation(
            "[CombatRotAdmin    ] Saved {File} Enabled={Enabled}",
            Path.GetFileName(RuntimeFlagsFilePath),
            options.Enabled);
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
