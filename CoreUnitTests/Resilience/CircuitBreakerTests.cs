using Core.Resilience;

using Microsoft.Extensions.Logging.Abstractions;

using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace CoreUnitTests.Resilience;

public sealed class CircuitBreakerTests
{
    [Fact]
    public async Task ExecuteAsync_OpensCircuit_AfterThresholdFailures()
    {
        CircuitBreaker<int> breaker = CreateBreaker(failureThreshold: 2, cooldownPeriod: TimeSpan.FromMilliseconds(500), fallbackValue: -1);

        int first = await breaker.ExecuteAsync(() => Task.FromException<int>(new InvalidOperationException("boom-1")));
        int second = await breaker.ExecuteAsync(() => Task.FromException<int>(new InvalidOperationException("boom-2")));

        Assert.Equal(-1, first);
        Assert.Equal(-1, second);
        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.Equal(2, breaker.FailureCount);
        Assert.Equal(2, breaker.TotalRequests);
        Assert.Equal(0, breaker.SuccessfulRequests);
        Assert.Equal(2, breaker.FailedRequests);
        Assert.Equal(0, breaker.RejectedRequests);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsFast_WhenCircuitOpen()
    {
        CircuitBreaker<int> breaker = CreateBreaker(failureThreshold: 1, cooldownPeriod: TimeSpan.FromSeconds(1), fallbackValue: -7);

        int first = await breaker.ExecuteAsync(() => Task.FromException<int>(new InvalidOperationException("trip")));
        Assert.Equal(-7, first);
        Assert.Equal(CircuitState.Open, breaker.State);

        int called = 0;
        int second = await breaker.ExecuteAsync(() =>
        {
            Interlocked.Increment(ref called);
            return Task.FromResult(123);
        });

        Assert.Equal(-7, second);
        Assert.Equal(0, Volatile.Read(ref called));
        Assert.Equal(2, breaker.TotalRequests);
        Assert.Equal(1, breaker.FailedRequests);
        Assert.Equal(1, breaker.RejectedRequests);
    }

    [Fact]
    public async Task ExecuteAsync_AfterCooldown_SuccessProbe_ClosesCircuit()
    {
        CircuitBreaker<int> breaker = CreateBreaker(failureThreshold: 1, cooldownPeriod: TimeSpan.FromMilliseconds(30), fallbackValue: -1);

        int tripped = await breaker.ExecuteAsync(() => Task.FromException<int>(new InvalidOperationException("trip")));
        Assert.Equal(-1, tripped);
        Assert.Equal(CircuitState.Open, breaker.State);

        await Task.Delay(60);

        int probeCalls = 0;
        int recovered = await breaker.ExecuteAsync(() =>
        {
            Interlocked.Increment(ref probeCalls);
            return Task.FromResult(42);
        });

        Assert.Equal(42, recovered);
        Assert.Equal(1, probeCalls);
        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.Equal(0, breaker.FailureCount);
        Assert.Equal(2, breaker.TotalRequests);
        Assert.Equal(1, breaker.SuccessfulRequests);
    }

    [Fact]
    public async Task ExecuteAsync_AfterCooldown_FailedProbe_ReopensCircuit()
    {
        CircuitBreaker<int> breaker = CreateBreaker(failureThreshold: 1, cooldownPeriod: TimeSpan.FromMilliseconds(25), fallbackValue: -5);

        int tripped = await breaker.ExecuteAsync(() => Task.FromException<int>(new InvalidOperationException("trip")));
        Assert.Equal(-5, tripped);
        Assert.Equal(CircuitState.Open, breaker.State);

        await Task.Delay(50);

        int probeResult = await breaker.ExecuteAsync(() => Task.FromException<int>(new InvalidOperationException("still failing")));

        Assert.Equal(-5, probeResult);
        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.True(breaker.FailureCount >= 2);
    }

    [Fact]
    public async Task ExecuteAsync_OperationCanceledException_DoesNotCountAsFailure()
    {
        CircuitBreaker<int> breaker = CreateBreaker(failureThreshold: 1, cooldownPeriod: TimeSpan.FromMilliseconds(50), fallbackValue: -1);

        CancellationToken cancellationToken = new(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => breaker.ExecuteAsync(() => Task.FromCanceled<int>(cancellationToken)));

        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.Equal(1, breaker.TotalRequests);
        Assert.Equal(0, breaker.SuccessfulRequests);
        Assert.Equal(0, breaker.FailedRequests);
        Assert.Equal(0, breaker.RejectedRequests);
    }

    [Fact]
    public void TripAndReset_UpdatesStateAndStats()
    {
        CircuitBreaker<int> breaker = CreateBreaker(failureThreshold: 3, cooldownPeriod: TimeSpan.FromSeconds(1), fallbackValue: -1);

        breaker.Trip();
        CircuitBreakerStats openStats = breaker.GetStats();

        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.Equal(CircuitState.Open, openStats.State);
        Assert.True(openStats.CooldownRemaining > TimeSpan.Zero);

        breaker.Reset();
        CircuitBreakerStats closedStats = breaker.GetStats();

        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.Equal(CircuitState.Closed, closedStats.State);
        Assert.Equal(0, breaker.FailureCount);
        Assert.Equal(TimeSpan.Zero, closedStats.CooldownRemaining);
    }

    private static CircuitBreaker<int> CreateBreaker(int failureThreshold, TimeSpan cooldownPeriod, int fallbackValue)
    {
        return new CircuitBreaker<int>(
            NullLogger.Instance,
            serviceName: "TestService",
            failureThreshold: failureThreshold,
            cooldownPeriod: cooldownPeriod,
            fallback: () => fallbackValue);
    }
}
