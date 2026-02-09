using System;
using System.Collections.Generic;

namespace Core.Diagnostics;

/// <summary>
/// Interface for tracing execution of goals, abilities, and other operations
/// for debugging and performance analysis.
/// </summary>
public interface IExecutionTracer
{
    /// <summary>
    /// Starts tracing an operation.
    /// </summary>
    /// <returns>A disposable scope that ends tracing when disposed.</returns>
    IDisposable BeginTrace(TraceCategory category, string operation, string? details = null);

    /// <summary>
    /// Records a trace entry after the operation completes.
    /// </summary>
    void RecordTrace(TraceEntry entry);

    /// <summary>
    /// Gets the most recent trace entries.
    /// </summary>
    List<TraceEntry> GetRecentTraces(int count);

    /// <summary>
    /// Gets traces filtered by category.
    /// </summary>
    List<TraceEntry> GetTracesByCategory(TraceCategory category, int count);

    /// <summary>
    /// Clears all trace history.
    /// </summary>
    void ClearTraces();

    /// <summary>
    /// Total number of traces recorded.
    /// </summary>
    int TotalTraces { get; }
}

/// <summary>
/// Represents a single trace entry.
/// </summary>
public sealed class TraceEntry
{
    public DateTime Timestamp { get; set; }
    public TraceCategory Category { get; set; }
    public string Operation { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Details { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}

/// <summary>
/// Categories of trace operations.
/// </summary>
public enum TraceCategory
{
    Goal,
    Ability,
    Navigation,
    Combat,
    System
}

/// <summary>
/// Helper class for disposable tracing scope.
/// </summary>
public sealed class TraceScope : IDisposable
{
    private readonly IExecutionTracer tracer;
    private readonly TraceEntry entry;
    private readonly System.Diagnostics.Stopwatch stopwatch;
    private bool disposed;

    public TraceScope(IExecutionTracer tracer, TraceCategory category, string operation, string? details)
    {
        this.tracer = tracer;
        stopwatch = System.Diagnostics.Stopwatch.StartNew();
        entry = new TraceEntry
        {
            Timestamp = DateTime.UtcNow,
            Category = category,
            Operation = operation,
            Details = details ?? string.Empty,
            Success = true
        };
    }

    public void MarkFailed(string reason)
    {
        entry.Success = false;
        entry.Details = reason;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        stopwatch.Stop();
        entry.DurationMs = stopwatch.ElapsedMilliseconds;
        tracer.RecordTrace(entry);
    }
}
