using System;

using Core.FeatureFlags;
using Core.GoalsComponent;
using Core.LLM;
using Core.Navigation;
using Core.Performance;
using Core.Resilience;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Core.Extensions;

/// <summary>
/// Extension methods for registering Phase 1 feature services.
/// </summary>
public static class Phase1ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Phase 1 feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root for binding options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPhase1Features(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure feature flags options - use default values for now
        // Can be enhanced later with JSON file binding if Microsoft.Extensions.Configuration.Binder is added
        services.Configure<FeatureFlagsOptions>(options =>
        {
            // Default values are set in the options class itself
            // This allows hot-reload to work through the FeatureFlagService
        });

        services.Configure<FeatureFlagServiceOptions>(options =>
        {
            options.ConfigFilePath = configuration["FeatureFlags:ConfigFilePath"]
                ?? "runtime_feature_flags.json";
        });

        // Register feature flag service as hosted service
        services.AddSingleton<FeatureFlagService>();
        services.AddHostedService(sp => sp.GetRequiredService<FeatureFlagService>());

        // Register circuit breaker factory
        services.AddSingleton<ICircuitBreakerFactory, CircuitBreakerFactory>();

        // Register breadcrumb tracker (transient - each Navigation gets its own instance)
        services.AddTransient<BreadcrumbTracker>();

        // Register navigation soak metrics service as a singleton UI/session bridge.
        // It late-binds to the active bot session's StuckDetector/Navigation instances.
        services.AddSingleton<NavSoakMetricsService>();

        // Register LLM services (disabled by default, controlled by HybridLLMDecisionOptions)
        services.AddSingleton<ILLMClient, NullLLMClient>();

        // Register circuit breaker for LLM decisions
        services.AddSingleton(sp =>
        {
            ICircuitBreakerFactory factory = sp.GetRequiredService<ICircuitBreakerFactory>();
            IOptionsMonitor<FeatureFlagsOptions> opts = sp.GetRequiredService<IOptionsMonitor<FeatureFlagsOptions>>();
            HybridLLMDecisionOptions llmOpts = opts.CurrentValue.HybridLLMDecision;
            return factory.GetOrCreate<LLMDecision>(
                "HybridLLM",
                static () => new LLMDecision("NoAction", "Circuit open", 0f),
                llmOpts.CircuitBreakerThreshold,
                TimeSpan.FromSeconds(llmOpts.CircuitBreakerCooldownSeconds));
        });

        // Register Hybrid LLM decision engine and event listener (SRP-compliant)
        services.AddSingleton<HybridLlmDecisionEngine>();
        services.AddSingleton<HybridLlmEventListener>();
        services.AddHostedService<HybridLLMDecisionService>();

        return services;
    }

    /// <summary>
    /// Adds object pooling services for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to pool. Must have a parameterless constructor.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="maxSize">Maximum pool capacity.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddObjectPool<T>(
        this IServiceCollection services,
        int maxSize = 100)
        where T : class, new()
    {
        services.AddSingleton(sp => new ObjectPool<T>(maxSize));
        return services;
    }
}
