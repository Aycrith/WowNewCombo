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

        private const int MIN_DISTANCE_TO_INTERRUPT_CAST = 60;

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
                addonReader.ActionBarCooldownReader.GetRemainingCooldown(classConfig.Mount) == 0;
        }

        public void MountUp()
        {
            if (playerReader.Class == UnitClass.Druid)
            {
                KeyAction? keyAction = null;
                for (int i = 0; i < classConfig.Form.Length; i++)
                {
                    keyAction = classConfig.Form[i];
                    if (keyAction.FormEnum is Form.Druid_Flight or Form.Druid_Travel)
                    {
                        break;
                    }
                }

                if (keyAction != null && castingHandler.SwitchForm(keyAction))
                {
                    return;
                }
            }

            wait.While(playerReader.Bits.IsFalling);

            stopMoving.Stop();
            wait.Update();

            input.Mount();

            (bool t, double e) =
                wait.Until(CastingHandler.SpellQueueTimeMs + playerReader.NetworkLatency.Value, CastDetected);
            Log($"Cast started ? {!t} {e}ms");

            if (!playerReader.Bits.IsMounted())
            {
                wait.Update();

                (t, e) =
                    wait.Until(playerReader.RemainCastMs + playerReader.NetworkLatency.Value, MountedOrNotCastingOrValidTargetOrEnteredCombat);
                Log($"Cast ended ? {!t} {e}ms");

                if (!HasValidTarget())
                {
                    (t, e) =
                        wait.Until(CastingHandler.SpellQueueTimeMs + playerReader.NetworkLatency.Value, playerReader.Bits.IsMounted);

                    Log($"Mounted ? {playerReader.Bits.IsMounted()} {e}ms");
                }

                if (playerReader.Bits.PlayerInCombat() && playerReader.Bits.HasTarget() && !playerReader.Bits.TargetOfTargetIsPlayerOrPet())
                {
                    input.ClearTarget();
                    wait.Update();
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
            return (playerReader.Class == UnitClass.Druid &&
                playerReader.Form is Form.Druid_Flight or Form.Druid_Travel)
                || playerReader.Bits.IsMounted();
        }

        private bool CastDetected() => playerReader.Bits.IsMounted() || playerReader.IsCasting();

        private bool MountedOrNotCastingOrValidTargetOrEnteredCombat() => playerReader.Bits.IsMounted() || !playerReader.IsCasting() || HasValidTarget() || playerReader.Bits.PlayerInCombat();

        private bool HasValidTarget() => playerReader.Bits.HasTarget() && !blacklist.IsTargetBlacklisted() && playerReader.MinRange() < MIN_DISTANCE_TO_INTERRUPT_CAST;

        private void Log(string text)
        {
            logger.LogInformation($"{nameof(MountHandler)}: {text}");
        }
    }
}
