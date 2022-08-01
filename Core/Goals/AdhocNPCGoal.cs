using Core.GOAP;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Threading;

#pragma warning disable 162

namespace Core.Goals
{
    public class AdhocNPCGoal : GoapGoal, IGoapEventListener, IRouteProvider, IDisposable
    {
        private enum PathState
        {
            ApproachPathStart,
            FollowPath,
            Finished,
        }

        private const bool debug = false;

        private const int TIMEOUT = 5000;

        public override float Cost => key.Cost;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly KeyAction key;
        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly Navigation navigation;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly ClassConfiguration classConfig;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly MountHandler mountHandler;

        private readonly ExecGameCommand execGameCommand;
        private readonly GossipReader gossipReader;

        private bool shouldMount;

        private PathState pathState;

        #region IRouteProvider

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

        public DateTime LastActive => navigation.LastActive;

        #endregion

        public AdhocNPCGoal(KeyAction key, ILogger logger, ConfigurableInput input,
            Wait wait, AddonReader addonReader, Navigation navigation, StopMoving stopMoving,
            NpcNameTargeting npcNameTargeting, ClassConfiguration classConfig,
            MountHandler mountHandler, ExecGameCommand exec)
            : base(nameof(AdhocNPCGoal))
        {
            this.logger = logger;
            this.input = input;
            this.key = key;
            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.stopMoving = stopMoving;
            this.npcNameTargeting = npcNameTargeting;
            this.classConfig = classConfig;
            this.mountHandler = mountHandler;
            this.execGameCommand = exec;
            this.gossipReader = addonReader.GossipReader;

            this.navigation = navigation;
            navigation.OnDestinationReached += Navigation_OnDestinationReached;
            navigation.OnWayPointReached += Navigation_OnWayPointReached;

            if (bool.TryParse(key.InCombat, out bool result))
            {
                if (!result)
                    AddPrecondition(GoapKey.dangercombat, result);
                else
                    AddPrecondition(GoapKey.incombat, result);
            }

            Keys = new KeyAction[] { key };
        }

        public void Dispose()
        {
            navigation.Dispose();
        }

        public override bool CanRun() => key.CanRun();

        public void OnGoapEvent(GoapEventArgs e)
        {
            if (e is ResumeEvent)
            {
                navigation.ResetStuckParameters();
            }
        }

        public override void OnEnter()
        {
            input.ClearTarget();
            stopMoving.Stop();

            navigation.SetWayPoints(key.Path.ToArray());

            pathState = PathState.ApproachPathStart;

            if (classConfig.UseMount &&
                mountHandler.CanMount() && !shouldMount &&
                mountHandler.ShouldMount(navigation.TotalRoute.Last()))
            {
                shouldMount = true;
                Log("Mount up since desination far away");
            }
        }

        public override void OnExit()
        {
            navigation.StopMovement();
            navigation.Stop();
            npcNameTargeting.ChangeNpcType(NpcNames.None);
        }

        public override void Update()
        {
            if (playerReader.Bits.IsDrowning())
                input.Jump();

            if (pathState != PathState.Finished)
                navigation.Update();

            MountIfRequired();

            wait.Update();
        }


        private void Navigation_OnWayPointReached()
        {
            if (pathState is PathState.ApproachPathStart)
            {
                LogDebug("1 Reached the start point of the path.");
                navigation.SimplifyRouteToWaypoint = false;
            }
        }

        private void Navigation_OnDestinationReached()
        {
            if (pathState == PathState.ApproachPathStart)
            {
                LogDebug("Reached defined path end");
                stopMoving.Stop();

                input.ClearTarget();
                wait.Update();

                bool found = false;

                if (!classConfig.KeyboardOnly)
                {
                    npcNameTargeting.ChangeNpcType(NpcNames.Friendly | NpcNames.Neutral);
                    npcNameTargeting.WaitForUpdate();
                    found = npcNameTargeting.FindBy(CursorType.Vendor, CursorType.Repair, CursorType.Innkeeper);
                    wait.Update();

                    if (!found)
                    {
                        LogWarn($"No target found by cursor({CursorType.Vendor.ToStringF()}, {CursorType.Repair.ToStringF()}, {CursorType.Innkeeper.ToStringF()})!");
                    }
                }

                if (!found)
                {
                    Log($"Use KeyAction.Key macro to aquire target");
                    input.Proc.KeyPress(key.ConsoleKey, input.defaultKeyPress);
                    wait.Update();
                }

                wait.Until(400, playerReader.Bits.HasTarget);
                if (!playerReader.Bits.HasTarget())
                {
                    LogWarn("No target found! Turn left to find NPC");
                    using CancellationTokenSource cts = new();
                    input.Proc.KeyPressSleep(input.Proc.TurnLeftKey, 250, cts);
                    return;
                }

                Log($"Found Target!");
                input.Interact();

                if (!OpenMerchantWindow())
                    return;

                input.Proc.KeyPress(ConsoleKey.Escape, input.defaultKeyPress);
                input.ClearTarget();
                wait.Update();

                Vector3[] reversePath = key.Path.ToArray();
                Array.Reverse(reversePath);
                navigation.SetWayPoints(reversePath);

                pathState++;

                LogDebug("Go back reverse to the start point of the path.");
                navigation.ResetStuckParameters();

                // At this point the BagsFull is false
                // which mean it it would exit the Goal
                // instead keep it trapped to follow the route back
                while (navigation.HasWaypoint())
                {
                    navigation.Update();
                    wait.Update();
                }

                pathState = PathState.Finished;

                LogDebug("2 Reached the start point of the path.");
                stopMoving.Stop();

                navigation.SimplifyRouteToWaypoint = true;
            }
        }


        private void MountIfRequired()
        {
            if (shouldMount && mountHandler.CanMount())
            {
                shouldMount = false;
                mountHandler.MountUp();
            }
        }

        private bool OpenMerchantWindow()
        {
            (bool t, double e) = wait.Until(TIMEOUT, gossipReader.GossipStartOrMerchantWindowOpened);
            if (gossipReader.MerchantWindowOpened())
            {
                LogWarn($"Gossip no options! {e}ms");
            }
            else
            {
                (t, e) = wait.Until(TIMEOUT, gossipReader.GossipEnd);
                if (t)
                {
                    LogWarn($"Gossip - {nameof(gossipReader.GossipEnd)} not fired after {e}ms");
                    return false;
                }
                else
                {
                    if (gossipReader.Gossips.TryGetValue(Gossip.Vendor, out int orderNum))
                    {
                        Log($"Picked {orderNum}th for {Gossip.Vendor.ToStringF()}");
                        execGameCommand.Run($"/run SelectGossipOption({orderNum})--");
                    }
                    else
                    {
                        LogWarn($"Target({playerReader.TargetId}) has no {Gossip.Vendor.ToStringF()} option!");
                        return false;
                    }
                }
            }

            Log($"Merchant window opened after {e}ms");

            (t, e) = wait.Until(TIMEOUT, gossipReader.MerchantWindowSelling);
            if (!t)
            {
                Log($"Merchant sell grey items started after {e}ms");

                (t, e) = wait.Until(TIMEOUT, gossipReader.MerchantWindowSellingFinished);
                if (!t)
                {
                    Log($"Merchant sell grey items finished, took {e}ms");
                    return true;
                }
                else
                {
                    Log($"Merchant sell grey items timeout! Too many items to sell?! Increase {nameof(TIMEOUT)} - {e}ms");
                    return true;
                }
            }
            else
            {
                Log($"Merchant sell nothing! {e}ms");
                return true;
            }
        }


        private void Log(string text)
        {
            logger.LogInformation(text);
        }

        private void LogDebug(string text)
        {
            if (debug)
                logger.LogDebug(text);
        }

        private void LogWarn(string text)
        {
            logger.LogWarning(text);
        }
    }
}