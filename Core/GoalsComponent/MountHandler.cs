using Core.Goals;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SharedLib.Extensions;

using System;
using System.Numerics;

namespace Core;

public sealed partial class MountHandler : IMountHandler
{
    private const int DISTANCE_TO_MOUNT = 40;

    private const int MIN_DISTANCE_TO_INTERRUPT_CAST = 60;

    private readonly ILogger<MountHandler> logger;
    private readonly ConfigurableInput input;
    private readonly ClassConfiguration classConfig;
    private readonly Wait wait;
    private readonly ActionBarBits<IUsableAction> usableAction;
    private readonly ActionBarCooldownReader cooldownReader;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly StopMoving stopMoving;
    private readonly IBlacklist targetBlacklist;
    private readonly IOptionsMonitor<MountUnlockOptions> mountUnlockOptions;
    private readonly ConfigurableInput configurableInput;

    private KeyAction? cachedStealthKey;
    private bool stealthKeySearched;

    public MountHandler(ILogger<MountHandler> logger, ConfigurableInput input,
        ClassConfiguration classConfig, AddonBits bits, Wait wait,
        PlayerReader playerReader, ActionBarBits<IUsableAction> usableAction,
        ActionBarCooldownReader cooldownReader,
        StopMoving stopMoving,
        IBlacklist targetBlacklist,
        IOptionsMonitor<MountUnlockOptions> mountUnlockOptions)
    {
        this.logger = logger;
        this.classConfig = classConfig;
        this.input = input;
        this.wait = wait;
        this.usableAction = usableAction;
        this.cooldownReader = cooldownReader;
        this.playerReader = playerReader;
        this.bits = bits;
        this.stopMoving = stopMoving;
        this.targetBlacklist = targetBlacklist;
        this.mountUnlockOptions = mountUnlockOptions;
        this.configurableInput = input;
    }

    public bool CanMount()
    {
        if (!MeetsMountUnlockRequirement())
        {
            return false;
        }

        return
            !IsMounted() &&
            !bits.Indoors() &&
            !bits.Combat() &&
            !bits.Swimming() &&
            !bits.Falling() &&
            usableAction.Is(classConfig.Mount) &&
            cooldownReader.Get(classConfig.Mount) == 0;
    }

    private bool MeetsMountUnlockRequirement()
    {
        MountUnlockOptions options = mountUnlockOptions.CurrentValue;
        if (!options.EnforceTbcMountLevelRequirement)
        {
            return true;
        }

        // Only apply this rule to TBC Classic.
        if (playerReader.Version != SharedLib.ClientVersion.TBC)
        {
            return true;
        }

        int requiredLevel = options.TbcMountUnlockLevel;
        if (requiredLevel <= 0)
        {
            requiredLevel = 30;
        }

        return playerReader.Level.Value >= requiredLevel;
    }

    public void MountUp()
    {
        wait.While(bits.Falling);

        stopMoving.Stop();
        wait.Update();

        input.PressMount();
        wait.Update();

        float e = wait.Until(
            playerReader.DoubleNetworkLatency,
            CastDetected);

        LogCastStarted(logger, e);

        wait.Update();

        e = wait.Until(
            playerReader.RemainCastMs + playerReader.DoubleNetworkLatency,
            MountedOrNotCastingOrValidTargetOrEnteredCombat);

        LogCastEnded(logger, e);

        if (HasValidTarget())
        {
            LogIsMounted(logger, bits.Mounted());
            return;
        }

        wait.Fixed(playerReader.NetworkLatency);
        LogIsMounted(logger, bits.Mounted());
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
        wait.Update();

        LogIsMounted(logger, bits.Mounted());
    }

    public bool IsMounted()
    {
        return bits.Mounted();
    }

    private bool CastDetected() =>
        bits.Mounted() || playerReader.IsCasting();

    private bool MountedOrNotCastingOrValidTargetOrEnteredCombat() =>
        bits.Mounted() ||
        !playerReader.IsCasting() ||
        HasValidTarget() ||
        bits.Combat();

    private bool HasValidTarget() =>
        bits.Target() && bits.Target_Alive() && !targetBlacklist.Is() &&
        playerReader.MinRange() < MIN_DISTANCE_TO_INTERRUPT_CAST;

    private KeyAction? FindStealthKey()
    {
        if (stealthKeySearched)
            return cachedStealthKey;

        stealthKeySearched = true;

        // Search in Pull sequence
        foreach (KeyAction key in classConfig.Pull.Sequence)
        {
            if (key.Name.Equals("Stealth", StringComparison.OrdinalIgnoreCase) ||
                key.Name.Equals("Prowl", StringComparison.OrdinalIgnoreCase))
            {
                cachedStealthKey = key;
                return cachedStealthKey;
            }
        }

        // Search in Adhoc sequence
        foreach (KeyAction key in classConfig.Adhoc.Sequence)
        {
            if (key.Name.Equals("Stealth", StringComparison.OrdinalIgnoreCase) ||
                key.Name.Equals("Prowl", StringComparison.OrdinalIgnoreCase))
            {
                cachedStealthKey = key;
                return cachedStealthKey;
            }
        }

        return null;
    }

    private bool ShouldUnstealthForTravel(float distance)
    {
        MountUnlockOptions options = mountUnlockOptions.CurrentValue;
        if (!options.AutoUnstealthForTravel)
            return false;

        if (!bits.Stealthed())
            return false;

        if (bits.Combat())
            return false;

        // Pre-mount characters (e.g. low-level rogues) can get into an Adhoc Stealth ->
        // Follow -> auto-unstealth travel churn loop that looks robotic and degrades path
        // following. Only use this travel-speed unstealth optimization once mounts are
        // actually unlocked for the current client/version/level.
        if (!MeetsMountUnlockRequirement())
            return false;

        if (CanMount())
            return false;

        return ShouldMount(distance);
    }

    private void UnstealthForTravel()
    {
        KeyAction? stealthKey = FindStealthKey();
        if (stealthKey == null)
        {
            LogStealthKeyNotFound(logger);
            return;
        }

        LogUnstealthingForTravel(logger);

        configurableInput.PressRandom(stealthKey);
        wait.Update();

        float elapsed = wait.Until(500, () => !bits.Stealthed());

        if (bits.Stealthed())
        {
            LogUnstealthTimeout(logger, elapsed);
        }
        else
        {
            LogUnstealthSuccess(logger, elapsed);
        }
    }

    public void OptimizeTravelSpeed(float totalDistance)
    {
        // First try mounting if possible
        if (classConfig.UseMount && CanMount() && ShouldMount(totalDistance))
        {
            Log("Mount up");
            MountUp();
            return;
        }

        // Fallback to unstealth for travel speed optimization
        if (ShouldUnstealthForTravel(totalDistance))
        {
            UnstealthForTravel();
        }
    }

    private void Log(string text)
    {
        logger.LogInformation(text);
    }


    #region Logging

    [LoggerMessage(
        EventId = 0110,
        Level = LogLevel.Information,
        Message = "Cast started {elapsed}ms")]
    static partial void LogCastStarted(ILogger logger, float elapsed);

    [LoggerMessage(
        EventId = 0111,
        Level = LogLevel.Information,
        Message = "Cast ended {elapsed}ms")]
    static partial void LogCastEnded(ILogger logger, float elapsed);

    [LoggerMessage(
        EventId = 0112,
        Level = LogLevel.Information,
        Message = "Mounted ? {mounted}")]
    static partial void LogIsMounted(ILogger logger, bool mounted);

    [LoggerMessage(
        EventId = 0113,
        Level = LogLevel.Information,
        Message = "[MountHandler      ] Unstealthing for travel")]
    static partial void LogUnstealthingForTravel(ILogger logger);

    [LoggerMessage(
        EventId = 0114,
        Level = LogLevel.Debug,
        Message = "[MountHandler      ] Unstealth success ({elapsed}ms)")]
    static partial void LogUnstealthSuccess(ILogger logger, float elapsed);

    [LoggerMessage(
        EventId = 0115,
        Level = LogLevel.Warning,
        Message = "[MountHandler      ] Unstealth timeout ({elapsed}ms) - stealth buff persists")]
    static partial void LogUnstealthTimeout(ILogger logger, float elapsed);

    [LoggerMessage(
        EventId = 0116,
        Level = LogLevel.Warning,
        Message = "[MountHandler      ] Stealth key not found in Pull or Adhoc sequences")]
    static partial void LogStealthKeyNotFound(ILogger logger);

    #endregion
}
