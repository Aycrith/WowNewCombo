# P1-2: Fix Thread Safety in LLMClientFactory with ConcurrentDictionary<Lazy<T>>

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the double-checked lock pattern in `LLMClientFactory` with `ConcurrentDictionary<string, Lazy<ILLMClient>>` to guarantee that concurrent `CreateClient()` calls never produce duplicate `HttpClient` instances.

**Priority:** P1 — HIGH reliability (race condition in production)

**Estimated time:** 5 minutes

---

## Context

### Current code (`Core/AI/LLM/LLMClientFactory.cs`)

```csharp
private readonly Dictionary<string, ILLMClient> clientCache = new();
private readonly object cacheLock = new();          // line 22

public ILLMClient CreateClient(string providerName) // lines 35-61
{
    // First check (outside lock) — RACE CONDITION:
    // Thread A and Thread B both see empty cache and pass this check simultaneously
    if (clientCache.ContainsKey(providerName))
        return clientCache[providerName];

    lock (cacheLock)
    {
        // Second check (inside lock) — one thread wins the lock
        if (clientCache.ContainsKey(providerName))
            return clientCache[providerName];

        // Both threads could reach here if they both passed the first check
        // before either acquired the lock — but standard double-checked locking
        // should prevent this... UNLESS the first check is on a non-volatile Dictionary
        // which is not thread-safe for concurrent reads.
        ILLMClient client = providerName.ToLowerInvariant() switch
        {
            "openai" => CreateOpenAIClient(),
            "local" or "llama" or "local_llama" => CreateLocalLlamaClient(),
            _ => throw new ArgumentException($"Unknown provider: {providerName}")
        };

        clientCache[providerName] = client;
        return client;
    }
}
```

**The actual race:** `Dictionary<TKey, TValue>` is NOT thread-safe for concurrent reads when a write is happening. The pattern is broken because Thread A could be reading `clientCache.ContainsKey()` while Thread B is writing `clientCache[providerName] = client`. This is undefined behavior in .NET.

**Reference:** https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/

**Correct pattern:** `ConcurrentDictionary<string, Lazy<ILLMClient>>` — the `Lazy<T>` with `ExecutionAndPublication` mode guarantees the factory delegate runs exactly once even if multiple threads race to add the same key.

---

## Files

1. **`C:/WowClassicGrindBot/Core/AI/LLM/LLMClientFactory.cs`** — replace Dictionary+lock with ConcurrentDictionary<Lazy<T>>
2. **Create: `C:/WowClassicGrindBot/CoreUnitTests/AI/LLMClientFactoryTests.cs`** — concurrent safety test

---

## Step 1: Create test file (write failing test first)

```csharp
using Core.AI.LLM;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CoreUnitTests.AI;

public sealed class LLMClientFactoryTests
{
    [Fact]
    public void CreateClient_CalledConcurrently_ReturnsSameInstance()
    {
        // Arrange
        LLMClientFactory factory = CreateFactory();
        ILLMClient[] results = new ILLMClient[16];

        // Act — 16 concurrent threads all request the same provider
        Parallel.For(0, 16, i =>
        {
            results[i] = factory.CreateClient("local");
        });

        // Assert — all 16 calls must return the SAME reference
        ILLMClient expected = results[0];
        foreach (ILLMClient result in results)
        {
            object.ReferenceEquals(result, expected).Should().BeTrue(
                "concurrent CreateClient calls must return identical instances");
        }
    }

    [Fact]
    public void CreateClient_CalledTwiceSequentially_ReturnsSameInstance()
    {
        LLMClientFactory factory = CreateFactory();

        ILLMClient first = factory.CreateClient("local");
        ILLMClient second = factory.CreateClient("local");

        object.ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void CreateClient_UnknownProvider_ThrowsArgumentException()
    {
        LLMClientFactory factory = CreateFactory();

        System.Action act = () => factory.CreateClient("nonexistent_provider_xyz_abc");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*nonexistent_provider_xyz_abc*");
    }

    [Fact]
    public void CreateClient_NullOrEmpty_ThrowsArgumentException()
    {
        LLMClientFactory factory = CreateFactory();

        System.Action actNull = () => factory.CreateClient(null!);
        System.Action actEmpty = () => factory.CreateClient(string.Empty);
        System.Action actWhitespace = () => factory.CreateClient("   ");

        actNull.Should().Throw<ArgumentException>();
        actEmpty.Should().Throw<ArgumentException>();
        actWhitespace.Should().Throw<ArgumentException>();
    }

    private static LLMClientFactory CreateFactory()
    {
        // Read LLMClientFactory constructor to get exact parameter types
        // Typical pattern based on codebase conventions:
        return new LLMClientFactory(
            serviceProvider: null!,
            NullLogger<LLMClientFactory>.Instance,
            Options.Create(new AIProfileGeneratorOptions()));
    }
}
```

**Run to confirm concurrent test currently FAILS (or passes for wrong reasons):**
```bash
dotnet test CoreUnitTests --filter "FullyQualifiedName~LLMClientFactoryTests" --verbosity detailed
```

## Step 2: Refactor LLMClientFactory.cs

**Replace the private fields (lines ~20-22):**
```csharp
// Remove:
private readonly Dictionary<string, ILLMClient> clientCache = new();
private readonly object cacheLock = new();

// Add:
private readonly ConcurrentDictionary<string, Lazy<ILLMClient>> _clientCache =
    new(StringComparer.OrdinalIgnoreCase);
```

**Replace CreateClient() method (lines 35-61):**
```csharp
public ILLMClient CreateClient(string providerName)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

    Lazy<ILLMClient> lazy = _clientCache.GetOrAdd(
        providerName,
        static (key, self) => new Lazy<ILLMClient>(
            () => self.CreateClientCore(key),
            LazyThreadSafetyMode.ExecutionAndPublication),
        this);

    return lazy.Value;
}

private ILLMClient CreateClientCore(string providerName)
{
    return providerName.ToLowerInvariant() switch
    {
        "openai" => CreateOpenAIClient(),
        "local" or "llama" or "local_llama" => CreateLocalLlamaClient(),
        _ => throw new ArgumentException(
            $"Unknown LLM provider: '{providerName}'. Supported: openai, local, llama, local_llama.",
            nameof(providerName))
    };
}
```

**Key points:**
- `GetOrAdd` is atomic for the Lazy wrapper — two threads may both create a `Lazy<T>`, but only one wins and the other is discarded. `Lazy<T>` itself is cheap to construct.
- `LazyThreadSafetyMode.ExecutionAndPublication` ensures `CreateClientCore` runs exactly once per key even under concurrent access.
- `static (key, self) =>` avoids closure allocation; `this` is passed as the factory argument.
- `StringComparer.OrdinalIgnoreCase` matches the original `ToLowerInvariant()` case-insensitive behavior.

**Remove the old `cacheLock` field entirely.**

## Step 3: Check GetDefaultClient() and other methods

`GetDefaultClient()` (lines 64-80) may also reference `clientCache` or `cacheLock`. Update any references.

## Step 4: Run tests
```bash
dotnet test CoreUnitTests --filter "FullyQualifiedName~LLMClientFactoryTests" --verbosity detailed
```
**Expected:** All 4 tests PASS.

## Step 5: Full suite
```bash
dotnet test MasterOfPuppets.sln --verbosity minimal
```

## Step 6: Commit
```bash
git add Core/AI/LLM/LLMClientFactory.cs CoreUnitTests/AI/LLMClientFactoryTests.cs
git commit -m "fix(ai): replace racy double-checked lock with ConcurrentDictionary<Lazy<T>> in LLMClientFactory"
```

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `Lazy<T>` exception propagation surprises | Low | `ExecutionAndPublication` caches exceptions too — if factory throws once, it throws forever. This is correct behavior for misconfigured providers. |
| `static` lambda captures break (IServiceProvider null) | Low | Test with null serviceProvider in test; production uses real SP |
| ConcurrentDictionary overhead vs Dictionary | Very Low | ConcurrentDictionary is ~10% slower than Dictionary for single-thread, but correct and safe |
