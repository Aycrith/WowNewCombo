using Core.FeatureFlags;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Core.CombatRotation;

/// <summary>
/// DI registration extension for the Combat Rotation Optimizer module.
/// Call from BlazorServer/Program.cs and HeadlessServer/Program.cs.
/// </summary>
public static class CombatRotationServiceExtensions
{
    /// <summary>
    /// Registers all combat rotation optimizer services.
    /// Safe to call even when the feature is disabled — services remain inert
    /// until enabled via runtime_feature_flags.json.
    /// </summary>
    public static IServiceCollection AddCombatRotationOptimizer(
        this IServiceCollection services)
    {
        // Role strategy (pluggable — DPS is the initial implementation)
        services.TryAddSingleton<IRoleStrategy, DpsRoleStrategy>();

        // Main optimizer
        services.TryAddSingleton<IRotationOptimizer, RotationOptimizer>();

        // Metrics collector (also an IHostedService for periodic flushing)
        services.TryAddSingleton<RotationMetricsCollector>();
        services.AddHostedService(sp => sp.GetRequiredService<RotationMetricsCollector>());

        return services;
    }
}
