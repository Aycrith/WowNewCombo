using Core.GOAP;

using Microsoft.Extensions.Logging;

using SharedLib.Extensions;

using System;
using System.Numerics;

using static System.MathF;

namespace Core.Goals
{
    public sealed class WrongZoneGoal : GoapGoal
    {
        public override float Cost => 19f;

        private const float RADIAN = MathF.PI * 2;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly PlayerReader playerReader;
        private readonly PlayerDirection playerDirection;
        private readonly StuckDetector stuckDetector;
        private readonly ClassConfiguration classConfiguration;

        private float lastDistance = 999;

        public DateTime LastActive { get; private set; }

        public WrongZoneGoal(PlayerReader playerReader, ConfigurableInput input, PlayerDirection playerDirection, ILogger logger, StuckDetector stuckDetector, ClassConfiguration classConfiguration)
            : base(nameof(WrongZoneGoal))
        {
            this.playerReader = playerReader;
            this.input = input;
            this.playerDirection = playerDirection;
            this.logger = logger;
            this.stuckDetector = stuckDetector;
            this.classConfiguration = classConfiguration;

            AddPrecondition(GoapKey.incombat, false);
        }

        public override bool CanRun()
        {
            return playerReader.UIMapId.Value == classConfiguration.WrongZone.ZoneId;
        }

        public override void Update()
        {
            Vector3 exitMap = classConfiguration.WrongZone.ExitZoneLocation;

            input.Proc.SetKeyState(input.Proc.ForwardKey, true);

            if ((DateTime.UtcNow - LastActive).TotalMilliseconds > 10000)
            {
                stuckDetector.SetTargetLocation(exitMap);
            }

            Vector3 playerMap = playerReader.MapPos;
            float mapDistance = playerMap.MapDistanceXYTo(exitMap);
            float heading = DirectionCalculator.CalculateMapHeading(playerMap, exitMap);

            if (lastDistance < mapDistance)
            {
                logger.LogInformation("Further away");
                playerDirection.SetDirection(heading, exitMap);
            }
            else if (!stuckDetector.IsGettingCloser())
            {
                // stuck so jump
                input.Proc.SetKeyState(input.Proc.ForwardKey, true);

                if (HasBeenActiveRecently())
                {
                    stuckDetector.Update();
                }
                else
                {
                    logger.LogInformation("Resuming movement");
                }
            }
            else // distance closer
            {
                float diff1 = Abs(RADIAN + heading - playerReader.Direction) % RADIAN;
                float diff2 = Abs(heading - playerReader.Direction - RADIAN) % RADIAN;

                if (Min(diff1, diff2) > 0.3)
                {
                    logger.LogInformation("Correcting direction");
                    playerDirection.SetDirection(heading, exitMap);
                }
            }

            lastDistance = mapDistance;

            LastActive = DateTime.UtcNow;
        }

        private bool HasBeenActiveRecently()
        {
            return (DateTime.UtcNow - LastActive).TotalMilliseconds < 2000;
        }
    }
}