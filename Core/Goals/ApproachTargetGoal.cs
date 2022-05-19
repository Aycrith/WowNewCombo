using Core.GOAP;
using Microsoft.Extensions.Logging;
using SharedLib.Extensions;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Core.Goals
{
    public class ApproachTargetGoal : GoapGoal
    {
        public override float CostOfPerformingAction { get => 8f; }

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly Wait wait;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly MountHandler mountHandler;
        private readonly CombatUtil combatUtil;

        private const bool debug = true;

        private const float minDistance = 0.01f;

        private readonly Random random = new();

        private DateTime approachStart;

        private float distance;
        private Vector3 lastPlayerLocation;

        private int initialTargetGuid;
        private float initialMinRange;

        private int SecondsSinceApproachStarted => (int)(DateTime.UtcNow - approachStart).TotalSeconds;

        private bool HasPickedUpAnAdd =>
            playerReader.Bits.PlayerInCombat &&
            !playerReader.Bits.TargetOfTargetIsPlayerOrPet;

        public ApproachTargetGoal(ILogger logger, ConfigurableInput input, Wait wait, PlayerReader playerReader, StopMoving stopMoving, MountHandler mountHandler, CombatUtil combatUtil)
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.playerReader = playerReader;
            this.stopMoving = stopMoving;
            this.mountHandler = mountHandler;
            this.combatUtil = combatUtil;

            distance = 0;
            lastPlayerLocation = playerReader.PlayerLocation;

            initialTargetGuid = playerReader.TargetGuid;
            initialMinRange = 0;

            AddPrecondition(GoapKey.hastarget, true);
            AddPrecondition(GoapKey.targetisalive, true);
            AddPrecondition(GoapKey.incombatrange, false);

            AddEffect(GoapKey.incombatrange, true);
        }

        public override ValueTask OnEnter()
        {
            combatUtil.Update();

            initialTargetGuid = playerReader.TargetGuid;
            initialMinRange = playerReader.MinRange;

            lastPlayerLocation = playerReader.PlayerLocation;

            approachStart = DateTime.UtcNow;

            return ValueTask.CompletedTask;
        }

        public override ValueTask PerformAction()
        {
            combatUtil.Update();

            if (HasPickedUpAnAdd)
            {
                logger.LogWarning($"Add on approach! Combat={playerReader.Bits.PlayerInCombat}, Targets me={playerReader.Bits.TargetOfTargetIsPlayerOrPet}");

                stopMoving.Stop();
                stopMoving.Stop();
                combatUtil.AquiredTarget();

                return ValueTask.CompletedTask;
            }

            if (input.ClassConfig.Approach.GetCooldownRemaining() == 0)
            {
                input.Approach();
            }

            distance = playerReader.PlayerLocation.DistanceXYTo(lastPlayerLocation);

            if (distance < minDistance && playerReader.LastUIErrorMessage == UI_ERROR.ERR_AUTOFOLLOW_TOO_FAR)
            {
                playerReader.LastUIErrorMessage = UI_ERROR.NONE;

                Log("Too far, start moving forward!");
                input.SetKeyState(input.ForwardKey, true);
            }

            if (SecondsSinceApproachStarted > 1 && distance < minDistance && !playerReader.Bits.PlayerInCombat)
            {
                input.ClearTarget();
                Log($"Seems stuck! Clear Target. Turn away. d: {distance}");
                input.KeyPress(random.Next(2) == 0 ? input.TurnLeftKey : input.TurnRightKey, 300 + random.Next(800));

                approachStart = DateTime.UtcNow;
            }

            if (SecondsSinceApproachStarted > 15 && !playerReader.Bits.PlayerInCombat)
            {
                input.ClearTarget();
                Log("Too long time. Clear Target. Turn away.");
                input.KeyPress(random.Next(2) == 0 ? input.TurnLeftKey : input.TurnRightKey, 300 + random.Next(800));

                approachStart = DateTime.UtcNow;
            }

            if (playerReader.TargetGuid == initialTargetGuid)
            {
                var initialTargetMinRange = playerReader.MinRange;
                if (!playerReader.Bits.PlayerInCombat)
                {
                    if (input.ClassConfig.TargetNearestTarget.GetCooldownRemaining() == 0)
                    {
                        Log("Try to find closer target...");
                        input.NearestTarget();
                        wait.Update();
                    }
                }

                if (playerReader.TargetGuid != initialTargetGuid)
                {
                    if (playerReader.HasTarget) // blacklist
                    {
                        if (playerReader.MinRange < initialTargetMinRange)
                        {
                            Log($"Found a closer target! {playerReader.MinRange} < {initialTargetMinRange}");
                            initialMinRange = playerReader.MinRange;
                        }
                        else
                        {
                            initialTargetGuid = -1;
                            Log("Stick to initial target!");
                            input.LastTarget();
                        }
                    }
                    else
                    {
                        Log($"Lost the target due blacklist!");
                    }
                }
            }

            if (initialMinRange < playerReader.MinRange && !playerReader.Bits.PlayerInCombat)
            {
                Log($"We are going away from the target! {initialMinRange} < {playerReader.MinRange}");
                input.ClearTarget();

                approachStart = DateTime.UtcNow;
            }

            RandomJump();

            lastPlayerLocation = playerReader.PlayerLocation;

            // due limited precision
            wait.Update();
            wait.Update();
            return ValueTask.CompletedTask;
        }

        public override void OnActionEvent(object sender, ActionEventArgs e)
        {
            if (e.Key == GoapKey.resume)
            {
                approachStart = DateTime.UtcNow;
            }
        }

        private void RandomJump()
        {
            if ((DateTime.UtcNow - approachStart).TotalSeconds > 2 && input.ClassConfig.Jump.MillisecondsSinceLastClick > random.Next(5000, 25_000))
            {
                input.Jump();
            }
        }

        private void Log(string text)
        {
            if (debug)
            {
                logger.LogInformation($"{nameof(ApproachTargetGoal)}: {text}");
            }
        }

    }
}