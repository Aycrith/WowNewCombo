using Core;
using Core.Resilience;

using Microsoft.Extensions.Logging.Abstractions;

using PPather.Data;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

using Xunit;

namespace CoreUnitTests.PPather;

public sealed class HybridPatherTests
{
    private static readonly Vector3 Origin = Vector3.Zero;
    private static readonly Vector3 Dest = new(100, 100, 0);
    private static readonly Vector3[] ValidPath = [new(10, 10, 0), new(50, 50, 0), new(100, 100, 0)];
    private static readonly Vector3[] FallbackPath = [new(5, 5, 0), new(100, 100, 0)];

    // ──────────────────────────────────────────────
    // Legacy (no circuit breaker) — Remote connected
    // ──────────────────────────────────────────────

    [Fact]
    public void FindMapRoute_Legacy_RemoteConnected_ReturnsRemotePath()
    {
        StubRemotePather remote = new() { Connected = true, MapRouteResult = ValidPath };
        StubPather fallback = new() { MapRouteResult = FallbackPath };
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        Vector3[] result = sut.FindMapRoute(1, Origin, Dest);

        Assert.Same(ValidPath, result);
        Assert.Equal(1, remote.FindMapRouteCalls);
        Assert.Equal(0, fallback.FindMapRouteCalls);
    }

    [Fact]
    public void FindWorldRoute_Legacy_RemoteConnected_ReturnsRemotePath()
    {
        StubRemotePather remote = new() { Connected = true, WorldRouteResult = ValidPath };
        StubPather fallback = new() { WorldRouteResult = FallbackPath };
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        Vector3[] result = sut.FindWorldRoute(1, false, Origin, Dest);

        Assert.Same(ValidPath, result);
        Assert.Equal(1, remote.FindWorldRouteCalls);
        Assert.Equal(0, fallback.FindWorldRouteCalls);
    }

    // ──────────────────────────────────────────────
    // Legacy — Remote connected but returns empty
    // ──────────────────────────────────────────────

    [Fact]
    public void FindMapRoute_Legacy_RemoteReturnsEmpty_FallsBackToLocal()
    {
        StubRemotePather remote = new() { Connected = true, MapRouteResult = [] };
        StubPather fallback = new() { MapRouteResult = FallbackPath };
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        Vector3[] result = sut.FindMapRoute(1, Origin, Dest);

        Assert.Same(FallbackPath, result);
        Assert.Equal(1, remote.FindMapRouteCalls);
        Assert.Equal(1, fallback.FindMapRouteCalls);
    }

    [Fact]
    public void FindWorldRoute_Legacy_RemoteReturnsEmpty_FallsBackToLocal()
    {
        StubRemotePather remote = new() { Connected = true, WorldRouteResult = [] };
        StubPather fallback = new() { WorldRouteResult = FallbackPath };
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        Vector3[] result = sut.FindWorldRoute(1, false, Origin, Dest);

        Assert.Same(FallbackPath, result);
        Assert.Equal(1, remote.FindWorldRouteCalls);
        Assert.Equal(1, fallback.FindWorldRouteCalls);
    }

    // ──────────────────────────────────────────────
    // Legacy — Remote disconnected
    // ──────────────────────────────────────────────

    [Fact]
    public void FindMapRoute_Legacy_RemoteDisconnected_UsesFallback()
    {
        StubRemotePather remote = new() { Connected = false };
        StubPather fallback = new() { MapRouteResult = FallbackPath };
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        Vector3[] result = sut.FindMapRoute(1, Origin, Dest);

        Assert.Same(FallbackPath, result);
        Assert.Equal(0, remote.FindMapRouteCalls);
        Assert.Equal(1, fallback.FindMapRouteCalls);
    }

    [Fact]
    public void FindWorldRoute_Legacy_RemoteDisconnected_UsesFallback()
    {
        StubRemotePather remote = new() { Connected = false };
        StubPather fallback = new() { WorldRouteResult = FallbackPath };
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        Vector3[] result = sut.FindWorldRoute(1, false, Origin, Dest);

        Assert.Same(FallbackPath, result);
        Assert.Equal(0, remote.FindWorldRouteCalls);
        Assert.Equal(1, fallback.FindWorldRouteCalls);
    }

    // ──────────────────────────────────────────────
    // CB path — Remote connected, valid path
    // ──────────────────────────────────────────────

    [Fact]
    public void FindMapRoute_CB_RemoteConnected_ReturnsRemotePath()
    {
        StubRemotePather remote = new() { Connected = true, MapRouteResult = ValidPath };
        StubPather fallback = new() { MapRouteResult = FallbackPath };
        CircuitBreaker<Vector3[]> cb = CreateCB();
        HybridPather sut = CreateSut(remote, fallback, cb);

        Vector3[] result = sut.FindMapRoute(1, Origin, Dest);

        Assert.Same(ValidPath, result);
        Assert.Equal(1, remote.FindMapRouteCalls);
        Assert.Equal(0, fallback.FindMapRouteCalls);
    }

    [Fact]
    public void FindWorldRoute_CB_RemoteConnected_ReturnsRemotePath()
    {
        StubRemotePather remote = new() { Connected = true, WorldRouteResult = ValidPath };
        StubPather fallback = new() { WorldRouteResult = FallbackPath };
        CircuitBreaker<Vector3[]> cb = CreateCB();
        HybridPather sut = CreateSut(remote, fallback, cb);

        Vector3[] result = sut.FindWorldRoute(1, false, Origin, Dest);

        Assert.Same(ValidPath, result);
        Assert.Equal(1, remote.FindWorldRouteCalls);
        Assert.Equal(0, fallback.FindWorldRouteCalls);
    }

    // ──────────────────────────────────────────────
    // CB path — Remote disconnected, CB returns fallback → local pather
    // ──────────────────────────────────────────────

    [Fact]
    public void FindMapRoute_CB_RemoteDisconnected_FallsBackToLocal()
    {
        StubRemotePather remote = new() { Connected = false };
        StubPather fallback = new() { MapRouteResult = FallbackPath };
        CircuitBreaker<Vector3[]> cb = CreateCB();
        HybridPather sut = CreateSut(remote, fallback, cb);

        Vector3[] result = sut.FindMapRoute(1, Origin, Dest);

        Assert.Same(FallbackPath, result);
        Assert.Equal(0, remote.FindMapRouteCalls);
        Assert.Equal(1, fallback.FindMapRouteCalls);
    }

    [Fact]
    public void FindWorldRoute_CB_RemoteDisconnected_FallsBackToLocal()
    {
        StubRemotePather remote = new() { Connected = false };
        StubPather fallback = new() { WorldRouteResult = FallbackPath };
        CircuitBreaker<Vector3[]> cb = CreateCB();
        HybridPather sut = CreateSut(remote, fallback, cb);

        Vector3[] result = sut.FindWorldRoute(1, false, Origin, Dest);

        Assert.Same(FallbackPath, result);
        Assert.Equal(0, remote.FindWorldRouteCalls);
        Assert.Equal(1, fallback.FindWorldRouteCalls);
    }

    // ──────────────────────────────────────────────
    // CB trips open after threshold failures
    // ──────────────────────────────────────────────

    [Fact]
    public void FindMapRoute_CB_TripsAfterThresholdFailures()
    {
        StubRemotePather remote = new() { Connected = true, MapRouteResult = [] };
        StubPather fallback = new() { MapRouteResult = FallbackPath };
        CircuitBreaker<Vector3[]> cb = CreateCB(failureThreshold: 2);
        HybridPather sut = CreateSut(remote, fallback, cb);

        // First two calls fail (empty path → throws in CB delegate → CB records failures)
        sut.FindMapRoute(1, Origin, Dest);
        sut.FindMapRoute(1, Origin, Dest);

        Assert.Equal(CircuitState.Open, cb.State);
        Assert.Equal(2, cb.FailureCount);

        // Third call: CB is open → returns empty array (fallback) without calling remote
        int remoteCallsBefore = remote.FindMapRouteCalls;
        Vector3[] result = sut.FindMapRoute(1, Origin, Dest);

        Assert.Same(FallbackPath, result);
        Assert.Equal(remoteCallsBefore, remote.FindMapRouteCalls); // remote NOT called
    }

    // ──────────────────────────────────────────────
    // CB recovery — after cooldown, successful probe closes circuit
    // ──────────────────────────────────────────────

    [Fact]
    public void FindMapRoute_CB_RecoverAfterCooldown()
    {
        StubRemotePather remote = new() { Connected = true, MapRouteResult = [] };
        StubPather fallback = new() { MapRouteResult = FallbackPath };
        CircuitBreaker<Vector3[]> cb = CreateCB(failureThreshold: 1, cooldownMs: 50);
        HybridPather sut = CreateSut(remote, fallback, cb);

        // Trip the CB
        sut.FindMapRoute(1, Origin, Dest);
        Assert.Equal(CircuitState.Open, cb.State);

        // Wait for cooldown
        System.Threading.Thread.Sleep(100);

        // Now remote returns a valid path
        remote.MapRouteResult = ValidPath;

        Vector3[] result = sut.FindMapRoute(1, Origin, Dest);

        Assert.Same(ValidPath, result);
        Assert.Equal(CircuitState.Closed, cb.State);
    }

    // ──────────────────────────────────────────────
    // DrawLines / DrawSphere delegation
    // ──────────────────────────────────────────────

    [Fact]
    public async Task DrawLines_RemoteConnected_DelegatesToRemote()
    {
        StubRemotePather remote = new() { Connected = true };
        StubPather fallback = new();
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        List<LineArgs> args = [];
        await sut.DrawLines(args);

        Assert.Equal(1, remote.DrawLinesCalls);
        Assert.Equal(0, fallback.DrawLinesCalls);
    }

    [Fact]
    public async Task DrawLines_RemoteDisconnected_DelegatesToFallback()
    {
        StubRemotePather remote = new() { Connected = false };
        StubPather fallback = new();
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        List<LineArgs> args = [];
        await sut.DrawLines(args);

        Assert.Equal(0, remote.DrawLinesCalls);
        Assert.Equal(1, fallback.DrawLinesCalls);
    }

    [Fact]
    public async Task DrawSphere_RemoteConnected_DelegatesToRemote()
    {
        StubRemotePather remote = new() { Connected = true };
        StubPather fallback = new();
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        SphereArgs args = new("test", Vector3.Zero, 0, 0);
        await sut.DrawSphere(args);

        Assert.Equal(1, remote.DrawSphereCalls);
        Assert.Equal(0, fallback.DrawSphereCalls);
    }

    [Fact]
    public async Task DrawSphere_RemoteDisconnected_DelegatesToFallback()
    {
        StubRemotePather remote = new() { Connected = false };
        StubPather fallback = new();
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        SphereArgs args = new("test", Vector3.Zero, 0, 0);
        await sut.DrawSphere(args);

        Assert.Equal(0, remote.DrawSphereCalls);
        Assert.Equal(1, fallback.DrawSphereCalls);
    }

    // ──────────────────────────────────────────────
    // Dispose delegation
    // ──────────────────────────────────────────────

    [Fact]
    public void Dispose_DisposesRemoteAndFallback()
    {
        StubRemotePather remote = new();
        DisposableStubPather fallback = new();
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        sut.Dispose();

        Assert.True(remote.Disposed);
        Assert.True(fallback.Disposed);
    }

    [Fact]
    public void Dispose_NonDisposableFallback_DoesNotThrow()
    {
        StubRemotePather remote = new();
        StubPather fallback = new(); // does not implement IDisposable
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        sut.Dispose(); // should not throw
        Assert.True(remote.Disposed);
    }

    // ──────────────────────────────────────────────
    // IsRemoteConnected
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsRemoteConnected_ReflectsRemoteState(bool connected)
    {
        StubRemotePather remote = new() { Connected = connected };
        StubPather fallback = new();
        HybridPather sut = CreateSut(remote, fallback, circuitBreaker: null);

        Assert.Equal(connected, sut.IsRemoteConnected);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static HybridPather CreateSut(
        IRemotePather remote,
        IPPather fallback,
        CircuitBreaker<Vector3[]>? circuitBreaker)
    {
        return new HybridPather(
            NullLogger<HybridPather>.Instance,
            remote,
            fallback,
            circuitBreaker);
    }

    private static CircuitBreaker<Vector3[]> CreateCB(
        int failureThreshold = 5,
        int cooldownMs = 60_000)
    {
        return new CircuitBreaker<Vector3[]>(
            NullLogger<CircuitBreaker<Vector3[]>>.Instance,
            "TestPathfinding",
            failureThreshold,
            TimeSpan.FromMilliseconds(cooldownMs),
            static () => Array.Empty<Vector3>());
    }

    // ──────────────────────────────────────────────
    // Test doubles
    // ──────────────────────────────────────────────

    private sealed class StubRemotePather : IRemotePather
    {
        public bool Connected { get; set; }
        public Vector3[] MapRouteResult { get; set; } = [];
        public Vector3[] WorldRouteResult { get; set; } = [];
        public bool Disposed { get; private set; }

        public int FindMapRouteCalls { get; private set; }
        public int FindWorldRouteCalls { get; private set; }
        public int DrawLinesCalls { get; private set; }
        public int DrawSphereCalls { get; private set; }

        public bool IsConnected => Connected;

        public Vector3[] FindMapRoute(int uiMap, Vector3 mapFrom, Vector3 mapTo)
        {
            FindMapRouteCalls++;
            return MapRouteResult;
        }

        public Vector3[] FindWorldRoute(int uiMap, bool startIndoors, Vector3 worldFrom, Vector3 worldTo)
        {
            FindWorldRouteCalls++;
            return WorldRouteResult;
        }

        public ValueTask DrawLines(List<LineArgs> lineArgs)
        {
            DrawLinesCalls++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DrawSphere(SphereArgs args)
        {
            DrawSphereCalls++;
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class StubPather : IPPather
    {
        public Vector3[] MapRouteResult { get; set; } = [];
        public Vector3[] WorldRouteResult { get; set; } = [];

        public int FindMapRouteCalls { get; private set; }
        public int FindWorldRouteCalls { get; private set; }
        public int DrawLinesCalls { get; private set; }
        public int DrawSphereCalls { get; private set; }

        public Vector3[] FindMapRoute(int uiMap, Vector3 mapFrom, Vector3 mapTo)
        {
            FindMapRouteCalls++;
            return MapRouteResult;
        }

        public Vector3[] FindWorldRoute(int uiMap, bool startIndoors, Vector3 worldFrom, Vector3 worldTo)
        {
            FindWorldRouteCalls++;
            return WorldRouteResult;
        }

        public ValueTask DrawLines(List<LineArgs> lineArgs)
        {
            DrawLinesCalls++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DrawSphere(SphereArgs args)
        {
            DrawSphereCalls++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DisposableStubPather : IPPather, IDisposable
    {
        public bool Disposed { get; private set; }

        public Vector3[] FindMapRoute(int uiMap, Vector3 mapFrom, Vector3 mapTo) => [];

        public Vector3[] FindWorldRoute(int uiMap, bool startIndoors, Vector3 worldFrom, Vector3 worldTo) => [];

        public ValueTask DrawLines(List<LineArgs> lineArgs) => ValueTask.CompletedTask;

        public ValueTask DrawSphere(SphereArgs args) => ValueTask.CompletedTask;

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
