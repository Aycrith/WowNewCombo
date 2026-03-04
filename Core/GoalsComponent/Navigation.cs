using Core.Database;
using Core.FeatureFlags;
using Core.GOAP;
using Core.Hazard;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Data;
using SharedLib.Extensions;
using SharedLib.Humanization;

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
    private const bool debug = false;

    /// <summary>Threshold for distance difference comparison (1.5f = 150%). Used to detect significant deviation from expected path.</summary>
    private const float DIFF_THRESHOLD = 1.5f;

    /// <summary>Divisor for uniform distance calculations (2 = 50% threshold). Controls how path distance variations are normalized.</summary>
    private const float UNIFORM_DIST_DIV = 2;

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
    private readonly RouteRehabilitationCoordinator routeRehabCoordinator;
    private readonly IRouteRerouter? routeRerouter;
    private readonly FeatureFlagService? featureFlagService;
    private readonly GoapCurrentGoalState? goapCurrentGoalState;

    private const float MinDistanceMount = 10;
    private readonly float MaxDistance = 200;
    private readonly float IndoorMinDistance = 1f;
    private readonly float OutDoorMinDistance = 10f;

    private float AvgDistance = 200_0000;
    private float lastWorldDistance = float.MaxValue;

    /// <summary>Minimum angle in radians (~5.14 degrees) before triggering turn adjustment. Prevents over-correction on minor heading deviations.</summary>
    private const float minAngleToTurn = PI / 35f;

    /// <summary>Minimum angle in radians (60 degrees) before stopping to turn. Matches upstream threshold for responsive turning.</summary>
    private const float minAngleToStopBeforeTurn = PI / 3f;

    private readonly Stack<Vector3> wayPoints = new();
    private readonly Stack<Vector3> routeToNextWaypoint = new();

    public Vector3[] TotalRoute { private set; get; } = Array.Empty<Vector3>();

    /// <summary>Running count of tail recalculations where the pather returned no usable path.</summary>
    public int TailRecalcFailures => tailRecalcFailures;

    public DateTime LastActive { get; private set; }

    public event Action? OnPathCalculated;
    public event Action? OnWayPointReached;
    public event Action? OnDestinationReached;
    public event Action? OnAnyPointReached;
    public event Action? OnNoPathFound;
    public event Action? OnDynamicDetourApplied;
    public event Action? OnSuccessfulReconnect;
    /// <summary>Fired each Update() tick with the perpendicular distance from
    /// the player to the current route segment. Used for route-adherence telemetry.</summary>
    public event Action<float>? OnDeviationSample;

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
    private const double DefaultRouteResetTimeoutMs = 8_000;
    private static readonly TimeSpan DynamicDetourCooldown = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FrontBypassCooldown = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan FrontBypassRepeatWindow = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan FrontBypassBreakerCooldown = TimeSpan.FromSeconds(18);
    private static readonly TimeSpan FrontBypassClusterWindow = TimeSpan.FromSeconds(16);
    private static readonly TimeSpan FrontBypassNoProgressWindow = TimeSpan.FromSeconds(16);
    private static readonly TimeSpan HazardDetourRepeatWindow = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan HazardDetourBreakerCooldown = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan CorpseRecoveryHazardSuppressGrace = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan FailedReconnectCooldown = TimeSpan.FromSeconds(35);
    private static readonly TimeSpan FailedReconnectClusterWindow = TimeSpan.FromSeconds(40);
    private const int MinRouteWaypointsForDynamicDetour = 1;
    private const float DynamicReconnectDuplicateDistance = 1.0f;
    private const float FailedReconnectDuplicateDistance = 8.0f;
    private const float FrontBypassRepeatDistance = 15.0f;
    private const int FrontBypassRepeatLimit = 4;
    private const float FrontBypassClusterDistance = 24.0f;
    private const int FrontBypassClusterLimit = 4;
    private const int FrontBypassNoProgressRepeatLimit = 3;
    private const int FrontBypassNoProgressMaxRouteDelta = 1;
    private const float HazardDetourRepeatDistance = 20.0f;
    private const int HazardDetourRepeatLimit = 3;
    private const float FailedReconnectClusterDistance = 18.0f;
    private const int FailedReconnectClusterSkipThreshold = 2;
    private const float HazardDetourMaxAdditionalDistance = 80.0f;
    private const float HazardDetourMaxLengthRatio = 1.75f;
    private const float HazardDetourHardMaxAdditionalDistance = 120.0f;
    private const float CorpseTargetMatchDistance = 8.0f;
    private const float PrecisionVerticalZDelta = 0.9f;
    private const float PrecisionReachedDistanceMin = 0.9f;
    private const float PrecisionReachedDistanceScale = 0.45f;
    private const float PrecisionSteeringIgnoreDistance = 0.4f;
    private const float PrecisionTurnLookaheadDistance = 8f;
    private const float TightTurnPrecisionRadians = PI / 5f;
    private const float SimplifyPreserveVerticalZDelta = 0.75f;
    private const float SimplifyPreserveTurnRadians = PI / 6f;

    private DateTime lastDynamicDetourAttemptUtc = DateTime.MinValue;
    private DateTime lastFrontBypassUtc = DateTime.MinValue;
    private DateTime hazardDetourBreakerUntilUtc = DateTime.MinValue;
    private DateTime lastHazardDetourRepeatUtc = DateTime.MinValue;
    private DateTime lastCorpseRecoveryContextUtc = DateTime.MinValue;
    private DateTime frontBypassBreakerUntilUtc = DateTime.MinValue;
    private DateTime lastFrontBypassRepeatUtc = DateTime.MinValue;
    private DateTime lastFrontBypassNoProgressUtc = DateTime.MinValue;
    private DateTime lastFailedReconnectUtc = DateTime.MinValue;
    private int frontBypassAttemptCount;
    private int repeatedHazardDetourCount;
    private int repeatedFrontBypassCount;
    private int repeatedFrontBypassNoProgressCount;
    private int tailRecalcFailures;
    private Vector3 lastHazardDetourRepeatPoint;
    private Vector3 lastFrontBypassRepeatPoint;
    private Vector3 lastFrontBypassNoProgressPoint;
    private Vector3 lastFailedReconnectPoint;
    private readonly Queue<ReconnectSample> recentFrontBypassReconnects = new();
    private readonly Queue<ReconnectSample> recentFailedReconnects = new();

    public Navigation(ILogger<Navigation> logger,
        CancellationTokenSource<GoapAgent> cts,
        PlayerDirection playerDirection,
        ConfigurableInput input,
        PlayerReader playerReader, AddonBits bits,
        StopMoving stopMoving,
        StuckDetector stuckDetector, IPPather pather, IMountHandler mountHandler,
        RouteRehabilitator routeRehabilitator,
        ClassConfiguration classConfiguration,
        AreaDB areaDB,
        IRouteRerouter? routeRerouter = null,
        IHumanizationProvider? humanizationProvider = null,
        FeatureFlagService? featureFlagService = null,
        Core.Navigation.NavSoakMetricsService? navSoakMetricsService = null,
        GoapCurrentGoalState? goapCurrentGoalState = null)
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
        this.routeRerouter = routeRerouter;
        this.featureFlagService = featureFlagService;
        this.goapCurrentGoalState = goapCurrentGoalState;

        routeRehabCoordinator = new RouteRehabilitationCoordinator(routeRehabilitator);

        patherName = pather.GetType().Name;
        navSoakMetricsService?.AttachRuntimeSources(stuckDetector, this);

        AvgDistance = OutDoorMinDistance;
        token = cts.Token;
        manualReset = new(false);
        pathfinderThread = new(PathFinderThread);
        pathfinderThread.Start();

        switch (classConfiguration.Mode)
        {
            case Mode.AutoGather:
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
        TrackCorpseRecoveryContext(DateTime.UtcNow);

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

        // Sample route deviation for telemetry (fires each active tick)
        if (routeToNextWaypoint.Count >= 2 &&
            TryGetUpcomingRoutePoints(out Vector3 deviationCurr, out Vector3 deviationNext))
        {
            Vector2 playerXY = new(playerW.X, playerW.Y);
            Vector2 closestOnSeg = VectorExt.GetClosestPointOnLineSegment(
                deviationCurr.AsVector2(), deviationNext.AsVector2(), playerXY);
            float deviation = Vector2.Distance(playerXY, closestOnSeg);
            OnDeviationSample?.Invoke(deviation);
        }

        Vector3 playerM = WorldMapAreaDB.ToMap_FlipXY(playerW, playerReader.WorldMapArea);
        Vector3 targetM = WorldMapAreaDB.ToMap_FlipXY(targetW, playerReader.WorldMapArea);
        float heading = DirectionCalculator.CalculateMapHeading(playerM, targetM);

        float reachedDistance = GetActiveReachedDistance(playerW, targetW, worldDistance);
        float steeringIgnoreDistance = GetSteeringIgnoreDistance(playerW, targetW, worldDistance);
        bool preciseTracking = steeringIgnoreDistance <= (PrecisionSteeringIgnoreDistance + 0.05f);

        if (worldDistance < reachedDistance)
        {
            if (targetW.Z != 0 && targetW.Z != playerW.Z)
            {
                playerReader.WorldPosZ = targetW.Z;
            }

            if (SimplifyRouteToWaypoint)
                ReduceByDistance(playerW, reachedDistance, preciseTracking);
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

                AdjustHeading(heading, steeringIgnoreDistance, token);

                return;
            }
        }

        if (routeToNextWaypoint.Count > 0)
        {
            if (stuckDetector.IsGettingCloser())
            {
                AdjustHeading(heading, steeringIgnoreDistance, token);
            }
            else
            {
                if (TryApplyDynamicHazardDetour(token))
                {
                    return;
                }

                if (stuckDetector.ActionDurationMs > GetRouteResetTimeoutMs())
                {
                    if (mountHandler.IsMounted())
                        mountHandler.Dismount();

                    if (IsLikelyCorpseRecoveryContext())
                    {
                        logger.LogWarning(
                            "[Navigation       ] Corpse run route cleared after {ElapsedMs:F0}ms stuck (route had {Count} remaining points, target={Target})",
                            stuckDetector.ActionDurationMs,
                            routeToNextWaypoint.Count,
                            routeToNextWaypoint.Count > 0 ? routeToNextWaypoint.Peek() : default);
                    }

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
        while (AdjustNextWaypointPointToClosest() && removed < 2) { removed++; }
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

        wayPoints.Clear();
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
            // Stop forward movement immediately when no path is available.
            // Prevents continuing in stale heading into terrain/water.
            stopMoving.Stop();

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

        // Log pathfinder success and populate route
        {
            Vector3[] pathToApply = result.Path;
            DateTime now = DateTime.UtcNow;
            if (routeRerouter?.IsEnabled == true &&
                result.Path.Length >= 2 &&
                CanUseHazardDetours(now, result.EndW))
            {
                try
                {
                    Vector3[]? detour = routeRerouter.CalculateDetourAsync(
                        result.Path,
                        playerReader.MapId,
                        token).GetAwaiter().GetResult();

                    if (detour is { Length: >= 2 })
                    {
                        if (IsExcessiveHazardDetour(result.Path, detour))
                        {
                            logger.LogDebug(
                                "[Navigation       ] Skipping excessive hazard detour on path apply ({OriginalCount} -> {DetourCount})",
                                result.Path.Length,
                                detour.Length);
                        }
                        else
                        {
                        pathToApply = detour;
                        logger.LogInformation(
                            "[Navigation       ] Applied hazard detour ({OriginalCount} -> {DetourCount} waypoints)",
                            result.Path.Length,
                            detour.Length);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[Navigation       ] Hazard detour calculation failed");
                }
            }

            LogPathfinderSuccess(logger, result.Distance, result.StartW, result.EndW, result.ElapsedMs);

            int rawPathPointCount = pathToApply.Length;
            for (int i = pathToApply.Length - 1; i >= 0; i--)
            {
                routeToNextWaypoint.Push(pathToApply[i]);
            }

            if (SimplifyRouteToWaypoint)
                SimplyfyRouteToWaypoint();

            if (IsLikelyCorpseRecoveryContext())
            {
                logger.LogInformation(
                    "[Navigation       ] Corpse run path: {RawCount} raw -> {FinalCount} final points, distance={Distance:F1}",
                    rawPathPointCount,
                    routeToNextWaypoint.Count,
                    result.Distance);
            }
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

    private float GetActiveReachedDistance(Vector3 playerW, Vector3 targetW, float worldDistance)
    {
        float baseDistance = ReachedDistance(OutDoorMinDistance);
        if (!RequiresPreciseTracking(playerW, targetW, worldDistance))
        {
            return baseDistance;
        }

        return Max(PrecisionReachedDistanceMin, baseDistance * PrecisionReachedDistanceScale);
    }

    private float GetSteeringIgnoreDistance(Vector3 playerW, Vector3 targetW, float worldDistance)
    {
        return RequiresPreciseTracking(playerW, targetW, worldDistance)
            ? PrecisionSteeringIgnoreDistance
            : OutDoorMinDistance;
    }

    private bool RequiresPreciseTracking(Vector3 playerW, Vector3 targetW, float worldDistance)
    {
        if (mountHandler.IsMounted())
        {
            return false;
        }

        if (Abs(targetW.Z - playerW.Z) >= PrecisionVerticalZDelta)
        {
            return true;
        }

        if (worldDistance > PrecisionTurnLookaheadDistance)
        {
            return false;
        }

        if (TryGetUpcomingRoutePoints(out Vector3 current, out Vector3 next))
        {
            if (Abs(next.Z - current.Z) >= PrecisionVerticalZDelta)
            {
                return true;
            }

            if (IsSharpTurn(playerW, current, next, TightTurnPrecisionRadians))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetUpcomingRoutePoints(out Vector3 current, out Vector3 next)
    {
        current = default;
        next = default;

        int index = 0;
        foreach (Vector3 point in routeToNextWaypoint)
        {
            if (index == 0)
            {
                current = point;
            }
            else if (index == 1)
            {
                next = point;
                return true;
            }

            index++;
        }

        return false;
    }

    private void ReduceByDistance(Vector3 playerW, float minDistance, bool singlePop = false)
    {
        float reached = ReachedDistance(minDistance);

        while (routeToNextWaypoint.Count > 0 &&
               playerW.WorldDistanceXYTo(routeToNextWaypoint.Peek()) < reached)
        {
            routeToNextWaypoint.Pop();

            if (singlePop)
            {
                break;
            }

            // If the next two waypoints form a significant turn AND the player is
            // still a meaningful distance from the turn apex, stop popping.
            // Guard: playerW must be > OutDoorMinDistance from curr so the
            // incoming vector playerW→curr is a valid forward direction, not
            // a near-zero or backward vector from slight overshoot.
            if (routeToNextWaypoint.Count >= 2 &&
                TryGetUpcomingRoutePoints(out Vector3 curr, out Vector3 next) &&
                playerW.WorldDistanceXYTo(curr) > 1f &&
                IsSharpTurn(playerW, curr, next, SimplifyPreserveTurnRadians))
            {
                break;
            }
        }
    }

    private void TryRehabilitateSuccessfulTraversal(Vector3 playerW)
    {
        routeRehabCoordinator.TryRehabilitate(
            playerW,
            playerReader.MapId,
            radius: 20f,
            severityFactor: 0.95f);
    }

    private void AdjustHeading(float heading, CancellationToken token)
    {
        AdjustHeading(heading, OutDoorMinDistance, token);
    }

    private void AdjustHeading(float heading, float steeringIgnoreDistance, CancellationToken token)
    {
        DateTime now = DateTime.UtcNow;
        float diff1 = Abs(Tau + heading - playerReader.Direction) % Tau;
        float diff2 = Abs(heading - playerReader.Direction - Tau) % Tau;

        float diff = Min(diff1, diff2);

        if (stuckDetector.IsCurrentlyStuck)
        {
            return;
        }

        // Nav recovery baseline: removed oscillation detector integration
        // and heading throttle. These added complexity that masked real stuck
        // conditions and created sluggish steering. Just turn if needed.

        if (diff > minAngleToTurn)
        {
            if (diff > minAngleToStopBeforeTurn)
            {
                stopMoving.Stop();
            }

            playerDirection.SetDirection(heading, routeToNextWaypoint.Peek(), steeringIgnoreDistance, token);
        }
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
        Vector3[] route = routeToNextWaypoint.ToArray();
        if (ShouldPreserveDetailedRoute(route))
        {
            if (route.Length > LongRoutePreserveThreshold)
            {
                logger.LogDebug(
                    "[Navigation       ] Preserving detailed route ({Count} points, long-path or terrain feature detected)",
                    route.Length);
            }

            return;
        }

        Span<Vector3> reduced = PathSimplify.Simplify(route, OutDoorMinDistance / 2, HighQuality);
        if (debug)
            LogDebug($"{nameof(SimplyfyRouteToWaypoint)} {routeToNextWaypoint.Count} -> {reduced.Length} | HQ: {HighQuality}");

        routeToNextWaypoint.Clear();
        for (int i = reduced.Length - 1; i >= 0; i--)
        {
            routeToNextWaypoint.Push(reduced[i]);
        }
    }

    /// <summary>
    /// Long paths (e.g. corpse runs) from the pathfinder with Catmull-Rom smoothing
    /// already have reasonable point spacing. Simplification of these removes critical
    /// terrain-following detail, creating straight-line segments that cross impassable
    /// terrain and cause stuck → repath → backtrack loops.
    /// </summary>
    private const int LongRoutePreserveThreshold = 40;

    private bool ShouldPreserveDetailedRoute(Vector3[] route)
    {
        if (route.Length < 3)
        {
            return false;
        }

        // Long paths from the pathfinder (e.g. corpse runs spanning 200+ yards) should
        // always preserve detail. The Catmull-Rom smoothed path already has reasonable
        // point spacing, and aggressive simplification (RDP) creates long straight-line
        // segments that frequently cross terrain the bot cannot traverse, causing
        // stuck → repath → backtrack oscillation.
        if (route.Length > LongRoutePreserveThreshold)
        {
            return true;
        }

        // Inspect the ENTIRE route for terrain features, not just the first few points.
        // Previously capped at 12 — this missed sharp turns and elevation changes deeper
        // in the path, allowing simplification to destroy critical waypoints.
        int inspectCount = route.Length;

        // Only check vertical terrain when not mounted (Z changes irrelevant at mount speed)
        if (!mountHandler.IsMounted())
        {
            for (int i = 1; i < inspectCount; i++)
            {
                if (Abs(route[i].Z - route[i - 1].Z) >= SimplifyPreserveVerticalZDelta)
                {
                    return true;
                }
            }
        }

        // Always preserve sharp turns regardless of mount status
        for (int i = 0; i < inspectCount - 2; i++)
        {
            if (IsSharpTurn(route[i], route[i + 1], route[i + 2], SimplifyPreserveTurnRadians))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsSharpTurn(Vector3 from, Vector3 via, Vector3 to, float minTurnRadians)
    {
        Vector2 incoming = new(via.X - from.X, via.Y - from.Y);
        Vector2 outgoing = new(to.X - via.X, to.Y - via.Y);

        if (incoming.LengthSquared() < 0.01f || outgoing.LengthSquared() < 0.01f)
        {
            return false;
        }

        incoming = Vector2.Normalize(incoming);
        outgoing = Vector2.Normalize(outgoing);

        float dot = Math.Clamp(Vector2.Dot(incoming, outgoing), -1f, 1f);
        float angle = MathF.Acos(dot);
        return angle >= minTurnRadians;
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

    private bool TryApplyDynamicHazardDetour(CancellationToken token)
    {
        // DISABLED for nav recovery baseline: The dual detour system
        // (hazard + front-bypass) with independent loop breakers produces
        // movement chaos. Re-enable incrementally after baseline stability.
        return false;

        if (routeToNextWaypoint.Count < MinRouteWaypointsForDynamicDetour)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;
        if ((now - lastDynamicDetourAttemptUtc) < DynamicDetourCooldown)
        {
            return false;
        }

        // Wait for sustained non-progress before rebuilding the active segment.
        if (stuckDetector.ActionDurationMs < GetRouteResetTimeoutMs() * 0.2)
        {
            return false;
        }

        lastDynamicDetourAttemptUtc = now;

        Vector3[] remainingPath = routeToNextWaypoint.ToArray();
        if (remainingPath.Length < MinRouteWaypointsForDynamicDetour)
        {
            return false;
        }

        Vector3 playerPosition = playerReader.WorldPos;
        Vector3 nextWaypoint = remainingPath[0];
        Vector3[] localSegment = [playerPosition, nextWaypoint];

        if (routeRerouter?.IsEnabled == true && CanUseHazardDetours(now, nextWaypoint))
        {
            try
            {
                Vector3[]? detour = routeRerouter.CalculateDetourAsync(
                    localSegment,
                    playerReader.MapId,
                    token).GetAwaiter().GetResult();

                if (IsMeaningfulDynamicDetour(localSegment, detour))
                {
                    if (IsExcessiveHazardDetour(localSegment, detour!))
                    {
                        logger.LogDebug(
                            "[Navigation       ] Skipping excessive dynamic hazard detour while stalled ({OriginalCount} -> {DetourCount})",
                            localSegment.Length,
                            detour!.Length);
                    }
                    else
                    {
                    Vector3 hazardReconnectPoint = detour![^1];
                    if (ShouldBreakHazardDetourLoop(hazardReconnectPoint, now))
                    {
                        routeToNextWaypoint.Clear();
                        logger.LogWarning(
                            "[Navigation       ] Hazard-detour loop detected near {Reconnect}; clearing active route and suppressing hazard detours for {Cooldown}s",
                            hazardReconnectPoint,
                            HazardDetourBreakerCooldown.TotalSeconds);
                        return true;
                    }

                    Vector3 reconnectPoint = hazardReconnectPoint;
                    if (ShouldSkipReconnectPoint(reconnectPoint, now))
                    {
                        logger.LogDebug(
                            "[Navigation       ] Skipping hazard detour reconnect near recent tail-recalc failure ({Reconnect})",
                            reconnectPoint);
                        return false;
                    }

                    Vector3[] integratedRoute = BuildIntegratedDynamicRoute(detour!, remainingPath);
                    ApplyDynamicRoute(integratedRoute);
                    OnDynamicDetourApplied?.Invoke();

                    logger.LogInformation(
                        "[Navigation       ] Dynamic hazard detour applied while stalled ({OriginalCount} -> {DetourCount} waypoints, reconnect={Reconnect})",
                        remainingPath.Length,
                        integratedRoute.Length,
                        nextWaypoint);

                    return true;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Navigation       ] Dynamic hazard detour attempt failed");
            }
        }

        // Fallback when no hazard-driven detour is available yet:
        // synthesize a short side-step route around a likely obstacle directly in front,
        // then reconnect to the intended next waypoint and keep the remaining route.
        if (goapCurrentGoalState?.IsCurrentGoal(nameof(WalkToCorpseGoal)) == true)
        {
            return false;
        }

        if (now < frontBypassBreakerUntilUtc)
        {
            return false;
        }

        if ((now - lastFrontBypassUtc) < FrontBypassCooldown)
        {
            return false;
        }

        frontBypassAttemptCount++;
        lastFrontBypassUtc = now;

        Vector3[]? bypassPath = BuildFrontObstacleBypassPath(playerPosition, nextWaypoint, frontBypassAttemptCount);
        if (bypassPath is null)
        {
            return false;
        }

        if (ShouldBreakFrontBypassLoop(bypassPath[^1], now))
        {
            routeToNextWaypoint.Clear();
            PrimeFailedReconnectCluster(bypassPath[^1], now);
            logger.LogWarning(
                "[Navigation       ] Front-bypass loop detected near {Reconnect}; clearing active route and suspending bypass for {Cooldown}s",
                bypassPath[^1],
                FrontBypassBreakerCooldown.TotalSeconds);
            return true;
        }

        Vector3[] integratedBypassRoute = BuildIntegratedDynamicRoute(bypassPath, remainingPath);
        if (ShouldBreakFrontBypassNoProgressLoop(remainingPath.Length, integratedBypassRoute.Length, bypassPath[^1], now))
        {
            routeToNextWaypoint.Clear();
            PrimeFailedReconnectCluster(bypassPath[^1], now);
            logger.LogWarning(
                "[Navigation       ] Front-bypass no-progress loop detected near {Reconnect} ({RemainingCount}->{BypassCount}); clearing active route and suspending bypass for {Cooldown}s",
                bypassPath[^1],
                remainingPath.Length,
                integratedBypassRoute.Length,
                FrontBypassBreakerCooldown.TotalSeconds);
            return true;
        }

        ApplyDynamicRoute(integratedBypassRoute);
        OnDynamicDetourApplied?.Invoke();

        logger.LogInformation(
            "[Navigation       ] Dynamic front-obstacle bypass applied ({OriginalCount} -> {BypassCount} waypoints, reconnect={Reconnect}, side={Side})",
            remainingPath.Length,
            integratedBypassRoute.Length,
            nextWaypoint,
            (frontBypassAttemptCount & 1) == 0 ? "left" : "right");

        return true;
    }

    private void PrimeFailedReconnectCluster(Vector3 reconnectPoint, DateTime now)
    {
        lastFailedReconnectPoint = reconnectPoint;
        lastFailedReconnectUtc = now;
        TrackFailedReconnectSample(reconnectPoint, now);
    }

    private void TrackCorpseRecoveryContext(DateTime now)
    {
        if (IsLikelyCorpseRecoveryContext())
        {
            lastCorpseRecoveryContextUtc = now;
        }
    }

    private bool CanUseHazardDetours(DateTime now, Vector3? targetWorld = null)
    {
        if (now < hazardDetourBreakerUntilUtc)
        {
            return false;
        }

        if (IsLikelyCorpseRecoveryContext())
        {
            lastCorpseRecoveryContextUtc = now;
            return false;
        }

        if (lastCorpseRecoveryContextUtc != DateTime.MinValue &&
            (now - lastCorpseRecoveryContextUtc) < CorpseRecoveryHazardSuppressGrace)
        {
            return false;
        }

        if (targetWorld.HasValue && IsCorpseRecoveryTarget(targetWorld.Value))
        {
            lastCorpseRecoveryContextUtc = now;
            return false;
        }

        return true;
    }

    private bool IsCorpseRecoveryTarget(Vector3 targetWorld)
    {
        if (!TryGetCorpseWorldPos(out Vector3 corpseWorld))
        {
            return false;
        }

        return Vector3.Distance(targetWorld, corpseWorld) <= CorpseTargetMatchDistance;
    }

    private bool IsLikelyCorpseRecoveryContext()
    {
        if (goapCurrentGoalState?.IsCurrentGoal(nameof(WalkToCorpseGoal)) == true)
        {
            return true;
        }

        if (bits.Dead() || bits.CorpseInRange())
        {
            return true;
        }

        // Do NOT use CorpseMapX/Y — those remain non-zero for the entire ghost run
        // and would suppress hazard detours even during normal FollowRouteGoal navigation.
        return false;
    }

    private bool TryGetCorpseWorldPos(out Vector3 corpseWorld)
    {
        corpseWorld = default;

        if (playerReader.CorpseMapX == 0f && playerReader.CorpseMapY == 0f)
        {
            return false;
        }

        corpseWorld = WorldMapAreaDB.ToWorld_FlipXY(playerReader.CorpseMapPos, playerReader.WorldMapArea);
        return true;
    }

    private static bool IsExcessiveHazardDetour(Vector3[] originalPath, Vector3[] detourPath)
    {
        if (originalPath.Length < 2 || detourPath.Length < 2)
        {
            return false;
        }

        float originalDistance = VectorExt.TotalDistance<Vector3>(originalPath, VectorExt.WorldDistanceXY);
        float detourDistance = VectorExt.TotalDistance<Vector3>(detourPath, VectorExt.WorldDistanceXY);

        if (originalDistance <= 0.01f || detourDistance <= originalDistance)
        {
            return false;
        }

        float additionalDistance = detourDistance - originalDistance;
        float ratio = detourDistance / originalDistance;

        if (additionalDistance >= HazardDetourHardMaxAdditionalDistance)
        {
            return true;
        }

        return additionalDistance >= HazardDetourMaxAdditionalDistance &&
            ratio >= HazardDetourMaxLengthRatio;
    }

    private bool ShouldBreakHazardDetourLoop(Vector3 reconnectPoint, DateTime now)
    {
        if (lastHazardDetourRepeatUtc == DateTime.MinValue ||
            (now - lastHazardDetourRepeatUtc) > HazardDetourRepeatWindow ||
            Vector3.Distance(reconnectPoint, lastHazardDetourRepeatPoint) > HazardDetourRepeatDistance)
        {
            repeatedHazardDetourCount = 1;
            lastHazardDetourRepeatPoint = reconnectPoint;
            lastHazardDetourRepeatUtc = now;
            return false;
        }

        repeatedHazardDetourCount++;
        lastHazardDetourRepeatPoint = reconnectPoint;
        lastHazardDetourRepeatUtc = now;

        if (repeatedHazardDetourCount < HazardDetourRepeatLimit)
        {
            return false;
        }

        repeatedHazardDetourCount = 0;
        hazardDetourBreakerUntilUtc = now + HazardDetourBreakerCooldown;
        return true;
    }

    private bool ShouldBreakFrontBypassLoop(Vector3 reconnectPoint, DateTime now)
    {
        TrackFrontBypassReconnectSample(reconnectPoint, now);

        if (lastFrontBypassRepeatUtc == DateTime.MinValue ||
            (now - lastFrontBypassRepeatUtc) > FrontBypassRepeatWindow ||
            Vector3.Distance(reconnectPoint, lastFrontBypassRepeatPoint) > FrontBypassRepeatDistance)
        {
            repeatedFrontBypassCount = 1;
            lastFrontBypassRepeatPoint = reconnectPoint;
            lastFrontBypassRepeatUtc = now;
            return false;
        }

        repeatedFrontBypassCount++;
        lastFrontBypassRepeatPoint = reconnectPoint;
        lastFrontBypassRepeatUtc = now;

        if (repeatedFrontBypassCount < FrontBypassRepeatLimit)
        {
            int nearbyReconnects = CountNearbyReconnectSamples(recentFrontBypassReconnects, reconnectPoint, FrontBypassClusterDistance);
            if (nearbyReconnects < FrontBypassClusterLimit)
            {
                return false;
            }
        }

        repeatedFrontBypassCount = 0;
        frontBypassBreakerUntilUtc = now + FrontBypassBreakerCooldown;
        return true;
    }

    private bool ShouldBreakFrontBypassNoProgressLoop(int remainingCount, int bypassCount, Vector3 reconnectPoint, DateTime now)
    {
        if (Math.Abs(bypassCount - remainingCount) > FrontBypassNoProgressMaxRouteDelta)
        {
            repeatedFrontBypassNoProgressCount = 0;
            return false;
        }

        if (lastFrontBypassNoProgressUtc == DateTime.MinValue ||
            (now - lastFrontBypassNoProgressUtc) > FrontBypassNoProgressWindow ||
            Vector3.Distance(reconnectPoint, lastFrontBypassNoProgressPoint) > FrontBypassClusterDistance)
        {
            repeatedFrontBypassNoProgressCount = 1;
            lastFrontBypassNoProgressPoint = reconnectPoint;
            lastFrontBypassNoProgressUtc = now;
            return false;
        }

        repeatedFrontBypassNoProgressCount++;
        lastFrontBypassNoProgressPoint = reconnectPoint;
        lastFrontBypassNoProgressUtc = now;

        if (repeatedFrontBypassNoProgressCount < FrontBypassNoProgressRepeatLimit)
        {
            return false;
        }

        repeatedFrontBypassNoProgressCount = 0;
        frontBypassBreakerUntilUtc = now + FrontBypassBreakerCooldown;
        return true;
    }

    private void TrackFrontBypassReconnectSample(Vector3 reconnectPoint, DateTime now)
    {
        PruneReconnectSamples(recentFrontBypassReconnects, now, FrontBypassClusterWindow);
        recentFrontBypassReconnects.Enqueue(new ReconnectSample(now, reconnectPoint));
    }

    private void TrackFailedReconnectSample(Vector3 reconnectPoint, DateTime now)
    {
        PruneReconnectSamples(recentFailedReconnects, now, FailedReconnectClusterWindow);
        recentFailedReconnects.Enqueue(new ReconnectSample(now, reconnectPoint));
    }

    private static void PruneReconnectSamples(Queue<ReconnectSample> samples, DateTime now, TimeSpan window)
    {
        while (samples.Count > 0 && (now - samples.Peek().TimestampUtc) > window)
        {
            samples.Dequeue();
        }
    }

    private static int CountNearbyReconnectSamples(Queue<ReconnectSample> samples, Vector3 point, float maxDistance)
    {
        int count = 0;
        foreach (ReconnectSample sample in samples)
        {
            if (Vector3.Distance(sample.Point, point) <= maxDistance)
            {
                count++;
            }
        }

        return count;
    }

    private readonly record struct ReconnectSample(DateTime TimestampUtc, Vector3 Point);

    internal static bool IsMeaningfulDynamicDetour(Vector3[] originalPath, Vector3[]? detour)
    {
        if (detour is not { Length: >= 2 })
        {
            return false;
        }

        if (detour.Length > originalPath.Length)
        {
            return true;
        }

        int compareCount = Math.Min(originalPath.Length, detour.Length);
        float accumulatedDelta = 0f;

        for (int i = 1; i < compareCount; i++)
        {
            accumulatedDelta += Vector3.Distance(originalPath[i], detour[i]);
        }

        return accumulatedDelta >= 2.5f;
    }

    internal static Vector3[] StitchDetourWithRemainingPath(
        Vector3[] detourPath,
        Vector3[] remainingPath,
        float duplicateDistance = 1.0f)
    {
        if (remainingPath.Length == 0)
        {
            return Array.Empty<Vector3>();
        }

        if (detourPath.Length < 2)
        {
            return remainingPath;
        }

        List<Vector3> stitched = new(detourPath.Length + remainingPath.Length);

        static void AddIfDistinct(List<Vector3> points, Vector3 candidate, float minDistance)
        {
            if (points.Count > 0 &&
                Vector3.Distance(points[points.Count - 1], candidate) <= minDistance)
            {
                return;
            }

            points.Add(candidate);
        }

        // Skip index 0 because it is the current position.
        for (int i = 1; i < detourPath.Length; i++)
        {
            AddIfDistinct(stitched, detourPath[i], duplicateDistance);
        }

        for (int i = 1; i < remainingPath.Length; i++)
        {
            AddIfDistinct(stitched, remainingPath[i], duplicateDistance);
        }

        return stitched.ToArray();
    }

    internal static Vector3[] MergeRouteSegments(
        Vector3[] firstSegment,
        Vector3[] secondSegment,
        float duplicateDistance = 1.0f)
    {
        if (firstSegment.Length == 0)
        {
            return secondSegment;
        }

        if (secondSegment.Length == 0)
        {
            return firstSegment;
        }

        List<Vector3> merged = new(firstSegment.Length + secondSegment.Length);

        static void AddIfDistinct(List<Vector3> points, Vector3 candidate, float minDistance)
        {
            if (points.Count > 0 &&
                Vector3.Distance(points[points.Count - 1], candidate) <= minDistance)
            {
                return;
            }

            points.Add(candidate);
        }

        for (int i = 0; i < firstSegment.Length; i++)
        {
            AddIfDistinct(merged, firstSegment[i], duplicateDistance);
        }

        for (int i = 0; i < secondSegment.Length; i++)
        {
            AddIfDistinct(merged, secondSegment[i], duplicateDistance);
        }

        return merged.ToArray();
    }

    internal static Vector3[]? BuildFrontObstacleBypassPath(Vector3 start, Vector3 nextWaypoint, int attemptIndex)
    {
        Vector3 toTarget = nextWaypoint - start;
        toTarget.Z = 0;

        float distance = toTarget.Length();
        if (distance < 1.5f)
        {
            return null;
        }

        Vector3 direction = Vector3.Normalize(toTarget);
        Vector3 perpendicular = Vector3.Normalize(new Vector3(-direction.Y, direction.X, 0));

        float sideSign = (attemptIndex & 1) == 0 ? 1f : -1f;
        float lateralDistance = Math.Clamp(distance * 0.5f, 3.0f, 7.5f);
        float forwardDistance = Math.Clamp(distance * 0.45f, 2.5f, 9.0f);

        Vector3 bypass = start + (direction * forwardDistance) + (perpendicular * lateralDistance * sideSign);
        bypass.Z = nextWaypoint.Z != 0 ? nextWaypoint.Z : start.Z;

        return [start, bypass, nextWaypoint];
    }

    private Vector3[] BuildIntegratedDynamicRoute(Vector3[] localDetour, Vector3[] remainingPath)
    {
        Vector3[]? recalculatedTail = TryRecalculateTailRoute(localDetour[^1]);
        if (recalculatedTail is { Length: >= 2 })
        {
            Vector3[] merged = MergeRouteSegments(localDetour, recalculatedTail, DynamicReconnectDuplicateDistance);

            if (merged.Length >= 2)
            {
                logger.LogInformation(
                    "[Navigation       ] Dynamic route recalculated from reconnect ({LocalCount} + {TailCount} -> {MergedCount})",
                    localDetour.Length,
                    recalculatedTail.Length,
                    merged.Length);

                OnSuccessfulReconnect?.Invoke();

                // ApplyDynamicRoute expects route waypoints without current player position at index 0.
                // localDetour starts at player, so strip only that first point.
                if (merged.Length > 1 && Vector3.Distance(merged[0], playerReader.WorldPos) <= DynamicReconnectDuplicateDistance)
                {
                    Vector3[] trimmed = new Vector3[merged.Length - 1];
                    Array.Copy(merged, 1, trimmed, 0, trimmed.Length);
                    return trimmed;
                }

                return merged;
            }
        }

        Interlocked.Increment(ref tailRecalcFailures);
        lastFailedReconnectPoint = localDetour[^1];
        lastFailedReconnectUtc = DateTime.UtcNow;
        TrackFailedReconnectSample(localDetour[^1], lastFailedReconnectUtc);
        logger.LogWarning(
            "[Navigation       ] Tail recalc unavailable - pather returned no usable path (failures: {FailureCount}, reconnect={Reconnect}); using stitched fallback",
            tailRecalcFailures,
            localDetour[^1]);
        return StitchDetourWithRemainingPath(localDetour, remainingPath, DynamicReconnectDuplicateDistance);
    }

    private bool ShouldSkipReconnectPoint(Vector3 reconnectPoint, DateTime now)
    {
        if (lastFailedReconnectUtc == DateTime.MinValue)
        {
            return false;
        }

        if ((now - lastFailedReconnectUtc) > FailedReconnectCooldown)
        {
            return false;
        }

        if (Vector3.Distance(reconnectPoint, lastFailedReconnectPoint) <= FailedReconnectDuplicateDistance)
        {
            return true;
        }

        PruneReconnectSamples(recentFailedReconnects, now, FailedReconnectClusterWindow);
        int nearbyFailures = CountNearbyReconnectSamples(recentFailedReconnects, reconnectPoint, FailedReconnectClusterDistance);
        if (nearbyFailures >= FailedReconnectClusterSkipThreshold)
        {
            logger.LogDebug(
                "[Navigation       ] Skipping reconnect near repeated tail-recalc failure cluster ({Reconnect}, nearbyFailures={NearbyFailures})",
                reconnectPoint,
                nearbyFailures);
            return true;
        }

        return false;
    }

    private Vector3[]? TryRecalculateTailRoute(Vector3 reconnectPoint)
    {
        if (wayPoints.Count == 0)
        {
            return null;
        }

        Vector3 destination = wayPoints.Peek();
        if (Vector3.Distance(reconnectPoint, destination) <= OutDoorMinDistance)
        {
            return [reconnectPoint, destination];
        }

        Vector3[] refreshed = pather.FindWorldRoute(
            playerReader.UIMapId.Value,
            bits.Indoors(),
            reconnectPoint,
            destination);

        if (refreshed.Length < 2)
        {
            return null;
        }

        // Optionally re-apply hazard detour on the refreshed tail for consistent global avoidance.
        if (routeRerouter?.IsEnabled == true && CanUseHazardDetours(DateTime.UtcNow, destination))
        {
            try
            {
                Vector3[]? hazardTail = routeRerouter.CalculateDetourAsync(
                    refreshed,
                    playerReader.MapId,
                    token).GetAwaiter().GetResult();

                if (hazardTail is { Length: >= 2 } &&
                    IsMeaningfulDynamicDetour(refreshed, hazardTail) &&
                    !IsExcessiveHazardDetour(refreshed, hazardTail))
                {
                    return hazardTail;
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "[Navigation       ] Tail hazard detour recalculation skipped due to error");
            }
        }

        return refreshed;
    }

    private void ApplyDynamicRoute(Vector3[] route)
    {
        routeToNextWaypoint.Clear();

        for (int i = route.Length - 1; i >= 0; i--)
        {
            routeToNextWaypoint.Push(route[i]);
        }

        if (routeToNextWaypoint.Count > 0)
        {
            stuckDetector.SetTargetLocation(routeToNextWaypoint.Peek());
        }

        UpdateTotalRoute();
    }

    private double GetRouteResetTimeoutMs()
    {
        if (featureFlagService == null)
        {
            return DefaultRouteResetTimeoutMs;
        }

        StuckSensitivityOptions options = featureFlagService.Current.StuckSensitivity;
        if (!options.Enabled)
        {
            return DefaultRouteResetTimeoutMs;
        }

        float multiplier = Math.Clamp(options.ApproachTimeoutMultiplier, 0.5f, 3f);
        return DefaultRouteResetTimeoutMs * multiplier;
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

    #region Humanization

    #endregion
}
