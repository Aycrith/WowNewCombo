using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Core;

public sealed class Wait
{
    private readonly ManualResetEventSlim globalTime;
    private readonly CancellationToken ct;

    public Wait(ManualResetEventSlim globalTime, CancellationTokenSource cts)
    {
        this.globalTime = globalTime;
        this.ct = cts.Token;
    }

    public void Update()
    {
        globalTime.Wait();
    }

    public void Fixed(int durationMs)
    {
        ct.WaitHandle.WaitOne(durationMs);
    }

    [SkipLocalsInit]
    public bool Till(int timeoutMs, Func<bool> interrupt)
    {
        DateTime start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            if (interrupt())
                return false;

            Update();
        }

        return true;
    }

    [SkipLocalsInit]
    public WaitResult Until(int timeoutMs, Func<bool> interrupt)
    {
        DateTime start = DateTime.UtcNow;
        double elapsedMs;
        while ((elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds) < timeoutMs)
        {
            if (interrupt())
                return new(false, elapsedMs);

            Update();
        }

        return new(true, elapsedMs);
    }

    [SkipLocalsInit]
    public WaitResult Until(int timeoutMs, CancellationToken token)
    {
        DateTime start = DateTime.UtcNow;
        double elapsedMs;
        while ((elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds) < timeoutMs)
        {
            if (token.IsCancellationRequested)
                return new(false, elapsedMs);

            Update();
        }

        return new(true, elapsedMs);
    }

    [SkipLocalsInit]
    public WaitResult Until(int timeoutMs, Func<bool> interrupt, Action repeat)
    {
        DateTime start = DateTime.UtcNow;
        double elapsedMs;
        while ((elapsedMs = (DateTime.UtcNow - start).TotalMilliseconds) < timeoutMs)
        {
            repeat.Invoke();
            if (interrupt())
                return new(false, elapsedMs);

            Update();
        }

        return new(true, elapsedMs);
    }

    public void While(Func<bool> condition)
    {
        while (condition())
        {
            Update();
        }
    }
}
