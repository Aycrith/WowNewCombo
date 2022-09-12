using Core.GOAP;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Core.Goals
{
    public sealed partial class WalkToCorpseGoal : GoapGoal, IGoapEventListener, IRouteProvider, IDisposable
    {
        public override float Cost => 1f;

        private readonly ILogger logger;
        private readonly Wait wait;
        private readonly ConfigurableInput input;

        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly Navigation navigation;
        private readonly StopMoving stopMoving;

        public List<Vector3> Deaths { get; } = new();

        private DateTime onEnterTime;

        #region IRouteProvider

        public DateTime LastActive => navigation.LastActive;

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

        public WalkToCorpseGoal(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, Navigation navigation, StopMoving stopMoving)
            : base(nameof(WalkToCorpseGoal))
        {
            this.logger = logger;
            this.wait = wait;
            this.input = input;

            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.stopMoving = stopMoving;

            this.navigation = navigation;

            AddPrecondition(GoapKey.isdead, true);
        }

        public void Dispose()
        {
            navigation.Dispose();
        }

        public void OnGoapEvent(GoapEventArgs e)
        {
            if (e is ResumeEvent)
            {
                navigation.ResetStuckParameters();
            }
        }

        public override void OnEnter()
        {
            playerReader.WorldPosZ = 0;
            addonReader.PlayerDied();

            wait.While(AliveOrLoadingScreen);
            Log($"Player teleported to the graveyard!");

            var corpseLocation = playerReader.CorpseMapPos;
            Log($"Corpse location is {corpseLocation}");

            Deaths.Add(corpseLocation);

            navigation.SetWayPoints(new Vector3[] { corpseLocation });

            onEnterTime = DateTime.UtcNow;
        }

        public override void OnExit()
        {
            navigation.StopMovement();
            navigation.Stop();
        }

        public override void Update()
        {
            if (!playerReader.Bits.CorpseInRange())
            {
                navigation.Update();
            }
            else
            {
                stopMoving.Stop();
                navigation.ResetStuckParameters();
            }

            RandomJump();

            wait.Update();
        }

        private void RandomJump()
        {
            if ((DateTime.UtcNow - onEnterTime).TotalSeconds > 5 && input.ClassConfig.Jump.MillisecondsSinceLastClick > Random.Shared.Next(10_000, 25_000))
            {
                Log("Random jump");
                input.Jump();
            }
        }

        private bool AliveOrLoadingScreen()
        {
            return playerReader.CorpseMapPos == Vector3.Zero;
        }

        private void Log(string text)
        {
            logger.LogInformation($"[{nameof(WalkToCorpseGoal)}]: {text}");
        }
    }
}