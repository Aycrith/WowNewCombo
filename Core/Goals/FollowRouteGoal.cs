using Core.GOAP;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using SharedLib.Extensions;

#pragma warning disable 162

namespace Core.Goals
{
    public class FollowRouteGoal : GoapGoal, IRouteProvider, IDisposable
    {
        public override float CostOfPerformingAction => 20f;

        private const bool debug = false;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly NpcNameFinder npcNameFinder;
        private readonly ClassConfiguration classConfig;
        private readonly MountHandler mountHandler;
        private readonly Navigation navigation;
        private readonly List<Vector3> routePoints;

        private readonly TargetFinder targetFinder;
        private readonly int minMs = 500, maxMs = 1000;
        private const NpcNames NpcNameToFind = NpcNames.Enemy | NpcNames.Neutral;

        private const int MIN_TIME_TO_START_CYCLE_PROFESSION = 5000;
        private const int CYCLE_PROFESSION_PERIOD = 8000;

        private CancellationTokenSource sideActivityCts;
        private Thread sideActivityThread = null!;

        private bool shouldMount;

        private readonly Random random = new();

        private DateTime onEnterTime;

        #region IRouteProvider

        public DateTime LastActive => navigation.LastActive;

        public List<Vector3> PathingRoute()
        {
            return navigation.TotalRoute;
        }

        public bool HasNext()
        {
            return navigation.HasNext();
        }

        public Vector3 NextPoint()
        {
            return navigation.NextPoint();
        }

        #endregion


        public FollowRouteGoal(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, ClassConfiguration classConfig, List<Vector3> points, Navigation navigation, MountHandler mountHandler, NpcNameFinder npcNameFinder, TargetFinder targetFinder)
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.addonReader = addonReader;
            this.classConfig = classConfig;
            this.playerReader = addonReader.PlayerReader;
            this.routePoints = points;
            this.npcNameFinder = npcNameFinder;
            this.mountHandler = mountHandler;
            this.targetFinder = targetFinder;

            this.navigation = navigation;
            navigation.OnPathCalculated += Navigation_OnPathCalculated;
            navigation.OnDestinationReached += Navigation_OnDestinationReached;
            navigation.OnWayPointReached += Navigation_OnWayPointReached;

            AddPrecondition(GoapKey.dangercombat, false);

            if (classConfig.Mode == Mode.AttendedGather)
            {
                navigation.OnAnyPointReached += Navigation_OnWayPointReached;
            }
            else
            {
                AddPrecondition(GoapKey.producedcorpse, false);
                AddPrecondition(GoapKey.consumecorpse, false);
            }

            sideActivityCts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            sideActivityCts.Dispose();
        }

        private void Abort()
        {
            sideActivityCts.Cancel();
            navigation.Stop();
        }

        private void Resume()
        {
            onEnterTime = DateTime.UtcNow;

            StartSideActivity();

            if (!navigation.HasWaypoint())
            {
                RefillWaypoints(true);
            }
            else
            {
                navigation.Resume();
            }

            if (!shouldMount &&
                classConfig.UseMount &&
                mountHandler.CanMount() &&
                mountHandler.ShouldMount(navigation.TotalRoute.Last()))
            {
                shouldMount = true;
                Log("Mount up since desination far away");
            }
        }

        public override void OnActionEvent(object sender, ActionEventArgs e)
        {
            if (e.Key == GoapKey.abort)
            {
                Abort();
            }

            if (e.Key == GoapKey.resume)
            {
                Resume();
            }
        }

        public override ValueTask OnEnter()
        {
            Resume();
            return base.OnEnter();
        }

        public override ValueTask OnExit()
        {
            Abort();
            return base.OnExit();
        }

        public override ValueTask PerformAction()
        {
            if (playerReader.HasTarget && playerReader.Bits.TargetIsDead)
            {
                Log("Has target but its dead.");
                input.ClearTarget();
                wait.Update(1);
            }

            if (playerReader.Bits.IsDrowning)
            {
                //Log("Drowning! Swim up");
                input.Jump();
            }

            if (playerReader.Bits.PlayerInCombat && classConfig.Mode != Mode.AttendedGather) { return ValueTask.CompletedTask; }

            navigation.Update(sideActivityCts);

            RandomJump();

            wait.Update(1);

            return ValueTask.CompletedTask;
        }

        private void StartSideActivity()
        {
            sideActivityCts.Dispose();
            sideActivityCts = new CancellationTokenSource();

            if (classConfig.Mode == Mode.AttendedGather)
            {
                if (classConfig.GatherFindKeyConfig.Count > 1)
                {
                    sideActivityThread = new Thread(Thread_AttendedGather);
                    sideActivityThread.Start();
                }
            }
            else
            {
                sideActivityThread = new Thread(Thread_LookingForTarget);
                sideActivityThread.Start();
            }
        }

        private void Thread_LookingForTarget()
        {
            Log("Start searching for target...");

            bool validTarget()
            {
                return playerReader.HasTarget &&
                    !playerReader.Bits.TargetIsDead;
            }

            SendActionEvent(new ActionEventArgs(GoapKey.wowscreen, true));

            bool found = false;
            while (!found && !sideActivityCts.IsCancellationRequested)
            {
                if (classConfig.TargetNearestTarget.MillisecondsSinceLastClick > random.Next(minMs, maxMs))
                {
                    found = targetFinder.Search(NpcNameToFind, validTarget, sideActivityCts);
                }
                wait.Update(1);
            }

            if (found)
            {
                sideActivityCts.Cancel();
                Log("Found target!");
            }

            SendActionEvent(new ActionEventArgs(GoapKey.wowscreen, false));
        }

        private void Thread_AttendedGather()
        {
            while (!sideActivityCts.IsCancellationRequested)
            {
                if ((DateTime.UtcNow - onEnterTime).TotalMilliseconds > MIN_TIME_TO_START_CYCLE_PROFESSION)
                {
                    AlternateGatherTypes();
                }
                sideActivityCts.Token.WaitHandle.WaitOne(CYCLE_PROFESSION_PERIOD);
            }
        }

        private void AlternateGatherTypes()
        {
            var oldestKey = classConfig.GatherFindKeyConfig.MaxBy(x => x.MillisecondsSinceLastClick);
            if (!playerReader.IsCasting &&
                oldestKey?.MillisecondsSinceLastClick > CYCLE_PROFESSION_PERIOD)
            {
                logger.LogInformation($"[{oldestKey.Key}] {oldestKey.Name} pressed for {input.defaultKeyPress}ms");
                input.KeyPress(oldestKey.ConsoleKey, input.defaultKeyPress);
                oldestKey.SetClicked();
            }
        }

        private void MountIfRequired()
        {
            if (shouldMount && classConfig.UseMount && !npcNameFinder.MobsVisible &&
                mountHandler.CanMount())
            {
                shouldMount = false;
                Log("Mount up");
                mountHandler.MountUp();
                navigation.ResetStuckParameters();
            }
        }

        #region Refill rules

        private void Navigation_OnPathCalculated()
        {
            MountIfRequired();
        }

        private void Navigation_OnDestinationReached()
        {
            LogDebug("Navigation_OnDestinationReached");
            RefillWaypoints(false);
        }

        private void Navigation_OnWayPointReached()
        {
            if (classConfig.Mode == Mode.AttendedGather)
            {
                shouldMount = true;
            }

            MountIfRequired();
        }

        public void RefillWaypoints(bool onlyClosest)
        {
            Log($"RefillWaypoints - findClosest:{onlyClosest} - ThereAndBack:{input.ClassConfig.PathThereAndBack}");

            var player = playerReader.PlayerLocation;
            var path = routePoints.ToList();

            var distanceToFirst = player.DistanceXYTo(path[0]);
            var distanceToLast = player.DistanceXYTo(path[^1]);

            if (distanceToLast < distanceToFirst)
            {
                path.Reverse();
            }

            var closestPoint = path.ToList().OrderBy(p => player.DistanceXYTo(p)).First();
            if (onlyClosest)
            {
                var closestPath = new List<Vector3> { closestPoint };
                LogDebug($"RefillWaypoints: Closest wayPoint: {closestPoint}");
                navigation.SetWayPoints(closestPath);
                return;
            }

            int closestIndex = path.IndexOf(closestPoint);
            if (closestPoint == path[0] || closestPoint == path[^1])
            {
                if (input.ClassConfig.PathThereAndBack)
                {
                    navigation.SetWayPoints(path);
                }
                else
                {
                    path.Reverse();
                    navigation.SetWayPoints(path);
                }
            }
            else
            {
                var points = path.Take(closestIndex).ToList();
                points.Reverse();
                Log($"RefillWaypoints - Set destination from closest to nearest endpoint - with {points.Count} waypoints");
                navigation.SetWayPoints(points);
            }
        }

        #endregion

        private void RandomJump()
        {
            if ((DateTime.UtcNow - onEnterTime).TotalSeconds > 5 && classConfig.Jump.MillisecondsSinceLastClick > random.Next(10_000, 25_000))
            {
                Log("Random jump");
                input.Jump();
            }
        }

        private void LogDebug(string text)
        {
            if (debug)
            {
                logger.LogDebug($"{nameof(FollowRouteGoal)}: {text}");
            }
        }

        private void Log(string text)
        {
            logger.LogInformation($"{nameof(FollowRouteGoal)}: {text}");
        }
    }
}