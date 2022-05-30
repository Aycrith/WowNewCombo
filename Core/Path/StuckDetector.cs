using Core.Goals;
using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System;
using System.Numerics;

#pragma warning disable 162

namespace Core
{
    public class StuckDetector
    {
        private const bool debug = false;

        private const float MIN_RANGE_DIFF = 5;
        private const float MIN_DISTANCE = 1;
        private const float MAX_RANGE = 999999;
        private const double UNSTUCK_AFTER_MS = 2000;
        private const double ACTION_STUCK_TIME = 3000;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly PlayerReader playerReader;
        private readonly PlayerDirection playerDirection;
        private readonly StopMoving stopMoving;

        private readonly Random random = new();

        private Vector3 target;
        private float prevDistance = MAX_RANGE;
        private DateTime startTime;
        private DateTime attemptTime;

        public double ActionDurationMs => (DateTime.UtcNow - startTime).TotalMilliseconds;
        private double UnstuckMs => (DateTime.UtcNow - attemptTime).TotalMilliseconds;

        public StuckDetector(ILogger logger, ConfigurableInput input, PlayerReader playerReader, PlayerDirection playerDirection, StopMoving stopMoving)
        {
            this.logger = logger;
            this.input = input;

            this.playerReader = playerReader;
            this.playerDirection = playerDirection;
            this.stopMoving = stopMoving;

            Reset();
        }

        public void SetTargetLocation(Vector3 target)
        {
            this.target = target;
            Reset();
        }

        public void Reset()
        {
            attemptTime = DateTime.UtcNow;
            startTime = DateTime.UtcNow;

            prevDistance = MAX_RANGE;
        }

        public void Update()
        {
            if (debug)
                logger.LogDebug($"Stuck for {ActionDurationMs}ms, last tried to unstick {UnstuckMs}ms ago.");

            if (UnstuckMs > UNSTUCK_AFTER_MS)
            {
                stopMoving.Stop();

                // Turn
                ConsoleKey turnKey = random.Next(2) == 0 ? input.TurnLeftKey : input.TurnRightKey;
                int turnDuration = random.Next(125) + 350;
                logger.LogInformation($"Unstuck by turning for {turnDuration}ms");
                input.KeyPress(turnKey, turnDuration);

                // Move
                ConsoleKey moveKey = random.Next(2) == 0 ? input.ForwardKey : input.BackwardKey;
                int moveDuration = random.Next(375) + 350;
                logger.LogInformation($"Unstuck by moving for {moveDuration}ms");
                input.KeyPress(moveKey, moveDuration);

                input.Jump();

                float heading = DirectionCalculator.CalculateHeading(playerReader.PlayerLocation, target);
                playerDirection.SetDirection(heading, target);

                attemptTime = DateTime.UtcNow;
            }
            else
            {
                input.Jump();
            }
        }

        public bool IsGettingCloser()
        {
            float distance = playerReader.PlayerLocation.DistanceXYTo(target);
            if (distance < prevDistance - MIN_RANGE_DIFF)
            {
                Reset();
                prevDistance = distance;
                return true;
            }

            return ActionDurationMs < ACTION_STUCK_TIME;
        }

        public bool IsMoving()
        {
            float distance = playerReader.PlayerLocation.DistanceXYTo(target);
            if (MathF.Abs(distance - prevDistance) > MIN_DISTANCE)
            {
                Reset();
                prevDistance = distance;
                return true;
            }

            return ActionDurationMs < ACTION_STUCK_TIME;
        }
    }
}