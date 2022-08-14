using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using static System.MathF;

using Microsoft.Extensions.Logging;

using SharedLib.Extensions;

#pragma warning disable 162

namespace Core.Goals
{
    internal readonly struct PathRequest
    {
        public int MapId { get; }
        public Vector3 Start { get; }
        public Vector3 End { get; }
        public float Distance { get; }
        public Action<PathResult> Callback { get; }
        public DateTime Time { get; }

        public PathRequest(int mapId, Vector3 start, Vector3 end, float distance, Action<PathResult> callback)
        {
            MapId = mapId;
            Start = start;
            End = end;
            Distance = distance;
            Callback = callback;
            Time = DateTime.UtcNow;
        }
    }

    internal readonly struct PathResult
    {
        public Vector3 Start { get; }
        public Vector3 End { get; }
        public float Distance { get; }
        public Vector3[] Path { get; }
        public bool Success { get; }
        public double ElapsedMs { get; }
        public Action<PathResult> Callback { get; }

        public PathResult(in PathRequest request, Vector3[] path, bool success, Action<PathResult> callback)
        {
            Start = request.Start;
            End = request.End;
            Distance = request.Distance;
            Path = path;
            Success = success;
            Callback = callback;
            ElapsedMs = (DateTime.UtcNow - request.Time).TotalMilliseconds;
        }
    }

    public partial class Navigation : IDisposable
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

        private const float MinMapDistanceMount = 15;
        private readonly float MaxMapDistance = 200;
        private readonly float MinMapDistance = 10;

        private bool UniformPath;
        private float AvgMapDistance;
        private float lastMapDistance = float.MaxValue;

        private const float minAngleToTurn = PI / 35f;              // 5.14 degree
        private const float minAngleToStopBeforeTurn = PI / 2f;     // 90 degree

        private readonly Stack<Vector3> wayPoints = new();
        private readonly Stack<Vector3> routeToNextWaypoint = new();

        public List<Vector3> TotalRoute { private init; get; } = new();

        public DateTime LastActive { get; private set; }

        public event Action? OnPathCalculated;
        public event Action? OnWayPointReached;
        public event Action? OnDestinationReached;
        public event Action? OnAnyPointReached;

        public bool SimplifyRouteToWaypoint { get; set; } = true;

        private bool active;
        private Vector3 lastPlayerMap;

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

            AvgMapDistance = MinMapDistance;

            manualReset = new(false);
            _cts = new();
            pathfinderThread = new(PathFinderThread);
            pathfinderThread.Start();

            switch (classConfiguration.Mode)
            {
                case Mode.AttendedGather:
                    MaxMapDistance = MinMapDistance;
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

            if (pathRequests.Count > 0 || ct.IsCancellationRequested)
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
            lastPlayerMap = playerReader.MapPos;
            Vector3 targetMap = routeToNextWaypoint.Peek();
            float mapDistance = lastPlayerMap.MapDistanceXYTo(targetMap);
            float heading = DirectionCalculator.CalculateHeading(lastPlayerMap, targetMap);

            if (mapDistance < ReachedDistance(MinMapDistance))
            {
                if (targetMap.Z != 0 && targetMap.Z != lastPlayerMap.Z)
                {
                    playerReader.MapZ = targetMap.Z;
                }

                if (SimplifyRouteToWaypoint)
                    ReduceByDistance(MinMapDistance);
                else
                    routeToNextWaypoint.Pop();

                OnAnyPointReached?.Invoke();

                lastMapDistance = float.MaxValue;
                UpdateTotalRoute();

                if (routeToNextWaypoint.Count == 0)
                {
                    if (wayPoints.Count > 0)
                    {
                        wayPoints.Pop();
                        UpdateTotalRoute();

                        if (debug)
                            LogDebug($"Reached wayPoint! Distance: {mapDistance} -- Remains: {wayPoints.Count}");

                        OnWayPointReached?.Invoke();
                    }
                }
                else
                {
                    targetMap = routeToNextWaypoint.Peek();
                    stuckDetector.SetTargetLocation(targetMap);
                    heading = DirectionCalculator.CalculateHeading(lastPlayerMap, targetMap);

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
                        mapDistance = lastPlayerMap.MapDistanceXYTo(routeToNextWaypoint.Peek());
                    }
                }
                else if (!ct.IsCancellationRequested) // distance closer
                {
                    AdjustHeading(heading, ct);
                }
            }

            lastMapDistance = mapDistance;
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

        public Vector3 NextPoint()
        {
            return routeToNextWaypoint.Peek();
        }

        public void SetWayPoints(Vector3[] points)
        {
            wayPoints.Clear();
            routeToNextWaypoint.Clear();

            UniformPath = true;
            float distanceXY = 0;
            Array.Reverse(points);
            for (int i = 0; i < points.Length; i++)
            {
                if (i > 0)
                {
                    float mapD = points[i].MapDistanceXYTo(points[i - 1]);
                    if (i > 1)
                    {
                        float cAvg = distanceXY / (i - 1);
                        UniformPath &= mapD <= MinMapDistance || Abs(cAvg - mapD) <= mapD / UNIFORM_DIST_DIV;
                    }
                    distanceXY += mapD;
                }

                wayPoints.Push(points[i]);
            }

            AvgMapDistance = wayPoints.Count > 1 ? Max(distanceXY / wayPoints.Count, MinMapDistance) : MinMapDistance;

            if (debug)
                LogDebug($"SetWayPoints: Added {wayPoints.Count} - Uniform ? {UniformPath} - AvgDistance: {AvgMapDistance} - TAvg: {DIFF_THRESHOLD * AvgMapDistance}");

            UpdateTotalRoute();
        }

        public void ResetStuckParameters()
        {
            stuckDetector.Reset();
        }

        private void RefillRouteToNextWaypoint()
        {
            routeToNextWaypoint.Clear();

            Vector3 playerMap = playerReader.MapPos;
            Vector3 targetMap = wayPoints.Peek();
            float mapDistance = playerMap.MapDistanceXYTo(targetMap);
            //if (distance > MaxDistance || distance > (AvgDistance + MinDistance))
            if (mapDistance > MaxMapDistance ||
                (UniformPath ? mapDistance > DIFF_THRESHOLD * AvgMapDistance : mapDistance > DIFF_THRESHOLD * MinMapDistance))
            {
                if (debug)
                    LogDebug($"Distance: {mapDistance} vs Avg: {AvgMapDistance} - TAVG: {DIFF_THRESHOLD * AvgMapDistance} ");

                stopMoving.Stop();
                PathRequest(new PathRequest(playerReader.UIMapId.Value, playerMap, targetMap, mapDistance, PathCalculatedCallback));
            }
            else
            {
                if (debug)
                    LogDebug($"non pathfinder - {mapDistance} - {playerMap} -> {targetMap}");

                routeToNextWaypoint.Push(targetMap);

                float heading = DirectionCalculator.CalculateHeading(playerMap, targetMap);
                AdjustHeading(heading, _cts.Token);

                stuckDetector.SetTargetLocation(targetMap);
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

            if (!result.Success || result.Path == null || result.Path.Length == 0)
            {
                if (lastFailedDestination != result.End)
                {
                    lastFailedDestination = result.End;
                    LogPathfinderFailed(logger, result.Start, result.End, result.ElapsedMs);
                }

                failedAttempt++;
                if (failedAttempt > 2)
                {
                    failedAttempt = 0;
                    stuckDetector.SetTargetLocation(result.End);
                    stuckDetector.Update();
                }
                return;
            }

            failedAttempt = 0;
            LogPathfinderSuccess(logger, result.Distance, result.Start, result.End, result.ElapsedMs);

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

        private async void PathFinderThread()
        {
            while (!_cts.IsCancellationRequested)
            {
                manualReset.Reset();
                if (pathRequests.TryPeek(out PathRequest pathRequest))
                {
                    var path = await pather.FindRoute(pathRequest.MapId, pathRequest.Start, pathRequest.End);
                    if (active)
                    {
                        pathResults.Enqueue(new PathResult(pathRequest, path, true, pathRequest.Callback));
                    }
                    pathRequests.Dequeue();
                }
                manualReset.WaitOne();
            }

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("PathFinder thread stopped!");
        }

        private float ReachedDistance(float minDistance)
        {
            return mountHandler.IsMounted() ? MinMapDistanceMount : minDistance;
        }

        private void ReduceByDistance(float minDistance)
        {
            Vector3 playerMap = playerReader.MapPos;
            float mapDistance = playerMap.MapDistanceXYTo(routeToNextWaypoint.Peek());
            while (mapDistance < ReachedDistance(minDistance) && routeToNextWaypoint.Count > 0)
            {
                routeToNextWaypoint.Pop();
                if (routeToNextWaypoint.Count > 0)
                {
                    mapDistance = playerMap.MapDistanceXYTo(routeToNextWaypoint.Peek());
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

                playerDirection.SetDirection(heading, routeToNextWaypoint.Peek(), MinMapDistance, ct);
            }
        }

        private bool AdjustNextWaypointPointToClosest()
        {
            if (wayPoints.Count < 2) { return false; }

            Vector3 mapA = wayPoints.Pop();
            Vector3 mapB = wayPoints.Peek();
            Vector2 mapResult = VectorExt.GetClosestPointOnLineSegment(mapA.AsVector2(), mapB.AsVector2(), playerReader.MapPos.AsVector2());
            Vector3 mapPoint = new(mapResult.X, mapResult.Y, playerReader.MapZ);

            if (mapPoint.MapDistanceXYTo(wayPoints.Peek()) > MinMapDistance)
            {
                wayPoints.Push(mapPoint);
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
            float totalDistance = TotalRoute.Zip(TotalRoute.Skip(1), VectorExt.MapDistanceXY).Sum();
            if (totalDistance > MaxMapDistance / 2)
            {
                Vector3 playerMap = playerReader.MapPos;
                float mapDistanceToRoute = playerMap.MapDistanceXYTo(routeToNextWaypoint.Peek());
                float mapDistanceToPrevLoc = playerMap.MapDistanceXYTo(lastPlayerMap);
                if (mapDistanceToRoute > 2 * MinMapDistanceMount &&
                    mapDistanceToPrevLoc > 2 * MinMapDistanceMount)
                {
                    LogV1ClearRouteToWaypoint(logger, patherName, mapDistanceToRoute);
                    routeToNextWaypoint.Clear();
                }
                else
                {
                    LogV1KeepRouteToWaypoint(logger, patherName, mapDistanceToRoute);
                    ResetStuckParameters();
                }
            }
            else
            {
                LogV1ClearRouteToWaypointTooFar(logger, patherName, totalDistance, MaxMapDistance / 2);
                routeToNextWaypoint.Clear();
            }
        }

        private void SimplyfyRouteToWaypoint()
        {
            List<Vector3> simple = PathSimplify.Simplify(routeToNextWaypoint.ToArray(), pather is RemotePathingAPIV3 ? 0.05f : 0.1f);
            simple.Reverse();

            routeToNextWaypoint.Clear();
            for (int i = 0; i < simple.Count; i++)
            {
                routeToNextWaypoint.Push(simple[i]);
            }
        }

        private void UpdateTotalRoute()
        {
            TotalRoute.Clear();
            TotalRoute.AddRange(routeToNextWaypoint);
            TotalRoute.AddRange(wayPoints);
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