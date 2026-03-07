using System.Threading.Tasks;
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
    public override bool CanRun() => pathSettings.CanRun() && !buffs.Food() && !buffs.Drink();

    private const bool debug = false;
    private const float RefillOrientationFlipPenalty = 1f;
    private const float RefillBackwardSegmentPenalty = 2f;
    private const int RefillBackwardSegmentGrace = 1;
    private const float RefillAnchorTeleportResetDistance = 40f;
    private const int RefillSameSegmentLoopLimit = 3;
    private const float RefillTurnaroundDistanceTolerance = 0.75f;
    private const float RefillSameSegmentProgressTolerance = 10f;
    private static readonly TimeSpan RefillSameSegmentLoopWindow = TimeSpan.FromSeconds(5);

    private readonly ILogger<FollowRouteGoal> logger;
    private readonly ConfigurableInput input;
    private readonly Wait wait;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly BuffStatus<IPlayer> buffs;
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

    private readonly AsyncManualResetEvent sideActivityManualReset;
    private Task? sideActivityTask;
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
    private bool hasRefillProgressAnchor;
    private bool refillRouteReversed;
    private int refillSegmentAnchorIndex;
    private Vector3 refillAnchorMapPoint;
    private bool hasRecentRefillApply;
    private bool lastRefillAppliedReversed;
    private int lastRefillAppliedSegmentIndex;
    private DateTime lastRefillAppliedUtc;
    private Vector3 lastRefillAppliedAnchorPoint;
    private int repeatedSameRefillCount;
    private int refillForcedMinSegmentForward = -1;
    private int refillForcedMinSegmentReversed = -1;
    private DateTime lastBrokenGearTargetScanWarning = DateTime.MinValue;
    private DateTime lastLowHealthTargetScanWarning = DateTime.MinValue;

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
        BuffStatus<IPlayer> buffs,
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
        this.buffs = buffs;
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
                sideActivityTask = Task.Run(Thread_AttendedGatherAsync);
            }
        }
        else
        {
            sideActivityTask = Task.Run(Thread_LookingForTargetAsync);
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
        ResetRefillProgressAnchor();
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

    private async Task Thread_LookingForTargetAsync()
    {
        await sideActivityManualReset.WaitAsync();

        while (!sideActivityCts.IsCancellationRequested)
        {
            if (IsHealthTooLowToSearchForTargets())
            {
                wait.Update();
                await sideActivityManualReset.WaitAsync();
                continue;
            }

            if (IsGearTooBrokenToSearchForTargets())
            {
                wait.Update();
                await sideActivityManualReset.WaitAsync();
                continue;
            }

            if (pathSettings.CanRunSideActivity() &&
                targetFinder.Search(NpcNameToFind, bits.Target_NotDead, sideActivityCts.Token))
            {
                if (bits.Target() && targetBlacklist.Is())
                {
                    Log("Blacklisted target found, clearing target");
                    input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
                    targetFinder.NotifyBlacklistedResult();
                    try { await Task.Delay(TargetFinder.BlacklistBackoffMs, sideActivityCts.Token).ConfigureAwait(false); } catch (OperationCanceledException) { }
                    continue; // Don't fall through - loop again to find a valid target
                }

                if (bits.Target())
                {
                    Log("Found target!");
                    sideActivityCts.Cancel();
                    sideActivityManualReset.Reset();
                }
            }

            try { await Task.Delay(10, sideActivityCts.Token).ConfigureAwait(false); } catch (OperationCanceledException) { }
            await sideActivityManualReset.WaitAsync();
        }

        if (logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
            logger.LogDebug("LookingForTarget Task stopped!");
    }

    private bool IsGearTooBrokenToSearchForTargets()
    {
        if (!bits.Items_Broken() && playerReader.AvgEquipDurability() > 5)
        {
            return false;
        }

        if ((DateTime.UtcNow - lastBrokenGearTargetScanWarning).TotalSeconds >= 5)
        {
            Log("Gear durability critically low/broken; suspending target search while following route.");
            lastBrokenGearTargetScanWarning = DateTime.UtcNow;
        }

        return true;
    }

    private bool IsHealthTooLowToSearchForTargets()
    {
        if (!classConfig.IntVariables.TryGetValue("FOOD_HP%", out int foodHpThreshold) ||
            foodHpThreshold <= 0)
        {
            return false;
        }

        if (bits.Combat() || playerReader.HealthPercent() >= foodHpThreshold)
        {
            return false;
        }

        if ((DateTime.UtcNow - lastLowHealthTargetScanWarning).TotalSeconds >= 5)
        {
            Log($"Health below FOOD_HP% ({playerReader.HealthPercent()}% < {foodHpThreshold}%); suspending target search to finish resting.");
            lastLowHealthTargetScanWarning = DateTime.UtcNow;
        }

        return true;
    }

    private async Task Thread_AttendedGatherAsync()
    {
        await sideActivityManualReset.WaitAsync();

        while (!sideActivityCts.IsCancellationRequested)
        {
            if ((DateTime.UtcNow - onEnterTime).TotalMilliseconds > MIN_TIME_TO_START_CYCLE_PROFESSION)
            {
                AlternateGatherTypes();
            }

            try { await Task.Delay(CYCLE_PROFESSION_PERIOD, sideActivityCts.Token).ConfigureAwait(false); } catch (OperationCanceledException) { }
            await sideActivityManualReset.WaitAsync();
        }

        if (logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
            logger.LogDebug("AttendedGather Task stopped!");
    }



    private void AlternateGatherTypes()
    {
        // Don't swap tracking if we are actively gathering (AutoGathering has a path)
        bool isGathering = playerReader.IsCasting() && (Array.BinarySearch(GatherSpells.Herbalism, playerReader.CastSpellId.Value) >= 0 || Array.BinarySearch(GatherSpells.Mining, playerReader.CastSpellId.Value) >= 0);
        if (targetBlacklist.Is() || bits.Combat() || (navigation.HasWaypoint() && isGathering))
        {
            return;
        }

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

        if (mapRoute.Length == 0)
        {
            return;
        }

        Vector3 playerMap = playerReader.MapPos;

        if (hasRefillProgressAnchor &&
            playerMap.MapDistanceXYTo(refillAnchorMapPoint) > RefillAnchorTeleportResetDistance)
        {
            ResetRefillProgressAnchor();
        }

        Vector3[] normalPath = mapRoute;
        int minSegmentIndex = hasRefillProgressAnchor
            ? refillSegmentAnchorIndex
            : 0;
        if (refillForcedMinSegmentForward >= 0)
        {
            minSegmentIndex = Math.Max(minSegmentIndex, refillForcedMinSegmentForward);
        }
        RefillCandidate normalCandidate = FindClosestRefillCandidate(normalPath, playerMap, minSegmentIndex);
        float normalScore = ScoreRefillCandidate(normalCandidate, reversed: false);
        Vector3[]? reversedPath = null;
        RefillCandidate reversedCandidate = default;
        float reversedScore = float.MaxValue;

        Vector3[] chosenPath = normalPath;
        RefillCandidate chosenCandidate = normalCandidate;
        bool chosenReversed = false;

        if (pathSettings.PathThereAndBack && mapRoute.Length > 1)
        {
            reversedPath = (Vector3[])mapRoute.Clone();
            Array.Reverse(reversedPath);

            int reversedMinSegment = hasRefillProgressAnchor
                ? Math.Max(0, (mapRoute.Length - 1 - refillSegmentAnchorIndex) - RefillBackwardSegmentGrace)
                : 0;
            if (refillForcedMinSegmentReversed >= 0)
            {
                reversedMinSegment = Math.Max(reversedMinSegment, refillForcedMinSegmentReversed);
            }
            reversedCandidate = FindClosestRefillCandidate(reversedPath, playerMap, reversedMinSegment);
            reversedScore = ScoreRefillCandidate(reversedCandidate, reversed: true);

            if (reversedScore < normalScore)
            {
                chosenPath = reversedPath;
                chosenCandidate = reversedCandidate;
                chosenReversed = true;
            }
        }

        if (reversedPath != null &&
            ShouldPreferThereAndBackTurnaround(normalCandidate, reversedCandidate))
        {
            chosenPath = refillRouteReversed ? normalPath : reversedPath;
            chosenCandidate = refillRouteReversed ? normalCandidate : reversedCandidate;
            chosenReversed = !refillRouteReversed;
        }

        int closestSegmentStartIndex = chosenCandidate.SegmentStartIndex;
        Vector3 mapClosestPoint = chosenCandidate.MapClosestPoint;
        ApplyRefillLoopBreaker(chosenPath, onlyClosest, chosenReversed, ref closestSegmentStartIndex, ref mapClosestPoint);

        if (!onlyClosest &&
            pathSettings.PathThereAndBack &&
            mapRoute.Length > 1 &&
            reversedPath != null)
        {
            int remainingAfterLoopBreaker = chosenPath.Length - (closestSegmentStartIndex + 1);
            if (remainingAfterLoopBreaker <= 0)
            {
                Vector3[] alternatePath = chosenReversed ? normalPath : reversedPath;
                bool alternateReversed = !chosenReversed;
                RefillCandidate alternateCandidate = FindClosestRefillCandidate(alternatePath, playerMap, 0);
                int alternateRemaining = alternatePath.Length - (alternateCandidate.SegmentStartIndex + 1);

                if (alternateRemaining > 0 ||
                    alternateCandidate.DistanceToRoute <= chosenCandidate.DistanceToRoute + RefillTurnaroundDistanceTolerance)
                {
                    LogWarning("Reached route boundary; switching ThereAndBack orientation to avoid endpoint refill loop");
                    chosenPath = alternatePath;
                    chosenCandidate = alternateCandidate;
                    chosenReversed = alternateReversed;
                    closestSegmentStartIndex = alternateCandidate.SegmentStartIndex;
                    mapClosestPoint = alternateCandidate.MapClosestPoint;
                    hasRecentRefillApply = false;
                    repeatedSameRefillCount = 0;
                }
            }
        }

        UpdateRefillProgressAnchor(new RefillCandidate(closestSegmentStartIndex, mapClosestPoint, chosenCandidate.DistanceToRoute), chosenReversed);

        if (onlyClosest)
        {
            if (debug)
                LogDebug($"{nameof(RefillWaypoints)}: Closest wayPoint: {mapClosestPoint}");

            navigation.SetWayPoints(stackalloc Vector3[1] { mapClosestPoint });

            return;
        }

        int remainingCount = chosenPath.Length - (closestSegmentStartIndex + 1);
        if (remainingCount <= 0)
        {
            if (!pathSettings.PathThereAndBack)
            {
                // Loop mode: reset anchor so next refill restarts from route beginning
                ResetRefillProgressAnchor();
            }
            navigation.SetWayPoints(stackalloc Vector3[1] { mapClosestPoint });
            return;
        }

        Vector3[] points = new Vector3[remainingCount + 1];
        points[0] = mapClosestPoint;
        for (int i = 0; i < remainingCount; i++)
        {
            points[i + 1] = chosenPath[closestSegmentStartIndex + 1 + i];
        }

        Log($"{nameof(RefillWaypoints)} - Set destination from closest segment - with {points.Length} waypoints");
        navigation.SetWayPoints(points);
    }

    private void ResetRefillProgressAnchor()
    {
        hasRefillProgressAnchor = false;
        refillRouteReversed = false;
        refillSegmentAnchorIndex = 0;
        refillAnchorMapPoint = default;
        hasRecentRefillApply = false;
        lastRefillAppliedReversed = false;
        lastRefillAppliedSegmentIndex = 0;
        lastRefillAppliedUtc = DateTime.MinValue;
        lastRefillAppliedAnchorPoint = default;
        repeatedSameRefillCount = 0;
        refillForcedMinSegmentForward = -1;
        refillForcedMinSegmentReversed = -1;
    }

    private void UpdateRefillProgressAnchor(RefillCandidate candidate, bool reversed)
    {
        hasRefillProgressAnchor = true;
        refillRouteReversed = reversed;
        refillSegmentAnchorIndex = candidate.SegmentStartIndex;
        refillAnchorMapPoint = candidate.MapClosestPoint;
    }

    private void ApplyRefillLoopBreaker(
        ReadOnlySpan<Vector3> chosenPath,
        bool onlyClosest,
        bool chosenReversed,
        ref int closestSegmentStartIndex,
        ref Vector3 mapClosestPoint)
    {
        if (onlyClosest)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        bool sameSegmentLoop =
            hasRecentRefillApply &&
            chosenReversed == lastRefillAppliedReversed &&
            closestSegmentStartIndex == lastRefillAppliedSegmentIndex &&
            mapClosestPoint.MapDistanceXYTo(lastRefillAppliedAnchorPoint) < RefillSameSegmentProgressTolerance &&
            (now - lastRefillAppliedUtc) <= RefillSameSegmentLoopWindow;

        if (sameSegmentLoop)
        {
            repeatedSameRefillCount++;
        }
        else
        {
            repeatedSameRefillCount = 1;
        }

        if (repeatedSameRefillCount >= RefillSameSegmentLoopLimit)
        {
            int advanceToPointIndex = closestSegmentStartIndex + 1;
            if (advanceToPointIndex < chosenPath.Length)
            {
                LogWarning($"Refill loop detected on segment {closestSegmentStartIndex} (x{repeatedSameRefillCount}) - advancing route anchor");
                closestSegmentStartIndex = advanceToPointIndex;
                mapClosestPoint = chosenPath[advanceToPointIndex];
                if (chosenReversed)
                {
                    refillForcedMinSegmentReversed = Math.Max(refillForcedMinSegmentReversed, advanceToPointIndex);
                }
                else
                {
                    refillForcedMinSegmentForward = Math.Max(refillForcedMinSegmentForward, advanceToPointIndex);
                }
            }
            repeatedSameRefillCount = 0;
        }

        hasRecentRefillApply = true;
        lastRefillAppliedReversed = chosenReversed;
        lastRefillAppliedSegmentIndex = closestSegmentStartIndex;
        lastRefillAppliedUtc = now;
        lastRefillAppliedAnchorPoint = mapClosestPoint;
    }

    private float ScoreRefillCandidate(RefillCandidate candidate, bool reversed)
    {
        float score = candidate.DistanceToRoute;

        if (!hasRefillProgressAnchor)
        {
            return score;
        }

        if (reversed != refillRouteReversed)
        {
            score += RefillOrientationFlipPenalty;
        }
        else
        {
            int backwardsSegments = refillSegmentAnchorIndex - candidate.SegmentStartIndex - RefillBackwardSegmentGrace;
            if (backwardsSegments > 0)
            {
                score += backwardsSegments * RefillBackwardSegmentPenalty;
            }
        }

        return score;
    }

    private bool ShouldPreferThereAndBackTurnaround(RefillCandidate normalCandidate, RefillCandidate reversedCandidate)
    {
        if (!pathSettings.PathThereAndBack || !hasRefillProgressAnchor || mapRoute.Length < 2)
        {
            return false;
        }

        int lastSegmentStart = Math.Max(0, mapRoute.Length - 2);
        if (!refillRouteReversed)
        {
            bool nearTail =
                refillSegmentAnchorIndex >= lastSegmentStart ||
                normalCandidate.SegmentStartIndex >= lastSegmentStart;

            return nearTail &&
                reversedCandidate.DistanceToRoute <= normalCandidate.DistanceToRoute + RefillTurnaroundDistanceTolerance;
        }

        bool nearHead =
            refillSegmentAnchorIndex <= RefillBackwardSegmentGrace ||
            normalCandidate.SegmentStartIndex <= RefillBackwardSegmentGrace;

        return nearHead &&
            normalCandidate.DistanceToRoute <= reversedCandidate.DistanceToRoute + RefillTurnaroundDistanceTolerance;
    }

    private static RefillCandidate FindClosestRefillCandidate(ReadOnlySpan<Vector3> pathMap, Vector3 playerMap, int minSegmentIndex = 0)
    {
        if (pathMap.Length == 1)
        {
            return new RefillCandidate(0, pathMap[0], playerMap.MapDistanceXYTo(pathMap[0]));
        }

        Vector2 playerXY = playerMap.AsVector2();
        int closestSegmentStartIndex = minSegmentIndex;
        Vector3 mapClosestPoint = pathMap[Math.Min(minSegmentIndex, pathMap.Length - 1)];
        float bestDistance = float.MaxValue;

        for (int i = minSegmentIndex; i < pathMap.Length - 1; i++)
        {
            Vector3 a = pathMap[i];
            Vector3 b = pathMap[i + 1];
            Vector2 closestOnSegment = VectorExt.GetClosestPointOnLineSegment(a.AsVector2(), b.AsVector2(), playerXY);
            float distance = Vector2.Distance(playerXY, closestOnSegment);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                closestSegmentStartIndex = i;
                mapClosestPoint = new Vector3(closestOnSegment.X, closestOnSegment.Y, 0);
            }
        }

        return new RefillCandidate(closestSegmentStartIndex, mapClosestPoint, bestDistance);
    }

    private readonly record struct RefillCandidate(int SegmentStartIndex, Vector3 MapClosestPoint, float DistanceToRoute);

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
            ResetRefillProgressAnchor();
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
