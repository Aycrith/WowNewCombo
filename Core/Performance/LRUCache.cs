using System;
using System.Collections.Generic;

namespace Core.Performance;

public sealed class LRUCache<TKey, TValue> where TKey : notnull
{
    private sealed class Node
    {
        public required TKey Key { get; init; }
        public required TValue Value { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public Node? Prev { get; set; }
        public Node? Next { get; set; }
    }

    private readonly object gate = new();
    private readonly Dictionary<TKey, Node> cache;
    private readonly int capacity;
    private readonly TimeSpan defaultTtl;

    private Node? head;
    private Node? tail;

    private long hits;
    private long misses;

    public LRUCache(int capacity, TimeSpan defaultTtl)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.capacity = capacity;
        this.defaultTtl = defaultTtl;
        cache = new Dictionary<TKey, Node>(capacity);
    }

    public int Capacity => capacity;

    public int Count
    {
        get
        {
            lock (gate)
            {
                return cache.Count;
            }
        }
    }

    public double HitRate
    {
        get
        {
            lock (gate)
            {
                long total = hits + misses;
                return total == 0 ? 0 : (double)hits / total;
            }
        }
    }

    public (long Hits, long Misses) GetStatistics()
    {
        lock (gate)
        {
            return (hits, misses);
        }
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        lock (gate)
        {
            if (!cache.TryGetValue(key, out Node? node))
            {
                misses++;
                value = default!;
                return false;
            }

            if (IsExpired(node, DateTimeOffset.UtcNow))
            {
                RemoveNode(node);
                cache.Remove(key);

                misses++;
                value = default!;
                return false;
            }

            MoveToHead(node);
            hits++;
            value = node.Value;
            return true;
        }
    }

    public void Set(TKey key, TValue value)
    {
        Set(key, value, ttl: null);
    }

    public void Set(TKey key, TValue value, TimeSpan? ttl)
    {
        DateTimeOffset expiresAtUtc = GetExpiresAtUtc(ttl);

        lock (gate)
        {
            if (cache.TryGetValue(key, out Node? existing))
            {
                existing.Value = value;
                existing.ExpiresAtUtc = expiresAtUtc;
                MoveToHead(existing);
                return;
            }

            Node node = new()
            {
                Key = key,
                Value = value,
                ExpiresAtUtc = expiresAtUtc
            };

            cache.Add(key, node);
            AddToHead(node);
            EvictIfNeeded();
        }
    }

    public bool TryRemove(TKey key)
    {
        lock (gate)
        {
            if (!cache.TryGetValue(key, out Node? node))
            {
                return false;
            }

            RemoveNode(node);
            cache.Remove(key);
            return true;
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            cache.Clear();
            head = null;
            tail = null;
        }
    }

    private DateTimeOffset GetExpiresAtUtc(TimeSpan? ttl)
    {
        TimeSpan effective = ttl ?? defaultTtl;
        if (effective <= TimeSpan.Zero)
        {
            return default;
        }

        return DateTimeOffset.UtcNow + effective;
    }

    private static bool IsExpired(Node node, DateTimeOffset nowUtc)
    {
        DateTimeOffset expiresAt = node.ExpiresAtUtc;
        return expiresAt != default && nowUtc >= expiresAt;
    }

    private void EvictIfNeeded()
    {
        while (cache.Count > capacity && tail != null)
        {
            Node evict = tail;
            RemoveNode(evict);
            cache.Remove(evict.Key);
        }
    }

    private void MoveToHead(Node node)
    {
        if (head == node)
        {
            return;
        }

        RemoveNode(node);
        AddToHead(node);
    }

    private void AddToHead(Node node)
    {
        node.Prev = null;
        node.Next = head;

        if (head != null)
        {
            head.Prev = node;
        }

        head = node;
        tail ??= node;
    }

    private void RemoveNode(Node node)
    {
        Node? prev = node.Prev;
        Node? next = node.Next;

        if (prev != null)
        {
            prev.Next = next;
        }
        else
        {
            head = next;
        }

        if (next != null)
        {
            next.Prev = prev;
        }
        else
        {
            tail = prev;
        }

        node.Prev = null;
        node.Next = null;
    }
}

