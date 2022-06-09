using Core.GOAP;
using Microsoft.Extensions.Logging;
using System;
using System.Numerics;

#pragma warning disable 162

namespace Core.Goals
{
    public class ApproachTargetGoal : GoapGoal
    {
        private const bool debug = true;
        private const double STUCK_INTERVAL_MS = 400; // cant be lower than Approach.Cooldown

        public override float CostOfPerformingAction => 8f;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly Wait wait;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly CombatUtil combatUtil;
        private readonly IBlacklist blacklist;

        private readonly Random random = new();

        private DateTime approachStart;

        private double nextStuckCheckTime;
        private Vector3 lastPlayerLocation;

        private int initialTargetGuid;
        private float initialMinRange;

        private double ApproachDurationMs => (DateTime.UtcNow - approachStart).TotalMilliseconds;

        private bool HasPickedUpAnAdd =>
            playerReader.Bits.PlayerInCombat &&
            !playerReader.Bits.TargetInCombat;

        public ApproachTargetGoal(ILogger logger, ConfigurableInput input, Wait wait, PlayerReader playerReader, StopMoving stopMoving, CombatUtil combatUtil, IBlacklist blacklist)
        {
            this.logger = logger;
            this.input = input;

            this.wait = wait;
            this.playerReader = playerReader;
            this.stopMoving = stopMoving;
            this.combatUtil = combatUtil;
            this.blacklist = blacklist;

            AddPrecondition(GoapKey.hastarget, true);
            AddPrecondition(GoapKey.targetisalive, true);
            AddPrecondition(GoapKey.incombatrange, false);

            AddEffect(GoapKey.incombatrange, true);
        }

        public override void OnActionEvent(object sender, ActionEventArgs e)
        {
            if (e.Key == GoapKey.resume)
            {
                approachStart = DateTime.UtcNow;
            }
        }

        public override void OnEnter()
        {
            initialTargetGuid = playerReader.TargetGuid;
            initialMinRange = playerReader.MinRange;
            lastPlayerLocation = playerReader.PlayerLocation;

            combatUtil.Update();

            approachStart = DateTime.UtcNow;
            SetNextStuckTimeCheck();
        }

        public override void PerformAction()
        {
            wait.Update();
            combatUtil.Update();

            if (combatUtil.EnteredCombat() && HasPickedUpAnAdd)
            {
                if (debug)
                    Log($"Add on approach! PlayerCombat={playerReader.Bits.PlayerInCombat}, Targets us={playerReader.Bits.TargetOfTargetIsPlayerOrPet}");

                stopMoving.Stop();
                combatUtil.AquiredTarget(5000);

                return;
            }

            if (input.ClassConfig.Approach.GetCooldownRemaining() == 0)
            {
                input.Approach();
            }

            if (!playerReader.Bits.PlayerInCombat)
            {
                NonCombatApproach();
            }

            RandomJump();
        }

        private void NonCombatApproach()
        {
            if (ApproachDurationMs >= nextStuckCheckTime)
            {
                SetNextStuckTimeCheck();

                Vector3 last = lastPlayerLocation;
                lastPlayerLocation = playerReader.PlayerLocation;
                if (!combatUtil.IsPlayerMoving(last))
                {
                    if (playerReader.LastUIErrorMessage == UI_ERROR.ERR_AUTOFOLLOW_TOO_FAR)
                    {
                        playerReader.LastUIErrorMessage = UI_ERROR.NONE;

                        if (debug)
                            Log("Too far, start moving forward!");

                        input.SetKeyState(input.ForwardKey, true);
                        return;
                    }

                    if (debug)
                        Log($"Seems stuck! Clear Target.");

                    input.ClearTarget();
                    input.KeyPress(random.Next(2) == 0 ? input.TurnLeftKey : input.TurnRightKey, 250 + random.Next(250));

                    return;
                }
            }

            if (ApproachDurationMs > 15_000)
            {
                if (debug)
                    Log("Too long time. Clear Target. Turn away.");

                input.ClearTarget();
                input.KeyPress(random.Next(2) == 0 ? input.TurnLeftKey : input.TurnRightKey, 250 + random.Next(250));

                return;
            }

            if (playerReader.TargetGuid == initialTargetGuid)
            {
                int initialTargetMinRange = playerReader.MinRange;
                if (input.ClassConfig.TargetNearestTarget.GetCooldownRemaining() == 0)
                {
                    input.NearestTarget();
                    wait.Update();
                }

                if (playerReader.TargetGuid != initialTargetGuid)
                {
                    if (playerReader.HasTarget && !blacklist.IsTargetBlacklisted()) // blacklist
                    {
                        if (playerReader.MinRange < initialTargetMinRange)
                        {
                            if (debug)
                                Log($"Found a closer target! {playerReader.MinRange} < {initialTargetMinRange}");

                            initialMinRange = playerReader.MinRange;
                        }
                        else
                        {
                            initialTargetGuid = -1;
                            if (debug)
                                Log("Stick to initial target!");

                            input.LastTarget();
                            wait.Update();
                        }
                    }
                    else
                    {
                        if (debug)
                            Log($"Lost the target due blacklist!");
                    }
                }
            }

            if (ApproachDurationMs > 2000 && initialMinRange < playerReader.MinRange)
            {
                if (debug)
                    Log($"Going away from the target! {initialMinRange} < {playerReader.MinRange}");

                input.ClearTarget();
            }
        }

        private void SetNextStuckTimeCheck()
        {
            nextStuckCheckTime = ApproachDurationMs + STUCK_INTERVAL_MS;
        }

        private void RandomJump()
        {
            if (ApproachDurationMs > 2000 &&
                input.ClassConfig.Jump.MillisecondsSinceLastClick > random.Next(5000, 25_000))
            {
                input.Jump();
            }
        }

        private void Log(string text)
        {
            logger.LogDebug(text);
        }
    }
}