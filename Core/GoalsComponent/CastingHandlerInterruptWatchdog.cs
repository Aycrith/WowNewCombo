using System;
using System.Threading;

using Core.GOAP;

using Microsoft.Extensions.Logging;

using SharedLib;

#pragma warning disable 162

namespace Core.Goals;

public sealed class CastingHandlerInterruptWatchdog : IDisposable
{
    private const bool Log = false;

    private readonly ILogger<CastingHandlerInterruptWatchdog> logger;
    private readonly Wait wait;
    private readonly CancellationToken token;

    private readonly Thread thread;
    private readonly ManualResetEventSlim resetEvent;

    private bool? initial;
    private Func<bool>? interrupt;

    private CancellationTokenSource interruptCts;

    public CastingHandlerInterruptWatchdog(
        ILogger<CastingHandlerInterruptWatchdog> logger, Wait wait,
        CancellationTokenSource<GoapAgent> cts)
    {
        this.logger = logger;
        this.wait = wait;
        this.token = cts.Token;

        resetEvent = new(false);

        interruptCts = new();

        thread = new(Watchdog);
        thread.Start();
    }

    public void Dispose()
    {
        interrupt = null;
        resetEvent.Set();
    }

    private void Watchdog()
    {
        while (!token.IsCancellationRequested)
        {
            while (initial == interrupt?.Invoke())
            {
                wait.Update();
                resetEvent.Wait();
            }

            interruptCts.Cancel();

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

        if (!interruptCts.TryReset())
        {
            interruptCts.Dispose();
            interruptCts = new();

            if (Log)
                logger.LogDebug("New cts");
        }
        else if (Log)
            logger.LogDebug("Reuse cts");

        resetEvent.Set();

        return interruptCts.Token;
    }
}
