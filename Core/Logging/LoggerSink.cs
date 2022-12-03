using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using System;

namespace Core
{
    public static class LoggerSinkExtensions
    {
        public static LoggerConfiguration LoggerSink(this LoggerSinkConfiguration loggerConfiguration)
        {
            return loggerConfiguration.Sink(new LoggerSink());
        }
    }

    public sealed class LoggerSink : ILogEventSink
    {
        public static event Action? OnLogChanged;

        public const int SIZE = 256;
        private static int callCount;
        public static LogEvent[] Log { get; private set; } = new LogEvent[SIZE];
        public static int Head => callCount % SIZE;

        public void Emit(LogEvent logEvent)
        {
            Log[callCount++ % SIZE] = logEvent;
            OnLogChanged?.Invoke();
        }

        public override bool Equals(object? obj)
        {
            return obj is LoggerSink;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }
}
