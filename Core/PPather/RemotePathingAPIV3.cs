using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PPather.Data;
using System.Threading;
using AnTCP.Client;
using SharedLib;
using System.Numerics;

#pragma warning disable 162

namespace Core
{
    public sealed class RemotePathingAPIV3 : IPPather, IDisposable
    {
        private const bool debug = false;
        private const int watchdogPollMs = 1000;

        public enum EMessageType
        {
            PATH,
            MOVE_ALONG_SURFACE,
            RANDOM_POINT,
            RANDOM_POINT_AROUND,
            CAST_RAY,
            RANDOM_PATH
        }

        public enum PathRequestFlags
        {
            NONE = 0,
            CHAIKIN = 1,
            CATMULLROM = 2,
            FIND_LOCATION = 4
        };

        private readonly ILogger logger;
        private readonly WorldMapAreaDB worldMapAreaDB;

        private readonly AnTcpClient Client;
        private readonly Thread ConnectionWatchdog;
        private readonly CancellationTokenSource cts;

        public RemotePathingAPIV3(ILogger logger, string ip, int port, WorldMapAreaDB worldMapAreaDB)
        {
            this.logger = logger;
            this.worldMapAreaDB = worldMapAreaDB;

            cts = new();

            Client = new AnTcpClient(ip, port);
            ConnectionWatchdog = new Thread(ObserveConnection);
            ConnectionWatchdog.Start();
        }

        public void Dispose()
        {
            RequestDisconnect();
        }

        #region old

        public ValueTask DrawLines(List<LineArgs> lineArgs)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask DrawSphere(SphereArgs args)
        {
            return ValueTask.CompletedTask;
        }

        public Vector3[] FindMapRoute(int uiMap, Vector3 mapFrom, Vector3 mapTo)
        {
            if (!Client.IsConnected)
                return Array.Empty<Vector3>();

            if (!worldMapAreaDB.TryGet(uiMap, out WorldMapArea area))
                return Array.Empty<Vector3>();

            try
            {
                Vector3 worldFrom = worldMapAreaDB.ToWorld_FlipXY(uiMap, mapFrom);
                Vector3 worldTo = worldMapAreaDB.ToWorld_FlipXY(uiMap, mapTo);

                // incase haven't asked a pathfinder for a route this value will be 0
                // that case use the highest location
                if (worldFrom.Z == 0)
                {
                    worldFrom.Z = area.LocTop / 2;
                    worldTo.Z = area.LocTop / 2;
                }

                if (debug)
                    LogInformation($"Finding map route from {mapFrom}({worldFrom}) map {uiMap} to {mapTo}({worldTo}) map {uiMap}...");

                Vector3[] path = Client.Send((byte)EMessageType.PATH,
                    (area.MapID, PathRequestFlags.FIND_LOCATION | PathRequestFlags.CATMULLROM,
                    worldFrom.X, worldFrom.Y, worldFrom.Z, worldTo.X, worldTo.Y, worldTo.Z)).AsArray<Vector3>();

                if (path.Length == 1 && path[0] == Vector3.Zero)
                    return Array.Empty<Vector3>();

                for (int i = 0; i < path.Length; i++)
                {
                    if (debug)
                        LogInformation($"new float[] {{ {path[i].X}f, {path[i].Y}f, {path[i].Z}f }},");

                    path[i] = worldMapAreaDB.ToMap_FlipXY(path[i], area.MapID, uiMap);
                }

                return path;
            }
            catch (Exception ex)
            {
                LogError($"Finding map route from {mapFrom} to {mapTo}", ex);
                Console.WriteLine(ex);
                return Array.Empty<Vector3>();
            }
        }

        public Vector3[] FindWorldRoute(int uiMap, Vector3 worldFrom, Vector3 worldTo)
        {
            if (!Client.IsConnected)
                return Array.Empty<Vector3>();

            if (!worldMapAreaDB.TryGet(uiMap, out WorldMapArea area))
                return Array.Empty<Vector3>();

            try
            {
                // incase haven't asked a pathfinder for a route this value will be 0
                // that case use the highest location
                if (worldFrom.Z == 0)
                {
                    worldFrom.Z = area.LocTop / 2;
                    worldTo.Z = area.LocTop / 2;
                }

                if (debug)
                    LogInformation($"Finding world route from {worldFrom}({worldFrom}) map {uiMap} to {worldTo}({worldTo}) map {uiMap}...");

                Vector3[] path = Client.Send((byte)EMessageType.PATH,
                    (area.MapID, PathRequestFlags.FIND_LOCATION | PathRequestFlags.CATMULLROM,
                    worldFrom.X, worldFrom.Y, worldFrom.Z, worldTo.X, worldTo.Y, worldTo.Z)).AsArray<Vector3>();

                if (path.Length == 1 && path[0] == Vector3.Zero)
                    return Array.Empty<Vector3>();

                return path;
            }
            catch (Exception ex)
            {
                LogError($"Finding world route from {worldFrom} to {worldTo}", ex);
                Console.WriteLine(ex);
                return Array.Empty<Vector3>();
            }
        }


        public bool PingServer()
        {
            using CancellationTokenSource cts = new();
            cts.CancelAfter(2 * watchdogPollMs);

            while (!cts.IsCancellationRequested)
            {
                if (Client.IsConnected)
                {
                    break;
                }
                cts.Token.WaitHandle.WaitOne(1);
            }

            return Client.IsConnected;
        }

        private void RequestDisconnect()
        {
            cts.Cancel();
            if (Client.IsConnected)
            {
                Client.Disconnect();
            }
        }

        #endregion old

        private void ObserveConnection()
        {
            while (!cts.IsCancellationRequested)
            {
                if (!Client.IsConnected)
                {
                    try
                    {
                        Client.Connect();
                    }
                    catch (Exception ex)
                    {
                        LogError(ex.Message, ex);
                        // ignored, will happen when we cant connect
                    }
                }

                cts.Token.WaitHandle.WaitOne(watchdogPollMs);
            }
        }

        #region Logging

        private void LogError(string text, Exception? ex = null)
        {
            logger.LogError($"{nameof(RemotePathingAPIV3)}: {text}", ex);
        }

        private void LogInformation(string text)
        {
            logger.LogInformation($"{nameof(RemotePathingAPIV3)}: {text}");
        }

        #endregion
    }
}