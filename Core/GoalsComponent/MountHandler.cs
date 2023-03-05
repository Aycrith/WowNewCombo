using Core.Goals;

using Microsoft.Extensions.Logging;

using SharedLib.Extensions;

using System.Numerics;

namespace Core;

public sealed class MountHandler : IMountHandler
{
    private const int DISTANCE_TO_MOUNT = 40;

    private const int MIN_DISTANCE_TO_INTERRUPT_CAST = 60;

    private readonly ILogger logger;
    private readonly ConfigurableInput input;
    private readonly ClassConfiguration classConfig;
    private readonly Wait wait;
    private readonly ActionBarBits usableAction;
    private readonly ActionBarCooldownReader cooldownReader;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly StopMoving stopMoving;
    private readonly IBlacklist targetBlacklist;

    public MountHandler(ILogger logger, ConfigurableInput input,
        ClassConfiguration classConfig, Wait wait, AddonReader addonReader,
        StopMoving stopMoving, IBlacklist blacklist)
    {
        this.logger = logger;
        this.classConfig = classConfig;
        this.input = input;
        this.wait = wait;
        this.usableAction = addonReader.UsableAction;
        this.cooldownReader = addonReader.ActionBarCooldownReader;
        this.playerReader = addonReader.PlayerReader;
        this.bits = playerReader.Bits;
        this.stopMoving = stopMoving;
        this.targetBlacklist = blacklist;
    }

    public bool CanMount()
    {
        return
            !IsMounted() &&
            !bits.IsIndoors() &&
            !bits.PlayerInCombat() &&
            !bits.IsSwimming() &&
            !bits.IsFalling() &&
            usableAction.Is(classConfig.Mount) &&
            cooldownReader.Get(classConfig.Mount) == 0;
    }

    public void MountUp()
    {
        wait.While(bits.IsFalling);

        stopMoving.Stop();
        wait.Update();

        input.PressMount();

        (bool t, double e) =
            wait.Until(CastingHandler.SPELL_QUEUE + playerReader.NetworkLatency.Value, CastDetected);
        Log($"Cast started ? {!t} {e}ms");

        if (bits.IsMounted())
            return;

        wait.Update();

        (t, e) =
            wait.Until(playerReader.RemainCastMs + playerReader.NetworkLatency.Value, MountedOrNotCastingOrValidTargetOrEnteredCombat);
        Log($"Cast ended ? {!t} {e}ms");

        if (bits.IsMounted())
            return;

        if (bits.HasTarget())
        {
            if (HasValidTarget())
            {
                return;
            }
            else if (!bits.IsMounted())
            {
                (t, e) = wait.Until(CastingHandler.SPELL_QUEUE + playerReader.NetworkLatency.Value, bits.IsMounted);
                Log($"Mounted ? {bits.IsMounted()} {e}ms");
                wait.Update();
            }
        }
    }

    public bool ShouldMount(Vector3 targetW)
    {
        Vector3 playerW = playerReader.WorldPos;
        float distance = playerW.WorldDistanceXYTo(targetW);
        return distance > DISTANCE_TO_MOUNT;
    }

    public static bool ShouldMount(float totalDistance)
    {
        return totalDistance > DISTANCE_TO_MOUNT;
    }

    public void Dismount()
    {
        input.PressDismount();
    }

    public bool IsMounted()
    {
        return bits.IsMounted();
    }

    private bool CastDetected() =>
        bits.IsMounted() || playerReader.IsCasting();

    private bool MountedOrNotCastingOrValidTargetOrEnteredCombat() =>
        bits.IsMounted() ||
        !playerReader.IsCasting() ||
        HasValidTarget() ||
        bits.PlayerInCombat();

    private bool HasValidTarget() =>
        bits.HasTarget() && bits.TargetAlive() && !targetBlacklist.Is() &&
        playerReader.MinRange() < MIN_DISTANCE_TO_INTERRUPT_CAST;

    private void Log(string text)
    {
        logger.LogInformation($"{nameof(MountHandler)}: {text}");
    }

    private void LogWarning(string text)
    {
        logger.LogWarning($"{nameof(MountHandler)}: {text}");
    }
}
