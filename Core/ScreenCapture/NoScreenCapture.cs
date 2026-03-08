using Microsoft.Extensions.Logging;

using System;

namespace Core;

public sealed class NoScreenCapture : ScreenCaptureCleaner
{
    public NoScreenCapture(ILogger logger, DataConfig dataConfig)
        : base(logger, dataConfig) { }

    public override void Request() { }

    public override ScreenCaptureResult Capture(string reason, string? correlationId = null, string? incidentId = null, int timeoutMs = 1500)
    {
        return new ScreenCaptureResult
        {
            CorrelationId = correlationId ?? string.Empty,
            IncidentId = incidentId ?? string.Empty,
            Reason = reason,
            RequestedUtc = DateTimeOffset.UtcNow,
            CompletedUtc = DateTimeOffset.UtcNow,
            Success = false,
            Error = "Screen capture is disabled."
        };
    }
}
