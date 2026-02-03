using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Testing;

/// <summary>
/// Helper utilities for test execution
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Wait for a condition to become true with timeout
    /// </summary>
    /// <param name="condition">Condition to check</param>
    /// <param name="timeoutMs">Maximum time to wait in milliseconds</param>
    /// <param name="pollIntervalMs">How often to check the condition</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>True if condition became true, false if timeout</returns>
    public static async Task<bool> WaitForCondition(
        Func<bool> condition,
        int timeoutMs,
        int pollIntervalMs = 50,
        CancellationToken token = default)
    {
        DateTime startTime = DateTime.UtcNow;
        TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMs);

        while ((DateTime.UtcNow - startTime) < timeout)
        {
            if (token.IsCancellationRequested)
                return false;

            if (condition())
                return true;

            await Task.Delay(pollIntervalMs, token);
        }

        return false;
    }

    /// <summary>
    /// Wait for a value to change from initial value
    /// </summary>
    /// <typeparam name="T">Type of value to monitor</typeparam>
    /// <param name="getValue">Function to get current value</param>
    /// <param name="initialValue">Initial value to compare against</param>
    /// <param name="timeoutMs">Maximum time to wait</param>
    /// <param name="pollIntervalMs">Poll interval</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>True if value changed, false if timeout</returns>
    public static async Task<bool> WaitForValueChange<T>(
        Func<T> getValue,
        T initialValue,
        int timeoutMs,
        int pollIntervalMs = 50,
        CancellationToken token = default) where T : IEquatable<T>
    {
        return await WaitForCondition(
            () => !getValue().Equals(initialValue),
            timeoutMs,
            pollIntervalMs,
            token);
    }

    /// <summary>
    /// Retry an action until it succeeds or timeout
    /// </summary>
    /// <param name="action">Action to retry</param>
    /// <param name="maxAttempts">Maximum number of attempts</param>
    /// <param name="delayBetweenMs">Delay between attempts</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>True if action succeeded, false otherwise</returns>
    public static async Task<bool> RetryUntilSuccess(
        Func<bool> action,
        int maxAttempts = 3,
        int delayBetweenMs = 100,
        CancellationToken token = default)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (token.IsCancellationRequested)
                return false;

            if (action())
                return true;

            if (attempt < maxAttempts - 1)
                await Task.Delay(delayBetweenMs, token);
        }

        return false;
    }

    /// <summary>
    /// Measure execution time of an action
    /// </summary>
    /// <param name="action">Action to measure</param>
    /// <returns>Elapsed time</returns>
    public static TimeSpan MeasureTime(Action action)
    {
        DateTime start = DateTime.UtcNow;
        action();
        return DateTime.UtcNow - start;
    }

    /// <summary>
    /// Measure execution time of an async action
    /// </summary>
    /// <param name="action">Async action to measure</param>
    /// <returns>Elapsed time</returns>
    public static async Task<TimeSpan> MeasureTimeAsync(Func<Task> action)
    {
        DateTime start = DateTime.UtcNow;
        await action();
        return DateTime.UtcNow - start;
    }

    /// <summary>
    /// Create a test check that compares expected vs actual values
    /// </summary>
    public static TestCheck CreateCheck<T>(string name, T expected, T actual, string? message = null)
    {
        bool passed = expected?.Equals(actual) ?? actual == null;
        return new TestCheck(
            name,
            passed,
            expected?.ToString() ?? "null",
            actual?.ToString() ?? "null",
            message);
    }

    /// <summary>
    /// Create a test check for a boolean condition
    /// </summary>
    public static TestCheck CreateBoolCheck(string name, bool condition, string? message = null)
    {
        return new TestCheck(
            name,
            condition,
            "true",
            condition.ToString(),
            message);
    }

    /// <summary>
    /// Create a test check for a range validation
    /// </summary>
    public static TestCheck CreateRangeCheck(string name, int value, int min, int max, string? message = null)
    {
        bool passed = value >= min && value <= max;
        return new TestCheck(
            name,
            passed,
            $"{min}-{max}",
            value.ToString(),
            message);
    }
}
