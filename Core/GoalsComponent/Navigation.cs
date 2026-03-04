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
using System.Threading.Tasks;

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
    private const float PrecisionVerticalZDelta = 0.9f;
    private const float PrecisionReachedDistanceMin = 0.9f;
    private const float PrecisionReachedDistanceScale = 0.45f;
    private const float PrecisionSteeringIgnoreDistance = 0.4f;
    private const float PrecisionTurnLookaheadDistance = 8f;
    private const float TightTurnPrecisionRadians = PI / 5f;
    private const float SimplifyPreserveVerticalZDelta = 0.75f;
    private const float SimplifyPreserveTurnRadians = PI / 6f;
    private const float SimplifyPreserveIndoorTurnRadians = PI / 9f; // 20 degrees
    private const float DetourDuplicateDistance = 1.0f;
    private const float DetourReconnectDistance = 2.0f;
    private const int RouteAwareDetourLookaheadPoints = 4;
    private const float RouteAwareDetourMinAnchorDistance = 8.0f;
    private const int PendingRerouteMaxAgeSeconds = 15;
    private const float PendingRerouteTargetMatchDistance = 4.0f;
    private const int TurnInPlaceStuckGraceMs = 1200;
    private const int SharpTurnStuckGraceMs = 1800;
    private int tailRecalcFailures;
    private float lastHeadingDiffRadians;
    private DateTime turnStuckGraceUntilUtc = DateTime.MinValue;
    private DateTime? lastSuppressedTurnStuckRecoveryUtc;
    private int suppressedTurnStuckRecoveryCount;
    private bool hadNoPathFailureSinceLastPath;

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

        // Apply any pending hazard-avoidance detour inline (feature-flag gated)
        if (routeRerouter != null &&
            featureFlagService?.Current.HazardAvoidance.Enabled == true &&
            routeRerouter.GetActiveReroute() is { } pendingReroute)
        {
            Vector3[] routeSnapshot = routeToNextWaypoint.ToArray();
            if (ShouldApplyPendingReroute(pendingReroute, routeSnapshot, DateTime.UtcNow))
            {
                InsertDetourIntoRoute(pendingReroute.DetourWaypoints);
            }
            else if (debug)
            {
                LogDebug($"[HazardAvoidance] Dropping stale/mismatched reroute {pendingReroute.Id} target={pendingReroute.OriginalTarget}");
            }

            ClearPendingReroute();
        }

        LastActive = DateTime.UtcNow;
        input.StartForward(true);

        // main loop
        Vector3 playerW = playerReader.WorldPos;
        playerWorldPos = playerW;
        Vector3 targetW = routeToNextWaypoint.Peek();
        float worldDistance = playerW.WorldDistanceXYTo(targetW);
        TryScheduleRouteAwareReroute(playerW, token);

        if (routeToNextWaypoint.Count >= 2 &&
            TryGetUpcomingRoutePoints(out Vector3 current, out Vector3 next))
        {
            Vector2 playerXY = new(playerW.X, playerW.Y);
            Vector2 closestPoint = VectorExt.GetClosestPointOnLineSegment(
                current.AsVector2(),
                next.AsVector2(),
                playerXY);
            float deviation = Vector2.Distance(playerXY, closestPoint);
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
                DateTime now = DateTime.UtcNow;
                if (ShouldSuppressStuckRecoveryForIntentionalTurn(now))
                {
                    suppressedTurnStuckRecoveryCount++;
                    lastSuppressedTurnStuckRecoveryUtc = now;
                    AdjustHeading(heading, steeringIgnoreDistance, token);
                    lastWorldDistance = worldDistance;
                    return;
                }

                if (stuckDetector.ActionDurationMs > GetRouteResetTimeoutMs())
                {
                    if (mountHandler.IsMounted())
                        mountHandler.Dismount();

                    LogClearRouteToWaypointStuck(logger, stuckDetector.ActionDurationMs);
                    stuckDetector.Reset();
                    routeToNextWaypoint.Clear();
                    ClearPendingReroute();
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
        ClearPendingReroute();
        hadNoPathFailureSinceLastPath = false;

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
        ClearPendingReroute();
        hadNoPathFailureSinceLastPath = false;

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
        turnStuckGraceUntilUtc = DateTime.MinValue;
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
            hadNoPathFailureSinceLastPath = true;

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

        bool recoveredFromNoPath = hadNoPathFailureSinceLastPath;
        hadNoPathFailureSinceLastPath = false;
        failedAttempt = 0;
        lastNoPathUtc = default;

        // Log pathfinder success and populate route
        {
            Vector3[] pathToApply = result.Path;

            LogPathfinderSuccess(logger, result.Distance, result.StartW, result.EndW, result.ElapsedMs);

            for (int i = pathToApply.Length - 1; i >= 0; i--)
            {
                routeToNextWaypoint.Push(pathToApply[i]);
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

        if (recoveredFromNoPath)
        {
            OnSuccessfulReconnect?.Invoke();
        }

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
        lastHeadingDiffRadians = diff;

        if (stuckDetector.IsCurrentlyStuck)
        {
            return;
        }

        // Nav recovery baseline: removed oscillation detector integration
        // and heading throttle. These added complexity that masked real stuck
        // conditions and created sluggish steering. Just turn if needed.
        if (diff > minAngleToTurn)
        {
            SetTurnStuckGraceWindow(now);

            if (diff > minAngleToStopBeforeTurn)
            {
                stopMoving.Stop();
            }

            playerDirection.SetDirection(heading, routeToNextWaypoint.Peek(), steeringIgnoreDistance, token);
        }
    }

    private void SetTurnStuckGraceWindow(DateTime now)
    {
        bool sharpTurn = false;
        if (routeToNextWaypoint.Count >= 2 &&
            TryGetUpcomingRoutePoints(out Vector3 current, out Vector3 next) &&
            IsSharpTurn(playerReader.WorldPos, current, next, TightTurnPrecisionRadians))
        {
            sharpTurn = true;
        }

        int graceMs = GetTurnStuckGraceMs(sharpTurn);
        DateTime candidate = now.AddMilliseconds(graceMs);
        if (candidate > turnStuckGraceUntilUtc)
        {
            turnStuckGraceUntilUtc = candidate;
        }
    }

    private int GetTurnStuckGraceMs(bool sharpTurn)
    {
        int baseGraceMs = sharpTurn ? SharpTurnStuckGraceMs : TurnInPlaceStuckGraceMs;
        if (featureFlagService == null)
        {
            return baseGraceMs;
        }

        StuckSensitivityOptions options = featureFlagService.Current.StuckSensitivity;
        if (!options.Enabled)
        {
            return baseGraceMs;
        }

        double unstuckAfterMs = Math.Clamp(options.UnstuckAfterMs, 750, 10_000);
        double baseWindow = Math.Clamp(unstuckAfterMs + 1200, 1_500, 15_000);
        double multiplier = Math.Clamp(options.ApproachTimeoutMultiplier, 1.0f, 3f);
        double actionThresholdMs = baseWindow * multiplier;

        int extraBufferMs = sharpTurn ? 450 : 300;
        int maxGraceMs = sharpTurn ? 7500 : 6000;
        int scaledGraceMs = (int)Math.Ceiling(Math.Clamp(actionThresholdMs + extraBufferMs, baseGraceMs, maxGraceMs));
        return scaledGraceMs;
    }

    private bool ShouldSuppressStuckRecoveryForIntentionalTurn(DateTime now)
    {
        if (stuckDetector.IsCurrentlyStuck || routeToNextWaypoint.Count == 0)
        {
            return false;
        }

        if (now >= turnStuckGraceUntilUtc)
        {
            return false;
        }

        return lastHeadingDiffRadians > minAngleToTurn;
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

        float simplifyTolerance = bits.Indoors() ? IndoorMinDistance / 2f : OutDoorMinDistance / 2f;
        Span<Vector3> reduced = PathSimplify.Simplify(route, simplifyTolerance, HighQuality);
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

        // Always preserve sharp turns regardless of mount status.
        // Use a tighter threshold indoors to catch moderate corridor bends (≥20°).
        float preserveTurnThreshold = bits.Indoors()
            ? SimplifyPreserveIndoorTurnRadians  // 20°
            : SimplifyPreserveTurnRadians;       // 30°
        for (int i = 0; i < inspectCount - 2; i++)
        {
            if (IsSharpTurn(route[i], route[i + 1], route[i + 2], preserveTurnThreshold))
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

        // Keep route resets conservative even if stuck sensitivity is tuned aggressively.
        // Lower values caused false "clear route" churn on normal turn-heavy waypoint segments.
        float multiplier = Math.Clamp(options.ApproachTimeoutMultiplier, 1.0f, 3f);
        return DefaultRouteResetTimeoutMs * multiplier;
    }

    public NavigationRuntimeSnapshot GetRuntimeSnapshot()
    {
        float? distanceToNextWaypoint = null;
        if (routeToNextWaypoint.Count > 0)
        {
            distanceToNextWaypoint = playerReader.WorldPos.WorldDistanceXYTo(routeToNextWaypoint.Peek());
        }

        DateTime now = DateTime.UtcNow;
        bool turnStuckGraceActive = now < turnStuckGraceUntilUtc;
        int turnStuckGraceRemainingMs = turnStuckGraceActive
            ? (int)Math.Ceiling((turnStuckGraceUntilUtc - now).TotalMilliseconds)
            : 0;

        return new NavigationRuntimeSnapshot(
            RouteToNextWaypointCount: routeToNextWaypoint.Count,
            WayPointCount: wayPoints.Count,
            TailRecalcFailures: tailRecalcFailures,
            DistanceToNextWaypoint: distanceToNextWaypoint,
            LastHeadingDiffRadians: lastHeadingDiffRadians,
            OscillationDetected: false,
            OscillationCount: 0,
            TurnStuckGraceActive: turnStuckGraceActive,
            TurnStuckGraceRemainingMs: turnStuckGraceRemainingMs,
            SuppressedTurnStuckRecoveryCount: suppressedTurnStuckRecoveryCount,
            LastSuppressedTurnStuckRecoveryUtc: lastSuppressedTurnStuckRecoveryUtc,
            FrontBypassBreakerActive: false,
            FrontBypassBreakerRemainingMs: 0,
            HazardDetourBreakerActive: false,
            HazardDetourBreakerRemainingMs: 0,
            FrontBypassAttemptCount: 0,
            RepeatedFrontBypassCount: 0,
            RepeatedFrontBypassNoProgressCount: 0,
            RepeatedHazardDetourCount: 0);
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

    #region HazardAvoidance

    private void TryScheduleRouteAwareReroute(Vector3 playerPosition, CancellationToken token)
    {
        IRouteRerouter? rerouter = routeRerouter;
        if (rerouter == null ||
            featureFlagService?.Current.HazardAvoidance.Enabled != true ||
            routeToNextWaypoint.Count < 2 ||
            rerouter.GetActiveReroute() != null)
        {
            return;
        }

        Vector3[] routeSnapshot = routeToNextWaypoint.ToArray();
        if (!TrySelectDetourAnchor(routeSnapshot, playerPosition, out Vector3 detourAnchor))
        {
            return;
        }

        _ = TriggerRouteAwareRerouteAsync(rerouter, playerPosition, detourAnchor, playerReader.UIMapId.Value, token);
    }

    private async Task TriggerRouteAwareRerouteAsync(
        IRouteRerouter rerouter,
        Vector3 playerPosition,
        Vector3 detourAnchor,
        int mapId,
        CancellationToken token)
    {
        try
        {
            await rerouter.TriggerRerouteAsync(playerPosition, detourAnchor, mapId, token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Navigation       ] Route-aware reroute scheduling failed");
        }
    }

    internal static bool TrySelectDetourAnchor(
        IReadOnlyList<Vector3> routeTopFirst,
        Vector3 playerPosition,
        out Vector3 detourAnchor,
        int lookaheadPoints = RouteAwareDetourLookaheadPoints,
        float minAnchorDistance = RouteAwareDetourMinAnchorDistance)
    {
        detourAnchor = default;
        if (routeTopFirst.Count == 0)
        {
            return false;
        }

        int inspectCount = Math.Min(Math.Max(1, routeTopFirst.Count - 1), Math.Max(1, lookaheadPoints));
        float furthestDistance = -1f;
        Vector3 furthestPoint = default;

        for (int i = 0; i < inspectCount; i++)
        {
            Vector3 candidate = routeTopFirst[i];
            float distance = playerPosition.WorldDistanceXYTo(candidate);

            if (distance > furthestDistance)
            {
                furthestDistance = distance;
                furthestPoint = candidate;
            }

            if (distance >= minAnchorDistance)
            {
                detourAnchor = candidate;
                return true;
            }
        }

        if (furthestDistance <= 0.01f)
        {
            return false;
        }

        detourAnchor = furthestPoint;
        return true;
    }

    internal static bool ShouldApplyPendingReroute(
        RerouteInfo pendingReroute,
        IReadOnlyList<Vector3> routeTopFirst,
        DateTime nowUtc,
        int maxAgeSeconds = PendingRerouteMaxAgeSeconds,
        float targetMatchDistance = PendingRerouteTargetMatchDistance)
    {
        if (routeTopFirst.Count == 0)
        {
            return false;
        }

        TimeSpan age = nowUtc - pendingReroute.StartedAt;
        if (age < TimeSpan.Zero || age > TimeSpan.FromSeconds(maxAgeSeconds))
        {
            return false;
        }

        for (int i = 0; i < routeTopFirst.Count; i++)
        {
            if (routeTopFirst[i].WorldDistanceXYTo(pendingReroute.OriginalTarget) <= targetMatchDistance)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearPendingReroute()
    {
        routeRerouter?.ClearActiveReroute();
    }

    internal static Vector3[] BuildInlineDetourRoute(
        Vector3[] detourWaypoints,
        IReadOnlyList<Vector3> preservedRouteTopFirst,
        Vector3 playerPosition,
        float reconnectDistance = DetourReconnectDistance,
        float duplicateDistance = DetourDuplicateDistance)
    {
        int continuationStartIndex = 0;
        if (detourWaypoints.Length > 0 && preservedRouteTopFirst.Count > 0)
        {
            Vector3 reconnectPoint = detourWaypoints[^1];
            int reconnectIndex = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < preservedRouteTopFirst.Count; i++)
            {
                float distance = preservedRouteTopFirst[i].WorldDistanceXYTo(reconnectPoint);
                if (distance <= reconnectDistance &&
                    (distance < bestDistance || (Abs(distance - bestDistance) <= 0.001f && i > reconnectIndex)))
                {
                    reconnectIndex = i;
                    bestDistance = distance;
                }
            }

            if (reconnectIndex >= 0)
            {
                continuationStartIndex = reconnectIndex + 1;
            }
            else
            {
                int destinationIndex = preservedRouteTopFirst.Count - 1;
                if (preservedRouteTopFirst[destinationIndex].WorldDistanceXYTo(reconnectPoint) <= reconnectDistance * 4f)
                {
                    continuationStartIndex = preservedRouteTopFirst.Count;
                }
            }
        }

        List<Vector3> mergedTopFirst = new(detourWaypoints.Length + Math.Max(0, preservedRouteTopFirst.Count - continuationStartIndex));

        static void AddIfDistinct(List<Vector3> points, Vector3 candidate, float minDistance)
        {
            if (points.Count > 0 &&
                Vector3.Distance(points[points.Count - 1], candidate) <= minDistance)
            {
                return;
            }

            points.Add(candidate);
        }

        for (int i = 0; i < detourWaypoints.Length; i++)
        {
            Vector3 candidate = detourWaypoints[i];
            if (mergedTopFirst.Count == 0 &&
                playerPosition.WorldDistanceXYTo(candidate) <= duplicateDistance)
            {
                continue;
            }

            AddIfDistinct(mergedTopFirst, candidate, duplicateDistance);
        }

        for (int i = continuationStartIndex; i < preservedRouteTopFirst.Count; i++)
        {
            AddIfDistinct(mergedTopFirst, preservedRouteTopFirst[i], duplicateDistance);
        }

        return mergedTopFirst.ToArray();
    }

    /// <summary>
    /// Prepends detour waypoints to the front of the current route without replacing it.
    /// After the detour is traversed, normal route progression resumes.
    /// </summary>
    private void InsertDetourIntoRoute(Vector3[] detourWaypoints)
    {
        if (detourWaypoints.Length == 0)
        {
            return;
        }

        // Drain current route into a temporary list (stack order: top-first = nearest-first)
        List<Vector3> preserved = new(routeToNextWaypoint.Count);
        int preservedCount = routeToNextWaypoint.Count;
        while (routeToNextWaypoint.Count > 0)
        {
            preserved.Add(routeToNextWaypoint.Pop());
        }

        Vector3[] mergedRoute = BuildInlineDetourRoute(
            detourWaypoints,
            preserved,
            playerReader.WorldPos);

        for (int i = mergedRoute.Length - 1; i >= 0; i--)
        {
            routeToNextWaypoint.Push(mergedRoute[i]);
        }

        stuckDetector.Reset();
        if (routeToNextWaypoint.Count > 0)
        {
            stuckDetector.SetTargetLocation(routeToNextWaypoint.Peek());
        }

        OnDynamicDetourApplied?.Invoke();
        if (debug)
        {
            LogDebug($"[HazardAvoidance] Inserted detour ({detourWaypoints.Length} points) into route ({preservedCount} points) => {routeToNextWaypoint.Count} points");
        }
    }

    #endregion
}
