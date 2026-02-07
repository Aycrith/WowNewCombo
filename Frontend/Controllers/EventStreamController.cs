using System.Text;
using System.Text.Json;
using Frontend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Frontend.Controllers;

/// <summary>
/// Provides Server-Sent Events (SSE) endpoint for real-time event streaming.
/// Consumes events from EventAggregationService and streams them to clients.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class EventStreamController : ControllerBase
{
    private readonly EventAggregationService eventAggregation;
    private readonly ILogger<EventStreamController> logger;

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public EventStreamController(
        EventAggregationService eventAggregation,
        ILogger<EventStreamController> logger)
    {
        this.eventAggregation = eventAggregation;
        this.logger = logger;
    }

    /// <summary>
    /// GET /api/eventstream/stream
    /// Streams aggregated events as Server-Sent Events.
    /// Format: event: {type}\ndata: {json}\n\n
    /// </summary>
    [HttpGet("stream")]
    public async Task StreamEvents(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // Disable nginx buffering

        string clientId = HttpContext.Connection.Id ?? Guid.NewGuid().ToString();
        logger.LogInformation("[EventStream     ] Client {ClientId} connected", clientId);

        try
        {
            await foreach (AggregatedEvent evt in eventAggregation.StreamEventsAsync(cancellationToken))
            {
                // SSE format: event: {type}\ndata: {json}\n\n
                string eventType = $"{evt.Source}.{evt.Type}";
                string json = JsonSerializer.Serialize(new
                {
                    evt.Timestamp,
                    evt.Source,
                    evt.Type,
                    evt.Severity,
                    Data = SerializeEventData(evt.Data)
                }, jsonOptions);

                string sseMessage = $"event: {eventType}\ndata: {json}\n\n";
                byte[] bytes = Encoding.UTF8.GetBytes(sseMessage);

                await Response.Body.WriteAsync(bytes, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("[EventStream     ] Client {ClientId} disconnected", clientId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[EventStream     ] Error streaming to client {ClientId}", clientId);
        }
    }

    /// <summary>
    /// GET /api/eventstream/health
    /// Simple health check endpoint for event stream service.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            Service = "EventStream",
            Status = "Running",
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    private static object SerializeEventData(object data)
    {
        return data switch
        {
            // Convert complex GOAP event args to simplified structure
            Core.GOAP.GoapEventArgs goapEvent => new
            {
                EventType = goapEvent.GetType().Name,
                // Add specific properties for known event types
                Properties = ExtractGoapEventProperties(goapEvent)
            },

            LogEventData logData => logData,

            // Fallback: attempt to serialize as-is
            _ => data
        };
    }

    private static Dictionary<string, object?> ExtractGoapEventProperties(Core.GOAP.GoapEventArgs e)
    {
        Dictionary<string, object?> props = new();

        // Use reflection to extract public properties
        foreach (System.Reflection.PropertyInfo prop in e.GetType().GetProperties())
        {
            try
            {
                object? value = prop.GetValue(e);
                props[prop.Name] = value;
            }
            catch
            {
                // Skip properties that throw on access
            }
        }

        return props;
    }
}
