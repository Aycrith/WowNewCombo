using Core.Resilience;

using Microsoft.Extensions.Logging;

using PPather.Data;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Core;

public sealed class HybridPather : IPPather, IDisposable
{
    private readonly ILogger<HybridPather> logger;
    private readonly RemotePathingAPIV3 remote;
    private readonly IPPather fallback;
    private readonly CircuitBreaker<Vector3[]>? circuitBreaker;

    private DateTimeOffset lastRemoteUnavailableLog;
    private DateTimeOffset lastRemoteNoPathLog;
    private int remoteUnavailableFallbackCount;
    private int remoteNoPathFallbackCount;
    private static readonly TimeSpan LogThrottleInterval = TimeSpan.FromSeconds(60);

    public bool IsRemoteConnected => remote.IsConnected;

    public HybridPather(ILogger<HybridPather> logger, RemotePathingAPIV3 remote, IPPather fallback,
        CircuitBreaker<Vector3[]>? circuitBreaker = null)
    {
        this.logger = logger;
        this.remote = remote;
        this.fallback = fallback;
        this.circuitBreaker = circuitBreaker;
    }

    public Vector3[] FindMapRoute(int uiMap, Vector3 mapFrom, Vector3 mapTo)
    {
        if (circuitBreaker != null)
        {
            Vector3[] result = circuitBreaker.Execute(() =>
            {
                if (!remote.IsConnected)
                    throw new InvalidOperationException("Remote navmesh not connected");

                Vector3[] path = remote.FindMapRoute(uiMap, mapFrom, mapTo);
                if (path.Length == 0)
                    throw new InvalidOperationException("Remote navmesh returned empty path");

                return path;
            });

            // CB fallback returns empty array — fall through to local pather
            if (result.Length == 0)
            {
                WarnRemoteUnavailableOnce();
                return fallback.FindMapRoute(uiMap, mapFrom, mapTo);
            }

            return result;
        }

        // Non-circuit-breaker path (legacy behavior)
        if (remote.IsConnected)
        {
            Vector3[] path = remote.FindMapRoute(uiMap, mapFrom, mapTo);
            if (path.Length != 0)
            {
                return path;
            }

            Vector3[] fallbackPath = fallback.FindMapRoute(uiMap, mapFrom, mapTo);
            if (fallbackPath.Length != 0)
            {
                WarnRemoteReturnedNoPathOnce();
                return fallbackPath;
            }

            return path;
        }

        WarnRemoteUnavailableOnce();
        return fallback.FindMapRoute(uiMap, mapFrom, mapTo);
    }

    public Vector3[] FindWorldRoute(int uiMap, bool startIndoors, Vector3 worldFrom, Vector3 worldTo)
    {
        if (circuitBreaker != null)
        {
            Vector3[] result = circuitBreaker.Execute(() =>
            {
                if (!remote.IsConnected)
                    throw new InvalidOperationException("Remote navmesh not connected");

                Vector3[] path = remote.FindWorldRoute(uiMap, startIndoors, worldFrom, worldTo);
                if (path.Length == 0)
                    throw new InvalidOperationException("Remote navmesh returned empty path");

                return path;
            });

            // CB fallback returns empty array — fall through to local pather
            if (result.Length == 0)
            {
                WarnRemoteUnavailableOnce();
                return fallback.FindWorldRoute(uiMap, startIndoors, worldFrom, worldTo);
            }

            return result;
        }

        // Non-circuit-breaker path (legacy behavior)
        if (remote.IsConnected)
        {
            Vector3[] path = remote.FindWorldRoute(uiMap, startIndoors, worldFrom, worldTo);
            if (path.Length != 0)
            {
                return path;
            }

            Vector3[] fallbackPath = fallback.FindWorldRoute(uiMap, startIndoors, worldFrom, worldTo);
            if (fallbackPath.Length != 0)
            {
                WarnRemoteReturnedNoPathOnce();
                return fallbackPath;
            }

            return path;
        }

        WarnRemoteUnavailableOnce();
        return fallback.FindWorldRoute(uiMap, startIndoors, worldFrom, worldTo);
    }

    public ValueTask DrawLines(List<LineArgs> lineArgs)
    {
        return remote.IsConnected
            ? remote.DrawLines(lineArgs)
            : fallback.DrawLines(lineArgs);
    }

    public ValueTask DrawSphere(SphereArgs args)
    {
        return remote.IsConnected
            ? remote.DrawSphere(args)
            : fallback.DrawSphere(args);
    }

    public void Dispose()
    {
        remote.Dispose();
        if (fallback is IDisposable d)
        {
            d.Dispose();
        }
    }

    private void WarnRemoteUnavailableOnce()
    {
        remoteUnavailableFallbackCount++;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - lastRemoteUnavailableLog < LogThrottleInterval)
        {
            return;
        }

        lastRemoteUnavailableLog = now;
        logger.LogWarning(
            "[HybridPather    ] Remote navmesh not connected; using local fallback ({Count} times since last log)",
            remoteUnavailableFallbackCount);
        remoteUnavailableFallbackCount = 0;
    }

    private void WarnRemoteReturnedNoPathOnce()
    {
        remoteNoPathFallbackCount++;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now - lastRemoteNoPathLog < LogThrottleInterval)
        {
            return;
        }

        lastRemoteNoPathLog = now;
        logger.LogWarning(
            "[HybridPather    ] Remote navmesh returned no path; using local fallback ({Count} times since last log)",
            remoteNoPathFallbackCount);
        remoteNoPathFallbackCount = 0;
    }
}
