using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Core.AI.LLM;
using Core.AI.ProfileGenerator;
using Core.Marketplace;

namespace Core.Extensions;

/// <summary>
/// Extension methods for registering Phase 2 services (AI Profile Generator, Marketplace).
/// </summary>
public static class Phase2ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Phase 2 feature services: AI Profile Generator and Profile Marketplace.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration for feature flags.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPhase2Features(
        this IServiceCollection services)
    {
        // AI Profile Generator
        services.AddAIProfileGenerator();

        // Profile Marketplace
        services.AddProfileMarketplace();

        return services;
    }

    /// <summary>
    /// Adds AI Profile Generator services.
    /// </summary>
    private static IServiceCollection AddAIProfileGenerator(
        this IServiceCollection services)
    {
        // LLM Client Factory
        services.AddSingleton<ILLMClientFactory, LLMClientFactory>();

        // Profile Generator Service
        services.AddSingleton<AIProfileGeneratorService>();

        // Profile Validator
        services.AddSingleton<ProfileValidator>();

        // Register loggers for LLM clients (needed by factory)
        services.AddSingleton<ILogger<OpenAIClient>>(sp =>
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<OpenAIClient>());
        services.AddSingleton<ILogger<LocalLlamaClient>>(sp =>
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<LocalLlamaClient>());

        return services;
    }

    /// <summary>
    /// Adds Profile Marketplace services with proper HttpClient management.
    /// Uses IHttpClientFactory to avoid socket exhaustion and DNS rotation issues.
    /// </summary>
    private static IServiceCollection AddProfileMarketplace(
        this IServiceCollection services)
    {
        // Register named HttpClient for marketplace (IHttpClientFactory pattern)
        services.AddHttpClient("GitHubMarketplace");

        // Marketplace Service uses IHttpClientFactory (not direct HttpClient)
        services.AddSingleton<ProfileMarketplaceService>();

        return services;
    }
}
