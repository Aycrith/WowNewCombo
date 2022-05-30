using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using System;
using Cyotek.Collections.Generic;

namespace Core
{
    public static class LoggerSinkExtensions
    {
        public static LoggerConfiguration LoggerSink(this LoggerSinkConfiguration loggerConfiguration)
        {
            return loggerConfiguration.Sink(new LoggerSink());
        }
    }

    public class LoggerSink : ILogEventSink
    {
        public static event Action? OnLogChanged;
        public static CircularBuffer<LogEvent> Log { get; private set; } = new(250);

        public void Emit(LogEvent logEvent)
        {
            Log.Put(logEvent);
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
