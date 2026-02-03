using System;
using System.Collections.Generic;

namespace Core.Testing;

/// <summary>
/// Unified test result model for all test types
/// </summary>
public record TestResult(
    bool Success,
    string TestName,
    DateTime Timestamp,
    TimeSpan Duration,
    List<TestCheck> Checks,
    Dictionary<string, object>? Data = null,
    string? Error = null,
    string? Message = null)
{
    /// <summary>
    /// Create a successful test result
    /// </summary>
    public static TestResult Pass(string testName, TimeSpan duration, List<TestCheck> checks, Dictionary<string, object>? data = null, string? message = null)
    {
        return new TestResult(
            Success: true,
            TestName: testName,
            Timestamp: DateTime.UtcNow,
            Duration: duration,
            Checks: checks,
            Data: data,
            Error: null,
            Message: message);
    }

    /// <summary>
    /// Create a failed test result
    /// </summary>
    public static TestResult Fail(string testName, TimeSpan duration, List<TestCheck> checks, string error, Dictionary<string, object>? data = null)
    {
        return new TestResult(
            Success: false,
            TestName: testName,
            Timestamp: DateTime.UtcNow,
            Duration: duration,
            Checks: checks,
            Data: data,
            Error: error,
            Message: null);
    }

    /// <summary>
    /// Create a result from an exception
    /// </summary>
    public static TestResult FromException(string testName, TimeSpan duration, Exception ex)
    {
        return new TestResult(
            Success: false,
            TestName: testName,
            Timestamp: DateTime.UtcNow,
            Duration: duration,
            Checks: [],
            Data: null,
            Error: $"{ex.GetType().Name}: {ex.Message}",
            Message: null);
    }
}

/// <summary>
/// Individual check within a test
/// </summary>
public record TestCheck(
    string Name,
    bool Passed,
    string Expected,
    string Actual,
    string? Message = null)
{
    public static TestCheck Pass(string name, string value, string? message = null)
    {
        return new TestCheck(name, true, value, value, message);
    }

    public static TestCheck Fail(string name, string expected, string actual, string? message = null)
    {
        return new TestCheck(name, false, expected, actual, message);
    }
}
