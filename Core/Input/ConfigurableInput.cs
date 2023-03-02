using Game;

namespace Core;

public sealed class ConfigurableInput
{
    public readonly WowProcessInput Proc;
    public readonly ClassConfiguration ClassConfig;

    public ConfigurableInput(WowProcessInput wowProcessInput, ClassConfiguration classConfig)
    {
        this.Proc = wowProcessInput;
        ClassConfig = classConfig;

        wowProcessInput.ForwardKey = classConfig.ForwardKey;
        wowProcessInput.BackwardKey = classConfig.BackwardKey;
        wowProcessInput.TurnLeftKey = classConfig.TurnLeftKey;
        wowProcessInput.TurnRightKey = classConfig.TurnRightKey;

        wowProcessInput.InteractMouseover = classConfig.InteractMouseOver.ConsoleKey;
        wowProcessInput.InteractMouseoverPress = classConfig.InteractMouseOver.PressDuration;
    }

    public void Stop()
    {
        Proc.KeyPress(Proc.ForwardKey, InputDuration.DefaultPress);
    }

    private void KeyPress(KeyAction keyAction)
    {
        Proc.KeyPress(keyAction.ConsoleKey, keyAction.PressDuration);
        keyAction.SetClicked();
    }

    public void Interact()
    {
        KeyPress(ClassConfig.Interact);
    }

    public void FastInteract()
    {
        Proc.KeyPress(ClassConfig.Interact.ConsoleKey, InputDuration.FastPress);
        ClassConfig.Interact.SetClicked();
    }

    public void ApproachOnCooldown()
    {
        if (ClassConfig.Approach.GetRemainingCooldown() == 0)
        {
            Proc.KeyPress(ClassConfig.Approach.ConsoleKey, InputDuration.FastPress);
            ClassConfig.Approach.SetClicked();
        }
    }

    public void Approach()
    {
        KeyPress(ClassConfig.Approach);
    }

    public void LastTarget()
    {
        KeyPress(ClassConfig.TargetLastTarget);
    }

    public void FastLastTarget()
    {
        Proc.KeyPress(ClassConfig.TargetLastTarget.ConsoleKey, InputDuration.FastPress);
        ClassConfig.TargetLastTarget.SetClicked();
    }

    public void StandUp()
    {
        KeyPress(ClassConfig.StandUp);
    }

    public void ClearTarget()
    {
        KeyPress(ClassConfig.ClearTarget);
    }

    public void StopAttack()
    {
        KeyPress(ClassConfig.StopAttack);
    }

    public void NearestTarget()
    {
        KeyPress(ClassConfig.TargetNearestTarget);
    }

    public void TargetPet()
    {
        KeyPress(ClassConfig.TargetPet);
    }

    public void TargetOfTarget()
    {
        KeyPress(ClassConfig.TargetTargetOfTarget);
    }

    public void Jump()
    {
        KeyPress(ClassConfig.Jump);
    }

    public void PetAttack()
    {
        KeyPress(ClassConfig.PetAttack);
    }

    public void Hearthstone()
    {
        KeyPress(ClassConfig.Hearthstone);
    }

    public void Mount()
    {
        KeyPress(ClassConfig.Mount);
    }

    public void Dismount()
    {
        Proc.KeyPress(ClassConfig.Mount.ConsoleKey, ClassConfig.Mount.PressDuration);
    }

    public void TargetFocus()
    {
        KeyPress(ClassConfig.TargetFocus);
    }

    public void FollowTarget()
    {
        KeyPress(ClassConfig.FollowTarget);
    }
}
