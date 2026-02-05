using Core.Database;
using Core.GOAP;
using Core.Hazard;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Data;
using SharedLib.Extensions;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

using static System.MathF;
using static System.Diagnostics.Stopwatch;

#pragma warning disable 162

namespace Core.Goals;

public sealed partial class Navigation : IDisposable
{
    /// <summary>
    /// Tracks a heading entry for oscillation detection
    /// </summary>
    private readonly record struct HeadingHistoryEntry(float Heading, long Timestamp);

    private const bool debug = false;

    private const float DIFF_THRESHOLD = 1.5f;   // within 50% difference
    private const float UNIFORM_DIST_DIV = 2;    // within 50% difference

    private readonly string patherName;

    private readonly ILogger<Navigation> logger;
    private readonly PlayerDirection playerDirection;
    private readonly ConfigurableInput input;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly StopMoving stopMoving;
    private readonly StuckDetector stuckDetector;
    private readonly IPPather pather;
    private readonly IMountHandler mountHandler;
    private readonly AreaDB areaDB;
    private readonly RouteRehabilitator routeRehabilitator;

    private const float MinDistanceMount = 10;
    private readonly float MaxDistance = 200;
    private readonly float IndoorMinDistance = 1f;
    private readonly float OutDoorMinDistance = 3f;

    private float AvgDistance;
    private float lastWorldDistance = float.MaxValue;

    private const float minAngleToTurn = PI / 35f;              // 5.14 degree
    private const float minAngleToStopBeforeTurn = PI / 2f;     // 90 degree

    // Oscillation detection constants
    private const int HEADING_HISTORY_SIZE = 12;
    private const int OSCILLATION_THRESHOLD = 6;  // Direction changes in history
    private const double OSCILLATION_RESET_TIME_MS = 2000;
    private const float MIN_ANGLE_CHANGE_FOR_OSCILLATION = 0.2f;  // ~11.5 degrees

    private readonly Stack<Vector3> wayPoints = new();
    private readonly Stack<Vector3> routeToNextWaypoint = new();

    // Oscillation tracking
    private readonly Queue<HeadingHistoryEntry> headingHistory = new(HEADING_HISTORY_SIZE);
    private long lastOscillationCheckTime;
    private int oscillationCount;
    private bool oscillationDetected;

    public Vector3[] TotalRoute { private set; get; } = Array.Empty<Vector3>();

    public DateTime LastActive { get; private set; }

    public event Action? OnPathCalculated;
    public event Action? OnWayPointReached;
    public event Action? OnDestinationReached;
    public event Action? OnAnyPointReached;
    public event Action? OnNoPathFound;

    public bool SimplifyRouteToWaypoint { get; set; } = true;

    private bool active;
    private Vector3 playerWorldPos;

    private readonly Queue<PathRequest> pathRequests = new(1);
    private readonly Queue<PathResult> pathResults = new(1);

    private readonly CancellationToken token;
    private readonly Thread pathfinderThread;
    private readonly ManualResetEventSlim manualReset;

    private int failedAttempt;
    private Vector3 lastFailedDestination;
    private DateTime lastNoPathUtc;
    private Vector3 lastNoPathDestination;

    private const int NoPathBackoffMs = 1500;

    private const float RehabMinDistance = 25f;
    private static readonly TimeSpan RehabMinInterval = TimeSpan.FromSeconds(20);

    private DateTime lastRehabUtc;
    private Vector3 lastRehabWorldPos;
    private bool hasLastRehab;

    public Navigation(ILogger<Navigation> logger,
        CancellationTokenSource<GoapAgent> cts,
        PlayerDirection playerDirection,
        ConfigurableInput input,
        PlayerReader playerReader, AddonBits bits,
        StopMoving stopMoving,
        StuckDetector stuckDetector, IPPather pather, IMountHandler mountHandler,
        RouteRehabilitator routeRehabilitator,
        ClassConfiguration classConfiguration,
        AreaDB areaDB)
    {
        this.logger = logger;
        this.playerDirection = playerDirection;
        this.input = input;
        this.playerReader = playerReader;
        this.bits = bits;
        this.stopMoving = stopMoving;
        this.stuckDetector = stuckDetector;
        this.pather = pather;
        this.mountHandler = mountHandler;
        this.areaDB = areaDB;
        this.routeRehabilitator = routeRehabilitator;

        patherName = pather.GetType().Name;

        AvgDistance = OutDoorMinDistance;
        token = cts.Token;
        manualReset = new(false);
        pathfinderThread = new(PathFinderThread);
        pathfinderThread.Start();

        switch (classConfiguration.Mode)
        {
            case Mode.AttendedGather:
                MaxDistance = OutDoorMinDistance;
                SimplifyRouteToWaypoint = false;
                break;
        }
    }

    public void Dispose()
    {
        manualReset.Set();
    }

    public void Update()
    {
        Update(token);
    }

    public void Update(CancellationToken token)
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

        if (token.IsCancellationRequested || pathRequests.Count > 0)
        {
            return;
        }

        if (routeToNextWaypoint.Count == 0)
        {
            RefillRouteToNextWaypoint(token);
            return;
        }

        LastActive = DateTime.UtcNow;
        input.StartForward(true);

        // main loop
        Vector3 playerW = playerReader.WorldPos;
        playerWorldPos = playerW;
        Vector3 targetW = routeToNextWaypoint.Peek();
        float worldDistance = playerW.WorldDistanceXYTo(targetW);

        Vector3 playerM = WorldMapAreaDB.ToMap_FlipXY(playerW, playerReader.WorldMapArea);
        Vector3 targetM = WorldMapAreaDB.ToMap_FlipXY(targetW, playerReader.WorldMapArea);
        float heading = DirectionCalculator.CalculateMapHeading(playerM, targetM);

        if (worldDistance < ReachedDistance(OutDoorMinDistance))
        {
            if (targetW.Z != 0 && targetW.Z != playerW.Z)
            {
                playerReader.WorldPosZ = targetW.Z;
            }

            if (SimplifyRouteToWaypoint)
                ReduceByDistance(playerW, OutDoorMinDistance);
            else
                routeToNextWaypoint.Pop();

            TryRehabilitateSuccessfulTraversal(playerW);

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

                playerM = WorldMapAreaDB.ToMap_FlipXY(playerW, playerReader.WorldMapArea);
                targetM = WorldMapAreaDB.ToMap_FlipXY(targetW, playerReader.WorldMapArea);
                heading = DirectionCalculator.CalculateMapHeading(playerM, targetM);

                AdjustHeading(heading, token);

                return;
            }
        }

        if (routeToNextWaypoint.Count > 0)
        {
            if (stuckDetector.IsGettingCloser())
            {
                AdjustHeading(heading, token);
            }
            else
            {
                if (stuckDetector.ActionDurationMs > 10_000)
                {
                    if (mountHandler.IsMounted())
                        mountHandler.Dismount();

                    LogClearRouteToWaypointStuck(logger, stuckDetector.ActionDurationMs);
                    stuckDetector.Reset();
                    routeToNextWaypoint.Clear();
                    return;
                }

                if (HasBeenActiveRecently())
                {
                    stuckDetector.Update(token);
                    worldDistance = playerW.WorldDistanceXYTo(routeToNextWaypoint.Peek());
                }
            }
        }

        lastWorldDistance = worldDistance;
    }

    public void Resume()
    {
        ResetStuckParameters();

        if (pather.GetType() != typeof(RemotePathingAPIV3) && routeToNextWaypoint.Count > 0)
        {
            V1_AttemptToKeepRouteToWaypoint();
        }

        int removed = 0;
        while (AdjustNextWaypointPointToClosest() && removed < 5) { removed++; };
        if (removed > 0)
        {
            UpdateTotalRoute();

            if (debug)
                LogDebug($"Resume: removed {removed} waypoint!");
        }
    }

    public void Stop()
    {
        active = false;

        if (pather.GetType() == typeof(RemotePathingAPIV3))
            routeToNextWaypoint.Clear();

        ResetStuckParameters();
    }

    public void StopMovement()
    {
        input.StopForward(true);
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

    public void SetWayPoints(Span<Vector3> points)
    {
        wayPoints.Clear();
        routeToNextWaypoint.Clear();

        float mapDistanceXY = 0;
        WorldMapArea wma = playerReader.WorldMapArea;
        for (int i = points.Length - 1; i >= 0; i--)
        {
            Vector3 point = points[i];
            if (IsMapPoint(point))
            {
                point = WorldMapAreaDB.ToWorld_FlipXY(point, wma);
            }

            if (i != points.Length - 1)
            {
                Vector3 prev = wayPoints.Peek();
                mapDistanceXY += point.WorldDistanceXYTo(prev);
            }

            wayPoints.Push(point);
        }

        AvgDistance = wayPoints.Count > 1 ? Max(mapDistanceXY / wayPoints.Count, OutDoorMinDistance) : OutDoorMinDistance;

        UpdateTotalRoute();

        static bool IsMapPoint(Vector3 p)
        {
            return
                p.X is >= 0 and <= 100 &&
                p.Y is >= 0 and <= 100;
        }
    }

    public void ResetStuckParameters()
    {
        stuckDetector.Reset();
    }

    private void RefillRouteToNextWaypoint(CancellationToken token)
    {
        routeToNextWaypoint.Clear();

        Vector3 playerW = playerReader.WorldPos;
        if (playerW.Z == 0 && areaDB.CurrentArea != null)
        {
            // The addon provides map XY but no reliable Z. Remote navmesh queries can be sensitive
            // to Z when locating the nearest polygon. Seed Z from the closest known spawnpoint
            // in the current map so pathfinding has a reasonable height hint.
            (_, Vector3 worldPos) = areaDB.FindClosestCreatureByNpcFlag(NpcFlags.None, new Vector3(playerW.X, playerW.Y, 0));
            if (worldPos != default && worldPos.Z != 0)
            {
                playerReader.WorldPosZ = worldPos.Z;
                playerW = new Vector3(playerW.X, playerW.Y, worldPos.Z);
            }
        }

        Vector3 targetW = wayPoints.Peek();
        float distance = playerW.WorldDistanceXYTo(targetW);

        if (distance > MaxDistance || distance > AvgDistance * 2)
        {
            if (debug)
                LogDebug($"Distance: {distance} vs Avg:({AvgDistance * 2},{AvgDistance}) - TAVG: {DIFF_THRESHOLD * AvgDistance} ");

            // When the pathfinder repeatedly returns "no path" for the same destination,
            // avoid hammering the pathing service in a tight loop. Give the stuck logic
            // a chance to move us to a slightly different position before retrying.
            if (lastNoPathDestination == targetW &&
                lastNoPathUtc != default &&
                (DateTime.UtcNow - lastNoPathUtc).TotalMilliseconds < NoPathBackoffMs)
            {
                stuckDetector.SetTargetLocation(targetW);
                stuckDetector.Update(token);
                return;
            }

            stopMoving.Stop();
            PathRequest(new PathRequest(playerReader.UIMapId.Value, bits.Indoors(), playerW, targetW, distance, PathCalculatedCallback));
        }
        else
        {
            if (debug)
                LogDebug($"non pathfinder - {distance} - {playerW} -> {targetW}");

            routeToNextWaypoint.Push(targetW);

            Vector3 playerM = WorldMapAreaDB.ToMap_FlipXY(playerW, playerReader.WorldMapArea);
            Vector3 targetM = WorldMapAreaDB.ToMap_FlipXY(targetW, playerReader.WorldMapArea);
            float heading = DirectionCalculator.CalculateMapHeading(playerM, targetM);
            AdjustHeading(heading, token);

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
        {
            return;
        }

        // Consider trivial proximity as a successful "no path needed"
        const float TrivialDistanceThreshold = MinDistanceMount;

        float distance = result.StartW.WorldDistanceXYTo(result.EndW);
        bool isTriviallyClose = result.Path.Length == 0 && distance < TrivialDistanceThreshold;

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug($"Pathfinder - Trivial: {isTriviallyClose} | {result.ElapsedMs}ms - {result.StartW.ToStringF()} -> {result.EndW.ToStringF()}");
        }

        if (result.Path.Length == 0 && !isTriviallyClose)
        {
            if (lastFailedDestination != result.EndW)
            {
                lastFailedDestination = result.EndW;
                LogPathfinderFailed(logger, result.StartW, result.EndW, result.ElapsedMs);
            }

            failedAttempt++;

            if (failedAttempt == 1 && bits.Indoors())
            {
                // try to find closest spawn point
                (Creature creature, Vector3 worldPos) = areaDB.FindClosestCreatureByNpcFlag(NpcFlags.None, playerReader.WorldPos);
                playerReader.WorldPosZ = worldPos.Z;

                logger.LogWarning($"Found closest spawn {creature.Name}");
            }
 
            if (failedAttempt > 2)
            {
                failedAttempt = 0;
                lastNoPathDestination = result.EndW;
                lastNoPathUtc = DateTime.UtcNow;
                stuckDetector.SetTargetLocation(result.EndW);
                stuckDetector.Update();

                OnNoPathFound?.Invoke();
            }
            return;
        }

        failedAttempt = 0;
        lastNoPathUtc = default;
        // TODO: fix this later
        //if (!isTriviallyClose)
        {
            LogPathfinderSuccess(logger, result.Distance, result.StartW, result.EndW, result.ElapsedMs);

            for (int i = result.Path.Length - 1; i >= 0; i--)
            {
                routeToNextWaypoint.Push(result.Path[i]);
            }

            if (SimplifyRouteToWaypoint)
                SimplyfyRouteToWaypoint();
        }

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
        while (!token.IsCancellationRequested)
        {
            manualReset.Reset();
            if (pathRequests.TryPeek(out PathRequest pathRequest))
            {
                Vector3[] path = pather.FindWorldRoute(pathRequest.MapId, pathRequest.StartIndoors, pathRequest.StartW, pathRequest.EndW);
                if (active)
                {
                    pathResults.Enqueue(new PathResult(pathRequest, path, pathRequest.Callback));
                }
                pathRequests.Dequeue();
            }
            manualReset.Wait();
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Thread stopped!");
    }

    private float ReachedDistance(float minDistance)
    {
        return mountHandler.IsMounted()
            ? MinDistanceMount
            : bits.Indoors()
                ? IndoorMinDistance
                : minDistance;
    }

    private void ReduceByDistance(Vector3 playerW, float minDistance)
    {
        while (routeToNextWaypoint.Count > 0 &&
            playerW.WorldDistanceXYTo(routeToNextWaypoint.Peek()) < ReachedDistance(minDistance))
        {
            routeToNextWaypoint.Pop();
        }
    }

    private void TryRehabilitateSuccessfulTraversal(Vector3 playerW)
    {
        DateTime now = DateTime.UtcNow;

        if (hasLastRehab)
        {
            if (now - lastRehabUtc < RehabMinInterval &&
                playerW.WorldDistanceXYTo(lastRehabWorldPos) < RehabMinDistance)
            {
                return;
            }
        }

        routeRehabilitator.ReportSuccessfulTraversal(
            playerW,
            playerReader.MapId,
            radius: 20f,
            severityFactor: 0.95f);

        lastRehabUtc = now;
        lastRehabWorldPos = playerW;
        hasLastRehab = true;
    }

    private void AdjustHeading(float heading, CancellationToken token)
    {
        float diff1 = Abs(Tau + heading - playerReader.Direction) % Tau;
        float diff2 = Abs(heading - playerReader.Direction - Tau) % Tau;

        float diff = Min(diff1, diff2);

        // Track heading for oscillation detection
        TrackHeadingForOscillation();

        // Check for oscillation
        if (oscillationDetected)
        {
            logger.LogWarning($"[Navigation] Oscillation detected ({oscillationCount} rapid direction changes). Notifying StuckDetector.");

            // Notify stuck detector of oscillation
            if (!stuckDetector.IsCurrentlyStuck)
            {
                stuckDetector.SetTargetLocation(routeToNextWaypoint.Peek());
                stuckDetector.Update(token);
            }

            // Stop movement to break oscillation
            stopMoving.Stop();

            // Reset oscillation tracking
            ResetOscillationTracking();
            return;
        }

        if (diff > minAngleToTurn)
        {
            if (diff > minAngleToStopBeforeTurn)
            {
                stopMoving.Stop();
            }

            playerDirection.SetDirection(heading, routeToNextWaypoint.Peek(), OutDoorMinDistance, token);
        }
    }

    /// <summary>
    /// Tracks current heading for oscillation detection
    /// </summary>
    private void TrackHeadingForOscillation()
    {
        long now = GetTimestamp();

        // Reset if too much time passed
        if (headingHistory.Count > 0)
        {
            double elapsedMs = GetElapsedTime(lastOscillationCheckTime).TotalMilliseconds;
            if (elapsedMs > OSCILLATION_RESET_TIME_MS)
            {
                ResetOscillationTracking();
            }
        }

        // Add new entry
        headingHistory.Enqueue(new HeadingHistoryEntry(playerReader.Direction, now));
        if (headingHistory.Count > HEADING_HISTORY_SIZE)
        {
            headingHistory.Dequeue();
        }

        lastOscillationCheckTime = now;

        // Check for oscillation
        oscillationDetected = DetectOscillation();
    }

    /// <summary>
    /// Detects oscillation by analyzing heading history for rapid direction changes
    /// </summary>
    private bool DetectOscillation()
    {
        if (headingHistory.Count < HEADING_HISTORY_SIZE)
            return false;

        int directionChanges = 0;
        float prevDelta = 0;

        var entries = headingHistory.ToArray();
        for (int i = 1; i < entries.Length; i++)
        {
            float delta = entries[i].Heading - entries[i - 1].Heading;

            // Normalize delta
            if (delta > PI)
                delta -= 2 * PI;
            else if (delta < -PI)
                delta += 2 * PI;

            // Check for significant direction change
            if (MathF.Abs(delta) > MIN_ANGLE_CHANGE_FOR_OSCILLATION)
            {
                // Check if direction reversed
                if (prevDelta != 0 && Math.Sign(delta) != Math.Sign(prevDelta))
                {
                    directionChanges++;
                }
                prevDelta = delta;
            }
        }

        if (directionChanges >= OSCILLATION_THRESHOLD)
        {
            oscillationCount++;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets oscillation tracking
    /// </summary>
    private void ResetOscillationTracking()
    {
        headingHistory.Clear();
        oscillationDetected = false;
        lastOscillationCheckTime = GetTimestamp();
    }

    private bool AdjustNextWaypointPointToClosest()
    {
        if (wayPoints.Count < 2) { return false; }

        Vector3 A = wayPoints.Pop();
        Vector3 B = wayPoints.Peek();
        Vector2 result = VectorExt.GetClosestPointOnLineSegment(A.AsVector2(), B.AsVector2(), playerReader.WorldPos.AsVector2());
        Vector3 newPoint = new(result.X, result.Y, playerReader.WorldPosZ);

        if (newPoint.WorldDistanceXYTo(wayPoints.Peek()) > OutDoorMinDistance)
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
        float totalDistance = VectorExt.TotalDistance<Vector3>(TotalRoute, VectorExt.WorldDistanceXY);
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
        const bool HighQuality = false;
        Span<Vector3> reduced = PathSimplify.Simplify(routeToNextWaypoint.ToArray(), OutDoorMinDistance / 2, HighQuality);
        if (debug)
            LogDebug($"{nameof(SimplyfyRouteToWaypoint)} {routeToNextWaypoint.Count} -> {reduced.Length} | HQ: {HighQuality}");

        routeToNextWaypoint.Clear();
        for (int i = reduced.Length - 1; i >= 0; i--)
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
        logger.LogDebug($"D: {text}");
    }

    #region Logging

    [LoggerMessage(
        EventId = 0040,
        Level = LogLevel.Warning,
        Message = "Unable to find path {start} -> {end}. Character may stuck! {elapsedMs}ms")]
    static partial void LogPathfinderFailed(ILogger logger, Vector3 start, Vector3 end, double elapsedMs);

    [LoggerMessage(
        EventId = 0041,
        Level = LogLevel.Information,
        Message = "Pathfinder - {distance} - {start} -> {end} {elapsedMs}ms")]
    static partial void LogPathfinderSuccess(ILogger logger, float distance, Vector3 start, Vector3 end, double elapsedMs);

    [LoggerMessage(
        EventId = 0042,
        Level = LogLevel.Information,
        Message = "Clear route to waypoint! Stucked for {elapsedMs}ms")]
    static partial void LogClearRouteToWaypointStuck(ILogger logger, double elapsedMs);

    [LoggerMessage(
        EventId = 0043,
        Level = LogLevel.Information,
        Message = "[{name}] distance from nearlest point is {distance}. Have to clear RouteToWaypoint.")]
    static partial void LogV1ClearRouteToWaypoint(ILogger logger, string name, float distance);

    [LoggerMessage(
        EventId = 0044,
        Level = LogLevel.Information,
        Message = "[{name}] distance is close {distance}. Keep RouteToWaypoint.")]
    static partial void LogV1KeepRouteToWaypoint(ILogger logger, string name, float distance);

    [LoggerMessage(
        EventId = 0045,
        Level = LogLevel.Information,
        Message = "[{name}] total distance {totalDistance} > {maxDistancehalf}. Have to clear RouteToWaypoint.")]
    static partial void LogV1ClearRouteToWaypointTooFar(ILogger logger, string name, float totalDistance, float maxDistancehalf);

    #endregion
}
