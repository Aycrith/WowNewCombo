using System;

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
    /// <remarks>
    /// Note: Changing the Role option requires a restart as IRoleStrategy is registered as singleton.
    /// Hot-reload of role selection is not supported.
    /// </remarks>
    public static IServiceCollection AddCombatRotationOptimizer(
        this IServiceCollection services)
    {
        // Register all role strategies
        services.TryAddSingleton<DpsRoleStrategy>();
        services.TryAddSingleton<TankRoleStrategy>();
        services.TryAddSingleton<HealerRoleStrategy>();

        // Role strategy selector based on feature flag configuration
        // Note: Requires restart to change role as this is evaluated once at DI resolution
        services.TryAddSingleton<IRoleStrategy>(sp =>
        {
            FeatureFlagService featureFlags = sp.GetRequiredService<FeatureFlagService>();
            string? role = featureFlags.Current.CombatRotationOptimizer.Role;

            if (string.Equals(role, "TANK", StringComparison.OrdinalIgnoreCase))
                return sp.GetRequiredService<TankRoleStrategy>();
            else if (string.Equals(role, "HEALER", StringComparison.OrdinalIgnoreCase))
                return sp.GetRequiredService<HealerRoleStrategy>();
            else
                return sp.GetRequiredService<DpsRoleStrategy>(); // Default to DPS
        });

        // Main optimizer
        services.TryAddSingleton<IRotationOptimizer, RotationOptimizer>();

        // Metrics collector (also an IHostedService for periodic flushing)
        services.TryAddSingleton<RotationMetricsCollector>();
        services.AddHostedService(sp => sp.GetRequiredService<RotationMetricsCollector>());

        return services;
    }
}
