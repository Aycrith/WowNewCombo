using Core.Performance;

using System;

using Xunit;

namespace CoreUnitTests.Performance;

public sealed class LRUCacheTests
{
    [Fact]
    public void Set_ThenTryGetValue_ReturnsValue()
    {
        LRUCache<string, int> cache = new(capacity: 3, defaultTtl: TimeSpan.FromMinutes(1));
        cache.Set("a", 1);

        Assert.True(cache.TryGetValue("a", out int v));
        Assert.Equal(1, v);
    }

    [Fact]
    public void Evicts_LeastRecentlyUsed()
    {
        LRUCache<string, int> cache = new(capacity: 2, defaultTtl: TimeSpan.FromMinutes(1));
        cache.Set("a", 1);
        cache.Set("b", 2);

        Assert.True(cache.TryGetValue("a", out _)); // a becomes MRU, b is LRU
        cache.Set("c", 3); // should evict b

        Assert.False(cache.TryGetValue("b", out _));
        Assert.True(cache.TryGetValue("a", out int a));
        Assert.True(cache.TryGetValue("c", out int c));
        Assert.Equal(1, a);
        Assert.Equal(3, c);
    }

    [Fact]
    public void Expired_Entries_AreNotReturned()
    {
        LRUCache<string, int> cache = new(capacity: 2, defaultTtl: TimeSpan.FromMilliseconds(25));
        cache.Set("a", 1);

        Assert.True(cache.TryGetValue("a", out _));
        System.Threading.Thread.Sleep(60);
        Assert.False(cache.TryGetValue("a", out _));
    }

    [Fact]
    public void HitRate_TracksHitsAndMisses()
    {
        LRUCache<string, int> cache = new(capacity: 2, defaultTtl: TimeSpan.FromMinutes(1));
        cache.Set("a", 1);

        Assert.True(cache.TryGetValue("a", out _));
        Assert.False(cache.TryGetValue("missing", out _));

        (long hits, long misses) = cache.GetStatistics();
        Assert.Equal(1, hits);
        Assert.Equal(1, misses);
        Assert.InRange(cache.HitRate, 0.49, 0.51);
    }

    [Fact]
    public void TryGetValue_DoesNotAllocate_OnRepeatedHits_AfterWarmup()
    {
        LRUCache<int, int> cache = new(capacity: 128, defaultTtl: TimeSpan.FromMinutes(1));
        for (int i = 0; i < 128; i++)
        {
            cache.Set(i, i);
        }

        // Warmup: access all keys once.
        for (int i = 0; i < 128; i++)
        {
            Assert.True(cache.TryGetValue(i, out _));
        }

        long before = GC.GetAllocatedBytesForCurrentThread();

        int sum = 0;
        for (int i = 0; i < 10_000; i++)
        {
            int key = i % 128;
            if (cache.TryGetValue(key, out int v))
            {
                sum += v;
            }
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        _ = sum;

        // Allow a tiny amount of noise; should not allocate per hit.
        Assert.True(after - before < 512);
    }
}
