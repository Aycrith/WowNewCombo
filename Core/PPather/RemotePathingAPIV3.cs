using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Core.PPather;
using Core.Database;
using System.Threading;
using AnTCP.Client;
using SharedLib;
using System.Numerics;

namespace Core
{
    public sealed class RemotePathingAPIV3 : IPPather, IDisposable
    {
        public enum EMessageType
        {
            PATH,
            MOVE_ALONG_SURFACE,
            RANDOM_POINT,
            RANDOM_POINT_AROUND,
            CAST_RAY,
            RANDOM_PATH
        }
        public enum PathRequestFlags : int
        {
            NONE = 0,
            CHAIKIN = 1,
            CATMULLROM = 2,
            FIND_LOCATION = 4
        };


        private readonly ILogger logger;
        private readonly WorldMapAreaDB worldMapAreaDB;
        private readonly bool debug = false;

        // TODO remove this
        private int watchdogPollMs = 1000;

        private List<LineArgs> lineArgs = new List<LineArgs>();

        private AnTcpClient Client { get; }
        private Thread ConnectionWatchdog { get; }

        private readonly CancellationTokenSource cts;

        public RemotePathingAPIV3(ILogger logger, string ip, int port, WorldMapAreaDB worldMapAreaDB)
        {
            this.logger = logger;
            this.worldMapAreaDB = worldMapAreaDB;

            cts = new CancellationTokenSource();

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

        public ValueTask DrawLines()
        {
            return DrawLines(lineArgs);
        }

        public ValueTask DrawSphere(SphereArgs args)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<List<Vector3>> FindRoute(int uiMapId, Vector3 fromPoint, Vector3 toPoint)
        {
            if (!Client.IsConnected)
            {
                return new ValueTask<List<Vector3>>();
            }

            try
            {
                Vector3 start = worldMapAreaDB.GetWorldLocation(uiMapId, fromPoint, true);
                Vector3 end = worldMapAreaDB.GetWorldLocation(uiMapId, toPoint, true);

                var result = new List<Vector3>();

                if (!worldMapAreaDB.TryGet(uiMapId, out WorldMapArea area))
                    return new ValueTask<List<Vector3>>(result);

                // incase haven't asked a pathfinder for a route this value will be 0
                // that case use the highest location
                if (start.Z == 0)
                {
                    start.Z = area.LocTop / 2;
                    end.Z = area.LocTop / 2;
                }

                if (debug)
                    LogInformation($"Finding route from {fromPoint}({start}) map {uiMapId} to {toPoint}({end}) map {uiMapId}...");

                var path = Client.Send((byte)EMessageType.PATH, (area.MapID, PathRequestFlags.FIND_LOCATION | PathRequestFlags.CATMULLROM, start, end)).AsArray<Vector3>();
                if (path.Length == 1 && path[0] == Vector3.Zero)
                    return new ValueTask<List<Vector3>>(result);

                for (int i = 0; i < path.Length; i++)
                {
                    if (debug)
                        LogInformation($"new float[] {{ {path[i].X}f, {path[i].Y}f, {path[i].Z}f }},");

                    Vector3 point = worldMapAreaDB.ToAreaLoc(path[i].X, path[i].Y, path[i].Z, area.Continent, uiMapId);
                    result.Add(point);
                }

                return new ValueTask<List<Vector3>>(result);
            }
            catch (Exception ex)
            {
                LogError($"Finding route from {fromPoint} to {toPoint}", ex);
                Console.WriteLine(ex);
                return new ValueTask<List<Vector3>>();
            }
        }


        public bool PingServer()
        {
            CancellationTokenSource cts = new();
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

        private void LogDebug(string text)
        {
            logger.LogDebug($"{nameof(RemotePathingAPIV3)}: {text}");
        }

        private void LogWarning(string text)
        {
            logger.LogWarning($"{nameof(RemotePathingAPIV3)}: {text}");
        }

        #endregion
    }
}