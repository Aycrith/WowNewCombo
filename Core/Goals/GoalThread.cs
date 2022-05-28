using Core.GOAP;
using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System.Linq;

namespace Core.Goals
{
    public partial class GoalThread
    {
        private readonly ILogger logger;
        private readonly GoapAgent goapAgent;
        private readonly AddonReader addonReader;
        private readonly RouteInfo routeInfo;
        private readonly ConfigurableInput input;

        private readonly StopMoving stopMoving;

        private GoapGoal? currentGoal;
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
                    foreach (GoapGoal goal in goapAgent.AvailableGoals)
                    {
                        goal.OnActionEvent(this, new ActionEventArgs(GoapKey.abort, true));
                    }

                    addonReader.SoftReset();
                    input.Reset();

                    stopMoving.Stop();
                }
                goapAgent.Active = active;
            }
        }

        public GoalThread(ILogger logger, GoapAgent goapAgent, AddonReader addonReader, ConfigurableInput input, RouteInfo routeInfo)
        {
            this.logger = logger;
            this.goapAgent = goapAgent;
            this.addonReader = addonReader;
            this.input = input;
            this.routeInfo = routeInfo;

            stopMoving = new(input, addonReader.PlayerReader);
        }

        public void OnActionEvent(object sender, ActionEventArgs e)
        {
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

        public void Update()
        {
            GoapGoal? newGoal = goapAgent.GetAction();
            if (newGoal != null)
            {
                if (newGoal != currentGoal)
                {
                    wasEmpty = false;
                    currentGoal?.OnExit();
                    currentGoal = newGoal;

                    LogNewGoal(logger, newGoal.Name);
                    currentGoal.OnEnter();
                }

                newGoal.PerformAction();
            }
            else
            {
                if (!wasEmpty)
                {
                    LogNewEmptyGoal(logger);
                    wasEmpty = true;
                }
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