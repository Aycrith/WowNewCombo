using Game;

using Microsoft.Extensions.Logging;

using SixLabors.ImageSharp;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Core;

public sealed partial class ScreenCapture : ScreenCaptureCleaner, IDisposable
{
    private readonly ILogger<ScreenCapture> logger;
    private readonly DataConfig dataConfig;
    private readonly IWowScreen screen;
    private readonly CancellationToken token;

    private readonly AutoResetEvent pendingWorkSignal;
    private readonly ConcurrentQueue<PendingScreenCaptureRequest> pendingRequests;
    private readonly Thread thread;

    public ScreenCapture(ILogger<ScreenCapture> logger, DataConfig dataConfig,
        CancellationTokenSource cts, IWowScreen screen)
        : base(logger, dataConfig)
    {
        this.logger = logger;
        this.dataConfig = dataConfig;
        this.token = cts.Token;
        this.screen = screen;

        pendingWorkSignal = new(false);
        pendingRequests = new ConcurrentQueue<PendingScreenCaptureRequest>();
        thread = new(Thread);
        thread.Start();
    }

    public void Dispose()
    {
        pendingWorkSignal.Set();
    }

    private void Thread()
    {
        while (!token.IsCancellationRequested)
        {
            pendingWorkSignal.WaitOne(250);

            while (pendingRequests.TryDequeue(out PendingScreenCaptureRequest? request))
            {
                try
                {
                    string fileName = BuildFileName(request.Result.Reason, request.Result.RequestId);
                    string filePath = Path.Join(dataConfig.Screenshot, fileName);
                    LogScreenCapture(logger, fileName);

                    screen.ScreenImage.SaveAsJpeg(filePath);

                    DateTimeOffset completedUtc = DateTimeOffset.UtcNow;
                    request.Result.Path = filePath;
                    request.Result.CompletedUtc = completedUtc;
                    request.Result.CaptureLatencyMs = Math.Round((completedUtc - request.Result.RequestedUtc).TotalMilliseconds, 2);
                    request.Result.Success = true;
                }
                catch (Exception ex)
                {
                    DateTimeOffset completedUtc = DateTimeOffset.UtcNow;
                    request.Result.CompletedUtc = completedUtc;
                    request.Result.CaptureLatencyMs = Math.Round((completedUtc - request.Result.RequestedUtc).TotalMilliseconds, 2);
                    request.Result.Success = false;
                    request.Result.Error = ex.Message;
                    logger.LogError(ex, ex.Message);
                }
                finally
                {
                    request.Completion.Set();
                }
            }
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Thread stopped!");
        }
    }

    public override void Request()
    {
        pendingRequests.Enqueue(new PendingScreenCaptureRequest
        {
            Result = new ScreenCaptureResult
            {
                Reason = "GoapEvent",
                RequestedUtc = DateTimeOffset.UtcNow
            }
        });

        pendingWorkSignal.Set();
    }

    public override ScreenCaptureResult Capture(string reason, string? correlationId = null, string? incidentId = null, int timeoutMs = 1500)
    {
        PendingScreenCaptureRequest request = new()
        {
            Result = new ScreenCaptureResult
            {
                CorrelationId = correlationId ?? string.Empty,
                IncidentId = incidentId ?? string.Empty,
                Reason = reason,
                RequestedUtc = DateTimeOffset.UtcNow
            },
            WaitForCompletion = true
        };

        pendingRequests.Enqueue(request);
        pendingWorkSignal.Set();

        if (!request.Completion.Wait(timeoutMs, token))
        {
            DateTimeOffset completedUtc = DateTimeOffset.UtcNow;
            request.Result.CompletedUtc = completedUtc;
            request.Result.CaptureLatencyMs = Math.Round((completedUtc - request.Result.RequestedUtc).TotalMilliseconds, 2);
            request.Result.Success = false;
            request.Result.Error = $"Timed out waiting for screenshot capture after {timeoutMs}ms.";
        }

        return request.Result;
    }

    private static string BuildFileName(string reason, string requestId)
    {
        string safeReason = reason;
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            safeReason = safeReason.Replace(invalid, '_');
        }

        if (string.IsNullOrWhiteSpace(safeReason))
        {
            safeReason = "capture";
        }

        if (safeReason.Length > 32)
        {
            safeReason = safeReason[..32];
        }

        return $"{DateTimeOffset.Now:MM_dd_HH_mm_ss_fff}_{safeReason}_{requestId[..8]}.jpg";
    }

    #region Logging

    [LoggerMessage(
        EventId = 0111,
        Level = LogLevel.Information,
        Message = "{fileName}")]
    static partial void LogScreenCapture(ILogger logger, string fileName);

    #endregion
}
