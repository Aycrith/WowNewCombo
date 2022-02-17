using Core.GOAP;
using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System;
using System.Linq;
using System.Threading;

namespace Core.Goals
{
    public partial class GoalThread
    {
        private readonly ILogger logger;
        private readonly GoapAgent goapAgent;
        private readonly AddonReader addonReader;
        private readonly RouteInfo? routeInfo;

        private GoapGoal? currentGoal;

        private bool active;
        public bool Active
        {
            get => active;
            set
            {
                active = value;
                if (!active)
                {
                    foreach (var goal in goapAgent.AvailableGoals)
                    {
                        goal.OnActionEvent(this, new ActionEventArgs(GoapKey.abort, true));
                    }
                }
                goapAgent.Active = active;
            }
        }

        public GoalThread(ILogger logger, GoapAgent goapAgent, AddonReader addonReader, RouteInfo? routeInfo)
        {
            this.logger = logger;
            this.goapAgent = goapAgent;
            this.addonReader = addonReader;
            this.routeInfo = routeInfo;
        }

        public void OnActionEvent(object sender, ActionEventArgs e)
        {
            if (e.Key == GoapKey.corpselocation && e.Value is CorpseLocation corpseLocation)
            {
                routeInfo?.PoiList.Add(new RouteInfoPoi(corpseLocation.WowPoint, "Corpse", "black", corpseLocation.Radius));
            }
            else if (e.Key == GoapKey.consumecorpse && (bool)e.Value == false)
            {
                if (routeInfo != null && routeInfo.PoiList.Count > 0)
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

        public void GoapPerformGoal()
        {
            var newGoal = goapAgent.GetAction();
            if (newGoal != null)
            {
                if (newGoal != currentGoal)
                {
                    if (currentGoal != null)
                    {
                        try
                        {
                            currentGoal.OnExit();
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, $"{nameof(currentGoal.OnExit)} on {currentGoal.Name}");
                        }
                    }

                    currentGoal = newGoal;

                    LogNewGoal(logger, newGoal.Name);

                    if (currentGoal != null)
                    {
                        try
                        {
                            currentGoal.OnEnter();
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, $"{nameof(newGoal.OnEnter)} on {currentGoal.Name}");
                        }
                    }
                }

                try
                {
                    newGoal.PerformAction();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"{nameof(newGoal.PerformAction)} on {newGoal.Name}");
                }
            }
            else
            {
                LogNewEmptyGoal(logger);
                Thread.Sleep(10);
            }
        }

        public void ResumeIfNeeded()
        {
            currentGoal?.OnActionEvent(this, new ActionEventArgs(GoapKey.resume, true));
        }


        #region Logging

        [LoggerMessage(
            EventId = 40,
            Level = LogLevel.Information,
            Message = "New Plan= {name}")]
        static partial void LogNewGoal(ILogger logger, string name);

        [LoggerMessage(
            EventId = 41,
            Level = LogLevel.Warning,
            Message = "New Plan= NO PLAN")]
        static partial void LogNewEmptyGoal(ILogger logger);

        #endregion
    }
}