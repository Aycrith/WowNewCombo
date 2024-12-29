using System.Threading;
using System;

using Game;
using Microsoft.Extensions.Logging;

namespace Core;

public sealed partial class ConfigurableInput
{
    private readonly ILogger<ConfigurableInput> logger;
    private readonly WowProcessInput input;
    private readonly ClassConfiguration classConfig;

    private readonly bool Log;

    public ConfigurableInput(ILogger<ConfigurableInput> logger,
        WowProcessInput input, ClassConfiguration classConfig)
    {
        this.logger = logger;
        this.input = input;
        this.classConfig = classConfig;
        Log = classConfig.Log;

        input.ForwardKey = classConfig.ForwardKey;
        input.BackwardKey = classConfig.BackwardKey;
        input.TurnLeftKey = classConfig.TurnLeftKey;
        input.TurnRightKey = classConfig.TurnRightKey;

        input.InteractMouseover = classConfig.InteractMouseOver.ConsoleKey;
        input.InteractMouseoverPress = classConfig.InteractMouseOver.PressDuration;
    }

    public void Reset() => input.Reset();

    public void StartForward(bool forced)
    {
        input.SetKeyState(ForwardKey, true, forced);
    }

    public void StopForward(bool forced)
    {
        if (input.IsKeyDown(ForwardKey))
            input.SetKeyState(ForwardKey, false, forced);
    }

    public void StartBackward(bool forced)
    {
        input.SetKeyState(BackwardKey, true, forced);
    }

    public void StopBackward(bool forced)
    {
        if (input.IsKeyDown(BackwardKey))
            input.SetKeyState(BackwardKey, false, forced);
    }

    public void TurnRandomDir(int milliseconds)
    {
        input.PressRandom(
            Random.Shared.Next(2) == 0
            ? input.TurnLeftKey
            : input.TurnRightKey, milliseconds);
    }

    public void PressRandom(KeyAction keyAction)
    {
        PressRandom(keyAction, CancellationToken.None);
    }

    public void PressRandom(KeyAction keyAction, CancellationToken token)
    {
        int elapsedMs = input.PressRandom(keyAction.ConsoleKey, keyAction.PressDuration, token);
        keyAction.SetClicked();

        if (Log && keyAction.Log)
        {
            if (keyAction.BaseAction)
                LogBaseActionPressRandom(logger, keyAction.Name, keyAction.ConsoleKey, elapsedMs);
            else
                LogKeyActionPressRandom(logger, keyAction.Name, keyAction.ConsoleKey, elapsedMs);
        }
    }

    public void PressFixed(ConsoleKey key, int milliseconds, CancellationToken token)
    {
        input.PressFixed(key, milliseconds, token);
    }

    public void PressRandom(ConsoleKey key, int milliseconds)
    {
        input.PressRandom(key, milliseconds);
    }

    public bool IsKeyDown(ConsoleKey key) => input.IsKeyDown(key);

    public void PressInteract() => PressRandom(Interact);

    public void PressFastInteract()
    {
        input.PressRandom(Interact.ConsoleKey, InputDuration.FastPress);
        Interact.SetClicked();
    }

    public void PressApproachOnCooldown()
    {
        if (Approach.GetRemainingCooldown() == 0)
        {
            input.PressRandom(Approach.ConsoleKey, InputDuration.FastPress);
            Approach.SetClicked();
        }
    }

    public void PressApproach() => PressRandom(Approach);

    public void PressLastTarget() => PressRandom(TargetLastTarget);

    public void PressFastLastTarget()
    {
        input.PressRandom(TargetLastTarget.ConsoleKey, InputDuration.FastPress);
        TargetLastTarget.SetClicked();
    }

    public void PressStandUp() => PressRandom(StandUp);

    public void PressClearTarget() => PressRandom(ClearTarget);

    public void PressStopAttack() => PressRandom(StopAttack);

    public void PressNearestTarget() => PressRandom(TargetNearestTarget);

    public void PressTargetPet() => PressRandom(TargetPet);

    public void PressTargetOfTarget() => PressRandom(TargetTargetOfTarget);

    public void PressJump() => PressRandom(Jump);

    public void PressPetAttack() => PressRandom(PetAttack);

    public void PressMount() => PressRandom(Mount);

    public void PressDismount()
    {
        input.PressRandom(Mount.ConsoleKey, Mount.PressDuration);
    }

    public void PressTargetFocus() => PressRandom(TargetFocus);

    public void PressFollowTarget() => PressRandom(FollowTarget);

    public void PressESC()
    {
        input.PressRandom(ConsoleKey.Escape, InputDuration.VeryFastPress);
    }

    #region Logging

    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Trace,
        Message = @"[{name}] {key} pressed {milliseconds}ms")]
    static partial void LogBaseActionPressRandom(ILogger logger, string name, ConsoleKey key, int milliseconds);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Debug,
        Message = @"[{name}] {key} pressed {milliseconds}ms")]
    static partial void LogKeyActionPressRandom(ILogger logger, string name, ConsoleKey key, int milliseconds);

    #endregion
}
