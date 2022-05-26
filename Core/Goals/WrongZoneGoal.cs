using Core.GOAP;
using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System;
using System.Threading.Tasks;

namespace Core.Goals
{
    public class WrongZoneGoal : GoapGoal
    {
        public override float CostOfPerformingAction => 19f;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly PlayerDirection playerDirection;
        private readonly StuckDetector stuckDetector;
        private readonly ClassConfiguration classConfiguration;

        private readonly float RADIAN = MathF.PI * 2;
        private float lastDistance = 999;

        public DateTime LastActive { get; private set; }

        public WrongZoneGoal(AddonReader addonReader, ConfigurableInput input, PlayerDirection playerDirection, ILogger logger, StuckDetector stuckDetector, ClassConfiguration classConfiguration)
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

        public override bool CheckIfActionCanRun()
        {
            return addonReader.UIMapId.Value == this.classConfiguration.WrongZone.ZoneId;
        }

        public override async ValueTask PerformAction()
        {
            var targetLocation = this.classConfiguration.WrongZone.ExitZoneLocation;

            await Task.Delay(200);
            input.SetKeyState(input.ForwardKey, true);

            if (this.playerReader.Bits.PlayerInCombat) { return; }

            if ((DateTime.UtcNow - LastActive).TotalSeconds > 10)
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
            else if (!this.stuckDetector.IsGettingCloser())
            {
                // stuck so jump
                input.SetKeyState(input.ForwardKey, true);
                await Task.Delay(100);
                if (HasBeenActiveRecently())
                {
                    this.stuckDetector.Update();
                }
                else
                {
                    await Task.Delay(1000);
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
            return (DateTime.UtcNow - LastActive).TotalSeconds < 2;
        }
    }
}