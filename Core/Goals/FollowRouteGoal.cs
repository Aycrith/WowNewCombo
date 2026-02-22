using Core.GOAP;

using Game;

using Microsoft.Extensions.Logging;

using SharedLib.Extensions;
using SharedLib.NpcFinder;

using System;
using System.Linq;
using System.Numerics;
using System.Threading;

#pragma warning disable 162

namespace Core.Goals;

public sealed class FollowRouteGoal : GoapGoal, IGoapEventListener, IRouteProvider, IEditedRouteReceiver, IDisposable
{
    public const float DEFAULT_COST = 20f;
    public const float COST_OFFSET = 0.1f;

    private readonly float cost;
    public override float Cost => cost;
    public override bool CanRun() => pathSettings.CanRun();

    private const bool debug = false;

    private readonly ILogger<FollowRouteGoal> logger;
    private readonly ConfigurableInput input;
    private readonly Wait wait;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly ClassConfiguration classConfig;
    private readonly IMountHandler mountHandler;
    private readonly Navigation navigation;
    private readonly ExecGameCommand execGameCommand;

    private readonly IBlacklist targetBlacklist;
    private readonly TargetFinder targetFinder;
    private const NpcNames NpcNameToFind = NpcNames.Enemy | NpcNames.Neutral;

    /// <summary>
    /// Minimum time in milliseconds before starting profession cycling when entering a route.
    /// Prevents immediate profession actions that might conflict with route initialization.
    /// </summary>
    private const int MIN_TIME_TO_START_CYCLE_PROFESSION = 5000;

    /// <summary>
    /// Period in milliseconds between profession cycles (e.g., mining, herbing checks).
    /// Balances responsiveness with performance - checking too frequently wastes CPU.
    /// </summary>
    private const int CYCLE_PROFESSION_PERIOD = 8000;

    private readonly ManualResetEventSlim sideActivityManualReset;
    private readonly Thread? sideActivityThread;
    private CancellationTokenSource sideActivityCts;

    private readonly PathSettings pathSettings;

    private Vector3[] mapRoute
    {
        get => pathSettings.Path;
        set => pathSettings.Path = value;
    }

    private DateTime onEnterTime;
    private bool refillByOther;
    private bool warnedSwimming;
    private bool warnedZoneMismatch;

    #region IRouteProvider

    public DateTime LastActive => navigation.LastActive;

    public Vector3[] MapRoute() => mapRoute;

    public Vector3[] PathingRoute()
    {
        return navigation.TotalRoute;
    }

    public bool HasNext()
    {
        return navigation.HasNext();
    }

    public Vector3 NextMapPoint()
    {
        return navigation.NextMapPoint();
    }

    #endregion

    public FollowRouteGoal(
        float cost,
        PathSettings pathSettings,
        ILogger<FollowRouteGoal> logger,
        ConfigurableInput input, Wait wait, PlayerReader playerReader,
        AddonBits bits,
        ClassConfiguration classConfig,
        Navigation navigation,
        IMountHandler mountHandler, TargetFinder targetFinder,
        IBlacklist targetBlacklist,
        ExecGameCommand execGameCommand)
    : base("Follow " + System.IO.Path.GetFileNameWithoutExtension(pathSettings.FileName))
    {
        this.cost = cost;

        this.logger = logger;
        this.input = input;
        this.wait = wait;
        this.classConfig = classConfig;
        this.playerReader = playerReader;
        this.bits = bits;
        this.pathSettings = pathSettings;
        this.mountHandler = mountHandler;
        this.targetFinder = targetFinder;
        this.targetBlacklist = targetBlacklist;

        if (pathSettings.Requirements.Count > 0)
        {
            Keys = [
             new KeyAction() {
                RequirementsRuntime = pathSettings.RequirementsRuntime,
                Name = "Follow " + System.IO.Path.GetFileNameWithoutExtension(pathSettings.FileName)
            }];
        }

        pathSettings.Finished = () => !navigation.HasWaypoint();

        this.navigation = navigation;
        navigation.OnPathCalculated += Navigation_OnPathCalculated;
        navigation.OnDestinationReached += Navigation_OnDestinationReached;
        navigation.OnWayPointReached += Navigation_OnWayPointReached;

        this.execGameCommand = execGameCommand;

        if (classConfig.Mode == Mode.AttendedGather)
        {
            AddPrecondition(GoapKey.dangercombat, false);
            navigation.OnAnyPointReached += Navigation_OnWayPointReached;
        }
        else
        {
            if (classConfig.Loot)
            {
                AddPrecondition(GoapKey.incombat, false);
            }

            AddPrecondition(GoapKey.damagedone, false);
            AddPrecondition(GoapKey.damagetaken, false);

            AddPrecondition(GoapKey.producedcorpse, false);
            AddPrecondition(GoapKey.consumecorpse, false);
        }

        sideActivityCts = new();
        sideActivityManualReset = new(false);

        if (classConfig.Mode == Mode.AttendedGather)
        {
            if (classConfig.GatherFindKeyConfig.Length > 0)
            {
                sideActivityThread = new(Thread_AttendedGather);
                sideActivityThread.Start();
            }
        }
        else
        {
            sideActivityThread = new(Thread_LookingForTarget);
            sideActivityThread.Start();
        }
    }

    public void Dispose()
    {
        navigation.Dispose();

        sideActivityCts.Cancel();
        sideActivityManualReset.Set();
    }

    private void Abort()
    {
        if (!targetBlacklist.Is())
            navigation.StopMovement();

        navigation.Stop();

        sideActivityManualReset.Reset();
        targetFinder.Reset();
    }

    private void Resume()
    {
        SendGoapEvent(FollowRouteChanged.Instance);

        if (sideActivityCts.IsCancellationRequested)
        {
            sideActivityCts = new();
        }
        sideActivityManualReset.Set();

        if (!navigation.HasWaypoint() || refillByOther)
        {
            refillByOther = false;
            RefillWaypoints(true);
        }
        else
        {
            navigation.Resume();
        }

        if (playerReader.Class != UnitClass.Druid)
            MountIfPossible();

        onEnterTime = DateTime.UtcNow;

        if (classConfig.Mode == Mode.AttendedGather &&
            classConfig.GatherFindKeyConfig.Length > 0)
        {
            // Ensure tracking is active even when only one profession is configured (e.g. Mining only).
            AlternateGatherTypes();
        }
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e.GetType() == typeof(AbortEvent))
        {
            Abort();
        }
        else if (e.GetType() == typeof(ResumeEvent))
        {
            Resume();
        }
        else if (e.GetType() == typeof(FollowRouteChanged))
        {
            refillByOther = true;
        }
    }

    public override void OnEnter() => Resume();

    public override void OnExit() => Abort();

    public override void Update()
    {
        if (pathSettings.IsZoneMismatch())
        {
            if (!warnedZoneMismatch)
            {
                LogWarning($"Route zone mismatch detected for '{pathSettings.FileName}'. Stopping movement.");
                warnedZoneMismatch = true;
            }

            navigation.StopMovement();
            navigation.Stop();
            return;
        }

        warnedZoneMismatch = false;

        if (bits.Swimming())
        {
            if (!warnedSwimming)
            {
                LogWarning("Swimming detected while following route. Stopping movement to avoid drifting farther into water.");
                warnedSwimming = true;
            }

            navigation.StopMovement();
            navigation.Stop();
            input.PressJump();
            return;
        }

        warnedSwimming = false;

        if (bits.Target() && bits.Target_Dead())
        {
            Log("Has target but its dead.");
            bool cleared = input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
            if (!cleared && bits.Target())
            {
                SendGoapEvent(ScreenCaptureEvent.Default);
                LogWarning($"Unable to clear target! Check Bindpad settings!");
            }
        }

        if (bits.Drowning())
        {
            input.PressJump();
        }

        if (bits.Combat() && classConfig.Mode != Mode.AttendedGather) { return; }

        if (!sideActivityCts.IsCancellationRequested)
        {
            navigation.Update(sideActivityCts.Token);
        }
        else
        {
            if (!bits.Target())
            {
                LogWarning($"{nameof(sideActivityCts)} is cancelled but needs to be restarted!");
                sideActivityCts = new();
                sideActivityManualReset.Set();
            }
        }

        RandomJump();

        wait.Update();
    }

    private void Thread_LookingForTarget()
    {
        sideActivityManualReset.Wait();

        while (!sideActivityCts.IsCancellationRequested)
        {
            if (pathSettings.CanRunSideActivity() &&
                targetFinder.Search(NpcNameToFind, bits.Target_NotDead, sideActivityCts.Token))
            {
                if (bits.Target() && targetBlacklist.Is())
                {
                    Log("Blacklisted target found, clearing target");
                    input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
                    continue; // Don't fall through - loop again to find a valid target
                }

                if (bits.Target())
                {
                    Log("Found target!");
                    sideActivityCts.Cancel();
                    sideActivityManualReset.Reset();
                }
            }

            wait.Update();
            sideActivityManualReset.Wait();
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("LookingForTarget Thread stopped!");
    }

    private void Thread_AttendedGather()
    {
        sideActivityManualReset.Wait();

        while (!sideActivityCts.IsCancellationRequested)
        {
            if ((DateTime.UtcNow - onEnterTime).TotalMilliseconds > MIN_TIME_TO_START_CYCLE_PROFESSION)
            {
                AlternateGatherTypes();
            }
            sideActivityCts.Token.WaitHandle.WaitOne(CYCLE_PROFESSION_PERIOD);
            sideActivityManualReset.Wait();
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("AttendedGather Thread stopped!");
    }

    private void AlternateGatherTypes()
    {
        var oldestKey = classConfig.GatherFindKeyConfig.MaxBy(x => x.SinceLastClickMs);
        if (!playerReader.IsCasting() &&
            oldestKey?.SinceLastClickMs > CYCLE_PROFESSION_PERIOD)
        {
            logger.LogInformation($"[{oldestKey.Key}] {oldestKey.Name} pressed for {InputDuration.DefaultPress}ms");
            input.PressRandom(oldestKey);
            oldestKey.SetClicked();
        }
    }

    private void MountIfPossible()
    {
        float totalDistance = VectorExt.TotalDistance<Vector3>(navigation.TotalRoute, VectorExt.WorldDistanceXY);

        // Optimize travel speed: mount if possible, otherwise unstealth for speed
        mountHandler.OptimizeTravelSpeed(totalDistance);

        if (mountHandler.IsMounted())
        {
            navigation.ResetStuckParameters();
        }
    }

    #region Refill rules

    private void Navigation_OnPathCalculated()
    {
        MountIfPossible();
    }

    private void Navigation_OnDestinationReached()
    {
        if (debug)
            LogDebug("Navigation_OnDestinationReached");

        RefillWaypoints(false);
        MountIfPossible();
    }

    private void Navigation_OnWayPointReached()
    {
        MountIfPossible();
    }

    public void RefillWaypoints(bool onlyClosest)
    {
        Log($"{nameof(RefillWaypoints)} - findClosest:{onlyClosest} - ThereAndBack:{pathSettings.PathThereAndBack}");

        Vector3 playerMap = playerReader.MapPos;

        Span<Vector3> pathMap = stackalloc Vector3[mapRoute.Length];
        mapRoute.CopyTo(pathMap);

        float mapDistanceToFirst = playerMap.MapDistanceXYTo(pathMap[0]);
        float mapDistanceToLast = playerMap.MapDistanceXYTo(pathMap[^1]);

        if (mapDistanceToLast < mapDistanceToFirst)
        {
            pathMap.Reverse();
        }

        int closestSegmentStartIndex = 0;
        Vector3 mapClosestPoint = pathMap[0];
        float distance = float.MaxValue;

        if (pathMap.Length == 1)
        {
            distance = playerMap.MapDistanceXYTo(pathMap[0]);
        }
        else
        {
            Vector2 playerXY = playerMap.AsVector2();
            for (int i = 0; i < pathMap.Length - 1; i++)
            {
                Vector3 a = pathMap[i];
                Vector3 b = pathMap[i + 1];
                Vector2 closestOnSegment = VectorExt.GetClosestPointOnLineSegment(a.AsVector2(), b.AsVector2(), playerXY);
                float d = Vector2.Distance(playerXY, closestOnSegment);
                if (d < distance)
                {
                    distance = d;
                    closestSegmentStartIndex = i;
                    mapClosestPoint = new Vector3(closestOnSegment.X, closestOnSegment.Y, 0);
                }
            }
        }

        if (onlyClosest)
        {
            if (debug)
                LogDebug($"{nameof(RefillWaypoints)}: Closest wayPoint: {mapClosestPoint}");

            navigation.SetWayPoints(stackalloc Vector3[1] { mapClosestPoint });

            return;
        }

        int remainingCount = pathMap.Length - (closestSegmentStartIndex + 1);
        if (remainingCount <= 0)
        {
            navigation.SetWayPoints(stackalloc Vector3[1] { mapClosestPoint });
            return;
        }

        Vector3[] points = new Vector3[remainingCount + 1];
        points[0] = mapClosestPoint;
        for (int i = 0; i < remainingCount; i++)
        {
            points[i + 1] = pathMap[closestSegmentStartIndex + 1 + i];
        }

        Log($"{nameof(RefillWaypoints)} - Set destination from closest segment - with {points.Length} waypoints");
        navigation.SetWayPoints(points);
    }

    #endregion

    /// <summary>
    /// Updates the current route when the path is modified.
    /// Only updates if the current route matches the expected old map to avoid
    /// race conditions during concurrent path modifications.
    /// </summary>
    public void ReceivePath(Vector3[] oldMap, Vector3[] newMap)
    {
        // Validate that current route matches expected state before updating
        // This prevents overwriting routes that changed since the update began
        if (mapRoute.SequenceEqual(oldMap))
        {
            this.mapRoute = newMap;
        }
    }

    private void RandomJump()
    {
        if (bits.Grounded() &&
            (DateTime.UtcNow - onEnterTime).TotalSeconds > 5 &&
            classConfig.Jump.SinceLastClickMs > Random.Shared.Next(10_000, 25_000))
        {
            Log("Random jump");
            input.PressJump();
        }
    }

    private void LogDebug(string text)
    {
        logger.LogDebug(text);
    }

    private void LogWarning(string text)
    {
        logger.LogWarning(text);
    }

    private void Log(string text)
    {
        logger.LogInformation(text);
    }
}
