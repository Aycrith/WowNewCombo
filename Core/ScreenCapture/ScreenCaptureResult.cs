using System;
using System.Threading;

using Core.Autonomy;

namespace Core;

public sealed class ScreenCaptureResult
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string CorrelationId { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTimeOffset RequestedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public double? CaptureLatencyMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }

    public ScreenshotRef ToScreenshotRef()
    {
        return new ScreenshotRef
        {
            RequestId = RequestId,
            CorrelationId = CorrelationId,
            IncidentId = IncidentId,
            Reason = Reason,
            Path = Path,
            RequestedUtc = RequestedUtc,
            CompletedUtc = CompletedUtc,
            CaptureLatencyMs = CaptureLatencyMs,
            Success = Success,
            Error = Error
        };
    }
}

internal sealed class PendingScreenCaptureRequest
{
    public ScreenCaptureResult Result { get; init; } = new();
    public ManualResetEventSlim Completion { get; init; } = new(false);
    public bool WaitForCompletion { get; init; }
}
