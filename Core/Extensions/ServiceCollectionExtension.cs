using System;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;

namespace Core.Extensions;
public static class ServiceCollectionExtension
{
    public static IServiceCollection ForwardSingleton<
        TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TInterface>
        (this IServiceCollection services)
    where TService : class, TInterface
    {
        services.AddSingleton(typeof(TService));
        services.AddSingleton(typeof(TInterface), GetRequired);

        return services;

        static TService GetRequired(IServiceProvider x)
            => x.GetRequiredService<TService>();
    }

    public static IServiceCollection ForwardSingleton<TService>(
        this IServiceCollection services,
        IServiceProvider sp)
        where TService : class
    {
        return services.AddSingleton(sp.GetRequiredService<TService>());
    }

    public static IServiceCollection ForwardSingleton<TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TInterface>(
        this IServiceCollection services,
        Func<IServiceProvider, TService> implementationFactory)
        where TService : class, TInterface
    {
        services.AddSingleton(typeof(TService), implementationFactory);
        services.AddSingleton(typeof(TInterface), GetRequired);

        return services;

        static TService GetRequired(IServiceProvider x)
            => x.GetRequiredService<TService>();
    }

    public static IServiceCollection ForwardSingleton<TService,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TInterface1,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TInterface2>(
    this IServiceCollection services,
    Func<IServiceProvider, TService> implementationFactory)
    where TService : class, TInterface1
    {
        services.AddSingleton(typeof(TService), implementationFactory);
        services.AddSingleton(typeof(TInterface1), GetRequired);
        services.AddSingleton(typeof(TInterface2), GetRequired);

        return services;

        static TService GetRequired(IServiceProvider x)
            => x.GetRequiredService<TService>();
    }
}
