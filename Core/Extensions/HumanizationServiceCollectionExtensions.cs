using Core.Humanization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SharedLib.Humanization;

using System;

namespace Core.Extensions;

public static class HumanizationServiceCollectionExtensions
{
    public static IServiceCollection AddHumanizationServices(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        services.TryAddSingleton<FatigueSimulator>();

        services.TryAddSingleton<MicroPauseService>();
        services.AddHostedService(sp => sp.GetRequiredService<MicroPauseService>());

        services.TryAddSingleton<ScheduledBreakService>();
        services.AddHostedService(sp => sp.GetRequiredService<ScheduledBreakService>());

        services.TryAddSingleton<HumanizationProvider>();
        services.TryAddSingleton<IHumanizationProvider>(sp => sp.GetRequiredService<HumanizationProvider>());

        // Phase 4: Monitoring & Analytics
        services.TryAddSingleton<HumanizationMetrics>();
        services.TryAddSingleton<DetectionRiskAnalyzer>();

        return services;
    }
}

