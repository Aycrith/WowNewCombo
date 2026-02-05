using Core.FeatureFlags;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Frontend;

/// <summary>
/// Persists humanization settings to <c>runtime_feature_flags.json</c>.
/// The FeatureFlagService hot-reloads that file so changes apply immediately.
/// </summary>
public sealed class HumanizationAdminService
{
    private static readonly object FileGate = new();

    private readonly ILogger<HumanizationAdminService> logger;
    private readonly IWebHostEnvironment env;

    public HumanizationAdminService(
        ILogger<HumanizationAdminService> logger,
        IWebHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
    }

    public string RuntimeFlagsFilePath => Path.Combine(env.ContentRootPath, "runtime_feature_flags.json");

    public void Save(HumanizationOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        lock (FileGate)
        {
            JsonObject root = LoadOrCreateRootObject(RuntimeFlagsFilePath);
            JsonObject features = root["Features"] as JsonObject ?? new JsonObject();

            var inputTiming = new JsonObject
            {
                [nameof(HumanizationInputTimingOptions.KeyHoldMeanMs)] = options.InputTiming.KeyHoldMeanMs,
                [nameof(HumanizationInputTimingOptions.KeyHoldStdDevMs)] = options.InputTiming.KeyHoldStdDevMs,
                [nameof(HumanizationInputTimingOptions.KeyHoldMinMs)] = options.InputTiming.KeyHoldMinMs,
                [nameof(HumanizationInputTimingOptions.KeyHoldMaxMs)] = options.InputTiming.KeyHoldMaxMs,
                [nameof(HumanizationInputTimingOptions.ReactionMaxMs)] = options.InputTiming.ReactionMaxMs
            };

            var mouse = new JsonObject
            {
                [nameof(HumanizationMouseMovementOptions.Enabled)] = options.MouseMovement.Enabled,
                [nameof(HumanizationMouseMovementOptions.StepsPerMovement)] = options.MouseMovement.StepsPerMovement,
                [nameof(HumanizationMouseMovementOptions.CurveIntensity)] = options.MouseMovement.CurveIntensity,
                [nameof(HumanizationMouseMovementOptions.MicroJitterPixels)] = options.MouseMovement.MicroJitterPixels,
                [nameof(HumanizationMouseMovementOptions.OvershootProbability)] = options.MouseMovement.OvershootProbability,
                [nameof(HumanizationMouseMovementOptions.StepDelayMinMs)] = options.MouseMovement.StepDelayMinMs,
                [nameof(HumanizationMouseMovementOptions.StepDelayMaxMs)] = options.MouseMovement.StepDelayMaxMs
            };

            var fatigue = new JsonObject
            {
                [nameof(HumanizationFatigueOptions.Enabled)] = options.Fatigue.Enabled,
                [nameof(HumanizationFatigueOptions.BreakIntervalMinutes)] = options.Fatigue.BreakIntervalMinutes,
                [nameof(HumanizationFatigueOptions.BreakDurationMinMinutes)] = options.Fatigue.BreakDurationMinMinutes,
                [nameof(HumanizationFatigueOptions.BreakDurationMaxMinutes)] = options.Fatigue.BreakDurationMaxMinutes,
                [nameof(HumanizationFatigueOptions.FatigueRatePerHour)] = options.Fatigue.FatigueRatePerHour,
                [nameof(HumanizationFatigueOptions.MaxFatigueMultiplier)] = options.Fatigue.MaxFatigueMultiplier
            };

            var behavior = new JsonObject
            {
                [nameof(HumanizationBehaviorOptions.MicroPauseEnabled)] = options.Behavior.MicroPauseEnabled,
                [nameof(HumanizationBehaviorOptions.MicroPauseIntervalSeconds)] = options.Behavior.MicroPauseIntervalSeconds,
                [nameof(HumanizationBehaviorOptions.MicroPauseMinMs)] = options.Behavior.MicroPauseMinMs,
                [nameof(HumanizationBehaviorOptions.MicroPauseMaxMs)] = options.Behavior.MicroPauseMaxMs
            };

            var humanization = new JsonObject
            {
                [nameof(HumanizationOptions.Enabled)] = options.Enabled,
                [nameof(HumanizationOptions.InputTiming)] = inputTiming,
                [nameof(HumanizationOptions.MouseMovement)] = mouse,
                [nameof(HumanizationOptions.Fatigue)] = fatigue,
                [nameof(HumanizationOptions.Behavior)] = behavior
            };

            features[nameof(FeatureFlagsOptions.Humanization)] = humanization;
            root["Features"] = features;
            root["LastModified"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            string json = root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            });

            WriteFileAtomic(RuntimeFlagsFilePath, json);
        }

        logger.LogInformation(
            "[HumanizationAdmin ] Saved {File} Enabled={Enabled}",
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

