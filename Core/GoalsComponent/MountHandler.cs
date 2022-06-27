using Core.Goals;
using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System.Numerics;

namespace Core
{
    public class MountHandler
    {
        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly ClassConfiguration classConfig;
        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly CastingHandler castingHandler;
        private readonly StopMoving stopMoving;

        private readonly int minLevelToMount = 30;
        private readonly int mountCastTimeMs = 5000;
        private readonly int spellQueueWindowMs = 400;
        private readonly int maxFallTimeMs = 10000;
        private readonly int minDistanceToMount = 40;

        public MountHandler(ILogger logger, ConfigurableInput input, ClassConfiguration classConfig, Wait wait, AddonReader addonReader, CastingHandler castingHandler, StopMoving stopMoving)
        {
            this.logger = logger;
            this.classConfig = classConfig;
            this.input = input;
            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.castingHandler = castingHandler;
            this.stopMoving = stopMoving;
        }

        public bool CanMount()
        {
            return playerReader.Level.Value >= minLevelToMount &&
                !playerReader.Bits.IsIndoors() &&
                !playerReader.Bits.PlayerInCombat() &&
                !playerReader.Bits.IsSwimming() &&
                !playerReader.Bits.IsFalling() &&
                !IsMounted() &&
                addonReader.UsableAction.Is(classConfig.Mount) &&
                addonReader.ActionBarCooldownReader.GetRemainingCooldown(playerReader, classConfig.Mount) == 0;
        }

        public void MountUp()
        {
            if (playerReader.Class == PlayerClassEnum.Druid)
            {
                int index = -1;
                for (int i = 0; i < classConfig.Form.Length; i++)
                {
                    if (classConfig.Form[i].FormEnum is Form.Druid_Flight or Form.Druid_Travel)
                    {
                        index = i;
                        break;
                    }
                }

                if (index > -1 &&
                    castingHandler.SwitchForm(playerReader.Form, classConfig.Form[index]))
                {
                    return;
                }
            }

            if (playerReader.Bits.IsFalling())
            {
                (bool fallTimeOut, double fallElapsedMs) = wait.UntilNot(maxFallTimeMs, playerReader.Bits.IsFalling);
                Log($"waited for landing interrupted: {!fallTimeOut} - {fallElapsedMs}ms");
            }

            stopMoving.Stop();
            wait.Update();

            input.Mount();

            (bool castStartTimeOut, double castStartElapsedMs) = wait.Until(spellQueueWindowMs, () => playerReader.Bits.IsMounted() || playerReader.IsCasting());
            Log($"cast_start: {!castStartTimeOut} | Mounted: {playerReader.Bits.IsMounted()} | Delay: {castStartElapsedMs}ms");

            if (!playerReader.Bits.IsMounted())
            {
                bool hadTarget = playerReader.Bits.HasTarget();
                (bool castEndTimeOut, double elapsedMs) = wait.Until(mountCastTimeMs, () => playerReader.Bits.IsMounted() || !playerReader.IsCasting() || playerReader.Bits.HasTarget() != hadTarget);
                Log($"cast_ended: {!castEndTimeOut} | Mounted: {playerReader.Bits.IsMounted()} | Delay: {elapsedMs}ms");

                (bool mountedTimeOut, elapsedMs) = wait.Until(spellQueueWindowMs, playerReader.Bits.IsMounted);
                Log($"interupted: {!mountedTimeOut} | Mounted: {playerReader.Bits.IsMounted()} | Delay: {elapsedMs}ms");
            }
        }

        public bool ShouldMount(Vector3 target)
        {
            var location = playerReader.PlayerLocation;
            var distance = location.DistanceXYTo(target);
            return distance > minDistanceToMount;
        }

        public void Dismount()
        {
            if (playerReader.Form is Form.Druid_Flight or Form.Druid_Travel)
            {
                int index = -1;
                for (int i = 0; i < classConfig.Form.Length; i++)
                {
                    if (classConfig.Form[i].FormEnum == playerReader.Form)
                    {
                        index = i;
                        break;
                    }
                }

                if (index > -1)
                {
                    input.KeyPress(classConfig.Form[index].ConsoleKey, input.defaultKeyPress);
                    return;
                }
            }

            input.Dismount();
        }

        public bool IsMounted()
        {
            return (playerReader.Class == PlayerClassEnum.Druid &&
                playerReader.Form is Form.Druid_Flight or Form.Druid_Travel)
                || playerReader.Bits.IsMounted();
        }

        private void Log(string text)
        {
            logger.LogInformation($"{nameof(MountHandler)}: {text}");
        }
    }
}
