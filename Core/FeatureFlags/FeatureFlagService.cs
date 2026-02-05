using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.FeatureFlags;

/// <summary>
/// Service that monitors feature flag configuration for hot-reload changes
/// and provides thread-safe access to current feature flag state.
/// </summary>
public sealed class FeatureFlagService : IHostedService, IDisposable
{
    private readonly ILogger<FeatureFlagService> logger;
    private readonly IOptionsMonitor<FeatureFlagsOptions> optionsMonitor;
    private readonly string configFilePath;

    private FileSystemWatcher? fileWatcher;
    private IDisposable? optionsChangeToken;
    private FeatureFlagsOptions currentOptions;
    private readonly object syncLock = new();

    /// <summary>
    /// Event raised when any feature flag value changes.
    /// </summary>
    public event Action<FeatureFlagsOptions>? OnFlagsChanged;

    /// <summary>
    /// Gets the current feature flag configuration (thread-safe).
    /// </summary>
    public FeatureFlagsOptions Current
    {
        get
        {
            lock (syncLock)
            {
                return currentOptions;
            }
        }
    }

    // Convenience properties for common feature checks
    public bool IsObjectPoolingEnabled => Current.ObjectPooling.Enabled;
    public bool IsCircuitBreakerEnabled => Current.CircuitBreaker.Enabled;
    public bool IsPathSmoothingEnabled => Current.PathSmoothing.Enabled;
    public bool IsEnhancedStuckRecoveryEnabled => Current.StuckRecoveryV2.Enabled;
    public bool IsHazardAvoidanceEnabled => Current.HazardAvoidance.Enabled;
    public bool IsAIProfileGeneratorEnabled => Current.AIProfileGenerator.Enabled;
    public bool IsProfileMarketplaceEnabled => Current.ProfileMarketplace.Enabled;
    public bool IsBehaviorTreeCombatEnabled => Current.BehaviorTreeCombat.Enabled;
    public bool IsHybridLLMDecisionEnabled => Current.HybridLLMDecision.Enabled;

    public FeatureFlagService(
        ILogger<FeatureFlagService> logger,
        IOptionsMonitor<FeatureFlagsOptions> optionsMonitor,
        IOptions<FeatureFlagServiceOptions> serviceOptions)
    {
        this.logger = logger;
        this.optionsMonitor = optionsMonitor;
        configFilePath = serviceOptions.Value.ConfigFilePath ?? "runtime_feature_flags.json";
        currentOptions = optionsMonitor.CurrentValue;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Subscribe to options changes from IOptionsMonitor
        optionsChangeToken = optionsMonitor.OnChange(OnOptionsChanged);

        // Set up file watcher for hot-reload
        SetupFileWatcher();

        logger.LogInformation(
            "[FeatureFlagSvc  ] Started. ObjectPooling={ObjectPooling}, CircuitBreaker={CircuitBreaker}, PathSmoothing={PathSmoothing}",
            IsObjectPoolingEnabled,
            IsCircuitBreakerEnabled,
            IsPathSmoothingEnabled);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("[FeatureFlagSvc  ] Stopping feature flag service");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        optionsChangeToken?.Dispose();
        fileWatcher?.Dispose();
    }

    private void SetupFileWatcher()
    {
        try
        {
            string? directory = Path.GetDirectoryName(Path.GetFullPath(configFilePath));
            string fileName = Path.GetFileName(configFilePath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                logger.LogWarning(
                    "[FeatureFlagSvc  ] Config directory not found: {Directory}. Hot-reload disabled.",
                    directory);
                return;
            }

            fileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            fileWatcher.Changed += OnFileChanged;
            fileWatcher.Error += OnFileWatcherError;

            logger.LogDebug(
                "[FeatureFlagSvc  ] File watcher set up for {FilePath}",
                configFilePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FeatureFlagSvc  ] Failed to set up file watcher");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce rapid file change notifications
        Task.Delay(100).ContinueWith(_ =>
        {
            try
            {
                logger.LogInformation(
                    "[FeatureFlagSvc  ] Config file changed: {FilePath}. Reloading...",
                    e.FullPath);

                // The IOptionsMonitor should automatically pick up file changes
                // if configured with reloadOnChange. This event is for logging/notification.
                var newOptions = optionsMonitor.CurrentValue;
                UpdateOptions(newOptions);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[FeatureFlagSvc  ] Failed to reload config");
            }
        });
    }

    private void OnFileWatcherError(object sender, ErrorEventArgs e)
    {
        logger.LogError(e.GetException(), "[FeatureFlagSvc  ] File watcher error");
    }

    private void OnOptionsChanged(FeatureFlagsOptions newOptions, string? name)
    {
        UpdateOptions(newOptions);
    }

    private void UpdateOptions(FeatureFlagsOptions newOptions)
    {
        FeatureFlagsOptions previous;
        lock (syncLock)
        {
            previous = currentOptions;
            currentOptions = newOptions;
        }

        // Log changes
        LogFeatureFlagChanges(previous, newOptions);

        // Notify subscribers
        OnFlagsChanged?.Invoke(newOptions);
    }

    private void LogFeatureFlagChanges(FeatureFlagsOptions previous, FeatureFlagsOptions current)
    {
        var changes = new List<string>();

        if (previous.ObjectPooling.Enabled != current.ObjectPooling.Enabled)
            changes.Add($"ObjectPooling: {previous.ObjectPooling.Enabled} -> {current.ObjectPooling.Enabled}");

        if (previous.CircuitBreaker.Enabled != current.CircuitBreaker.Enabled)
            changes.Add($"CircuitBreaker: {previous.CircuitBreaker.Enabled} -> {current.CircuitBreaker.Enabled}");

        if (previous.PathSmoothing.Enabled != current.PathSmoothing.Enabled)
            changes.Add($"PathSmoothing: {previous.PathSmoothing.Enabled} -> {current.PathSmoothing.Enabled}");

        if (previous.StuckRecoveryV2.Enabled != current.StuckRecoveryV2.Enabled)
            changes.Add($"StuckRecoveryV2: {previous.StuckRecoveryV2.Enabled} -> {current.StuckRecoveryV2.Enabled}");

        if (previous.HazardAvoidance.Enabled != current.HazardAvoidance.Enabled)
            changes.Add($"HazardAvoidance: {previous.HazardAvoidance.Enabled} -> {current.HazardAvoidance.Enabled}");

        if (changes.Count > 0)
        {
            logger.LogInformation(
                "[FeatureFlagSvc  ] Feature flags changed: {Changes}",
                string.Join(", ", changes));
        }
    }

    /// <summary>
    /// Checks if a specific feature is enabled by name.
    /// </summary>
    public bool IsFeatureEnabled(string featureName)
    {
        return featureName.ToLowerInvariant() switch
        {
            "objectpooling" => IsObjectPoolingEnabled,
            "circuitbreaker" => IsCircuitBreakerEnabled,
            "pathsmoothing" => IsPathSmoothingEnabled,
            "enhancedstuckrecovery" => IsEnhancedStuckRecoveryEnabled,
            "hazardavoidance" => IsHazardAvoidanceEnabled,
            "aiprofilegenerator" => IsAIProfileGeneratorEnabled,
            "profilemarketplace" => IsProfileMarketplaceEnabled,
            "behaviortreecombat" => IsBehaviorTreeCombatEnabled,
            "hybridllmdecision" => IsHybridLLMDecisionEnabled,
            _ => false
        };
    }

    /// <summary>
    /// Gets all current feature states as a dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, bool> GetAllFeatureStates()
    {
        return new Dictionary<string, bool>
        {
            ["ObjectPooling"] = IsObjectPoolingEnabled,
            ["CircuitBreaker"] = IsCircuitBreakerEnabled,
            ["PathSmoothing"] = IsPathSmoothingEnabled,
            ["EnhancedStuckRecovery"] = IsEnhancedStuckRecoveryEnabled,
            ["HazardAvoidance"] = IsHazardAvoidanceEnabled,
            ["AIProfileGenerator"] = IsAIProfileGeneratorEnabled,
            ["ProfileMarketplace"] = IsProfileMarketplaceEnabled,
            ["BehaviorTreeCombat"] = IsBehaviorTreeCombatEnabled,
            ["HybridLLMDecision"] = IsHybridLLMDecisionEnabled
        };
    }
}

/// <summary>
/// Configuration options for the FeatureFlagService.
/// </summary>
public sealed class FeatureFlagServiceOptions
{
    /// <summary>
    /// Path to the runtime feature flags configuration file.
    /// </summary>
    public string ConfigFilePath { get; set; } = "runtime_feature_flags.json";
}
