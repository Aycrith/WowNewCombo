using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Core.Resilience;

/// <summary>
/// Circuit states following the standard circuit breaker pattern.
/// </summary>
public enum CircuitState
{
    /// <summary>Normal operation, requests flow through.</summary>
    Closed,

    /// <summary>Circuit tripped, requests are rejected immediately.</summary>
    Open,

    /// <summary>Testing if service recovered, allowing single request through.</summary>
    HalfOpen
}

/// <summary>
/// Implements the circuit breaker pattern to prevent cascading failures
/// when external services (pathfinding APIs, LLM APIs) are unavailable.
/// <para>
/// Thread-safe implementation suitable for concurrent access.
/// </para>
/// </summary>
/// <typeparam name="TResult">The type returned by the protected operation.</typeparam>
/// <remarks>
/// Reference: Release It! by Michael Nygard (2007)
/// Pattern: https://docs.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker
/// </remarks>
public sealed class CircuitBreaker<TResult>
{
    private readonly ILogger _logger;
    private readonly int _failureThreshold;
    private readonly TimeSpan _cooldownPeriod;
    private readonly Func<TResult> _fallback;
    private readonly string _serviceName;

    private int _failureCount;
    private DateTime _lastFailure;
    private CircuitState _state = CircuitState.Closed;
    private readonly object _lock = new();

    private long _totalRequests;
    private long _successfulRequests;
    private long _failedRequests;
    private long _rejectedRequests;

    /// <summary>
    /// Initializes a new circuit breaker instance.
    /// </summary>
    /// <param name="logger">Logger for state change notifications.</param>
    /// <param name="serviceName">Name of the protected service (for logging).</param>
    /// <param name="failureThreshold">Number of failures before opening circuit.</param>
    /// <param name="cooldownPeriod">Time to wait before attempting recovery.</param>
    /// <param name="fallback">Function to call when circuit is open.</param>
    public CircuitBreaker(
        ILogger logger,
        string serviceName,
        int failureThreshold,
        TimeSpan cooldownPeriod,
        Func<TResult> fallback)
    {
        _logger = logger;
        _serviceName = serviceName;
        _failureThreshold = failureThreshold;
        _cooldownPeriod = cooldownPeriod;
        _fallback = fallback;
    }

    /// <summary>Current circuit state.</summary>
    public CircuitState State => _state;

    /// <summary>Current failure count since last reset.</summary>
    public int FailureCount => _failureCount;

    /// <summary>Total requests processed through this circuit.</summary>
    public long TotalRequests => Interlocked.Read(ref _totalRequests);

    /// <summary>Successfully completed requests.</summary>
    public long SuccessfulRequests => Interlocked.Read(ref _successfulRequests);

    /// <summary>Failed requests that contributed to opening the circuit.</summary>
    public long FailedRequests => Interlocked.Read(ref _failedRequests);

    /// <summary>Requests rejected while circuit was open.</summary>
    public long RejectedRequests => Interlocked.Read(ref _rejectedRequests);

    /// <summary>
    /// Executes an asynchronous operation with circuit breaker protection.
    /// </summary>
    /// <param name="action">The async operation to execute.</param>
    /// <returns>Result from the operation or fallback if circuit is open.</returns>
    public async Task<TResult> ExecuteAsync(Func<Task<TResult>> action)
    {
        Interlocked.Increment(ref _totalRequests);

        // Check if we should allow the request through
        lock (_lock)
        {
            if (_state == CircuitState.Open)
            {
                if (DateTime.UtcNow - _lastFailure >= _cooldownPeriod)
                {
                    // Attempt recovery
                    _state = CircuitState.HalfOpen;
                    _logger.LogInformation(
                        "[CircuitBreaker   ] {Service}: Entering half-open state after {Cooldown}s cooldown",
                        _serviceName, _cooldownPeriod.TotalSeconds);
                }
                else
                {
                    // Still in cooldown, reject immediately
                    Interlocked.Increment(ref _rejectedRequests);
                    _logger.LogDebug(
                        "[CircuitBreaker   ] {Service}: Circuit open, using fallback",
                        _serviceName);
                    return _fallback();
                }
            }
        }

        try
        {
            TResult result = await action().ConfigureAwait(false);

            // Success - reset circuit if needed
            lock (_lock)
            {
                Interlocked.Increment(ref _successfulRequests);

                if (_state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Closed;
                    _failureCount = 0;
                    _logger.LogInformation(
                        "[CircuitBreaker   ] {Service}: Circuit closed - service recovered",
                        _serviceName);
                }
                else if (_failureCount > 0)
                {
                    // Gradual recovery while closed
                    _failureCount = Math.Max(0, _failureCount - 1);
                }
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is not a failure
            throw;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedRequests);

            lock (_lock)
            {
                _failureCount++;
                _lastFailure = DateTime.UtcNow;

                if (_state == CircuitState.HalfOpen)
                {
                    // Recovery failed, reopen circuit
                    _state = CircuitState.Open;
                    _logger.LogWarning(
                        "[CircuitBreaker   ] {Service}: Recovery failed, circuit reopened: {Error}",
                        _serviceName, ex.Message);
                }
                else if (_failureCount >= _failureThreshold)
                {
                    _state = CircuitState.Open;
                    _logger.LogWarning(
                        "[CircuitBreaker   ] {Service}: Circuit opened after {Count} failures: {Error}",
                        _serviceName, _failureCount, ex.Message);
                }
                else
                {
                    _logger.LogDebug(
                        "[CircuitBreaker   ] {Service}: Failure {Count}/{Threshold}: {Error}",
                        _serviceName, _failureCount, _failureThreshold, ex.Message);
                }
            }

            return _fallback();
        }
    }

    /// <summary>
    /// Executes a synchronous operation with circuit breaker protection.
    /// </summary>
    /// <param name="action">The operation to execute.</param>
    /// <returns>Result from the operation or fallback if circuit is open.</returns>
    public TResult Execute(Func<TResult> action)
    {
        Interlocked.Increment(ref _totalRequests);

        lock (_lock)
        {
            if (_state == CircuitState.Open)
            {
                if (DateTime.UtcNow - _lastFailure >= _cooldownPeriod)
                {
                    _state = CircuitState.HalfOpen;
                    _logger.LogInformation(
                        "[CircuitBreaker   ] {Service}: Entering half-open state",
                        _serviceName);
                }
                else
                {
                    Interlocked.Increment(ref _rejectedRequests);
                    return _fallback();
                }
            }
        }

        try
        {
            TResult result = action();

            lock (_lock)
            {
                Interlocked.Increment(ref _successfulRequests);

                if (_state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Closed;
                    _failureCount = 0;
                    _logger.LogInformation(
                        "[CircuitBreaker   ] {Service}: Circuit closed - service recovered",
                        _serviceName);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedRequests);

            lock (_lock)
            {
                _failureCount++;
                _lastFailure = DateTime.UtcNow;

                if (_failureCount >= _failureThreshold || _state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Open;
                    _logger.LogWarning(
                        "[CircuitBreaker   ] {Service}: Circuit opened: {Error}",
                        _serviceName, ex.Message);
                }
            }

            return _fallback();
        }
    }

    /// <summary>
    /// Manually resets the circuit to closed state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _state = CircuitState.Closed;
            _failureCount = 0;
            _logger.LogInformation(
                "[CircuitBreaker   ] {Service}: Circuit manually reset to closed",
                _serviceName);
        }
    }

    /// <summary>
    /// Manually trips the circuit to open state.
    /// </summary>
    public void Trip()
    {
        lock (_lock)
        {
            _state = CircuitState.Open;
            _lastFailure = DateTime.UtcNow;
            _logger.LogWarning(
                "[CircuitBreaker   ] {Service}: Circuit manually tripped to open",
                _serviceName);
        }
    }

    /// <summary>
    /// Gets statistics about circuit breaker operation.
    /// </summary>
    public CircuitBreakerStats GetStats() => new(
        State: _state,
        FailureCount: _failureCount,
        FailureThreshold: _failureThreshold,
        TotalRequests: TotalRequests,
        SuccessfulRequests: SuccessfulRequests,
        FailedRequests: FailedRequests,
        RejectedRequests: RejectedRequests,
        LastFailure: _lastFailure,
        CooldownRemaining: _state == CircuitState.Open
            ? TimeSpan.FromTicks(Math.Max(0,
                (_lastFailure + _cooldownPeriod - DateTime.UtcNow).Ticks))
            : TimeSpan.Zero
    );
}

/// <summary>
/// Statistics about circuit breaker operation.
/// </summary>
public record CircuitBreakerStats(
    CircuitState State,
    int FailureCount,
    int FailureThreshold,
    long TotalRequests,
    long SuccessfulRequests,
    long FailedRequests,
    long RejectedRequests,
    DateTime LastFailure,
    TimeSpan CooldownRemaining);
