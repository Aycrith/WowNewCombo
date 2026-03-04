using Core.Diagnostics;
using Core.Goals;
using Core.Session;
using Core.Hazard;

using Game;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Extensions;
using SharedLib.Humanization;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace Core.GOAP;

public sealed partial class GoapAgent : IDisposable
{
    private readonly ILogger logger;
    private readonly ILogger globalLogger;

    private readonly ClassConfiguration classConfig;
    private readonly AddonReader addonReader;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly IWowScreen screen;
    private readonly RouteInfo routeInfo;
    private readonly ConfigurableInput input;
    private readonly IMountHandler mountHandler;
    private readonly CombatLog combatLog;

    private readonly IGrindSessionHandler sessionHandler;
    private readonly StopMoving stopMoving;

    private readonly Thread goapThread;
    private readonly CancellationTokenSource<GoapAgent> cts;
    private readonly ManualResetEventSlim sessionPauseEvent;

    private readonly IScreenCapture screenCapture;
    private readonly IBagChangeTracker bagChangeTracker;
    private readonly HazardEventCollector hazardEventCollector;
    private readonly IHumanizationProvider? humanizationProvider;
    private readonly IGoapEventHistory? goapEventHistory;
    private readonly GoapCurrentGoalState currentGoalState;

    private readonly IEnumerable<IGoapEventListener> extraListeners;

    /// <summary>
    /// Goal-switch hysteresis: a new goal must win for this many consecutive
    /// ticks before the agent actually transitions to it. Prevents single-frame
    /// plan oscillation (e.g. FollowRoute → Adhoc → FollowRoute churn).
    /// </summary>
    private const int GoalSwitchHysteresisThreshold = 3;
    private GoapGoal? pendingGoal;
    private int pendingGoalTicks;

    /// <summary>
    /// Hysteresis for withinpullrange: once in pull range, hold true for 500 ms
    /// to prevent single-frame oscillation at the range boundary that causes
    /// PullTargetGoal ↔ ApproachTargetGoal flip-flopping.
    /// </summary>
    private DateTime _pullRangeHysteresisUntilUtc = DateTime.MinValue;

    private bool active;
    public bool Active
    {
        get => active;
        set
        {
            active = value;
            if (!active)
            {
                sessionPauseEvent.Reset();

                foreach (IGoapEventListener goal in AvailableGoals.OfType<IGoapEventListener>())
                {
                    goal.OnGoapEvent(new AbortEvent());
                }

                input.Reset();
                stopMoving.Stop();

                if (classConfig.Mode is Mode.AttendedGrind or Mode.Grind)
                {
                    sessionHandler.Stop("Stopped", false);
                }

                screen.Enabled = false;
            }
            else
            {
                addonReader.SessionReset();
                SessionStat.Reset();

                if (CurrentGoal is IGoapEventListener listener)
                {
                    listener.OnGoapEvent(new ResumeEvent());
                }

                sessionPauseEvent.Set();

                if (classConfig.Mode is Mode.AttendedGrind or Mode.Grind)
                {
                    SessionStat.Start();
                    sessionHandler.Start(classConfig.OverridePathFilename ?? classConfig.PathFilename);
                }
            }
        }
    }

    public BitVector32 WorldState { get; private set; }

    public SessionStat SessionStat { get; }

    public GoapAgentState State { get; }
    public GoapGoal[] AvailableGoals { get; }

    public Stack<GoapGoal> Plan { get; private set; }
    public GoapGoal? CurrentGoal { get; private set; }

    public GoapAgent(
        ILogger<GoapAgent> logger,
        ILogger globalLogger,
        CancellationTokenSource<GoapAgent> cts,
        RouteInfo routeInfo,
        IScreenCapture screenCapture,
        ClassConfiguration classConfiguration,
        IWowScreen screen,
        GoapAgentState state,
        AddonReader addonReader,
        PlayerReader playerReader,
        AddonBits bits,
        ConfigurableInput input,
        IMountHandler mountHandler,
        CombatLog combatLog,
        IBagChangeTracker bagChangeTracker,
        SessionStat sessionStat,
        StopMoving stopMoving,
        IGrindSessionHandler sessionHandler,
        IEnumerable<GoapGoal> availableGoals,
        HazardEventCollector hazardEventCollector,
        IHumanizationProvider? humanizationProvider = null,
        IGoapEventHistory? goapEventHistory = null,
        IEnumerable<IGoapEventListener>? extraListeners = null,
        GoapCurrentGoalState? goapCurrentGoalState = null
    )
    {
        this.routeInfo = routeInfo;

        this.cts = cts;

        this.logger = logger;
        this.globalLogger = globalLogger;

        this.screenCapture = screenCapture;
        this.classConfig = classConfiguration;

        this.screen = screen;
        this.State = state;
        currentGoalState = goapCurrentGoalState ?? new GoapCurrentGoalState();
        this.addonReader = addonReader;
        this.playerReader = playerReader;
        this.bits = bits;

        this.input = input;
        this.mountHandler = mountHandler;

        this.combatLog = combatLog;
        this.bagChangeTracker = bagChangeTracker;
        this.hazardEventCollector = hazardEventCollector;
        this.humanizationProvider = humanizationProvider;
        this.goapEventHistory = goapEventHistory;

        this.extraListeners = extraListeners ?? Array.Empty<IGoapEventListener>();

        SessionStat = sessionStat;

        this.stopMoving = stopMoving;

        this.sessionHandler = sessionHandler;

        this.AvailableGoals = availableGoals.OrderBy(a => a.Cost).ToArray();

        combatLog.KillCredit += OnKillCredit;
        combatLog.PlayerDeath += PlayerDied;

        addonReader.SessionReset();
        sessionStat.Reset();

        this.Plan = new();

        foreach (GoapGoal a in AvailableGoals)
        {
            a.GoapEvent += HandleGoapEvent;

            foreach (IGoapEventListener b in AvailableGoals.OfType<IGoapEventListener>())
            {
                if (b != a)
                    a.GoapEvent += b.OnGoapEvent;
            }

            // Wire extra listeners (DI-registered services like FailureAnalyticsEventListener, HybridLlmEventListener)
            foreach (IGoapEventListener extraListener in this.extraListeners)
            {
                a.GoapEvent += extraListener.OnGoapEvent;
            }
        }

        sessionPauseEvent = new(false);
        goapThread = new(GoapThread);
        goapThread.Start();
    }

    public void Dispose()
    {
        currentGoalState.Clear();
        cts.Cancel();
        sessionPauseEvent.Set();

        goapThread.Join(TimeSpan.FromSeconds(5));

        foreach (GoapGoal a in AvailableGoals)
        {
            a.GoapEvent -= HandleGoapEvent;

            foreach (IGoapEventListener b in AvailableGoals.OfType<IGoapEventListener>())
            {
                if (b != a)
                    a.GoapEvent -= b.OnGoapEvent;
            }

            // Unwire extra listeners
            foreach (IGoapEventListener extraListener in this.extraListeners)
            {
                a.GoapEvent -= extraListener.OnGoapEvent;
            }
        }

        combatLog.KillCredit -= OnKillCredit;
        combatLog.PlayerDeath -= PlayerDied;

        sessionPauseEvent.Dispose();
        cts.Dispose();
    }

    /// <summary>
    /// Advances the goal-switch hysteresis state machine. When a new goal differs from the
    /// current goal, this method tracks how many consecutive ticks the new goal has won
    /// planning. When the threshold is reached, returns true to signal that the transition
    /// should be committed.
    ///
    /// Prevents single-frame plan oscillation (e.g., FollowRoute → Adhoc → FollowRoute churn)
    /// caused by transient world-state bits like DamageTaken.
    /// </summary>
    /// <param name="newGoal">The goal returned by this tick's GOAP planning pass.</param>
    /// <returns>
    /// True if the hysteresis threshold has been satisfied (newGoal has won for
    /// GoalSwitchHysteresisThreshold consecutive ticks). False if the goal is still
    /// accumulating ticks or has just switched.
    /// </returns>
    internal bool TryAdvanceHysteresis(GoapGoal? newGoal)
    {
        // If newGoal == CurrentGoal, clear any pending alternative and return true
        // (no state change needed — execute the goal normally).
        if (newGoal == CurrentGoal)
        {
            pendingGoal = null;
            pendingGoalTicks = 0;
            return true;
        }

        // Track consecutive wins for the new goal
        if (newGoal == pendingGoal)
        {
            pendingGoalTicks++;
        }
        else
        {
            pendingGoal = newGoal;
            pendingGoalTicks = 1;
        }

        // Return true when threshold is satisfied
        return pendingGoalTicks >= GoalSwitchHysteresisThreshold;
    }

    private void GoapThread()
    {
        bool wasEmpty = false;

        sessionPauseEvent.Wait();

        WaitHandle[] waitHandles = [
            addonReader.DataReady.WaitHandle,
            cts.Token.WaitHandle
        ];

        while (!cts.IsCancellationRequested)
        {
            if (humanizationProvider?.IsOnBreak == true)
            {
                stopMoving.Stop();
                input.Reset();

                TimeSpan remaining = humanizationProvider.RemainingBreakTime;
                int waitMs = remaining <= TimeSpan.Zero
                    ? 50
                    : (int)Math.Clamp(remaining.TotalMilliseconds, 50, 250);

                cts.Token.WaitHandle.WaitOne(waitMs);
                continue;
            }

            GoapGoal? newGoal = NextGoal();
            if (newGoal != null)
            {
                if (newGoal != CurrentGoal)
                {
                    if (!TryAdvanceHysteresis(newGoal))
                    {
                        // Not enough consecutive wins yet — keep executing current goal
                        CurrentGoal?.Update();
                        Thread.Sleep(2);

                        try
                        {
                            WaitHandle.WaitAny(waitHandles);
                            sessionPauseEvent.Wait(cts.Token);
                        }
                        catch (OperationCanceledException) { break; }
                        catch (ObjectDisposedException) { break; }
                        continue;
                    }

                    // Hysteresis satisfied — commit the transition
                    wasEmpty = false;
                    string? fromGoalName = CurrentGoal?.Name;
                    CurrentGoal?.OnExit();
                    CurrentGoal = newGoal;
                    currentGoalState.SetCurrentGoal(newGoal);

                    LogNewGoal(logger, newGoal.Name);
                    CurrentGoal.OnEnter();

                    // Record goal transition for diagnostics
                    goapEventHistory?.RecordTransition(
                        fromGoalName,
                        newGoal.Name,
                        "Plan execution",
                        Plan.Count);

                    // Phase 3: Humanization - Add reaction delay after goal transition
                    ApplyReactionDelay(newGoal);
                }

                newGoal.Update();

                // If the selected goal can no longer run after its Update(),
                // clear the plan so the planner re-evaluates on the next tick.
                // This handles cases where a goal's CanRun() flips mid-execution
                // (e.g., Stealth entered → AdhocGoal can no longer cast).
                if (!newGoal.CanRun())
                {
                    Plan.Clear();
                    GoapPlanner.InvalidateCache();
                }
            }
            else if (!wasEmpty)
            {
                currentGoalState.Clear();
                LogNewEmptyGoal(logger);
                wasEmpty = true;

                // Record NO PLAN event for diagnostics
                goapEventHistory?.RecordNoPlanEvent(
                    WorldState.ToString(),
                    AvailableGoals.Select(g => g.Name).ToList(),
                    $"WorldState: {WorldState}");
            }

            Thread.Sleep(2);

            try
            {
                WaitHandle.WaitAny(waitHandles);
                sessionPauseEvent.Wait(cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Thread stopped!");
    }

    private GoapGoal? NextGoal()
    {
        UpdateWorldState();

        if (Plan.Count == 0)
        {
            Plan = GoapPlanner.Plan(AvailableGoals, WorldState, GoapPlanner.EmptyGoalState);
        }

        return Plan.Count > 0 ? Plan.Pop() : null;
    }

    private void UpdateWorldState()
    {
        AddonBits b = bits;

        bool dmgTaken = combatLog.DamageTakenCount() > 0;
        bool dmgDone = combatLog.DamageDoneCount() > 0;
        bool hasTarget = b.Target();
        bool playerCombat = b.Combat();

        int data =
            (B(hasTarget) << (int)GoapKey.hastarget) |
            (B(playerCombat && dmgTaken) << (int)GoapKey.dangercombat) |
            (B(dmgTaken) << (int)GoapKey.damagetaken) |
            (B(dmgDone) << (int)GoapKey.damagedone) |
            (B(dmgTaken || dmgDone) << (int)GoapKey.damagetakenordone) |
            (B(hasTarget && !b.Target_Dead()) << (int)GoapKey.targetisalive) |

            // A target that is targeting us is only "suspicious" (may be a tagged/linked
            // mob) when we did NOT initiate the pull ourselves. Once a mob is in ToPull
            // (we cast the first pull spell at it), its TargetTarget flipping to Me is
            // expected — excluding it prevents PullTargetGoal from being ejected mid-
            // sequence after the first DoT aggros but before all pull casts complete.
            (B((hasTarget &&
            playerReader.TargetHealthPercent() < 30) ||
            ((playerReader.TargetTarget is UnitsTarget.Me or
                UnitsTarget.Pet or UnitsTarget.PartyOrPet) &&
            !combatLog.ToPull.Contains(playerReader.TargetGuid))) << (int)GoapKey.targettargetsus) |

            (B(playerCombat) << (int)GoapKey.incombat) |
            (B(playerReader.PetTarget() && !b.PetTarget_Dead()) << (int)GoapKey.pethastarget) |
            (B(mountHandler.IsMounted()) << (int)GoapKey.ismounted) |
            (B(WithInPullRangeHysteresis()) << (int)GoapKey.withinpullrange) |
            (B(playerReader.WithInCombatRange()) << (int)GoapKey.incombatrange) |
            (B(bits.Combat() && bits.Target_Combat() && combatLog.ToPullCount() > 0) << (int)GoapKey.pulled) |
            (B(b.Dead()) << (int)GoapKey.isdead) |
            (B(State.LootableCorpseCount > 0) << (int)GoapKey.shouldloot) |
            (B(State.GatherableCorpseCount > 0) << (int)GoapKey.shouldgather) |
            (B(State.LastCombatKillCount > 0) << (int)GoapKey.producedcorpse) |
            (B(State.ShouldConsumeCorpse) << (int)GoapKey.consumecorpse) |
            (B(b.Swimming()) << (int)GoapKey.isswimming) |
            (B(b.Items_Broken()) << (int)GoapKey.itemsbroken) |
            (B(State.Gathering) << (int)GoapKey.gathering) |
            (B(b.Target_Hostile() || (bits.Target() && combatLog.ToPull.Contains(playerReader.TargetGuid))) << (int)GoapKey.targethostile) |
            (B(b.Focus()) << (int)GoapKey.hasfocus) |
            (B(b.FocusTarget()) << (int)GoapKey.focushastarget) |
            (B(State.ConsumableCorpseCount > 0) << (int)GoapKey.consumablecorpsenearby) |

            // Encode player Form (stance) into upper bits for planner cache
            // invalidation. Without this, entering/exiting stealth or other
            // forms does not change worldStateBits, causing the GoapPlanner
            // to reuse a stale usable-goals cache and repeatedly select a
            // goal whose CanRun() has since become false.
            ((int)playerReader.Form << (int)GoapKey.LENGTH)
            ;

        WorldState = new(data);

        static int B(bool b) => b ? 1 : 0;
    }

    /// <summary>
    /// Returns true if the player is currently in pull range OR was within
    /// pull range within the last 500 ms. The hysteresis prevents
    /// PullTargetGoal ↔ ApproachTargetGoal oscillation when the target drifts
    /// just inside/outside the pull-range boundary on consecutive ticks.
    /// </summary>
    private bool WithInPullRangeHysteresis()
    {
        bool nowInRange = playerReader.WithInPullRange();
        if (nowInRange)
        {
            _pullRangeHysteresisUntilUtc = DateTime.UtcNow.AddMilliseconds(500);
            return true;
        }

        return DateTime.UtcNow < _pullRangeHysteresisUntilUtc;
    }

    private void HandleGoapEvent(GoapEventArgs e)
    {
        if (e is GoapStateEvent g)
        {
            switch (g.Key)
            {
                case GoapKey.consumecorpse:
                    State.ShouldConsumeCorpse = g.Value;
                    break;
                case GoapKey.gathering:
                    State.Gathering = g.Value;
                    break;
            }
        }
        else if (e is CorpseEvent c)
        {
            routeInfo.PoiList.Add(new RouteInfoPoi(c.MapLoc, CorpseEvent.NAME, CorpseEvent.COLOR, c.Radius));
        }
        else if (e is SkinCorpseEvent s)
        {
            routeInfo.PoiList.Add(new RouteInfoPoi(s.MapLoc, SkinCorpseEvent.NAME, SkinCorpseEvent.COLOR, s.Radius));
        }
        else if (e is RemoveClosestPoi r)
        {
            RemoveClosestPoiByType(r.Name);
        }
        else if (e is ScreenCaptureEvent)
        {
            screenCapture.Request();
        }
    }

    private void OnKillCredit()
    {
        if (!Active)
        {
            LogInactiveKillDetected(logger);
            return;
        }

        SessionStat.Kills++;

        State.LastCombatKillCount++;
        State.ConsumableCorpseCount++;

        BroadcastGoapEvent(GoapKey.producedcorpse, true);

        LogActiveKillDetected(logger, SessionStat.Kills, State.LastCombatKillCount, combatLog.DamageTakenCount());
    }

    public void PlayerDied()
    {
        SessionStat.Deaths++;
    }

    private void BroadcastGoapEvent(GoapKey goapKey, bool value)
    {
        foreach (IGoapEventListener goal in AvailableGoals.OfType<IGoapEventListener>())
        {
            goal.OnGoapEvent(new GoapStateEvent(goapKey, value));
        }
    }

    private void RemoveClosestPoiByType(string type)
    {
        if (routeInfo.PoiList.Count == 0)
            return;

        int index = -1;
        float minDistance = float.MaxValue;
        Vector3 playerMap = playerReader.MapPos;
        for (int i = 0; i < routeInfo.PoiList.Count; i++)
        {
            RouteInfoPoi poi = routeInfo.PoiList[i];
            if (poi.Name != type)
                continue;

            float mapMin = playerMap.MapDistanceXYTo(poi.MapLoc);
            if (mapMin < minDistance)
            {
                minDistance = mapMin;
                index = i;
            }
        }

        if (index > -1)
        {
            routeInfo.PoiList.RemoveAt(index);
        }
    }

    public bool HasState(GoapKey key) => WorldState[1 << (int)key];

    public void NodeFound()
    {
        State.Gathering = true;
        BroadcastGoapEvent(GoapKey.gathering, true);
    }

    #region Logging

    [LoggerMessage(
        EventId = 0050,
        Level = LogLevel.Information,
        Message = "Kill credit detected! Session Total: {sessionTotal} | Last Combat: {lastCombatCount} | Currently fighting: {currentCombatRemain}")]
    static partial void LogActiveKillDetected(ILogger logger, int sessionTotal, int lastCombatCount, int currentCombatRemain);

    [LoggerMessage(
        EventId = 0051,
        Level = LogLevel.Information,
        Message = "Inactive, kill credit detected!")]
    static partial void LogInactiveKillDetected(ILogger logger);

    [LoggerMessage(
        EventId = 0052,
        Level = LogLevel.Information,
        Message = "New Plan= {name}")]
    static partial void LogNewGoal(ILogger logger, string name);

    [LoggerMessage(
        EventId = 0053,
        Level = LogLevel.Warning,
        Message = "New Plan= NO PLAN")]
    static partial void LogNewEmptyGoal(ILogger logger);

    #endregion

    #region Humanization

    /// <summary>
    /// Applies a reaction delay after goal transitions to simulate human reaction time.
    /// Complexity and movement type affect the delay duration.
    /// </summary>
    private void ApplyReactionDelay(GoapGoal goal)
    {
        if (humanizationProvider?.Enabled != true)
            return;

        // Determine complexity based on goal name/type
        int complexity = GetGoalComplexity(goal);

        // Determine if this is a movement action
        bool isMovement = goal.Name.Contains("Move", StringComparison.OrdinalIgnoreCase) ||
                         goal.Name.Contains("Walk", StringComparison.OrdinalIgnoreCase) ||
                         goal.Name.Contains("Route", StringComparison.OrdinalIgnoreCase);

        // Get delay from humanization provider
        int delayMs = humanizationProvider.GetPreActionReactionDelayMs(complexity, isMovement);

        if (delayMs > 0)
        {
            LogReactionDelay(logger, goal.Name, delayMs);
            cts.Token.WaitHandle.WaitOne(delayMs);
        }
    }

    /// <summary>
    /// Assigns a complexity score to goals for reaction delay calculation.
    /// Higher complexity = longer reaction time.
    /// </summary>
    private static int GetGoalComplexity(GoapGoal goal)
    {
        string name = goal.Name;

        // Combat actions - high complexity (tactical decisions)
        if (name.Contains("Combat", StringComparison.OrdinalIgnoreCase))
            return 4;
        if (name.Contains("Pull", StringComparison.OrdinalIgnoreCase))
            return 3;

        // Movement actions - low complexity (instinctive)
        if (name.Contains("Move", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Walk", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Route", StringComparison.OrdinalIgnoreCase))
            return 1;

        // Reactive actions - medium complexity
        if (name.Contains("Adhoc", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Corpse", StringComparison.OrdinalIgnoreCase))
            return 2;

        // Default
        return 2;
    }

    [LoggerMessage(
        EventId = 0054,
        Level = LogLevel.Debug,
        Message = "[Humanization] Reaction delay before '{goalName}': {delayMs}ms")]
    static partial void LogReactionDelay(ILogger logger, string goalName, int delayMs);

    #endregion
}
