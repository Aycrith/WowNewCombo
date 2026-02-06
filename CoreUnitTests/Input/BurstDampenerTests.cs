using System.Diagnostics;
using System.Threading;

using Game.Input.Security;

using Xunit;

namespace CoreUnitTests.Input;

/// <summary>
/// Unit tests for BurstDampener.
/// </summary>
public class BurstDampenerTests
{
    [Fact]
    public void CheckAndDampen_RingBufferNotFull_NoDampening()
    {
        BurstDampener dampener = new(windowSize: 8, maxActionsPerSecond: 12.0);
        using ManualResetEvent waitHandle = new(false);

        // First few calls should not trigger dampening
        for (int i = 0; i < 5; i++)
        {
            bool dampened = dampener.CheckAndDampen(waitHandle);
            Assert.False(dampened);
        }
    }

    [Fact]
    public void CheckAndDampen_SlowRate_NoDampening()
    {
        BurstDampener dampener = new(windowSize: 4, maxActionsPerSecond: 10.0);
        using ManualResetEvent waitHandle = new(false);

        // Space out calls to stay under the rate limit
        for (int i = 0; i < 10; i++)
        {
            bool dampened = dampener.CheckAndDampen(waitHandle);
            Assert.False(dampened);
            Thread.Sleep(150); // ~6.7 actions/sec, under the 10/sec limit
        }
    }

    [Fact]
    public void CheckAndDampen_FastRate_TriggersDampening()
    {
        BurstDampener dampener = new(windowSize: 4, maxActionsPerSecond: 10.0);
        using ManualResetEvent waitHandle = new(false);

        // Fill the ring buffer first
        for (int i = 0; i < 4; i++)
        {
            dampener.CheckAndDampen(waitHandle);
            Thread.Sleep(10);
        }

        // Now call rapidly - should trigger dampening
        bool anyDampened = false;
        for (int i = 0; i < 10; i++)
        {
            bool dampened = dampener.CheckAndDampen(waitHandle);
            if (dampened) anyDampened = true;
        }

        Assert.True(anyDampened, "Expected dampening to trigger for rapid calls");
    }

    [Fact]
    public void GetCurrentRate_EmptyBuffer_ReturnsZero()
    {
        BurstDampener dampener = new(windowSize: 8, maxActionsPerSecond: 12.0);

        double rate = dampener.GetCurrentRate();

        Assert.Equal(0, rate);
    }

    [Fact]
    public void GetCurrentRate_AfterCalls_ReturnsReasonableRate()
    {
        BurstDampener dampener = new(windowSize: 4, maxActionsPerSecond: 100.0);
        using ManualResetEvent waitHandle = new(false);

        // Make calls at a known rate
        for (int i = 0; i < 4; i++)
        {
            dampener.CheckAndDampen(waitHandle);
            if (i < 3) Thread.Sleep(100); // ~10 calls/sec
        }

        // Small delay to ensure measurable time has passed
        Thread.Sleep(50);

        double rate = dampener.GetCurrentRate();

        // Rate should be positive (exact value depends on timing)
        Assert.True(rate > 0, $"Expected positive rate, got {rate}");
    }

    [Fact]
    public void Reset_ClearsState()
    {
        BurstDampener dampener = new(windowSize: 4, maxActionsPerSecond: 10.0);
        using ManualResetEvent waitHandle = new(false);

        // Fill the buffer
        for (int i = 0; i < 4; i++)
        {
            dampener.CheckAndDampen(waitHandle);
        }

        // Reset
        dampener.Reset();

        // Should behave like empty buffer again
        double rate = dampener.GetCurrentRate();
        Assert.Equal(0, rate);
    }
}
