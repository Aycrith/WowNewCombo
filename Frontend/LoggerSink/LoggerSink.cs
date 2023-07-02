using Serilog.Core;
using Serilog.Events;

namespace Frontend;

public sealed class LoggerSink : ILogEventSink
{
    public event Action? OnLogChanged;

    public const int SIZE = 256;

    private int callCount;
    public LogEvent[] Log { get; }
    public int Head => callCount % SIZE;

    public LoggerSink()
    {
        Log = new LogEvent[SIZE];
    }

    public void Emit(LogEvent logEvent)
    {
        Log[callCount++ % SIZE] = logEvent;
        OnLogChanged?.Invoke();
    }
}
