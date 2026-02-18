using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Core.Performance;

/// <summary>
/// Interface for objects that can be reset and returned to pool.
/// </summary>
public interface IResettable
{
    /// <summary>
    /// Resets the object to its initial state for reuse.
    /// </summary>
    void Reset();
}

/// <summary>
/// Generic object pool to reduce GC pressure in hot paths.
/// Thread-safe implementation using ConcurrentBag for multi-threaded access.
/// </summary>
/// <typeparam name="T">The type of objects to pool. Must have a parameterless constructor.</typeparam>
/// <remarks>
/// <para>
/// Use this pool for objects that are frequently created and destroyed
/// in performance-critical code paths (e.g., combat loops, navigation updates).
/// </para>
/// <para>
/// Pattern derived from: noisiver/AmeisenBotX/Core/Cache/ObjectCache.cs
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create pool with default capacity
/// var pool = new ObjectPool&lt;MyClass&gt;();
/// 
/// // Rent and use
/// var obj = pool.Rent();
/// try
/// {
///     obj.DoWork();
/// }
/// finally
/// {
///     pool.Return(obj);
/// }
/// </code>
/// </example>
public sealed class ObjectPool<T> where T : class, new()
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly int _maxSize;
    private int _currentSize;
    private long _rentCount;
    private long _returnCount;
    private long _createCount;
    private long _discardCount;

    /// <summary>
    /// Initializes a new object pool with the specified maximum size.
    /// </summary>
    /// <param name="maxSize">Maximum number of objects to retain in pool.</param>
    public ObjectPool(int maxSize = 100)
    {
        _maxSize = maxSize;
    }

    /// <summary>
    /// Current number of objects in the pool.
    /// </summary>
    public int Count => _currentSize;

    /// <summary>
    /// Maximum pool capacity.
    /// </summary>
    public int MaxSize => _maxSize;

    /// <summary>
    /// Total number of Rent() calls.
    /// </summary>
    public long RentCount => Interlocked.Read(ref _rentCount);

    /// <summary>
    /// Total number of Return() calls.
    /// </summary>
    public long ReturnCount => Interlocked.Read(ref _returnCount);

    /// <summary>
    /// Number of new objects created (cache miss).
    /// </summary>
    public long CreateCount => Interlocked.Read(ref _createCount);

    /// <summary>
    /// Number of objects discarded due to pool being full.
    /// </summary>
    public long DiscardCount => Interlocked.Read(ref _discardCount);

    /// <summary>
    /// Pool hit rate as a percentage (0-100).
    /// </summary>
    public double HitRate
    {
        get
        {
            long total = RentCount;
            long misses = CreateCount;
            return total == 0 ? 100.0 : (1.0 - (double)misses / total) * 100.0;
        }
    }

    /// <summary>
    /// Rents an object from the pool or creates a new one if pool is empty.
    /// </summary>
    /// <returns>An object ready for use.</returns>
    public T Rent()
    {
        Interlocked.Increment(ref _rentCount);

        if (_pool.TryTake(out T? item))
        {
            Interlocked.Decrement(ref _currentSize);
            return item;
        }

        Interlocked.Increment(ref _createCount);
        return new T();
    }

    /// <summary>
    /// Returns an object to the pool for reuse.
    /// If the pool is full, the object is discarded.
    /// </summary>
    /// <param name="item">The object to return.</param>
    public void Return(T item)
    {
        if (item == null) return;

        Interlocked.Increment(ref _returnCount);

        // Reset if possible
        if (item is IResettable resettable)
            resettable.Reset();

        // Only add if under capacity
        if (Interlocked.Increment(ref _currentSize) <= _maxSize)
        {
            _pool.Add(item);
        }
        else
        {
            // Pool is full, discard
            Interlocked.Decrement(ref _currentSize);
            Interlocked.Increment(ref _discardCount);
        }
    }

    /// <summary>
    /// Clears all objects from the pool.
    /// </summary>
    public void Clear()
    {
        while (_pool.TryTake(out _))
        {
            Interlocked.Decrement(ref _currentSize);
        }
    }

    /// <summary>
    /// Pre-warms the pool by creating the specified number of objects.
    /// </summary>
    /// <param name="count">Number of objects to pre-create.</param>
    public void PreWarm(int count)
    {
        int toCreate = Math.Min(count, _maxSize - _currentSize);
        for (int i = 0; i < toCreate; i++)
        {
            var item = new T();
            if (Interlocked.Increment(ref _currentSize) <= _maxSize)
            {
                _pool.Add(item);
            }
            else
            {
                Interlocked.Decrement(ref _currentSize);
                break;
            }
        }
    }

    /// <summary>
    /// Gets statistics about pool usage.
    /// </summary>
    public ObjectPoolStats GetStats() => new(
        CurrentSize: Count,
        MaxSize: MaxSize,
        RentCount: RentCount,
        ReturnCount: ReturnCount,
        CreateCount: CreateCount,
        DiscardCount: DiscardCount,
        HitRate: HitRate
    );
}

/// <summary>
/// Statistics about object pool usage.
/// </summary>
public record ObjectPoolStats(
    int CurrentSize,
    int MaxSize,
    long RentCount,
    long ReturnCount,
    long CreateCount,
    long DiscardCount,
    double HitRate);

/// <summary>
/// Extension methods for using object pools with using statements.
/// </summary>
public static class ObjectPoolExtensions
{
    /// <summary>
    /// Rents an object and returns a disposable that automatically returns it.
    /// </summary>
    /// <typeparam name="T">The pooled object type.</typeparam>
    /// <param name="pool">The pool to rent from.</param>
    /// <returns>A rental handle that returns the object when disposed.</returns>
    /// <example>
    /// <code>
    /// using var rental = pool.RentDisposable();
    /// rental.Value.DoWork();
    /// // Automatically returned when disposed
    /// </code>
    /// </example>
    public static PooledObjectRental<T> RentDisposable<T>(this ObjectPool<T> pool)
        where T : class, new()
    {
        return new PooledObjectRental<T>(pool, pool.Rent());
    }
}

/// <summary>
/// Disposable wrapper that returns a pooled object when disposed.
/// </summary>
/// <typeparam name="T">The pooled object type.</typeparam>
public readonly struct PooledObjectRental<T> : IDisposable
    where T : class, new()
{
    private readonly ObjectPool<T> _pool;

    /// <summary>
    /// The rented object.
    /// </summary>
    public T Value { get; }

    internal PooledObjectRental(ObjectPool<T> pool, T value)
    {
        _pool = pool;
        Value = value;
    }

    /// <summary>
    /// Returns the object to the pool.
    /// </summary>
    public void Dispose()
    {
        _pool.Return(Value);
    }
}
