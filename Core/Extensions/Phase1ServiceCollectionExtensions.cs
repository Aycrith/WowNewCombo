using System;

using Core.FeatureFlags;
using Core.GoalsComponent;
using Core.LLM;
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

        // Register LLM services (disabled by default, controlled by HybridLLMDecisionOptions)
        services.AddSingleton<ILLMClient, NullLLMClient>();
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

/// <summary>
/// Factory interface for creating circuit breakers.
/// </summary>
public interface ICircuitBreakerFactory
{
    /// <summary>
    /// Creates or retrieves a named circuit breaker.
    /// </summary>
    /// <typeparam name="TResult">The result type for the circuit breaker.</typeparam>
    /// <param name="name">Unique name for the circuit breaker.</param>
    /// <param name="fallback">Fallback function to call when circuit is open.</param>
    /// <param name="failureThreshold">Number of failures before opening.</param>
    /// <param name="cooldownPeriod">Time before attempting half-open.</param>
    /// <returns>The circuit breaker instance.</returns>
    CircuitBreaker<TResult> GetOrCreate<TResult>(
        string name,
        Func<TResult> fallback,
        int failureThreshold = 5,
        TimeSpan? cooldownPeriod = null);
}

/// <summary>
/// Default implementation of circuit breaker factory.
/// </summary>
public sealed class CircuitBreakerFactory : ICircuitBreakerFactory
{
    private readonly IServiceProvider serviceProvider;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> breakers = new();

    public CircuitBreakerFactory(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public CircuitBreaker<TResult> GetOrCreate<TResult>(
        string name,
        Func<TResult> fallback,
        int failureThreshold = 5,
        TimeSpan? cooldownPeriod = null)
    {
        return (CircuitBreaker<TResult>)breakers.GetOrAdd(
            $"{name}_{typeof(TResult).Name}",
            _ =>
            {
                var logger = serviceProvider.GetRequiredService<
                    Microsoft.Extensions.Logging.ILogger<CircuitBreaker<TResult>>>();

                return new CircuitBreaker<TResult>(
                    logger,
                    name,
                    failureThreshold,
                    cooldownPeriod ?? TimeSpan.FromSeconds(30),
                    fallback);
            });
    }
}
