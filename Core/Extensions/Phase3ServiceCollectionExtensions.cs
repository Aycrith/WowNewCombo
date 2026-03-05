namespace Core.Extensions;

using System;

using Core.AI.HybridDecision;
using Core.AI.LLM;
using Core.BehaviorTree;
using Core.FeatureFlags;
using Core.GOAP;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Extension methods for registering Phase 3 services (Behavior Trees, Hybrid LLM Decision).
/// </summary>
public static class Phase3ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Phase 3 feature services: Behavior Tree Combat System and Hybrid GOAP+LLM Decision.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPhase3Features(this IServiceCollection services)
    {
        // Behavior Tree Combat System
        services.AddBehaviorTreeCombat();

        // Hybrid GOAP+LLM Decision System
        services.AddHybridDecision();

        return services;
    }

    /// <summary>
    /// Adds Behavior Tree Combat System services.
    /// </summary>
    private static IServiceCollection AddBehaviorTreeCombat(this IServiceCollection services)
    {
        // Behavior Tree Combat Engine Factory
        services.AddSingleton<IBehaviorTreeCombatEngineFactory, BehaviorTreeCombatEngineFactory>();

        return services;
    }

    /// <summary>
    /// Adds Hybrid GOAP+LLM Decision System services.
    /// </summary>
    private static IServiceCollection AddHybridDecision(this IServiceCollection services)
    {
        // Hybrid Decision Engine
        services.AddSingleton<HybridDecisionEngine>(sp =>
        {
            ILogger<HybridDecisionEngine> logger = sp.GetRequiredService<ILogger<HybridDecisionEngine>>();
            GoapAgent goapAgent = sp.GetRequiredService<GoapAgent>();
            ILLMClientFactory llmFactory = sp.GetRequiredService<ILLMClientFactory>();
            IOptions<FeatureFlagsOptions> options = sp.GetRequiredService<IOptions<FeatureFlagsOptions>>();
            PlayerReader playerReader = sp.GetRequiredService<PlayerReader>();
            AddonBits bits = sp.GetRequiredService<AddonBits>();

            return new HybridDecisionEngine(logger, goapAgent, llmFactory, options, playerReader, bits);
        });

        return services;
    }
}
