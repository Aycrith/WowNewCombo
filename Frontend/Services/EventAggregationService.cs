using System.Threading.Channels;
using Core.GOAP;
using Core.Goals;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace Frontend.Services;

/// <summary>
/// Aggregates events from multiple sources (GOAP events, log events) and provides
/// a unified stream for real-time monitoring and LLM agent consumption.
/// </summary>
public sealed class EventAggregationService : BackgroundService, IGoapEventListener
{
    private readonly ILogger<EventAggregationService> logger;
    private readonly LoggerSink loggerSink;
    private readonly Channel<AggregatedEvent> eventChannel;

    // Hold references to all GOAP goals to maintain event subscriptions
    private readonly List<GoapGoal> subscribedGoals = new();

    public EventAggregationService(
        ILogger<EventAggregationService> logger,
        LoggerSink loggerSink,
        IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.loggerSink = loggerSink;

        // Unbounded channel for event streaming (backpressure handled by SSE)
        eventChannel = Channel.CreateUnbounded<AggregatedEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        // Subscribe to LoggerSink events
        loggerSink.OnLogChanged += HandleLogChanged;

        // Subscribe to all GOAP goals' events
        GoapGoal[] goals = serviceProvider.GetServices<GoapGoal>().ToArray();
        foreach (GoapGoal goal in goals)
        {
            goal.GoapEvent += HandleGoapEvent;
            subscribedGoals.Add(goal);
        }

        logger.LogInformation("[EventAggregation ] Subscribed to {GoalCount} GOAP goals", goals.Length);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[EventAggregation ] Event aggregation service started");

        // Keep service alive until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override void Dispose()
    {
        // Unsubscribe from events
        loggerSink.OnLogChanged -= HandleLogChanged;

        foreach (GoapGoal goal in subscribedGoals)
        {
            goal.GoapEvent -= HandleGoapEvent;
        }

        subscribedGoals.Clear();
        eventChannel.Writer.Complete();

        base.Dispose();
    }

    /// <summary>
    /// Streams aggregated events as they occur. Multiple consumers supported.
    /// </summary>
    public IAsyncEnumerable<AggregatedEvent> StreamEventsAsync(CancellationToken cancellationToken = default)
    {
        return eventChannel.Reader.ReadAllAsync(cancellationToken);
    }

    // IGoapEventListener implementation
    public void OnGoapEvent(GoapEventArgs e)
    {
        AggregatedEvent evt = new(
            Timestamp: DateTimeOffset.UtcNow,
            Source: "GOAP",
            Type: e.GetType().Name,
            Data: e,
            Severity: GetGoapEventSeverity(e)
        );

        eventChannel.Writer.TryWrite(evt);
    }

    // Event handler adapter for GoapGoal.GoapEvent delegate
    private void HandleGoapEvent(GoapEventArgs e) => OnGoapEvent(e);

    private void HandleLogChanged()
    {
        // Get the most recent log event from the ring buffer
        // Capture head index locally to avoid race with concurrent writers
        int head = loggerSink.Head;
        LogEvent? logEvent = loggerSink.Log[head];
        if (logEvent == null)
            return;

        AggregatedEvent evt = new(
            Timestamp: logEvent.Timestamp,
            Source: "Log",
            Type: logEvent.Level.ToString(),
            Data: new LogEventData(
                logEvent.MessageTemplate.Text,
                logEvent.Level,
                logEvent.Exception?.GetType().Name
            ),
            Severity: MapLogLevelToSeverity(logEvent.Level)
        );

        eventChannel.Writer.TryWrite(evt);
    }

    private static string GetGoapEventSeverity(GoapEventArgs e)
    {
        return e switch
        {
            AbortEvent => "Warning",
            CorpseEvent => "Warning",
            _ when e.GetType().Name.Contains("Error") => "Error",
            _ when e.GetType().Name.Contains("Failed") => "Error",
            _ => "Info"
        };
    }

    private static string MapLogLevelToSeverity(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Fatal => "Critical",
            LogEventLevel.Error => "Error",
            LogEventLevel.Warning => "Warning",
            LogEventLevel.Information => "Info",
            LogEventLevel.Debug => "Debug",
            LogEventLevel.Verbose => "Trace",
            _ => "Info"
        };
    }
}

/// <summary>
/// Unified event model for streaming to LLM agents and monitoring endpoints.
/// </summary>
public sealed record AggregatedEvent(
    DateTimeOffset Timestamp,
    string Source,
    string Type,
    object Data,
    string Severity
);

/// <summary>
/// Simplified log event data for serialization.
/// </summary>
public sealed record LogEventData(
    string Message,
    LogEventLevel Level,
    string? ExceptionType
);
