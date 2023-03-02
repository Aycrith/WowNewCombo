using Game;

namespace Core;

public sealed class ConfigurableInput
{
    private const int fastKeyPressMs = 30;

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
        Proc.KeyPress(Proc.ForwardKey, KeyAction.DefaultPressDuration);
    }

    public void Interact()
    {
        Proc.KeyPress(ClassConfig.Interact.ConsoleKey, KeyAction.DefaultPressDuration);
        ClassConfig.Interact.SetClicked();
    }

    public void FastInteract()
    {
        Proc.KeyPress(ClassConfig.Interact.ConsoleKey, fastKeyPressMs);
        ClassConfig.Interact.SetClicked();
    }

    public void ApproachOnCooldown()
    {
        if (ClassConfig.Approach.GetRemainingCooldown() == 0)
        {
            Proc.KeyPress(ClassConfig.Approach.ConsoleKey, fastKeyPressMs);
            ClassConfig.Approach.SetClicked();
        }
    }

    public void Approach()
    {
        Proc.KeyPress(ClassConfig.Approach.ConsoleKey, ClassConfig.Approach.PressDuration);
        ClassConfig.Approach.SetClicked();
    }

    public void LastTarget()
    {
        Proc.KeyPress(ClassConfig.TargetLastTarget.ConsoleKey, KeyAction.DefaultPressDuration);
        ClassConfig.TargetLastTarget.SetClicked();
    }

    public void FastLastTarget()
    {
        Proc.KeyPress(ClassConfig.TargetLastTarget.ConsoleKey, fastKeyPressMs);
        ClassConfig.TargetLastTarget.SetClicked();
    }

    public void StandUp()
    {
        Proc.KeyPress(ClassConfig.StandUp.ConsoleKey, KeyAction.DefaultPressDuration);
        ClassConfig.StandUp.SetClicked();
    }

    public void ClearTarget()
    {
        Proc.KeyPress(ClassConfig.ClearTarget.ConsoleKey, KeyAction.DefaultPressDuration);
        ClassConfig.ClearTarget.SetClicked();
    }

    public void StopAttack()
    {
        Proc.KeyPress(ClassConfig.StopAttack.ConsoleKey, ClassConfig.StopAttack.PressDuration);
        ClassConfig.StopAttack.SetClicked();
    }

    public void NearestTarget()
    {
        Proc.KeyPress(ClassConfig.TargetNearestTarget.ConsoleKey, KeyAction.DefaultPressDuration);
        ClassConfig.TargetNearestTarget.SetClicked();
    }

    public void TargetPet()
    {
        Proc.KeyPress(ClassConfig.TargetPet.ConsoleKey, KeyAction.DefaultPressDuration);
        ClassConfig.TargetPet.SetClicked();
    }

    public void TargetOfTarget()
    {
        Proc.KeyPress(ClassConfig.TargetTargetOfTarget.ConsoleKey, KeyAction.DefaultPressDuration);
        ClassConfig.TargetTargetOfTarget.SetClicked();
    }

    public void Jump()
    {
        Proc.KeyPress(ClassConfig.Jump.ConsoleKey, KeyAction.DefaultPressDuration);
        ClassConfig.Jump.SetClicked();
    }

    public void PetAttack()
    {
        Proc.KeyPress(ClassConfig.PetAttack.ConsoleKey, ClassConfig.PetAttack.PressDuration);
        ClassConfig.PetAttack.SetClicked();
    }

    public void Hearthstone()
    {
        Proc.KeyPress(ClassConfig.Hearthstone.ConsoleKey, KeyAction.DefaultPressDuration);
        ClassConfig.Hearthstone.SetClicked();
    }

    public void Mount()
    {
        Proc.KeyPress(ClassConfig.Mount.ConsoleKey, KeyAction.DefaultPressDuration);
        ClassConfig.Mount.SetClicked();
    }

    public void Dismount()
    {
        Proc.KeyPress(ClassConfig.Mount.ConsoleKey, KeyAction.DefaultPressDuration);
    }

    public void TargetFocus()
    {
        Proc.KeyPress(ClassConfig.TargetFocus.ConsoleKey, KeyAction.DefaultPressDuration);
        ClassConfig.TargetFocus.SetClicked();
    }

    public void FollowTarget()
    {
        Proc.KeyPress(ClassConfig.FollowTarget.ConsoleKey, KeyAction.DefaultPressDuration);
        ClassConfig.FollowTarget.SetClicked();
    }
}
