using Core.GOAP;
using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System;

namespace Core.Goals
{
    public class WrongZoneGoal : GoapGoal
    {
        public override float Cost => 19f;

        private const float RADIAN = MathF.PI * 2;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly PlayerDirection playerDirection;
        private readonly StuckDetector stuckDetector;
        private readonly ClassConfiguration classConfiguration;

        private float lastDistance = 999;

        public DateTime LastActive { get; private set; }

        public WrongZoneGoal(AddonReader addonReader, ConfigurableInput input, PlayerDirection playerDirection, ILogger logger, StuckDetector stuckDetector, ClassConfiguration classConfiguration)
            : base(nameof(WrongZoneGoal))
        {
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.input = input;
            this.playerDirection = playerDirection;
            this.logger = logger;
            this.stuckDetector = stuckDetector;
            this.classConfiguration = classConfiguration;

            AddPrecondition(GoapKey.incombat, false);
        }

        public override bool CanRun()
        {
            return addonReader.UIMapId.Value == classConfiguration.WrongZone.ZoneId;
        }

        public override void Update()
        {
            var targetLocation = classConfiguration.WrongZone.ExitZoneLocation;

            input.Proc.SetKeyState(input.Proc.ForwardKey, true);

            if ((DateTime.UtcNow - LastActive).TotalMilliseconds > 10000)
            {
                this.stuckDetector.SetTargetLocation(targetLocation);
            }

            var location = playerReader.PlayerLocation;
            var distance = location.DistanceXYTo(targetLocation);
            var heading = DirectionCalculator.CalculateHeading(location, targetLocation);

            if (lastDistance < distance)
            {
                logger.LogInformation("Further away");
                playerDirection.SetDirection(heading, targetLocation);
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
                var diff1 = MathF.Abs(RADIAN + heading - playerReader.Direction) % RADIAN;
                var diff2 = MathF.Abs(heading - playerReader.Direction - RADIAN) % RADIAN;

                if (MathF.Min(diff1, diff2) > 0.3)
                {
                    logger.LogInformation("Correcting direction");
                    playerDirection.SetDirection(heading, targetLocation);
                }
            }

            lastDistance = distance;

            LastActive = DateTime.UtcNow;
        }

        private bool HasBeenActiveRecently()
        {
            return (DateTime.UtcNow - LastActive).TotalMilliseconds < 2000;
        }
    }
}