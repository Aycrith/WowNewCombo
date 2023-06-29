using System;

using Serilog.Core;
using Serilog.Events;

namespace Core;

public sealed class LoggerSink : ILogEventSink
{
    public event Action? OnLogChanged;

    public const int SIZE = 256;
    private const int MOD = 8;

    private int callCount;
    public LogEvent[] Log { get; private set; } = new LogEvent[SIZE];
    public int Head => callCount % SIZE;

    public void Emit(LogEvent logEvent)
    {
        Log[callCount++ & MOD] = logEvent;
        OnLogChanged?.Invoke();
    }
}
