using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using static System.MathF;

using Microsoft.Extensions.Logging;

using SharedLib.Extensions;
using SharedLib;

#pragma warning disable 162

namespace Core.Goals
{
    internal readonly struct PathRequest
    {
        public readonly int MapId;
        public readonly Vector3 StartW;
        public readonly Vector3 EndW;
        public readonly float Distance;
        public readonly Action<PathResult> Callback;
        public readonly DateTime Time;

        public PathRequest(int mapId, Vector3 startW, Vector3 endW, float distance, Action<PathResult> callback)
        {
            MapId = mapId;
            StartW = startW;
            EndW = endW;
            Distance = distance;
            Callback = callback;
            Time = DateTime.UtcNow;
        }
    }

    internal readonly struct PathResult
    {
        public readonly Vector3 StartW;
        public readonly Vector3 EndW;
        public readonly float Distance;
        public readonly Vector3[] Path;
        public readonly double ElapsedMs;
        public readonly Action<PathResult> Callback;

        public PathResult(in PathRequest request, Vector3[] path, Action<PathResult> callback)
        {
            StartW = request.StartW;
            EndW = request.EndW;
            Distance = request.Distance;
            Path = path;
            Callback = callback;
            ElapsedMs = (DateTime.UtcNow - request.Time).TotalMilliseconds;
        }
    }

    public sealed partial class Navigation : IDisposable
    {
        private const bool debug = false;

        private const float RADIAN = PI * 2;

        private const float DIFF_THRESHOLD = 1.5f;   // within 50% difference
        private const float UNIFORM_DIST_DIV = 2;    // within 50% difference

        private readonly string patherName;

        private readonly ILogger logger;
        private readonly PlayerDirection playerDirection;
        private readonly ConfigurableInput input;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly StuckDetector stuckDetector;
        private readonly IPPather pather;
        private readonly MountHandler mountHandler;

        private const float MinDistanceMount = 25;
        private readonly float MaxDistance = 200;
        private readonly float MinDistance = 5;

        private float AvgDistance;
        private float lastWorldDistance = float.MaxValue;

        private const float minAngleToTurn = PI / 35f;              // 5.14 degree
        private const float minAngleToStopBeforeTurn = PI / 2f;     // 90 degree

        private readonly Stack<Vector3> wayPoints = new();
        private readonly Stack<Vector3> routeToNextWaypoint = new();

        public Vector3[] TotalRoute { private set; get; } = Array.Empty<Vector3>();

        public DateTime LastActive { get; private set; }

        public event Action? OnPathCalculated;
        public event Action? OnWayPointReached;
        public event Action? OnDestinationReached;
        public event Action? OnAnyPointReached;

        public bool SimplifyRouteToWaypoint { get; set; } = true;

        private bool active;
        private Vector3 playerWorldPos;

        private readonly Queue<PathRequest> pathRequests = new(1);
        private readonly Queue<PathResult> pathResults = new(1);

        private readonly CancellationTokenSource _cts;
        private readonly Thread pathfinderThread;
        private readonly ManualResetEvent manualReset;

        private int failedAttempt;
        private Vector3 lastFailedDestination;

        public Navigation(ILogger logger, PlayerDirection playerDirection, ConfigurableInput input, AddonReader addonReader, StopMoving stopMoving, StuckDetector stuckDetector, IPPather pather, MountHandler mountHandler, ClassConfiguration classConfiguration)
        {
            this.logger = logger;
            this.playerDirection = playerDirection;
            this.input = input;
            this.addonReader = addonReader;
            playerReader = addonReader.PlayerReader;
            this.stopMoving = stopMoving;
            this.stuckDetector = stuckDetector;
            this.pather = pather;
            this.mountHandler = mountHandler;

            patherName = pather.GetType().Name;

            AvgDistance = MinDistance;

            manualReset = new(false);
            _cts = new();
            pathfinderThread = new(PathFinderThread);
            pathfinderThread.Start();

            switch (classConfiguration.Mode)
            {
                case Mode.AttendedGather:
                    MaxDistance = MinDistance;
                    SimplifyRouteToWaypoint = false;
                    break;
            }
        }

        public void Dispose()
        {
            manualReset.Set();
            _cts.Cancel();
        }

        public void Update()
        {
            Update(_cts.Token);
        }

        public void Update(CancellationToken ct)
        {
            active = true;

            if (wayPoints.Count == 0 && routeToNextWaypoint.Count == 0)
            {
                OnDestinationReached?.Invoke();
                return;
            }

            while (pathResults.TryDequeue(out PathResult result))
            {
                result.Callback(result);
            }

            if (pathRequests.Count > 0 || pathRequests.Count > 0 ||
                ct.IsCancellationRequested)
            {
                return;
            }

            if (routeToNextWaypoint.Count == 0)
            {
                RefillRouteToNextWaypoint();
                return;
            }

            LastActive = DateTime.UtcNow;
            input.Proc.SetKeyState(input.Proc.ForwardKey, true, true);

            // main loop
            playerWorldPos = playerReader.WorldPos;
            Vector3 targetW = routeToNextWaypoint.Peek();
            float worldDistance = playerWorldPos.WorldDistanceXYTo(targetW);

            Vector3 playerM = WorldMapAreaDB.ToMap_FlipXY(playerWorldPos, playerReader.WorldMapArea);
            Vector3 targetM = WorldMapAreaDB.ToMap_FlipXY(targetW, playerReader.WorldMapArea);
            float heading = DirectionCalculator.CalculateMapHeading(playerM, targetM);

            if (worldDistance < ReachedDistance(MinDistance))
            {
                if (targetW.Z != 0 && targetW.Z != playerWorldPos.Z)
                {
                    playerReader.WorldPosZ = targetW.Z;
                }

                if (SimplifyRouteToWaypoint)
                    ReduceByDistance(MinDistance);
                else
                    routeToNextWaypoint.Pop();

                OnAnyPointReached?.Invoke();

                lastWorldDistance = float.MaxValue;
                UpdateTotalRoute();

                if (routeToNextWaypoint.Count == 0)
                {
                    if (wayPoints.Count > 0)
                    {
                        wayPoints.Pop();
                        UpdateTotalRoute();

                        if (debug)
                            LogDebug($"Reached wayPoint! Distance: {worldDistance} -- Remains: {wayPoints.Count}");

                        OnWayPointReached?.Invoke();
                    }
                }
                else
                {
                    targetW = routeToNextWaypoint.Peek();
                    stuckDetector.SetTargetLocation(targetW);

                    playerM = WorldMapAreaDB.ToMap_FlipXY(playerWorldPos, playerReader.WorldMapArea);
                    targetM = WorldMapAreaDB.ToMap_FlipXY(targetW, playerReader.WorldMapArea);
                    heading = DirectionCalculator.CalculateMapHeading(playerM, targetM);

                    if (!ct.IsCancellationRequested)
                        AdjustHeading(heading, ct);
                    return;
                }
            }

            if (routeToNextWaypoint.Count > 0)
            {
                if (!stuckDetector.IsGettingCloser())
                {
                    if (stuckDetector.ActionDurationMs > 10_000)
                    {
                        LogClearRouteToWaypointStuck(logger, stuckDetector.ActionDurationMs);
                        stuckDetector.Reset();
                        routeToNextWaypoint.Clear();
                        return;
                    }

                    if (HasBeenActiveRecently())
                    {
                        stuckDetector.Update();
                        worldDistance = playerWorldPos.WorldDistanceXYTo(routeToNextWaypoint.Peek());
                    }
                }
                else if (!ct.IsCancellationRequested)
                {
                    AdjustHeading(heading, ct);
                }
            }

            lastWorldDistance = worldDistance;
        }

        public void Resume()
        {
            ResetStuckParameters();

            if (pather is not RemotePathingAPIV3 && routeToNextWaypoint.Count > 0)
            {
                V1_AttemptToKeepRouteToWaypoint();
            }

            int removed = 0;
            while (AdjustNextWaypointPointToClosest() && removed < 5) { removed++; };
            if (removed > 0)
            {
                if (debug)
                    LogDebug($"Resume: removed {removed} waypoint!");
            }
        }

        public void Stop()
        {
            active = false;

            if (pather is RemotePathingAPIV3)
                routeToNextWaypoint.Clear();

            ResetStuckParameters();
        }

        public void StopMovement()
        {
            if (input.Proc.IsKeyDown(input.Proc.ForwardKey))
                input.Proc.SetKeyState(input.Proc.ForwardKey, false, true);
        }

        public bool HasWaypoint()
        {
            return wayPoints.Count != 0;
        }

        public bool HasNext()
        {
            return routeToNextWaypoint.Count != 0;
        }

        public Vector3 NextMapPoint()
        {
            return WorldMapAreaDB.ToMap_FlipXY(routeToNextWaypoint.Peek(), playerReader.WorldMapArea);
        }

        public void SetWayPoints(Vector3[] mapPoints)
        {
            wayPoints.Clear();
            routeToNextWaypoint.Clear();

            float mapDistanceXY = 0;
            Array.Reverse(mapPoints);
            for (int i = 0; i < mapPoints.Length; i++)
            {
                Vector3 worldPos = WorldMapAreaDB.ToWorld_FlipXY(mapPoints[i], playerReader.WorldMapArea);
                if (i > 0)
                {
                    Vector3 last = wayPoints.Peek();
                    mapDistanceXY += worldPos.WorldDistanceXYTo(last);
                }
                wayPoints.Push(worldPos);
            }

            AvgDistance = wayPoints.Count > 1 ? Max(mapDistanceXY / wayPoints.Count, MinDistance) : MinDistance;

            UpdateTotalRoute();
        }

        public void ResetStuckParameters()
        {
            stuckDetector.Reset();
        }

        private void RefillRouteToNextWaypoint()
        {
            routeToNextWaypoint.Clear();

            Vector3 playerW = playerReader.WorldPos;
            Vector3 targetW = wayPoints.Peek();
            float distance = playerW.WorldDistanceXYTo(targetW);

            if (distance > MaxDistance || distance > AvgDistance * 2)
            {
                if (debug)
                    LogDebug($"Distance: {distance} vs Avg:({AvgDistance * 2},{AvgDistance}) - TAVG: {DIFF_THRESHOLD * AvgDistance} ");

                stopMoving.Stop();
                PathRequest(new PathRequest(playerReader.UIMapId.Value, playerW, targetW, distance, PathCalculatedCallback));
            }
            else
            {
                if (debug)
                    LogDebug($"non pathfinder - {distance} - {playerW} -> {targetW}");

                routeToNextWaypoint.Push(targetW);

                Vector3 playerM = WorldMapAreaDB.ToMap_FlipXY(playerW, playerReader.WorldMapArea);
                Vector3 targetM = WorldMapAreaDB.ToMap_FlipXY(targetW, playerReader.WorldMapArea);
                float heading = DirectionCalculator.CalculateMapHeading(playerM, targetM);
                AdjustHeading(heading, _cts.Token);

                stuckDetector.SetTargetLocation(targetW);
                UpdateTotalRoute();
            }
        }

        private void PathRequest(PathRequest pathRequest)
        {
            pathRequests.Enqueue(pathRequest);
            manualReset.Set();
        }

        private void PathCalculatedCallback(PathResult result)
        {
            if (!active)
                return;

            if (result.Path.Length == 0)
            {
                if (lastFailedDestination != result.EndW)
                {
                    lastFailedDestination = result.EndW;
                    LogPathfinderFailed(logger, result.StartW, result.EndW, result.ElapsedMs);
                }

                failedAttempt++;
                if (failedAttempt > 2)
                {
                    failedAttempt = 0;
                    stuckDetector.SetTargetLocation(result.EndW);
                    stuckDetector.Update();
                }
                return;
            }

            failedAttempt = 0;
            LogPathfinderSuccess(logger, result.Distance, result.StartW, result.EndW, result.ElapsedMs);

            Array.Reverse(result.Path);
            for (int i = 0; i < result.Path.Length; i++)
            {
                routeToNextWaypoint.Push(result.Path[i]);
            }

            if (SimplifyRouteToWaypoint)
                SimplyfyRouteToWaypoint();

            if (routeToNextWaypoint.Count == 0)
            {
                routeToNextWaypoint.Push(wayPoints.Peek());

                if (debug)
                    LogDebug($"RefillRouteToNextWaypoint -- WayPoint reached! {wayPoints.Count}");
            }

            stuckDetector.SetTargetLocation(routeToNextWaypoint.Peek());
            UpdateTotalRoute();

            OnPathCalculated?.Invoke();
        }

        private void PathFinderThread()
        {
            while (!_cts.IsCancellationRequested)
            {
                manualReset.Reset();
                if (pathRequests.TryPeek(out PathRequest pathRequest))
                {
                    Vector3[] path = pather.FindWorldRoute(pathRequest.MapId, pathRequest.StartW, pathRequest.EndW);
                    if (active)
                    {
                        pathResults.Enqueue(new PathResult(pathRequest, path, pathRequest.Callback));
                    }
                    pathRequests.Dequeue();
                }
                manualReset.WaitOne();
            }

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("PathFinder thread stopped!");
        }

        private static float ReachedDistance(float minDistance)
        {
            //return mountHandler.IsMounted() ? MinDistanceMount : minDistance;
            return minDistance;
        }

        private void ReduceByDistance(float minDistance)
        {
            Vector3 playerW = playerReader.WorldPos;
            float worldDistance = playerW.WorldDistanceXYTo(routeToNextWaypoint.Peek());
            while (worldDistance < ReachedDistance(minDistance) && routeToNextWaypoint.Count > 0)
            {
                routeToNextWaypoint.Pop();
                if (routeToNextWaypoint.Count > 0)
                {
                    worldDistance = playerW.WorldDistanceXYTo(routeToNextWaypoint.Peek());
                }
            }
        }

        private void AdjustHeading(float heading, CancellationToken ct)
        {
            float diff1 = Abs(RADIAN + heading - playerReader.Direction) % RADIAN;
            float diff2 = Abs(heading - playerReader.Direction - RADIAN) % RADIAN;

            float diff = Min(diff1, diff2);
            if (diff > minAngleToTurn)
            {
                if (diff > minAngleToStopBeforeTurn)
                {
                    stopMoving.Stop();
                }

                playerDirection.SetDirection(heading, routeToNextWaypoint.Peek(), MinDistance, ct);
            }
        }

        private bool AdjustNextWaypointPointToClosest()
        {
            if (wayPoints.Count < 2) { return false; }

            Vector3 A = wayPoints.Pop();
            Vector3 B = wayPoints.Peek();
            Vector2 result = VectorExt.GetClosestPointOnLineSegment(A.AsVector2(), B.AsVector2(), playerReader.WorldPos.AsVector2());
            Vector3 newPoint = new(result.X, result.Y, playerReader.WorldPosZ);

            if (newPoint.WorldDistanceXYTo(wayPoints.Peek()) > MinDistance)
            {
                wayPoints.Push(newPoint);
                if (debug)
                    LogDebug("Adjusted resume point");

                return false;
            }

            if (debug)
                LogDebug("Skipped next point in path");

            return true;
        }

        private void V1_AttemptToKeepRouteToWaypoint()
        {
            float totalDistance = TotalRoute.Zip(TotalRoute.Skip(1), VectorExt.WorldDistanceXY).Sum();
            if (totalDistance > MaxDistance / 2)
            {
                Vector3 playerW = playerReader.WorldPos;
                float distanceToRoute = playerW.WorldDistanceXYTo(routeToNextWaypoint.Peek());
                float distanceToPrevLoc = playerW.WorldDistanceXYTo(playerWorldPos);
                if (distanceToRoute > 2 * MinDistanceMount &&
                    distanceToPrevLoc > 2 * MinDistanceMount)
                {
                    LogV1ClearRouteToWaypoint(logger, patherName, distanceToRoute);
                    routeToNextWaypoint.Clear();
                }
                else
                {
                    LogV1KeepRouteToWaypoint(logger, patherName, distanceToRoute);
                    ResetStuckParameters();
                }
            }
            else
            {
                LogV1ClearRouteToWaypointTooFar(logger, patherName, totalDistance, MaxDistance / 2);
                routeToNextWaypoint.Clear();
            }
        }

        private void SimplyfyRouteToWaypoint()
        {
            Vector3[] reduced = PathSimplify.Simplify(routeToNextWaypoint.ToArray(), MinDistance / 4);
            Array.Reverse(reduced);

            routeToNextWaypoint.Clear();
            for (int i = 0; i < reduced.Length; i++)
            {
                routeToNextWaypoint.Push(reduced[i]);
            }
        }

        private void UpdateTotalRoute()
        {
            TotalRoute = new Vector3[routeToNextWaypoint.Count + wayPoints.Count];
            routeToNextWaypoint.CopyTo(TotalRoute, 0);
            wayPoints.CopyTo(TotalRoute, routeToNextWaypoint.Count);
        }

        private bool HasBeenActiveRecently()
        {
            return (DateTime.UtcNow - LastActive).TotalSeconds < 2;
        }


        private void LogDebug(string text)
        {
            logger.LogDebug($"{nameof(Navigation)}: {text}");
        }

        #region Logging

        [LoggerMessage(
            EventId = 40,
            Level = LogLevel.Warning,
            Message = "Unable to find path {start} -> {end}. Character may stuck! {elapsedMs}ms")]
        static partial void LogPathfinderFailed(ILogger logger, Vector3 start, Vector3 end, double elapsedMs);

        [LoggerMessage(
            EventId = 41,
            Level = LogLevel.Information,
            Message = "Pathfinder - {distance} - {start} -> {end} {elapsedMs}ms")]
        static partial void LogPathfinderSuccess(ILogger logger, float distance, Vector3 start, Vector3 end, double elapsedMs);

        [LoggerMessage(
            EventId = 42,
            Level = LogLevel.Information,
            Message = "Clear route to waypoint! Stucked for {elapsedMs}ms")]
        static partial void LogClearRouteToWaypointStuck(ILogger logger, double elapsedMs);

        [LoggerMessage(
            EventId = 43,
            Level = LogLevel.Information,
            Message = "[{name}] distance from nearlest point is {distance}. Have to clear RouteToWaypoint.")]
        static partial void LogV1ClearRouteToWaypoint(ILogger logger, string name, float distance);

        [LoggerMessage(
            EventId = 44,
            Level = LogLevel.Information,
            Message = "[{name}] distance is close {distance}. Keep RouteToWaypoint.")]
        static partial void LogV1KeepRouteToWaypoint(ILogger logger, string name, float distance);

        [LoggerMessage(
            EventId = 45,
            Level = LogLevel.Information,
            Message = "[{name}] total distance {totalDistance} > {maxDistancehalf}. Have to clear RouteToWaypoint.")]
        static partial void LogV1ClearRouteToWaypointTooFar(ILogger logger, string name, float totalDistance, float maxDistancehalf);

        #endregion
    }
}