using System;
using System.Collections.Concurrent;
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
        public Action<List<Vector3>, bool> Callback { get; }

        public PathRequest(int mapId, Vector3 start, Vector3 end, Action<List<Vector3>, bool> callback)
        {
            MapId = mapId;
            Start = start;
            End = end;
            Callback = callback;
        }
    }

    internal readonly struct PathResult
    {
        public List<Vector3> Path { get; }
        public bool Success { get; }
        public Action<List<Vector3>, bool> Callback { get; }

        public PathResult(List<Vector3> path, bool success, Action<List<Vector3>, bool> callback)
        {
            Path = path;
            Success = success;
            Callback = callback;
        }
    }

    public class Navigation : IDisposable
    {
        private const bool debug = false;
        private readonly float RADIAN = MathF.PI * 2;

        private readonly ILogger logger;
        private readonly PlayerDirection playerDirection;
        private readonly ConfigurableInput input;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly StuckDetector stuckDetector;
        private readonly IPPather pather;
        private readonly MountHandler mountHandler;

        private readonly int MinDistance = 10;
        private readonly int MinDistanceMount = 15;
        private readonly int MaxDistance = 200;

        private float AvgDistance;
        private float lastDistance = float.MaxValue;

        private readonly float minAngleToTurn = MathF.PI / 35;          // 5.14 degree
        private readonly float minAngleToStopBeforeTurn = MathF.PI / 3; // 60 degree

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

        private readonly ConcurrentQueue<PathRequest> pathRequests = new();
        private readonly ConcurrentQueue<PathResult> pathResults = new();
        private readonly Thread? pathfinderThread;
        private readonly ManualResetEvent manualReset;
        private bool isWorking;
        private readonly CancellationTokenSource _cts;

        public Navigation(ILogger logger, PlayerDirection playerDirection, ConfigurableInput input, AddonReader addonReader, StopMoving stopMoving, StuckDetector stuckDetector, IPPather pather, MountHandler mountHandler, Mode mode)
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

            AvgDistance = MinDistance;

            manualReset = new(false);
            _cts = new();
            pathfinderThread = new(PathFinderThread);
            pathfinderThread.Start();

            switch (mode)
            {
                case Mode.AttendedGather:
                    MaxDistance = MinDistance;
                    break;
            }
        }

        public void Dispose()
        {
            manualReset.Set();
            _cts.Cancel();
            _cts.Dispose();
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
                result.Callback(result.Path, result.Success);
            }

            if (isWorking)
            {
                return;
            }

            if (routeToNextWaypoint.Count == 0)
            {
                RefillRouteToNextWaypoint();
                return;
            }

            LastActive = DateTime.UtcNow;
            input.SetKeyState(input.ForwardKey, true, true);

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
                        Log($"Clear route to waypoit since stucked for {stuckDetector.ActionDurationMs} ms");
                        stuckDetector.Reset();
                        routeToNextWaypoint.Clear();
                        return;
                    }

                    if (HasBeenActiveRecently())
                    {
                        stuckDetector.Update();
                        distance = location.DistanceXYTo(routeToNextWaypoint.Peek());
                    }
                    else
                    {
                        Log("Resume from stuck");
                    }
                }
                else // distance closer
                {
                    AdjustHeading(heading, cts);
                }
            }

            lastDistance = distance;
        }

        public void Resume()
        {
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

        public void SetWayPoints(List<Vector3> points)
        {
            wayPoints.Clear();
            routeToNextWaypoint.Clear();

            points.Reverse();
            points.ForEach(x => wayPoints.Push(x));

            if (wayPoints.Count > 1)
            {
                float sum = 0;
                sum = points.Zip(points.Skip(1), (a, b) => a.DistanceXYTo(b)).Sum();
                AvgDistance = sum / points.Count;
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

                Log($"pathfinder - {distance} - {location} -> {wayPoints.Peek()}");
                PathRequest(new PathRequest(addonReader.UIMapId.Value, location, wayPoints.Peek(), (List<Vector3> path, bool success) =>
                {
                    if (!active)
                        return;

                    if (!success || path == null || path.Count == 0)
                    {
                        LogWarn($"Unable to find path {location} -> {wayPoints.Peek()}. Character may stuck!");
                        return;
                    }

                    path.Reverse();
                    path.ForEach(p => routeToNextWaypoint.Push(p));

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
                }));
            }
            else
            {
                if (debug)
                    LogDebug($"non pathfinder - {distance} - {location} -> {wayPoints.Peek()}");

                routeToNextWaypoint.Push(wayPoints.Peek());

                var heading = DirectionCalculator.CalculateHeading(location, wayPoints.Peek());
                if (debug)
                    LogDebug("Reached waypoint");
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

        private async void PathFinderThread()
        {
            while (!_cts.IsCancellationRequested)
            {
                manualReset.WaitOne();
                isWorking = true;

                if (pathRequests.TryDequeue(out PathRequest pathRequest))
                {
                    var path = await pather.FindRoute(pathRequest.MapId, pathRequest.Start, pathRequest.End);
                    if (active)
                    {
                        pathResults.Enqueue(new PathResult(path, true, pathRequest.Callback));
                    }
                }

                isWorking = false;
                manualReset.Reset();
            }
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
                    Log($"[{pather.GetType().Name}] distance from nearlest point is {distance}. Have to clear RouteToWaypoint.");
                    routeToNextWaypoint.Clear();
                }
                else
                {
                    Log($"[{pather.GetType().Name}] distance is close {distance}. Keep RouteToWaypoint.");
                    ResetStuckParameters();
                }
            }
            else
            {
                Log($"[{pather.GetType().Name}] total distance {totalDistance}<{MaxDistance / 2}. Have to clear RouteToWaypoint.");
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

        private void Log(string text)
        {
            logger.LogInformation($"{nameof(Navigation)}: {text}");
        }

        private void LogWarn(string text)
        {
            logger.LogWarning($"{nameof(Navigation)}: {text}");
        }
    }
}