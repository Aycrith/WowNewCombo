using System;
using System.Threading;

using Microsoft.Extensions.Logging;

#pragma warning disable 162

namespace Core.Goals;

public sealed class CastingHandlerInterruptWatchdog : IDisposable
{
    private const bool Log = false;

    private readonly ILogger<CastingHandlerInterruptWatchdog> logger;
    private readonly Wait wait;

    private readonly Thread thread;
    private readonly CancellationTokenSource threadCts;
    private readonly ManualResetEventSlim resetEvent;

    private bool? initial;
    private Func<bool>? interrupt;

    private CancellationTokenSource cts;

    public CastingHandlerInterruptWatchdog(
        ILogger<CastingHandlerInterruptWatchdog> logger, Wait wait)
    {
        this.logger = logger;
        this.wait = wait;

        threadCts = new();
        resetEvent = new(false);

        cts = new();

        thread = new(Watchdog);
        thread.Start();
    }

    public void Dispose()
    {
        interrupt = null;

        threadCts.Cancel();
        resetEvent.Set();
    }

    private void Watchdog()
    {
        while (!threadCts.IsCancellationRequested)
        {
            while (initial == interrupt?.Invoke())
            {
                wait.Update();
                resetEvent.Wait();
            }

            cts.Cancel();

            if (Log)
            {
                logger.LogWarning("Interrupted! Waiting...");
            }

            resetEvent.Reset();
            resetEvent.Wait();
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Thread stopped!");
    }

    public CancellationToken Set(Func<bool> interrupt)
    {
        resetEvent.Reset();

        this.initial = interrupt();
        this.interrupt = interrupt;

        if (!cts.TryReset())
        {
            cts.Dispose();
            cts = new();

            if (Log)
                logger.LogInformation("New cts");
        }
        else if (Log)
            logger.LogInformation("Reuse cts");

        resetEvent.Set();

        return cts.Token;
    }
}
