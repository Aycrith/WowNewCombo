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

    private bool warnedRemoteUnavailable;
    private bool warnedRemoteReturnedNoPath;

    public HybridPather(ILogger<HybridPather> logger, RemotePathingAPIV3 remote, IPPather fallback)
    {
        this.logger = logger;
        this.remote = remote;
        this.fallback = fallback;
    }

    public Vector3[] FindMapRoute(int uiMap, Vector3 mapFrom, Vector3 mapTo)
    {
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
        if (warnedRemoteUnavailable)
        {
            return;
        }

        warnedRemoteUnavailable = true;
        logger.LogWarning("[HybridPather] Remote navmesh is not connected; using local pathing fallback.");
    }

    private void WarnRemoteReturnedNoPathOnce()
    {
        if (warnedRemoteReturnedNoPath)
        {
            return;
        }

        warnedRemoteReturnedNoPath = true;
        logger.LogWarning("[HybridPather] Remote navmesh returned no path; using local pathing fallback.");
    }
}
