using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Core.Hazard;

public static class HazardServiceCollectionExtensions
{
    public static IServiceCollection AddHazardAvoidance(this IServiceCollection services)
    {
        services.TryAddSingleton<HazardZoneStore>();
        services.TryAddSingleton<LocalHazardDAO>();
        services.TryAddSingleton<HazardClusterAnalyzer>();
        services.TryAddSingleton<RouteRehabilitator>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, HazardAnalyticsBackgroundService>());

        return services;
    }
}

