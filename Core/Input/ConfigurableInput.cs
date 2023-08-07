using System.Threading;
using System;

using Game;

namespace Core;

public sealed partial class ConfigurableInput
{
    private readonly WowProcessInput proc;
    private readonly ClassConfiguration classConfig;

    public ConfigurableInput(WowProcessInput wowProcessInput, ClassConfiguration classConfig)
    {
        this.proc = wowProcessInput;
        this.classConfig = classConfig;

        wowProcessInput.ForwardKey = classConfig.ForwardKey;
        wowProcessInput.BackwardKey = classConfig.BackwardKey;
        wowProcessInput.TurnLeftKey = classConfig.TurnLeftKey;
        wowProcessInput.TurnRightKey = classConfig.TurnRightKey;

        wowProcessInput.InteractMouseover = classConfig.InteractMouseOver.ConsoleKey;
        wowProcessInput.InteractMouseoverPress = classConfig.InteractMouseOver.PressDuration;
    }

    public void Reset() => proc.Reset();

    public void StartForward(bool forced)
    {
        proc.SetKeyState(ForwardKey, true, forced);
    }

    public void StopForward(bool forced)
    {
        if (proc.IsKeyDown(ForwardKey))
            proc.SetKeyState(ForwardKey, false, forced);
    }

    public void StartBackward(bool forced)
    {
        proc.SetKeyState(BackwardKey, true, forced);
    }

    public void StopBackward(bool forced)
    {
        if (proc.IsKeyDown(BackwardKey))
            proc.SetKeyState(BackwardKey, false, forced);
    }

    public void TurnRandomDir(int milliseconds)
    {
        proc.PressRandom(
            Random.Shared.Next(2) == 0
            ? proc.TurnLeftKey
            : proc.TurnRightKey, milliseconds);
    }

    public void PressRandom(KeyAction keyAction)
    {
        PressRandom(keyAction, CancellationToken.None);
    }

    public void PressRandom(KeyAction keyAction, CancellationToken ct)
    {
        proc.PressRandom(keyAction.ConsoleKey, keyAction.PressDuration, ct);
        keyAction.SetClicked();
    }

    public void PressFixed(ConsoleKey key, int milliseconds, CancellationToken ct)
    {
        proc.PressFixed(key, milliseconds, ct);
    }

    public void PressRandom(ConsoleKey key, int milliseconds)
    {
        proc.PressRandom(key, milliseconds);
    }

    public bool IsKeyDown(ConsoleKey key) => proc.IsKeyDown(key);

    public void PressInteract() => PressRandom(Interact);

    public void PressFastInteract()
    {
        proc.PressRandom(Interact.ConsoleKey, InputDuration.FastPress);
        Interact.SetClicked();
    }

    public void PressApproachOnCooldown()
    {
        if (Approach.GetRemainingCooldown() == 0)
        {
            proc.PressRandom(Approach.ConsoleKey, InputDuration.FastPress);
            Approach.SetClicked();
        }
    }

    public void PressApproach() => PressRandom(Approach);

    public void PressLastTarget() => PressRandom(TargetLastTarget);

    public void PressFastLastTarget()
    {
        proc.PressRandom(TargetLastTarget.ConsoleKey, InputDuration.FastPress);
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
        proc.PressRandom(Mount.ConsoleKey, Mount.PressDuration);
    }

    public void PressTargetFocus() => PressRandom(TargetFocus);

    public void PressFollowTarget() => PressRandom(FollowTarget);
}
