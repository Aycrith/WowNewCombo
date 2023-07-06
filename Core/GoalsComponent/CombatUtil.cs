using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System.Numerics;

namespace Core;

public sealed class CombatUtil
{
    private const float MIN_DISTANCE = 0.01f;

    private readonly ILogger<CombatUtil> logger;
    private readonly PlayerReader playerReader;
    private readonly CombatLog combatLog;
    private readonly AddonBits bits;
    private readonly ConfigurableInput input;
    private readonly Wait wait;

    private const bool debug = true;

    private bool outOfCombat;

    public CombatUtil(ILogger<CombatUtil> logger, ConfigurableInput input,
        AddonBits bits, Wait wait, PlayerReader playerReader, CombatLog combatLog)
    {
        this.logger = logger;
        this.input = input;
        this.wait = wait;
        this.playerReader = playerReader;
        this.bits = bits;
        this.combatLog = combatLog;

        outOfCombat = !bits.PlayerInCombat();
    }

    public void Update()
    {
        outOfCombat = !bits.PlayerInCombat();
    }

    public bool EnteredCombat()
    {
        if (!outOfCombat && !bits.PlayerInCombat())
        {
            Log("Combat Leave");
            outOfCombat = true;
            return false;
        }

        if (outOfCombat && bits.PlayerInCombat())
        {
            Log("Combat Enter");
            outOfCombat = false;
            return true;
        }

        return false;
    }

    public bool AcquiredTarget(int maxTimeMs = 400)
    {
        if (!bits.PlayerInCombat())
            return false;

        if (playerReader.PetHasTarget())
        {
            input.PressTargetPet();
            Log($"Pets target {playerReader.TargetTarget}");
            if (playerReader.TargetTarget == UnitsTarget.PetHasATarget)
            {
                Log($"{nameof(AcquiredTarget)}: Found target by pet");
                input.PressTargetOfTarget();
                return true;
            }
        }

        input.PressNearestTarget();
        wait.Update();

        if (bits.HasTarget() &&
            bits.TargetInCombat() &&
            (bits.TargetOfTargetIsPlayerOrPet() ||
            combatLog.DamageTaken.Contains(playerReader.TargetGuid)))
        {
            Log("Found target");
            return true;
        }

        input.PressClearTarget();
        wait.Update();

        if (!wait.Till(maxTimeMs, PlayerOrPetHasTarget))
        {
            Log($"{nameof(AcquiredTarget)}: Someone started attacking me!");
            return true;
        }

        Log($"{nameof(AcquiredTarget)}: No target found after {maxTimeMs}ms");
        input.PressClearTarget();
        wait.Update();
        return false;
    }

    public bool IsPlayerMoving(Vector3 map)
    {
        float mapDistance = playerReader.MapPos.MapDistanceXYTo(map);
        return mapDistance > MIN_DISTANCE;
    }

    private bool PlayerOrPetHasTarget()
    {
        return bits.HasTarget() || playerReader.PetHasTarget();
    }

    private void Log(string text)
    {
        if (debug)
        {
            logger.LogDebug($"{text}");
        }
    }
}
