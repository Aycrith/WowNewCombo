using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System.Numerics;

namespace Core
{
    public class CombatUtil
    {
        private const float MIN_DISTANCE = 0.01f;

        private readonly ILogger logger;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly ConfigurableInput input;
        private readonly Wait wait;

        private const bool debug = true;

        private bool outOfCombat;

        public CombatUtil(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader)
        {
            this.logger = logger;
            this.input = input;
            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;

            outOfCombat = !playerReader.Bits.PlayerInCombat();
        }

        public void Update()
        {
            outOfCombat = !playerReader.Bits.PlayerInCombat();
        }

        public bool EnteredCombat()
        {
            if (!outOfCombat && !playerReader.Bits.PlayerInCombat())
            {
                Log("Combat Leave");
                outOfCombat = true;
                return false;
            }

            if (outOfCombat && playerReader.Bits.PlayerInCombat())
            {
                Log("Combat Enter");
                outOfCombat = false;
                return true;
            }

            return false;
        }

        public bool AquiredTarget(int maxTimeMs = 400)
        {
            if (this.playerReader.Bits.PlayerInCombat())
            {
                if (this.playerReader.PetHasTarget)
                {
                    input.TargetPet();
                    Log($"Pets target {playerReader.TargetTarget}");
                    if (playerReader.TargetTarget == TargetTargetEnum.PetHasATarget)
                    {
                        Log($"{nameof(AquiredTarget)}: Found target by pet");
                        input.TargetOfTarget();
                        return true;
                    }
                }

                input.NearestTarget();
                wait.Update();

                if (playerReader.Bits.HasTarget() &&
                    playerReader.Bits.TargetInCombat() &&
                    (playerReader.Bits.TargetOfTargetIsPlayerOrPet() ||
                    addonReader.CombatLog.DamageTaken.Contains(playerReader.TargetGuid)))
                {
                    Log("Found target");
                    return true;
                }

                input.ClearTarget();
                wait.Update();

                if (!wait.Till(maxTimeMs, PlayerOrPetHasTarget))
                {
                    Log($"{nameof(AquiredTarget)}: Someone started attacking me!");
                    return true;
                }

                Log($"{nameof(AquiredTarget)}: No target found after {maxTimeMs}ms");
                input.ClearTarget();
                wait.Update();
            }
            return false;
        }

        public bool IsPlayerMoving(Vector3 lastPos)
        {
            float distance = playerReader.PlayerLocation.DistanceXYTo(lastPos);
            return distance > MIN_DISTANCE;
        }

        private bool PlayerOrPetHasTarget()
        {
            return playerReader.Bits.HasTarget() || playerReader.PetHasTarget;
        }

        private void Log(string text)
        {
            if (debug)
            {
                logger.LogDebug($"{nameof(CombatUtil)}: {text}");
            }
        }
    }
}
