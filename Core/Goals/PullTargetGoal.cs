using Core.GOAP;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Goals
{
    public class PullTargetGoal : GoapGoal
    {
        public override float CostOfPerformingAction => 7f;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly StuckDetector stuckDetector;

        private readonly CastingHandler castingHandler;
        private readonly MountHandler mountHandler;
        private readonly CombatUtil combatUtil;

        private readonly Random random = new();

        private readonly KeyAction? approachKey;
        private readonly Action approachAction;

        private readonly Func<bool> pullPrevention;

        private readonly bool requiresNpcNameFinder;

        private DateTime pullStart;

        private double PullDurationMs => (DateTime.UtcNow - pullStart).TotalMilliseconds;

        public PullTargetGoal(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, IBlacklist blacklist, StopMoving stopMoving, CastingHandler castingHandler, MountHandler mountHandler, StuckDetector stuckDetector, CombatUtil combatUtil)
        {
            this.logger = logger;
            this.input = input;
            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.stopMoving = stopMoving;
            this.castingHandler = castingHandler;
            this.mountHandler = mountHandler;

            this.stuckDetector = stuckDetector;
            this.combatUtil = combatUtil;

            approachKey = input.ClassConfig.Pull.Sequence.Find(x => x.Name == input.ClassConfig.Approach.Name);
            approachAction = approachKey == null ? DefaultApproach : ConditionalApproach;

            pullPrevention = () => blacklist.IsTargetBlacklisted() &&
                playerReader.TargetTarget is not
                TargetTargetEnum.None or
                TargetTargetEnum.Me or
                TargetTargetEnum.Pet;

            input.ClassConfig.Pull.Sequence.Where(k => k != null).ToList().ForEach(key => Keys.Add(key));

            requiresNpcNameFinder = Keys.Exists(k => k.Requirements.Contains(RequirementFactory.AddVisible));

            AddPrecondition(GoapKey.targetisalive, true);
            AddPrecondition(GoapKey.incombat, false);
            AddPrecondition(GoapKey.hastarget, true);
            AddPrecondition(GoapKey.pulled, false);
            AddPrecondition(GoapKey.withinpullrange, true);

            AddEffect(GoapKey.pulled, true);
        }

        public override ValueTask OnEnter()
        {
            combatUtil.Update();

            if (mountHandler.IsMounted())
            {
                mountHandler.Dismount();
            }

            if (Keys.Count != 0 && input.ClassConfig.StopAttack.GetCooldownRemaining() == 0)
            {
                Log("Stop auto interact!");
                input.StopAttack();
                wait.Update();
            }

            if (requiresNpcNameFinder)
            {
                SendActionEvent(new ActionEventArgs(GoapKey.wowscreen, true));
            }

            pullStart = DateTime.UtcNow;

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnExit()
        {
            if (requiresNpcNameFinder)
            {
                SendActionEvent(new ActionEventArgs(GoapKey.wowscreen, false));
            }
            return base.OnExit();
        }

        public override void OnActionEvent(object sender, ActionEventArgs e)
        {
            if (e.Key == GoapKey.resume)
            {
                pullStart = DateTime.UtcNow;
            }
        }

        public override ValueTask PerformAction()
        {
            combatUtil.Update();
            wait.Update();

            if (PullDurationMs > 15_000)
            {
                input.ClearTarget();
                Log("Pull taking too long. Clear target and face away!");
                input.KeyPress(random.Next(2) == 0 ? input.TurnLeftKey : input.TurnRightKey, 1000);

                return ValueTask.CompletedTask;
            }

            if (!Pull())
            {
                if (HasPickedUpAnAdd)
                {
                    Log($"Add on pull! Combat={playerReader.Bits.PlayerInCombat}, Targets me={playerReader.Bits.TargetOfTargetIsPlayerOrPet}");

                    stopMoving.Stop();
                    if (combatUtil.AquiredTarget())
                    {
                        pullStart = DateTime.UtcNow;
                    }
                    stopMoving.Stop();
                    return ValueTask.CompletedTask;
                }

                approachAction();
            }
            else
            {
                SendActionEvent(new ActionEventArgs(GoapKey.pulled, true));
            }

            return ValueTask.CompletedTask;
        }

        protected bool HasPickedUpAnAdd =>
            playerReader.Bits.PlayerInCombat &&
            !playerReader.Bits.TargetOfTargetIsPlayerOrPet;

        protected void WaitForWithinMeleeRange(KeyAction item, bool lastCastSuccess)
        {
            stopMoving.Stop();
            wait.Update();

            var start = DateTime.UtcNow;
            var lastKnownHealth = playerReader.HealthCurrent;
            double maxWaitTimeMs = 10_000;

            Log($"Waiting for the target to reach melee range - max {maxWaitTimeMs}ms");

            while (playerReader.HasTarget && !playerReader.IsInMeleeRange && (DateTime.UtcNow - start).TotalMilliseconds < maxWaitTimeMs)
            {
                if (playerReader.HealthCurrent < lastKnownHealth)
                {
                    Log("Got damage. Stop waiting for melee range.");
                    break;
                }

                if (playerReader.IsTargetCasting)
                {
                    Log("Target started casting. Stop waiting for melee range.");
                    break;
                }

                if (lastCastSuccess && addonReader.UsableAction.Is(item))
                {
                    Log($"While waiting, repeat current action: {item.Name}");
                    lastCastSuccess = castingHandler.CastIfReady(item);
                    Log($"Repeat current action: {lastCastSuccess}");
                }

                wait.Update();
            }
        }

        public bool Pull()
        {
            if (playerReader.Bits.HasPet && !playerReader.PetHasTarget)
            {
                if (input.ClassConfig.PetAttack.GetCooldownRemaining() == 0)
                    input.PetAttack();
            }

            bool castAny = false;
            foreach (var item in Keys)
            {
                if (!castingHandler.CanRun(item))
                {
                    continue;
                }

                bool success = castingHandler.Cast(item, pullPrevention);
                if (success)
                {
                    if (!playerReader.HasTarget)
                    {
                        return false;
                    }

                    castAny = true;

                    if (item.WaitForWithinMeleeRange)
                    {
                        WaitForWithinMeleeRange(item, success);
                    }
                }
                else if (pullPrevention() &&
                    (playerReader.IsCasting ||
                     playerReader.Bits.IsAutoRepeatSpellOn_AutoAttack ||
                     playerReader.Bits.IsAutoRepeatSpellOn_AutoShot ||
                     playerReader.Bits.IsAutoRepeatSpellOn_Shoot))
                {
                    Log("Preventing pulling possible tagged target!");
                    input.StopAttack();
                    input.ClearTarget();
                    wait.Update();
                    return false;
                }
            }

            if (castAny)
            {
                (bool timeout, double elapsedMs) = wait.Until(1000,
                    () => playerReader.TargetTarget is
                        TargetTargetEnum.Me or
                        TargetTargetEnum.Pet or
                        TargetTargetEnum.PartyOrPet);
                if (!timeout)
                {
                    Log($"Entered combat after {elapsedMs}ms");
                }
            }

            return playerReader.Bits.PlayerInCombat;
        }

        private void DefaultApproach()
        {
            if (!stuckDetector.IsMoving())
            {
                stuckDetector.Update();
            }

            if (input.ClassConfig.Approach.GetCooldownRemaining() == 0)
            {
                input.Approach();
            }
        }

        private void ConditionalApproach()
        {
            if (approachKey != null && approachKey.CanRun())
            {
                if (approachKey.GetCooldownRemaining() == 0)
                {
                    input.Approach();
                }

                if (!stuckDetector.IsMoving())
                {
                    stuckDetector.Update();
                }
            }
            else
            {
                stopMoving.Stop();
                stopMoving.Stop();
            }
        }

        private void Log(string text)
        {
            logger.LogInformation(text);
        }
    }
}