using Core.Goals;
using Game;
using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Core.Session;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;

namespace Core.GOAP;

public sealed partial class GoapAgent : IDisposable
{
    private readonly IServiceScope scope;

    private readonly ILogger logger;
    private readonly ClassConfiguration classConfig;
    private readonly AddonReader addonReader;
    private readonly PlayerReader playerReader;
    private readonly IWowScreen wowScreen;
    private readonly RouteInfo routeInfo;
    private readonly ConfigurableInput input;
    private readonly IMountHandler mountHandler;

    private readonly IGrindSessionHandler sessionHandler;
    private readonly StopMoving stopMoving;

    private readonly Thread goapThread;
    private readonly CancellationTokenSource cts;
    private readonly ManualResetEventSlim manualReset;

    private readonly IScreenCapture screenCapture;

    private bool active;
    public bool Active
    {
        get => active;
        set
        {
            active = value;
            if (!active)
            {
                manualReset.Reset();

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

                addonReader.SessionReset();

                wowScreen.Enabled = false;
            }
            else
            {
                if (CurrentGoal is IGoapEventListener listener)
                {
                    listener.OnGoapEvent(new ResumeEvent());
                }

                manualReset.Set();

                if (classConfig.Mode is Mode.AttendedGrind or Mode.Grind)
                {
                    sessionHandler.Start(classConfig.OverridePathFilename ?? classConfig.PathFilename);
                }
            }
        }
    }

    public BitVector32 WorldState { get; private set; }

    public GoapAgentState State { get; }
    public GoapGoal[] AvailableGoals { get; }

    public Stack<GoapGoal> Plan { get; private set; }
    public GoapGoal? CurrentGoal { get; private set; }

    public GoapAgent(IServiceScope scope, DataConfig dataConfig,
        IGrindSessionDAO sessionDAO, IWowScreen wowScreen, IScreenCapture screenCapture,
        RouteInfo routeInfo)
    {
        this.scope = scope;

        this.logger = scope.ServiceProvider.GetRequiredService<ILogger>();
        this.screenCapture = screenCapture;
        this.classConfig = scope.ServiceProvider.GetRequiredService<ClassConfiguration>();
        this.cts = new();
        this.wowScreen = wowScreen;
        this.State = scope.ServiceProvider.GetRequiredService<GoapAgentState>();
        this.addonReader = scope.ServiceProvider.GetRequiredService<AddonReader>();
        this.playerReader = addonReader.PlayerReader;
        this.routeInfo = routeInfo;
        this.input = scope.ServiceProvider.GetRequiredService<ConfigurableInput>();
        this.mountHandler = scope.ServiceProvider.GetRequiredService<IMountHandler>();

        this.addonReader.CombatLog.KillCredit += OnKillCredit;

        sessionHandler = new GrindSessionHandler(logger, dataConfig, addonReader, sessionDAO, cts);

        WowProcessInput baseInput = scope.ServiceProvider.GetRequiredService<WowProcessInput>();
        stopMoving = new StopMoving(baseInput, playerReader, cts);

        this.AvailableGoals = scope.ServiceProvider.GetServices<GoapGoal>().OrderBy(a => a.Cost).ToArray();
        this.Plan = new();

        foreach (GoapGoal a in AvailableGoals)
        {
            a.GoapEvent += HandleGoapEvent;

            foreach (IGoapEventListener b in AvailableGoals.OfType<IGoapEventListener>())
            {
                if (b != a)
                    a.GoapEvent += b.OnGoapEvent;
            }
        }

        manualReset = new(false);
        goapThread = new(GoapThread);
        goapThread.Start();
    }

    public void Dispose()
    {
        cts.Cancel();
        manualReset.Set();

        foreach (GoapGoal a in AvailableGoals)
        {
            a.GoapEvent -= HandleGoapEvent;

            foreach (IGoapEventListener b in AvailableGoals.OfType<IGoapEventListener>())
            {
                if (b != a)
                    a.GoapEvent -= b.OnGoapEvent;
            }
        }

        scope.Dispose();

        addonReader.CombatLog.KillCredit -= OnKillCredit;
    }

    private void GoapThread()
    {
        bool wasEmpty = false;

        while (!cts.IsCancellationRequested)
        {
            manualReset.Wait();

            GoapGoal? newGoal = NextGoal();
            if (!cts.IsCancellationRequested && newGoal != null)
            {
                if (newGoal != CurrentGoal)
                {
                    wasEmpty = false;
                    CurrentGoal?.OnExit();
                    CurrentGoal = newGoal;

                    LogNewGoal(logger, newGoal.Name);
                    CurrentGoal.OnEnter();
                }

                newGoal.Update();
            }
            else if (!cts.IsCancellationRequested && !wasEmpty)
            {
                LogNewEmptyGoal(logger);
                wasEmpty = true;
            }

            cts.Token.WaitHandle.WaitOne(1);
        }

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Goap thread stopped!");
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
        AddonBits bits = playerReader.Bits;

        bool dmgTaken = addonReader.DamageTakenCount() > 0;
        bool dmgDone = addonReader.DamageDoneCount() > 0;
        bool hasTarget = bits.HasTarget();
        bool playerCombat = bits.PlayerInCombat();

        int data =
            (B(hasTarget) << (int)GoapKey.hastarget) |
            (B(playerCombat && dmgTaken) << (int)GoapKey.dangercombat) |
            (B(dmgTaken) << (int)GoapKey.damagetaken) |
            (B(dmgDone) << (int)GoapKey.damagedone) |
            (B(dmgTaken || dmgDone) << (int)GoapKey.damagetakenordone) |
            (B(hasTarget && !bits.TargetIsDead()) << (int)GoapKey.targetisalive) |

            (B((hasTarget &&
            playerReader.TargetHealthPercentage() < 30) ||
            playerReader.TargetTarget is UnitsTarget.Me or
                UnitsTarget.Pet or UnitsTarget.PartyOrPet) << (int)GoapKey.targettargetsus) |

            (B(playerCombat) << (int)GoapKey.incombat) |
            (B(playerReader.PetHasTarget() && !bits.PetTargetIsDead()) << (int)GoapKey.pethastarget) |
            (B(mountHandler.IsMounted()) << (int)GoapKey.ismounted) |
            (B(playerReader.WithInPullRange()) << (int)GoapKey.withinpullrange) |
            (B(playerReader.WithInCombatRange()) << (int)GoapKey.incombatrange) |
            // pulled always false
            (B(bits.IsDead()) << (int)GoapKey.isdead) |
            (B(State.LootableCorpseCount > 0) << (int)GoapKey.shouldloot) |
            (B(State.GatherableCorpseCount > 0) << (int)GoapKey.shouldgather) |
            (B(State.LastCombatKillCount > 0) << (int)GoapKey.producedcorpse) |
            (B(State.ShouldConsumeCorpse) << (int)GoapKey.consumecorpse) |
            (B(bits.IsSwimming()) << (int)GoapKey.isswimming) |
            (B(bits.ItemsAreBroken()) << (int)GoapKey.itemsbroken) |
            (B(State.Gathering) << (int)GoapKey.gathering) |
            (B(bits.TargetCanBeHostile()) << (int)GoapKey.targethostile) |
            (B(bits.HasFocus()) << (int)GoapKey.hasfocus) |
            (B(bits.FocusHasTarget()) << (int)GoapKey.focushastarget) |
            (B(State.ConsumableCorpseCount > 0) << (int)GoapKey.consumablecorpsenearby)
            ;

        WorldState = new(data);

        static int B(bool b) => b ? 1 : 0;
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
        if (Active)
        {
            State.LastCombatKillCount++;
            State.ConsumableCorpseCount++;
            BroadcastGoapEvent(GoapKey.producedcorpse, true);

            LogActiveKillDetected(logger, State.LastCombatKillCount, addonReader.DamageTakenCount());
        }
        else
        {
            LogInactiveKillDetected(logger);
        }
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
        Vector3 playerMap = addonReader.PlayerReader.MapPos;
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
        EventId = 50,
        Level = LogLevel.Information,
        Message = "Kill credit detected! Known kills: {count} | Fighting with: {remain}")]
    static partial void LogActiveKillDetected(ILogger logger, int count, int remain);

    [LoggerMessage(
        EventId = 51,
        Level = LogLevel.Information,
        Message = "Inactive, kill credit detected!")]
    static partial void LogInactiveKillDetected(ILogger logger);

    [LoggerMessage(
        EventId = 52,
        Level = LogLevel.Information,
        Message = "New Plan= {name}")]
    static partial void LogNewGoal(ILogger logger, string name);

    [LoggerMessage(
        EventId = 53,
        Level = LogLevel.Warning,
        Message = "New Plan= NO PLAN")]
    static partial void LogNewEmptyGoal(ILogger logger);

    #endregion
}