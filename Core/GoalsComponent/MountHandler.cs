using Core.Goals;
using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System.Numerics;

namespace Core
{
    public class MountHandler
    {
        private const int MIN_LEVEL_TO_MOUNT = 30;
        private const int DISTANCE_TO_MOUNT = 40;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly ClassConfiguration classConfig;
        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly CastingHandler castingHandler;
        private readonly StopMoving stopMoving;
        private readonly IBlacklist blacklist;

        public MountHandler(ILogger logger, ConfigurableInput input, ClassConfiguration classConfig, Wait wait, AddonReader addonReader, CastingHandler castingHandler, StopMoving stopMoving, IBlacklist blacklist)
        {
            this.logger = logger;
            this.classConfig = classConfig;
            this.input = input;
            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.castingHandler = castingHandler;
            this.stopMoving = stopMoving;
            this.blacklist = blacklist;
        }

        public bool CanMount()
        {
            return playerReader.Level.Value >= MIN_LEVEL_TO_MOUNT &&
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
                wait.While(playerReader.Bits.IsFalling);
            }

            stopMoving.Stop();
            wait.Update();

            input.Mount();

            (bool timeOut, double elapsedMs) =
                wait.Until(CastingHandler.SpellQueueTimeMs + playerReader.NetworkLatency.Value, CastDetected);
            Log($"Cast started ? {!timeOut} {elapsedMs}ms");

            if (!playerReader.Bits.IsMounted())
            {
                wait.Update();

                (timeOut, elapsedMs) =
                    wait.Until(playerReader.RemainCastMs + playerReader.NetworkLatency.Value, MountedOrNotCastingOrValidTarget);
                Log($"Cast ended ? {!timeOut} {elapsedMs}ms");

                if (!HasValidTarget())
                {
                    (timeOut, elapsedMs) =
                        wait.Until(CastingHandler.SpellQueueTimeMs + playerReader.NetworkLatency.Value, playerReader.Bits.IsMounted);

                    Log($"Mounted ? {playerReader.Bits.IsMounted()} {elapsedMs}ms");
                }
            }
        }

        public bool ShouldMount(Vector3 target)
        {
            var location = playerReader.PlayerLocation;
            var distance = location.DistanceXYTo(target);
            return distance > DISTANCE_TO_MOUNT;
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
                    input.Proc.KeyPress(classConfig.Form[index].ConsoleKey, input.defaultKeyPress);
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

        private bool CastDetected() => playerReader.Bits.IsMounted() || playerReader.IsCasting();

        private bool MountedOrNotCastingOrValidTarget() => playerReader.Bits.IsMounted() || !playerReader.IsCasting() || HasValidTarget();

        private bool HasValidTarget() => playerReader.Bits.HasTarget() && !blacklist.IsTargetBlacklisted();

        private void Log(string text)
        {
            logger.LogInformation($"{nameof(MountHandler)}: {text}");
        }
    }
}
