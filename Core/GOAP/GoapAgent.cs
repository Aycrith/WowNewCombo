using Core.Goals;
using Game;
using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Core.Session;

namespace Core.GOAP
{
    public sealed partial class GoapAgent : IDisposable
    {
        private readonly ILogger logger;
        private readonly ClassConfiguration classConfig;
        private readonly IGrindSessionDAO sessionDAO;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly WowScreen wowScreen;
        private readonly RouteInfo routeInfo;
        private readonly ConfigurableInput input;

        private readonly IGrindSessionHandler sessionHandler;

        private readonly StopMoving stopMoving;

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

                    foreach (GoapGoal goal in AvailableGoals)
                    {
                        goal.OnActionEvent(this, new ActionEventArgs(GoapKey.abort, true));
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
                    CurrentGoal?.OnActionEvent(this, new ActionEventArgs(GoapKey.resume, true));
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

        private bool wasEmpty;

        private readonly Thread goapThread;
        private readonly CancellationTokenSource cts;
        private readonly ManualResetEvent manualReset;

        public GoapAgent(ILogger logger, ClassConfiguration classConfig, IGrindSessionDAO sessionDAO, WowScreen wowScreen, GoapAgentState goapAgentState, AddonReader addonReader, HashSet<GoapGoal> availableGoals, RouteInfo routeInfo, ConfigurableInput input)
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

            stopMoving = new StopMoving(input, playerReader);

            this.addonReader.CreatureHistory.KillCredit += OnKillCredit;

            this.AvailableGoals = availableGoals.OrderBy(a => a.CostOfPerformingAction);
            this.Plan = new();

            foreach (GoapGoal a in AvailableGoals)
            {
                a.ActionEvent += OnActionEvent;

                foreach (GoapGoal b in AvailableGoals)
                {
                    if (b != a)
                        a.ActionEvent += b.OnActionEvent;
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
                a.ActionEvent -= OnActionEvent;

                foreach (GoapGoal b in AvailableGoals)
                {
                    if (b != a)
                        a.ActionEvent -= b.OnActionEvent;
                }
            }

            stopMoving.Dispose();

            foreach (var goal in AvailableGoals.Where(x => x is IDisposable).OfType<IDisposable>())
            {
                goal.Dispose();
            }

            addonReader.CreatureHistory.KillCredit -= OnKillCredit;
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

                    newGoal.PerformAction();
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
                { GoapKey.hastarget, playerReader.HasTarget },
                { GoapKey.dangercombat, playerReader.Bits.PlayerInCombat && addonReader.CombatCreatureCount > 0 },
                { GoapKey.pethastarget, playerReader.PetHasTarget },
                { GoapKey.targetisalive, playerReader.HasTarget && !playerReader.Bits.TargetIsDead },
                { GoapKey.targettargetsus, (playerReader.HasTarget && playerReader.TargetHealthPercentage < 30) || playerReader.TargetTarget is // hacky way to keep attacking fleeing humanoids
                    TargetTargetEnum.Me or
                    TargetTargetEnum.Pet or
                    TargetTargetEnum.PartyOrPet },
                { GoapKey.incombat, playerReader.Bits.PlayerInCombat },
                { GoapKey.ismounted,
                    (playerReader.Class == PlayerClassEnum.Druid &&
                    playerReader.Form is Form.Druid_Travel or Form.Druid_Flight)
                    || playerReader.Bits.IsMounted },
                { GoapKey.withinpullrange, playerReader.WithInPullRange },
                { GoapKey.incombatrange, playerReader.WithInCombatRange },
                { GoapKey.pulled, false },
                { GoapKey.isdead, playerReader.Bits.DeadStatus },
                { GoapKey.isswimming, playerReader.Bits.IsSwimming },
                { GoapKey.itemsbroken, playerReader.Bits.ItemsAreBroken },
                { GoapKey.producedcorpse, State.LastCombatKillCount > 0 },

                // these hold their state
                { GoapKey.consumecorpse, State.ShouldConsumeCorpse },
                { GoapKey.shouldloot, State.NeedLoot },
                { GoapKey.shouldskin, State.NeedSkin },
                { GoapKey.gathering, State.Gathering }
            };
        }

        private void OnActionEvent(object sender, ActionEventArgs e)
        {
            switch (e.Key)
            {
                case GoapKey.consumecorpse:
                    State.ShouldConsumeCorpse = (bool)e.Value;
                    break;
                case GoapKey.shouldloot:
                    State.NeedLoot = (bool)e.Value;
                    break;
                case GoapKey.shouldskin:
                    State.NeedSkin = (bool)e.Value;
                    break;
                case GoapKey.gathering:
                    State.Gathering = (bool)e.Value;
                    break;
                case GoapKey.wowscreen:
                    wowScreen.Enabled = (bool)e.Value;
                    break;
            }

            if (e.Key == GoapKey.corpselocation && e.Value is CorpseLocation corpseLocation)
            {
                routeInfo.PoiList.Add(new RouteInfoPoi(corpseLocation.Location, "Corpse", "black", corpseLocation.Radius));
            }
            else if (e.Key == GoapKey.consumecorpse && (bool)e.Value == false)
            {
                if (routeInfo.PoiList.Count > 0)
                {
                    var closest = routeInfo.PoiList.Where(p => p.Name == "Corpse").
                        Select(i => new { i, d = addonReader.PlayerReader.PlayerLocation.DistanceXYTo(i.Location) }).
                        Aggregate((a, b) => a.d <= b.d ? a : b);

                    if (closest.i != null)
                    {
                        routeInfo.PoiList.Remove(closest.i);
                    }
                }
            }
        }

        private void OnKillCredit()
        {
            if (Active)
            {
                State.LastCombatKillCount++;
                Broadcast(GoapKey.producedcorpse, true);

                LogActiveKillDetected(logger, State.LastCombatKillCount, addonReader.CombatCreatureCount);
            }
            else
            {
                LogInactiveKillDetected(logger);
            }
        }

        private void Broadcast(GoapKey goapKey, bool value)
        {
            if (CurrentGoal != null)
            {
                CurrentGoal.OnActionEvent(this, new ActionEventArgs(goapKey, value));
            }
            else
            {
                foreach (GoapGoal goal in AvailableGoals)
                {
                    goal.OnActionEvent(this, new ActionEventArgs(goapKey, value));
                }
            }
        }

        public void NodeFound()
        {
            State.Gathering = true;
            Broadcast(GoapKey.gathering, true);
        }

        #region Logging

        [LoggerMessage(
            EventId = 50,
            Level = LogLevel.Information,
            Message = "Kill credit detected! Known kills: {count} | Remains: {remain}")]
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