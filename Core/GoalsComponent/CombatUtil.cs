using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System.Numerics;

namespace Core;

public sealed class CombatUtil
{
    private const float MIN_DISTANCE = 0.01f;

    private readonly ILogger<CombatUtil> logger;
    private readonly AddonReader addonReader;
    private readonly PlayerReader playerReader;
    private readonly ConfigurableInput input;
    private readonly Wait wait;

    private const bool debug = true;

    private bool outOfCombat;

    public CombatUtil(ILogger<CombatUtil> logger, ConfigurableInput input,
        Wait wait, AddonReader addonReader)
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
            if (this.playerReader.PetHasTarget())
            {
                input.PressTargetPet();
                Log($"Pets target {playerReader.TargetTarget}");
                if (playerReader.TargetTarget == UnitsTarget.PetHasATarget)
                {
                    Log($"{nameof(AquiredTarget)}: Found target by pet");
                    input.PressTargetOfTarget();
                    return true;
                }
            }

            input.PressNearestTarget();
            wait.Update();

            if (playerReader.Bits.HasTarget() &&
                playerReader.Bits.TargetInCombat() &&
                (playerReader.Bits.TargetOfTargetIsPlayerOrPet() ||
                addonReader.CombatLog.DamageTaken.Contains(playerReader.TargetGuid)))
            {
                Log("Found target");
                return true;
            }

            input.PressClearTarget();
            wait.Update();

            if (!wait.Till(maxTimeMs, PlayerOrPetHasTarget))
            {
                Log($"{nameof(AquiredTarget)}: Someone started attacking me!");
                return true;
            }

            Log($"{nameof(AquiredTarget)}: No target found after {maxTimeMs}ms");
            input.PressClearTarget();
            wait.Update();
        }
        return false;
    }

    public bool IsPlayerMoving(Vector3 map)
    {
        float mapDistance = playerReader.MapPos.MapDistanceXYTo(map);
        return mapDistance > MIN_DISTANCE;
    }

    private bool PlayerOrPetHasTarget()
    {
        return playerReader.Bits.HasTarget() || playerReader.PetHasTarget();
    }

    private void Log(string text)
    {
        if (debug)
        {
            logger.LogDebug($"{text}");
        }
    }
}
