using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Core.Diagnostics;

/// <summary>
/// Thread-safe in-memory implementation of execution tracing.
/// Tracks operations with timing and success/failure information.
/// </summary>
public sealed class ExecutionTracer : IExecutionTracer
{
    private readonly ConcurrentQueue<TraceEntry> traces = new();
    private int totalTraces;

    /// <inheritdoc />
    public int TotalTraces => totalTraces;

    /// <inheritdoc />
    public IDisposable BeginTrace(TraceCategory category, string operation, string? details = null)
    {
        return new TraceScope(this, category, operation, details);
    }

    /// <inheritdoc />
    public void RecordTrace(TraceEntry entry)
    {
        traces.Enqueue(entry);
        Interlocked.Increment(ref totalTraces);

        // Keep only last 5000 traces to prevent unbounded memory growth
        while (traces.Count > 5000 && traces.TryDequeue(out _)) { }
    }

    /// <inheritdoc />
    public List<TraceEntry> GetRecentTraces(int count)
    {
        return traces.TakeLast(count).ToList();
    }

    /// <inheritdoc />
    public List<TraceEntry> GetTracesByCategory(TraceCategory category, int count)
    {
        return traces.Where(t => t.Category == category).TakeLast(count).ToList();
    }

    /// <inheritdoc />
    public void ClearTraces()
    {
        while (traces.TryDequeue(out _)) { }
        Interlocked.Exchange(ref totalTraces, 0);
    }
}
