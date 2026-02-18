using System.Threading;

using Game.Input.Security;

using Xunit;

namespace CoreUnitTests.Input;

/// <summary>
/// Unit tests for KeyRepeatTimer.
/// </summary>
public class KeyRepeatTimerTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var timer = new KeyRepeatTimer(
            windowHandle: new nint(0x1234),
            initialDelayMs: 300,
            intervalMs: 50);

        Assert.NotNull(timer);
        Assert.False(timer.IsRunning);

        timer.Dispose();
    }

    [Fact]
    public void Start_SetsIsRunning()
    {
        using var timer = new KeyRepeatTimer(
            windowHandle: new nint(0x1234),
            initialDelayMs: 500, // Long delay so we can check state
            intervalMs: 50);

        timer.Start(virtualKey: 0x57, extended: false); // 'W' key

        Assert.True(timer.IsRunning);

        timer.Stop();
    }

    [Fact]
    public void Stop_ClearsIsRunning()
    {
        using var timer = new KeyRepeatTimer(
            windowHandle: new nint(0x1234),
            initialDelayMs: 500,
            intervalMs: 50);

        timer.Start(virtualKey: 0x57, extended: false);
        Assert.True(timer.IsRunning);

        timer.Stop();
        Assert.False(timer.IsRunning);
    }

    [Fact]
    public void Start_NewKey_StopsPrevious()
    {
        using var timer = new KeyRepeatTimer(
            windowHandle: new nint(0x1234),
            initialDelayMs: 500,
            intervalMs: 50);

        timer.Start(virtualKey: 0x57, extended: false); // 'W'
        Assert.True(timer.IsRunning);

        // Starting a new key should seamlessly transition
        timer.Start(virtualKey: 0x41, extended: false); // 'A'
        Assert.True(timer.IsRunning);
    }

    [Fact]
    public void Dispose_StopsTimer()
    {
        var timer = new KeyRepeatTimer(
            windowHandle: new nint(0x1234),
            initialDelayMs: 500,
            intervalMs: 50);

        timer.Start(virtualKey: 0x57, extended: false);
        Assert.True(timer.IsRunning);

        timer.Dispose();
        Assert.False(timer.IsRunning);
    }

    [Fact]
    public void IsRunning_ThreadSafe_AccessibleFromMultipleThreads()
    {
        using var timer = new KeyRepeatTimer(
            windowHandle: new nint(0x1234),
            initialDelayMs: 500,
            intervalMs: 50);

        timer.Start(virtualKey: 0x57, extended: false);

        // Access from multiple threads should not throw
        var threads = new Thread[4];
        var results = new bool[4];

        for (int i = 0; i < threads.Length; i++)
        {
            int index = i;
            threads[i] = new Thread(() => results[index] = timer.IsRunning);
            threads[i].Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        // All threads should have observed the same state
        Assert.All(results, r => Assert.True(r));

        timer.Stop();
    }
}
