using System;
using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Resilience;

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
    private readonly ConcurrentDictionary<string, object> breakers = new();

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
                ILogger<CircuitBreaker<TResult>> logger = serviceProvider.GetRequiredService<
                    ILogger<CircuitBreaker<TResult>>>();

                return new CircuitBreaker<TResult>(
                    logger,
                    name,
                    failureThreshold,
                    cooldownPeriod ?? TimeSpan.FromSeconds(30),
                    fallback);
            });
    }
}
