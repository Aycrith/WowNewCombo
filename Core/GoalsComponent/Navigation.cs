using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
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
        public List<Vector3> Path { get; }
        public bool Success { get; }
        public double ElapsedMs { get; }
        public Action<PathResult> Callback { get; }

        public PathResult(in PathRequest request, List<Vector3> path, bool success, Action<PathResult> callback)
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
        private const float RADIAN = MathF.PI * 2;

        private readonly ILogger logger;
        private readonly PlayerDirection playerDirection;
        private readonly ConfigurableInput input;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly StuckDetector stuckDetector;
        private readonly IPPather pather;
        private readonly MountHandler mountHandler;

        private readonly string patherName;

        private const int MinDistance = 10;
        private const int MinDistanceMount = 15;
        private readonly int MaxDistance = 200;

        private float AvgDistance;
        private float lastDistance = float.MaxValue;

        private const float minAngleToTurn = MathF.PI / 35;          // 5.14 degree
        private const float minAngleToStopBeforeTurn = MathF.PI / 2; // 90 degree

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
            Update(_cts);
        }

        public void Update(CancellationTokenSource cts)
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

            if (pathRequests.Count > 0 || cts.IsCancellationRequested)
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
            var location = playerReader.PlayerLocation;
            var distance = location.DistanceXYTo(routeToNextWaypoint.Peek());
            var heading = DirectionCalculator.CalculateHeading(location, routeToNextWaypoint.Peek());

            if (distance < ReachedDistance(MinDistance))
            {
                if (routeToNextWaypoint.Count > 0)
                {
                    if (routeToNextWaypoint.Peek().Z != 0 && routeToNextWaypoint.Peek().Z != location.Z)
                    {
                        playerReader.ZCoord = routeToNextWaypoint.Peek().Z;
                        if (debug)
                            LogDebug($"Update PlayerLocation.Z = {playerReader.ZCoord}");
                    }

                    if (SimplifyRouteToWaypoint)
                        ReduceByDistance(MinDistance);
                    else
                        routeToNextWaypoint.Pop();

                    OnAnyPointReached?.Invoke();

                    lastDistance = float.MaxValue;
                    UpdateTotalRoute();
                }

                if (routeToNextWaypoint.Count == 0)
                {
                    if (wayPoints.Count > 0)
                    {
                        wayPoints.Pop();
                        UpdateTotalRoute();

                        if (debug)
                            LogDebug($"Reached wayPoint! Distance: {distance} -- Remains: {wayPoints.Count}");

                        OnWayPointReached?.Invoke();
                    }
                }
                else
                {
                    stuckDetector.SetTargetLocation(routeToNextWaypoint.Peek());

                    heading = DirectionCalculator.CalculateHeading(location, routeToNextWaypoint.Peek());
                    if (debug)
                        LogDebug("Turn to next point");

                    if (!cts.IsCancellationRequested)
                        AdjustHeading(heading, cts);
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
                        distance = location.DistanceXYTo(routeToNextWaypoint.Peek());
                    }
                }
                else if (!cts.IsCancellationRequested) // distance closer
                {
                    AdjustHeading(heading, cts);
                }
            }

            lastDistance = distance;
        }

        public void Resume()
        {
            ResetStuckParameters();

            if (pather is not RemotePathingAPIV3 && routeToNextWaypoint.Count > 0)
            {
                V1_AttemptToKeepRouteToWaypoint();
            }

            var removed = 0;
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

            Array.Reverse(points);
            for (int i = 0; i < points.Length; i++)
            {
                wayPoints.Push(points[i]);
            }

            if (wayPoints.Count > 1)
            {
                float sum = 0;
                sum = points.Zip(points.Skip(1), (a, b) => a.DistanceXYTo(b)).Sum();
                AvgDistance = sum / points.Length;
            }
            else
            {
                AvgDistance = MinDistance;
            }

            if (debug)
                LogDebug($"SetWayPoints: Added {wayPoints.Count} - AvgDistance: {AvgDistance}");

            UpdateTotalRoute();
        }

        public void ResetStuckParameters()
        {
            stuckDetector.Reset();
        }

        private void RefillRouteToNextWaypoint()
        {
            routeToNextWaypoint.Clear();

            var location = playerReader.PlayerLocation;
            var distance = location.DistanceXYTo(wayPoints.Peek());
            if (distance > MaxDistance || distance > (AvgDistance + MinDistance))
            {
                stopMoving.Stop();
                PathRequest(new PathRequest(addonReader.UIMapId.Value, location, wayPoints.Peek(), distance, PathCalculatedCallback));
            }
            else
            {
                if (debug)
                    LogDebug($"non pathfinder - {distance} - {location} -> {wayPoints.Peek()}");

                routeToNextWaypoint.Push(wayPoints.Peek());

                if (debug)
                    LogDebug("Reached waypoint");

                float heading = DirectionCalculator.CalculateHeading(location, wayPoints.Peek());
                AdjustHeading(heading, _cts);

                stuckDetector.SetTargetLocation(routeToNextWaypoint.Peek());
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

            if (!result.Success || result.Path == null || result.Path.Count == 0)
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

            result.Path.Reverse();
            result.Path.ForEach(p => routeToNextWaypoint.Push(p));

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

        private int ReachedDistance(int minDistance)
        {
            return mountHandler.IsMounted() ? MinDistanceMount : minDistance;
        }

        private void ReduceByDistance(int minDistance)
        {
            var location = playerReader.PlayerLocation;
            var distance = location.DistanceXYTo(routeToNextWaypoint.Peek());
            while (distance < ReachedDistance(minDistance) && routeToNextWaypoint.Count > 0)
            {
                routeToNextWaypoint.Pop();
                if (routeToNextWaypoint.Count > 0)
                {
                    distance = location.DistanceXYTo(routeToNextWaypoint.Peek());
                }
            }
        }

        private void AdjustHeading(float heading, CancellationTokenSource cts)
        {
            var diff1 = MathF.Abs(RADIAN + heading - playerReader.Direction) % RADIAN;
            var diff2 = MathF.Abs(heading - playerReader.Direction - RADIAN) % RADIAN;

            var diff = MathF.Min(diff1, diff2);
            if (diff > minAngleToTurn)
            {
                if (diff > minAngleToStopBeforeTurn)
                {
                    stopMoving.Stop();
                }

                playerDirection.SetDirection(heading, routeToNextWaypoint.Peek(), MinDistance, cts);
            }
        }

        private bool AdjustNextWaypointPointToClosest()
        {
            if (wayPoints.Count < 2) { return false; }

            var A = wayPoints.Pop();
            var B = wayPoints.Peek();
            var result = VectorExt.GetClosestPointOnLineSegment(A.AsVector2(), B.AsVector2(), playerReader.PlayerLocation.AsVector2());
            var newPoint = new Vector3(result.X, result.Y, 0);
            if (newPoint.DistanceXYTo(wayPoints.Peek()) > MinDistance)
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
            float totalDistance = TotalRoute.Zip(TotalRoute.Skip(1), VectorExt.DistanceXY).Sum();
            if (totalDistance > MaxDistance / 2)
            {
                var location = playerReader.PlayerLocation;
                float distance = location.DistanceXYTo(routeToNextWaypoint.Peek());
                if (distance > 2 * MinDistanceMount)
                {
                    LogV1ClearRouteToWaypoint(logger, patherName, distance);
                    routeToNextWaypoint.Clear();
                }
                else
                {
                    LogV1KeepRouteToWaypoint(logger, patherName, distance);
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
            var simple = PathSimplify.Simplify(routeToNextWaypoint.ToArray(), pather is RemotePathingAPIV3 ? 0.05f : 0.1f);
            simple.Reverse();

            routeToNextWaypoint.Clear();
            simple.ForEach((x) => routeToNextWaypoint.Push(x));
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