using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Core.FeatureFlags;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frontend.Controllers;

/// <summary>
/// API controller for runtime feature flag management.
/// Allows external agents to query and toggle features via HTTP.
/// </summary>
[ApiController]
[Route("api/features")]
public sealed class FeatureFlagController : ControllerBase
{
    private readonly FeatureFlagService featureFlagService;
    private readonly ILogger<FeatureFlagController> logger;
    private readonly string flagsFilePath;

    public FeatureFlagController(
        FeatureFlagService featureFlagService,
        ILogger<FeatureFlagController> logger)
    {
        this.featureFlagService = featureFlagService;
        this.logger = logger;

        // Assumes BlazorServer context — adjust path if needed
        flagsFilePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "runtime_feature_flags.json");
    }

    /// <summary>
    /// GET /api/features — Returns all current feature flags.
    /// </summary>
    [HttpGet]
    public IActionResult GetAll()
    {
        FeatureFlagsOptions flags = featureFlagService.Current;
        return Ok(new
        {
            GlobalKillSwitch = flags.GlobalKillSwitch,
            DebugMode = flags.DebugMode,
            EnabledFeatures = flags.GetEnabledFeatures().ToArray(),
            Features = new
            {
                flags.ObjectPooling,
                flags.CircuitBreaker,
                flags.PathSmoothing,
                flags.StuckRecoveryV2,
                flags.HazardAvoidance,
                flags.Humanization,
                flags.AIProfileGenerator,
                flags.ProfileMarketplace,
                flags.BehaviorTreeCombat,
                flags.HybridLLMDecision,
                flags.InputSecurity,
                flags.CombatRotationOptimizer
            }
        });
    }

    /// <summary>
    /// GET /api/features/{name} — Returns a specific feature's enabled state.
    /// </summary>
    [HttpGet("{name}")]
    public IActionResult Get(string name)
    {
        FeatureFlagsOptions flags = featureFlagService.Current;
        bool isEnabled = flags.IsEnabled(name);

        return Ok(new { Name = name, Enabled = isEnabled });
    }

    /// <summary>
    /// PUT /api/features/{name} — Toggles a specific feature.
    /// Body: { "enabled": true/false }
    /// </summary>
    [HttpPut("{name}")]
    public async Task<IActionResult> Toggle(string name, [FromBody] ToggleRequest request)
    {
        if (!System.IO.File.Exists(flagsFilePath))
        {
            return NotFound(new { Error = "runtime_feature_flags.json not found" });
        }

        try
        {
            // Read current flags
            string json = await System.IO.File.ReadAllTextAsync(flagsFilePath);

            // Build a mutable dictionary tree
            Dictionary<string, object> mutableRoot = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                ?? throw new InvalidOperationException("Failed to deserialize feature flags");

            // Navigate to Features section
            if (!mutableRoot.TryGetValue("Features", out object? featuresObj) ||
                featuresObj is not JsonElement featuresElement)
            {
                return BadRequest(new { Error = "Features section not found" });
            }

            Dictionary<string, object> features = JsonSerializer.Deserialize<Dictionary<string, object>>(
                featuresElement.ToString() ?? "{}") ?? new();

            // Find and toggle the feature
            if (!features.TryGetValue(name, out object? featureObj) ||
                featureObj is not JsonElement featureElement)
            {
                return NotFound(new { Error = $"Feature '{name}' not found" });
            }

            Dictionary<string, object> feature = JsonSerializer.Deserialize<Dictionary<string, object>>(
                featureElement.ToString() ?? "{}") ?? new();

            feature["Enabled"] = request.Enabled;
            features[name] = feature;
            mutableRoot["Features"] = features;

            // Write back with formatting
            string updatedJson = JsonSerializer.Serialize(mutableRoot, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await System.IO.File.WriteAllTextAsync(flagsFilePath, updatedJson);

            logger.LogInformation(
                "[FeatureFlagCtrl  ] Feature '{Name}' set to {State} via API",
                name, request.Enabled);

            return Ok(new { Name = name, Enabled = request.Enabled });
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[FeatureFlagCtrl  ] Failed to parse runtime_feature_flags.json");
            return StatusCode(500, new { Error = "Invalid JSON in feature flags file" });
        }
    }

    /// <summary>
    /// POST /api/features/killswitch — Emergency stop toggle.
    /// Body: { "active": true/false }
    /// </summary>
    [HttpPost("killswitch")]
    public async Task<IActionResult> KillSwitch([FromBody] KillSwitchRequest request)
    {
        if (!System.IO.File.Exists(flagsFilePath))
        {
            return NotFound(new { Error = "runtime_feature_flags.json not found" });
        }

        try
        {
            string json = await System.IO.File.ReadAllTextAsync(flagsFilePath);
            Dictionary<string, object> root = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                ?? throw new InvalidOperationException("Failed to deserialize feature flags");

            root["GlobalKillSwitch"] = request.Active;

            string updatedJson = JsonSerializer.Serialize(root, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await System.IO.File.WriteAllTextAsync(flagsFilePath, updatedJson);

            logger.LogWarning(
                "[FeatureFlagCtrl  ] GlobalKillSwitch set to {State} via API",
                request.Active);

            return Ok(new { GlobalKillSwitch = request.Active });
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[FeatureFlagCtrl  ] Failed to parse runtime_feature_flags.json");
            return StatusCode(500, new { Error = "Invalid JSON in feature flags file" });
        }
    }
}

public record ToggleRequest(bool Enabled);
public record KillSwitchRequest(bool Active);
