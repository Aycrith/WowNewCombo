using Microsoft.Extensions.Logging;
using System;
using Game;

namespace Core
{
    public class ConfigurableInput : WowProcessInput
    {
        public ClassConfiguration ClassConfig { get; }

        public readonly int defaultKeyPress = 50;

        public ConsoleKey ForwardKey { get; }
        public ConsoleKey BackwardKey { get; }
        public ConsoleKey TurnLeftKey { get; }
        public ConsoleKey TurnRightKey { get; }

        public ConfigurableInput(ILogger logger, WowProcess wowProcess, ClassConfiguration classConfig) : base(logger, wowProcess)
        {
            ClassConfig = classConfig;

            ForwardKey = classConfig.ForwardKey;
            BackwardKey = classConfig.BackwardKey;
            TurnLeftKey = classConfig.TurnLeftKey;
            TurnRightKey = classConfig.TurnRightKey;
        }

        public void Stop()
        {
            KeyPress(ForwardKey, defaultKeyPress);
        }

        public void Interact()
        {
            KeyPress(ClassConfig.Interact.ConsoleKey, defaultKeyPress);
            ClassConfig.Interact.SetClicked();
        }

        public void Approach()
        {
            KeyPress(ClassConfig.Approach.ConsoleKey, ClassConfig.Approach.PressDuration);
            ClassConfig.Approach.SetClicked();
        }

        public void LastTarget()
        {
            KeyPress(ClassConfig.TargetLastTarget.ConsoleKey, defaultKeyPress);
            ClassConfig.TargetLastTarget.SetClicked();
        }

        public void StandUp()
        {
            KeyPress(ClassConfig.StandUp.ConsoleKey, defaultKeyPress);
            ClassConfig.StandUp.SetClicked();
        }

        public void ClearTarget()
        {
            KeyPress(ClassConfig.ClearTarget.ConsoleKey, defaultKeyPress);
            ClassConfig.ClearTarget.SetClicked();
        }

        public void StopAttack()
        {
            KeyPress(ClassConfig.StopAttack.ConsoleKey, ClassConfig.StopAttack.PressDuration);
            ClassConfig.StopAttack.SetClicked();
        }

        public void NearestTarget()
        {
            KeyPress(ClassConfig.TargetNearestTarget.ConsoleKey, defaultKeyPress);
            ClassConfig.TargetNearestTarget.SetClicked();
        }

        public void TargetPet()
        {
            KeyPress(ClassConfig.TargetPet.ConsoleKey, defaultKeyPress);
            ClassConfig.TargetPet.SetClicked();
        }

        public void TargetOfTarget()
        {
            KeyPress(ClassConfig.TargetTargetOfTarget.ConsoleKey, defaultKeyPress);
            ClassConfig.TargetTargetOfTarget.SetClicked();
        }

        public void Jump()
        {
            KeyPress(ClassConfig.Jump.ConsoleKey, defaultKeyPress);
            ClassConfig.Jump.SetClicked();
        }

        public void PetAttack()
        {
            KeyPress(ClassConfig.PetAttack.ConsoleKey, ClassConfig.PetAttack.PressDuration);
            ClassConfig.PetAttack.SetClicked();
        }

        public void Hearthstone()
        {
            KeyPress(ClassConfig.Hearthstone.ConsoleKey, defaultKeyPress);
            ClassConfig.Hearthstone.SetClicked();
        }

        public void Mount()
        {
            KeyPress(ClassConfig.Mount.ConsoleKey, defaultKeyPress);
            ClassConfig.Mount.SetClicked();
        }

        public void Dismount()
        {
            KeyPress(ClassConfig.Mount.ConsoleKey, defaultKeyPress);
        }

        public void TargetFocus()
        {
            KeyPress(ClassConfig.TargetFocus.ConsoleKey, defaultKeyPress);
            ClassConfig.TargetFocus.SetClicked();
        }

        public void FollowTarget()
        {
            KeyPress(ClassConfig.FollowTarget.ConsoleKey, defaultKeyPress);
            ClassConfig.FollowTarget.SetClicked();
        }
    }
}
