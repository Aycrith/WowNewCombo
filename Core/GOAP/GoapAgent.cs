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

namespace Core.GOAP
{
    public sealed partial class GoapAgent : IDisposable
    {
        private readonly ILogger logger;
        private readonly ClassConfiguration classConfig;
        private readonly IGrindSessionDAO sessionDAO;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly IWowScreen wowScreen;
        private readonly RouteInfo routeInfo;
        private readonly ConfigurableInput input;

        private readonly IGrindSessionHandler sessionHandler;
        private readonly StopMoving stopMoving;

        private readonly Thread goapThread;
        private readonly CancellationTokenSource cts;
        private readonly ManualResetEvent manualReset;

        private bool wasEmpty;

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

                    input.Proc.Reset();
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

        public GoapAgentState State { get; }
        public IEnumerable<GoapGoal> AvailableGoals { get; }

        public Stack<GoapGoal> Plan { get; private set; }
        public GoapGoal? CurrentGoal { get; private set; }

        public GoapAgent(ILogger logger, ClassConfiguration classConfig, IGrindSessionDAO sessionDAO, IWowScreen wowScreen, GoapAgentState goapAgentState, AddonReader addonReader, IEnumerable<GoapGoal> availableGoals, RouteInfo routeInfo, ConfigurableInput input)
        {
            this.logger = logger;
            this.classConfig = classConfig;
            this.sessionDAO = sessionDAO;
            this.cts = new();
            this.wowScreen = wowScreen;
            this.State = goapAgentState;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.routeInfo = routeInfo;
            this.input = input;

            sessionHandler = new GrindSessionHandler(logger, addonReader, sessionDAO, cts);

            stopMoving = new StopMoving(input.Proc, playerReader, cts);

            this.addonReader.CombatLog.KillCredit += OnKillCredit;

            this.AvailableGoals = availableGoals.OrderBy(a => a.Cost);
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

            foreach (IDisposable goal in AvailableGoals.OfType<IDisposable>())
            {
                goal.Dispose();
            }

            addonReader.CombatLog.KillCredit -= OnKillCredit;
        }

        private void GoapThread()
        {
            while (!cts.IsCancellationRequested)
            {
                manualReset.WaitOne();

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
            if (Plan.Count == 0)
            {
                Plan = GoapPlanner.Plan(AvailableGoals, GetWorldState(), GoapPlanner.EmptyGoalState);
            }

            return Plan.Count > 0 ? Plan.Pop() : null;
        }

        private Dictionary<GoapKey, bool> GetWorldState()
        {
            return new()
            {
                { GoapKey.hastarget, playerReader.Bits.HasTarget() },
                { GoapKey.targethostile, playerReader.Bits.TargetCanBeHostile() },
                { GoapKey.dangercombat, playerReader.Bits.PlayerInCombat() && addonReader.DamageTakenCount() > 0 },
                { GoapKey.damagetaken, addonReader.DamageTakenCount() > 0 },
                { GoapKey.damagedone, addonReader.DamageDoneCount() > 0 },
                { GoapKey.damagetakenordone, addonReader.DamageTakenCount() > 0 || addonReader.DamageDoneCount() > 0 },
                { GoapKey.pethastarget, playerReader.PetHasTarget && !playerReader.Bits.PetTargetIsDead() },
                { GoapKey.targetisalive, playerReader.Bits.HasTarget() && !playerReader.Bits.TargetIsDead() },
                { GoapKey.targettargetsus, (playerReader.Bits.HasTarget() && playerReader.TargetHealthPercentage() < 30) || playerReader.TargetTarget is // hacky way to keep attacking fleeing humanoids
                    TargetTargetEnum.Me or
                    TargetTargetEnum.Pet or
                    TargetTargetEnum.PartyOrPet },
                { GoapKey.incombat, playerReader.Bits.PlayerInCombat() },
                { GoapKey.ismounted,
                    (playerReader.Class == UnitClass.Druid &&
                    playerReader.Form is Form.Druid_Travel or Form.Druid_Flight)
                    || playerReader.Bits.IsMounted() },
                { GoapKey.withinpullrange, playerReader.WithInPullRange() },
                { GoapKey.incombatrange, playerReader.WithInCombatRange() },
                { GoapKey.pulled, false },
                { GoapKey.isdead, playerReader.Bits.DeadStatus() },
                { GoapKey.isswimming, playerReader.Bits.IsSwimming() },
                { GoapKey.itemsbroken, playerReader.Bits.ItemsAreBroken() },
                { GoapKey.producedcorpse, State.LastCombatKillCount > 0 },

                { GoapKey.hasfocus, playerReader.Bits.HasFocus() },
                { GoapKey.focushastarget, playerReader.Bits.FocusHasTarget() },

                { GoapKey.consumablecorpsenearby, State.ConsumableCorpseCount > 0 },
                // these hold their state
                { GoapKey.consumecorpse, State.ShouldConsumeCorpse },
                { GoapKey.shouldloot, State.NeedLoot },
                { GoapKey.shouldgather, State.NeedGather },
                { GoapKey.gathering, State.Gathering }
            };
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
                    case GoapKey.shouldloot:
                        State.NeedLoot = g.Value;
                        if (!g.Value)
                            RemoveClosestPoiByType(CorpseEvent.NAME);
                        break;
                    case GoapKey.shouldgather:
                        State.NeedGather = g.Value;
                        if (!g.Value)
                            RemoveClosestPoiByType(SkinCorpseEvent.NAME);
                        break;
                    case GoapKey.gathering:
                        State.Gathering = g.Value;
                        break;
                }
            }
            else if (e is CorpseEvent c)
            {
                routeInfo.PoiList.Add(new RouteInfoPoi(c.Location, CorpseEvent.NAME, CorpseEvent.COLOR, c.Radius));
            }
            else if (e is SkinCorpseEvent s)
            {
                routeInfo.PoiList.Add(new RouteInfoPoi(s.Location, SkinCorpseEvent.NAME, SkinCorpseEvent.COLOR, s.Radius));
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
            Vector3 playerLocation = addonReader.PlayerReader.PlayerLocation;
            for (int i = 0; i < routeInfo.PoiList.Count; i++)
            {
                RouteInfoPoi? poi = routeInfo.PoiList[i];
                if (poi?.Name != type)
                    continue;

                float min = playerLocation.DistanceXYTo(poi.Location);
                if (min < minDistance)
                {
                    minDistance = min;
                    index = i;
                }
            }

            if (index > -1)
            {
                routeInfo.PoiList.RemoveAt(index);
            }
        }

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
}