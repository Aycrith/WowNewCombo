using Game;

namespace Core
{
    public class ConfigurableInput
    {
        public readonly WowProcessInput Proc;
        public readonly ClassConfiguration ClassConfig;

        public readonly int defaultKeyPress = 50;
        public readonly int fastKeyPress = 30;

        public ConfigurableInput(WowProcessInput wowProcessInput, ClassConfiguration classConfig)
        {
            this.Proc = wowProcessInput;
            ClassConfig = classConfig;

            wowProcessInput.ForwardKey = classConfig.ForwardKey;
            wowProcessInput.BackwardKey = classConfig.BackwardKey;
            wowProcessInput.TurnLeftKey = classConfig.TurnLeftKey;
            wowProcessInput.TurnRightKey = classConfig.TurnRightKey;
        }

        public void Stop()
        {
            Proc.KeyPress(Proc.ForwardKey, defaultKeyPress);
        }

        public void Interact()
        {
            Proc.KeyPress(ClassConfig.Interact.ConsoleKey, defaultKeyPress);
            ClassConfig.Interact.SetClicked();
        }

        public void FastInteract()
        {
            Proc.KeyPress(ClassConfig.Interact.ConsoleKey, fastKeyPress);
            ClassConfig.Interact.SetClicked();
        }

        public void ApproachOnCooldown()
        {
            if (ClassConfig.Approach.GetCooldownRemaining() == 0)
            {
                Proc.KeyPress(ClassConfig.Approach.ConsoleKey, fastKeyPress);
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
            Proc.KeyPress(ClassConfig.TargetLastTarget.ConsoleKey, defaultKeyPress);
            ClassConfig.TargetLastTarget.SetClicked();
        }

        public void FastLastTarget()
        {
            Proc.KeyPress(ClassConfig.TargetLastTarget.ConsoleKey, fastKeyPress);
            ClassConfig.TargetLastTarget.SetClicked();
        }

        public void StandUp()
        {
            Proc.KeyPress(ClassConfig.StandUp.ConsoleKey, defaultKeyPress);
            ClassConfig.StandUp.SetClicked();
        }

        public void ClearTarget()
        {
            Proc.KeyPress(ClassConfig.ClearTarget.ConsoleKey, defaultKeyPress);
            ClassConfig.ClearTarget.SetClicked();
        }

        public void StopAttack()
        {
            Proc.KeyPress(ClassConfig.StopAttack.ConsoleKey, ClassConfig.StopAttack.PressDuration);
            ClassConfig.StopAttack.SetClicked();
        }

        public void NearestTarget()
        {
            Proc.KeyPress(ClassConfig.TargetNearestTarget.ConsoleKey, defaultKeyPress);
            ClassConfig.TargetNearestTarget.SetClicked();
        }

        public void TargetPet()
        {
            Proc.KeyPress(ClassConfig.TargetPet.ConsoleKey, defaultKeyPress);
            ClassConfig.TargetPet.SetClicked();
        }

        public void TargetOfTarget()
        {
            Proc.KeyPress(ClassConfig.TargetTargetOfTarget.ConsoleKey, defaultKeyPress);
            ClassConfig.TargetTargetOfTarget.SetClicked();
        }

        public void Jump()
        {
            Proc.KeyPress(ClassConfig.Jump.ConsoleKey, defaultKeyPress);
            ClassConfig.Jump.SetClicked();
        }

        public void PetAttack()
        {
            Proc.KeyPress(ClassConfig.PetAttack.ConsoleKey, ClassConfig.PetAttack.PressDuration);
            ClassConfig.PetAttack.SetClicked();
        }

        public void Hearthstone()
        {
            Proc.KeyPress(ClassConfig.Hearthstone.ConsoleKey, defaultKeyPress);
            ClassConfig.Hearthstone.SetClicked();
        }

        public void Mount()
        {
            Proc.KeyPress(ClassConfig.Mount.ConsoleKey, defaultKeyPress);
            ClassConfig.Mount.SetClicked();
        }

        public void Dismount()
        {
            Proc.KeyPress(ClassConfig.Mount.ConsoleKey, defaultKeyPress);
        }

        public void TargetFocus()
        {
            Proc.KeyPress(ClassConfig.TargetFocus.ConsoleKey, defaultKeyPress);
            ClassConfig.TargetFocus.SetClicked();
        }

        public void FollowTarget()
        {
            Proc.KeyPress(ClassConfig.FollowTarget.ConsoleKey, defaultKeyPress);
            ClassConfig.FollowTarget.SetClicked();
        }
    }
}
