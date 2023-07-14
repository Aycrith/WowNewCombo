using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Core;

public sealed class Wait
{
    private readonly AutoResetEvent globalTime;
    private readonly CancellationToken ct;

    public Wait(AutoResetEvent globalTime, CancellationTokenSource cts)
    {
        this.globalTime = globalTime;
        this.ct = cts.Token;
    }

    public void Update()
    {
        globalTime.WaitOne();
    }

    public bool Update(int timeout)
    {
        return globalTime.WaitOne(timeout);
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
    public float Until(int timeoutMs, Func<bool> interrupt)
    {
        DateTime start = DateTime.UtcNow;
        float elapsedMs;
        while ((elapsedMs = (float)(DateTime.UtcNow - start).TotalMilliseconds) < timeoutMs)
        {
            if (interrupt())
                return elapsedMs;

            Update();
        }

        return -elapsedMs;
    }

    [SkipLocalsInit]
    public float Until(int timeoutMs, CancellationToken token)
    {
        DateTime start = DateTime.UtcNow;
        float elapsedMs;
        while ((elapsedMs = (float)(DateTime.UtcNow - start).TotalMilliseconds) < timeoutMs)
        {
            if (token.IsCancellationRequested)
                return elapsedMs;

            Update();
        }

        return -elapsedMs;
    }

    [SkipLocalsInit]
    public float Until(int timeoutMs, Func<bool> interrupt, Action repeat)
    {
        DateTime start = DateTime.UtcNow;
        float elapsedMs;
        while ((elapsedMs = (float)(DateTime.UtcNow - start).TotalMilliseconds) < timeoutMs)
        {
            repeat.Invoke();
            if (interrupt())
                return elapsedMs;

            Update();
        }

        return -elapsedMs;
    }

    [SkipLocalsInit]
    public float AfterEquals<T>(int timeoutMs, int updateCount, Func<T> func, Action? repeat = null)
    {
        DateTime start = DateTime.UtcNow;
        float elapsedMs;
        while ((elapsedMs = (float)(DateTime.UtcNow - start).TotalMilliseconds) < timeoutMs)
        {
            T initial = func();

            repeat?.Invoke();

            for (int i = 0; i < updateCount; i++)
                Update();

            if (EqualityComparer<T>.Default.Equals(initial, func()))
                return elapsedMs;
        }

        return -elapsedMs;
    }

    public void While(Func<bool> condition)
    {
        while (condition())
        {
            Update();
        }
    }
}
