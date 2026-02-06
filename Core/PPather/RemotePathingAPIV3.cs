using AnTCP.Client;

using Microsoft.Extensions.Logging;

using PPather;
using PPather.Data;

using SharedLib;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable 162

namespace Core;

public sealed class RemotePathingAPIV3 : IPPather, IDisposable
{
    private const bool debug = false;
    private const int watchdogPollMs = 500;
    private const float DefaultZFallback = 64f;
    private const int InitialConnectDelayMs = 2500;
    private const int ConnectBackoffMinMs = 500;
    private const int ConnectBackoffMaxMs = 30_000;

    private const EMessageType TYPE = EMessageType.PATH;
    private const PathRequestFlags FLAGS = PathRequestFlags.SMOOTH_CATMULLROM | PathRequestFlags.VALIDATE_CPOP;

    private enum EMessageType
    {
        PATH,                   // Generate a simple straight path
        MOVE_ALONG_SURFACE,     // Move an entity by small deltas using pathfinding (usefull to prevent falling off edges...)
        RANDOM_POINT,           // Get a random point on the mesh
        RANDOM_POINT_AROUND,    // Get a random point on the mesh in a circle
        CAST_RAY,               // Cast a movement ray to test for obstacles
        RANDOM_PATH,            // Generate a straight path where the nodes get offsetted by a random value
        EXPLORE_POLY,           // Generate a route to explore the polygon (W.I.P)
        CONFIGURE_FILTER,       // Cpnfigure the clients dtQueryFilter area costs
    }

    private enum PathRequestFlags
    {
        NONE = 0,
        SMOOTH_CHAIKIN = 1 << 0,        // Smooth path using Chaikin Curve
        SMOOTH_CATMULLROM = 1 << 1,     // Smooth path using Catmull-Rom Spline
        SMOOTH_BEZIERCURVE = 1 << 2,    // Smooth path using Bezier Curve
        VALIDATE_CPOP = 1 << 3,         // Validate smoothed path using closestPointOnPoly
        VALIDATE_MAS = 1 << 4,          // Validate smoothed path using moveAlongSurface
    };

    private readonly ILogger<RemotePathingAPIV3> logger;
    private readonly WorldMapAreaDB areaDB;

    private readonly AnTcpClient client;
    private readonly Thread connectionWatchdog;
    private readonly CancellationTokenSource cts;

    private readonly IPathVizualizer pathViz;

    private int uiMap;
    private Vector3[] result = Array.Empty<Vector3>();
    private float? zHint;
    private int zHintUiMap;
    private DateTime lastConnectErrorLogUtc;

    public bool IsConnected => client.IsConnected;

    public RemotePathingAPIV3(
        IPathVizualizer pathViz,
        ILogger<RemotePathingAPIV3> logger,
        string ip, int port, WorldMapAreaDB areaDB)
    {
        this.logger = logger;
        this.areaDB = areaDB;
        this.pathViz = pathViz;

        cts = new();

        client = new AnTcpClient(ip, port);
        connectionWatchdog = new Thread(ObserveConnection);
        connectionWatchdog.Start();
    }

    public void Dispose()
    {
        RequestDisconnect();
    }

    public ValueTask DrawLines(List<LineArgs> lineArgs)
    {
        if (pathViz is NoPathVisualizer || result == Array.Empty<Vector3>())
            return ValueTask.CompletedTask;

        StringContent content =
            new(JsonSerializer.Serialize(new DrawMapPathRequest(uiMap, result), pathViz.Options),
            Encoding.UTF8, "application/json");

        pathViz.DrawLines(lineArgs).AsTask().Wait();

        return new(pathViz.Client.PostAsync("DrawMapPath", content));
    }

    public ValueTask DrawSphere(SphereArgs args)
    {
        if (pathViz is NoPathVisualizer)
            return ValueTask.CompletedTask;

        return pathViz.DrawSphere(args);
    }

    public Vector3[] FindMapRoute(int uiMap, Vector3 mapFrom, Vector3 mapTo)
    {
        if (!client.IsConnected ||
            !areaDB.TryGet(uiMap, out WorldMapArea area))
            return result = Array.Empty<Vector3>();

        try
        {
            Vector3 worldFrom = areaDB.ToWorld_FlipXY(uiMap, mapFrom);
            Vector3 worldTo = areaDB.ToWorld_FlipXY(uiMap, mapTo);

            ApplyZHint(uiMap, ref worldFrom, ref worldTo);

            if (debug)
                logger.LogDebug($"Finding map route from {mapFrom}({worldFrom}) map {uiMap} to {mapTo}({worldTo}) map {uiMap}...");

            Vector3[] path = client.Send(
                (byte)TYPE,
                (area.MapID, FLAGS,
                worldFrom.X, worldFrom.Y, worldFrom.Z, worldTo.X, worldTo.Y, worldTo.Z)).AsArray<Vector3>();

            if (path.Length == 1 && path[0] == Vector3.Zero)
            {
                if (TryWithFallbackZ(uiMap, area, ref worldFrom, ref worldTo, out path))
                {
                    // ok
                }
                else
                {
                    return result = Array.Empty<Vector3>();
                }
            }

            for (int i = 0; i < path.Length; i++)
            {
                if (debug)
                    logger.LogDebug($"new float[] {{ {path[i].X}f, {path[i].Y}f, {path[i].Z}f }},");

                path[i] = areaDB.ToMap_FlipXY(path[i], area.MapID, uiMap);
            }

            UpdateZHint(uiMap, path, mapSpace: true, area);
            return result = path;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Finding map route from {mapFrom} to {mapTo}");
            return result = Array.Empty<Vector3>();
        }
    }

    public Vector3[] FindWorldRoute(int uiMap, bool startIndoors, Vector3 worldFrom, Vector3 worldTo)
    {
        if (!client.IsConnected)
            return result = Array.Empty<Vector3>();

        if (!areaDB.TryGet(uiMap, out WorldMapArea area))
            return result = Array.Empty<Vector3>();

        this.uiMap = uiMap;

        try
        {
            ApplyZHint(uiMap, ref worldFrom, ref worldTo);

            if (debug)
                logger.LogDebug($"Finding world route from {worldFrom}({worldFrom}) map {uiMap} to {worldTo}({worldTo}) map {uiMap}...");

            Vector3[] path = client.Send(
                (byte)TYPE,
                (area.MapID, FLAGS,
                worldFrom.X, worldFrom.Y, worldFrom.Z, worldTo.X, worldTo.Y, worldTo.Z)).AsArray<Vector3>();

            if (path.Length == 1 && path[0] == Vector3.Zero)
            {
                if (!TryWithFallbackZ(uiMap, area, ref worldFrom, ref worldTo, out path))
                {
                    return result = Array.Empty<Vector3>();
                }
            }

            UpdateZHint(uiMap, path, mapSpace: false, area);
            return result = path;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Finding world route from {worldFrom} to {worldTo}");
            return result = Array.Empty<Vector3>();
        }
    }

    private void ApplyZHint(int uiMap, ref Vector3 worldFrom, ref Vector3 worldTo)
    {
        if (worldFrom.Z != 0 && worldTo.Z == 0)
        {
            worldTo.Z = worldFrom.Z;
            return;
        }

        if (worldTo.Z != 0 && worldFrom.Z == 0)
        {
            worldFrom.Z = worldTo.Z;
            return;
        }

        if (worldFrom.Z != 0 || worldTo.Z != 0)
        {
            return;
        }

        if (zHint.HasValue && zHintUiMap == uiMap)
        {
            worldFrom.Z = zHint.Value;
            worldTo.Z = zHint.Value;
        }
    }

    private bool TryWithFallbackZ(int uiMap, WorldMapArea area, ref Vector3 worldFrom, ref Vector3 worldTo, out Vector3[] path)
    {
        path = Array.Empty<Vector3>();

        if (!client.IsConnected)
        {
            return false;
        }

        if (worldFrom.Z != 0 || worldTo.Z != 0)
        {
            return false;
        }

        // No Z information is available from the addon (map XY only). Some navmesh queries
        // are sensitive to Z; try a small set of sane fallbacks rather than using unrelated
        // WorldMapArea bounds (LocTop/LocBottom etc are world XY, not height).
        ReadOnlySpan<float> candidates = [
            DefaultZFallback,
            DefaultZFallback * 2,
            DefaultZFallback * 4,
            0f
        ];

        for (int i = 0; i < candidates.Length; i++)
        {
            float z = candidates[i];
            worldFrom.Z = z;
            worldTo.Z = z;

            Vector3[] candidate = client.Send(
                (byte)TYPE,
                (area.MapID, FLAGS,
                worldFrom.X, worldFrom.Y, worldFrom.Z, worldTo.X, worldTo.Y, worldTo.Z)).AsArray<Vector3>();

            if (candidate.Length == 1 && candidate[0] == Vector3.Zero)
            {
                continue;
            }

            path = candidate;
            zHint = candidate[0].Z;
            zHintUiMap = uiMap;
            return true;
        }

        worldFrom.Z = 0;
        worldTo.Z = 0;
        return false;
    }

    private void UpdateZHint(int uiMap, Vector3[] path, bool mapSpace, WorldMapArea area)
    {
        if (path.Length == 0)
        {
            return;
        }

        Vector3 first = path[0];
        if (mapSpace)
        {
            // Convert first map point to world for a stable Z hint
            first = areaDB.ToWorld_FlipXY(uiMap, first);
        }

        if (first.Z == 0)
        {
            return;
        }

        zHint = first.Z;
        zHintUiMap = uiMap;
    }

    public bool PingServer()
    {
        using CancellationTokenSource cts = new();
        cts.CancelAfter(watchdogPollMs);

        while (!cts.IsCancellationRequested)
        {
            if (client.IsConnected)
            {
                break;
            }
            cts.Token.WaitHandle.WaitOne(watchdogPollMs / 10);
        }

        return client.IsConnected;
    }

    private void RequestDisconnect()
    {
        cts.Cancel();
        if (client.IsConnected)
        {
            client.Disconnect();
        }
    }

    private void ObserveConnection()
    {
        int backoffMs = ConnectBackoffMinMs;
        bool delayNextConnectAttempt = true;
        bool wasConnected = client.IsConnected;

        while (!cts.IsCancellationRequested)
        {
            bool isConnected = client.IsConnected;
            if (!isConnected)
            {
                if (wasConnected)
                {
                    delayNextConnectAttempt = true;
                }

                if (delayNextConnectAttempt)
                {
                    cts.Token.WaitHandle.WaitOne(InitialConnectDelayMs);
                    if (cts.IsCancellationRequested)
                    {
                        break;
                    }

                    delayNextConnectAttempt = false;
                }

                try
                {
                    client.Connect();
                    backoffMs = ConnectBackoffMinMs;
                }
                catch (Exception ex)
                {
                    // Avoid log spam if the navigation server is intentionally stopped or starting up.
                    if ((DateTime.UtcNow - lastConnectErrorLogUtc).TotalSeconds >= 30)
                    {
                        lastConnectErrorLogUtc = DateTime.UtcNow;
                        logger.LogError(
                            "[RemotePathingAPIV3] Connect failed: {Message} (retry in {BackoffMs}ms)",
                            ex.Message,
                            backoffMs);
                    }
                    // ignored, will happen when we cant connect
                    backoffMs = Math.Min(backoffMs * 2, ConnectBackoffMaxMs);
                }
            }

            wasConnected = client.IsConnected;

            int waitMs = client.IsConnected ? watchdogPollMs : backoffMs;
            cts.Token.WaitHandle.WaitOne(waitMs);
        }
    }
}
