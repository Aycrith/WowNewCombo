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
        private readonly IBlacklist blacklist;
        private readonly StopMoving stopMoving;
        private readonly StuckDetector stuckDetector;
        private readonly ClassConfiguration classConfiguration;

        private readonly CastingHandler castingHandler;
        private readonly MountHandler mountHandler;

        private readonly Random random = new();

        private DateTime pullStart;

        private int SecondsSincePullStarted => (int)(DateTime.UtcNow - pullStart).TotalSeconds;

        private readonly Func<bool> pullPrevention;

        private bool? requiresNpcNameFinder;

        public PullTargetGoal(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, IBlacklist blacklist, StopMoving stopMoving, CastingHandler castingHandler, MountHandler mountHandler, StuckDetector stuckDetector, ClassConfiguration classConfiguration)
        {
            this.logger = logger;
            this.input = input;
            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.blacklist = blacklist;
            this.stopMoving = stopMoving;
            this.castingHandler = castingHandler;
            this.mountHandler = mountHandler;

            this.stuckDetector = stuckDetector;
            this.classConfiguration = classConfiguration;

            pullPrevention = () => blacklist.IsTargetBlacklisted() &&
                playerReader.TargetTarget is not
                TargetTargetEnum.None or
                TargetTargetEnum.Me or
                TargetTargetEnum.Pet;

            classConfiguration.Pull.Sequence.Where(k => k != null).ToList().ForEach(key => Keys.Add(key));

            AddPrecondition(GoapKey.targetisalive, true);
            AddPrecondition(GoapKey.incombat, false);
            AddPrecondition(GoapKey.hastarget, true);
            AddPrecondition(GoapKey.pulled, false);
            AddPrecondition(GoapKey.withinpullrange, true);

            AddEffect(GoapKey.pulled, true);
        }

        public override ValueTask OnEnter()
        {
            if (mountHandler.IsMounted())
            {
                mountHandler.Dismount();
            }

            if (Keys.Count != 0)
            {
                if (requiresNpcNameFinder == null)
                {
                    requiresNpcNameFinder = false;
                    Keys.ForEach(key =>
                    {
                        if (key.Requirements.Contains(RequirementFactory.AddVisible))
                        {
                            requiresNpcNameFinder = true;
                        }
                    });
                }

                if (input.ClassConfig.StopAttack.GetCooldownRemaining() == 0)
                {
                    Log("Stop auto interact!");
                    input.StopAttack();
                    wait.Update();
                }
            }

            if (requiresNpcNameFinder == true)
            {
                SendActionEvent(new ActionEventArgs(GoapKey.wowscreen, true));
            }

            pullStart = DateTime.UtcNow;

            return ValueTask.CompletedTask;
        }

        public override ValueTask OnExit()
        {
            if (requiresNpcNameFinder == true)
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
            if (SecondsSincePullStarted > 10)
            {
                input.ClearTarget();
                Log("Too much time to pull!");
                input.KeyPress(random.Next(2) == 0 ? input.TurnLeftKey : input.TurnRightKey, 1000);
                pullStart = DateTime.UtcNow;

                return ValueTask.CompletedTask;
            }

            SendActionEvent(new ActionEventArgs(GoapKey.fighting, true));

            if (!Pull())
            {
                if (HasPickedUpAnAdd)
                {
                    Log($"Combat={playerReader.Bits.PlayerInCombat}, targeting me={playerReader.Bits.TargetOfTargetIsPlayer}");
                    Log($"Add on approach");

                    stopMoving.Stop();

                    input.NearestTarget();
                    wait.Update();

                    if (playerReader.HasTarget && playerReader.Bits.TargetInCombat &&
                        playerReader.TargetTarget == TargetTargetEnum.Me)
                    {
                        return ValueTask.CompletedTask;
                    }

                    input.ClearTarget();
                    wait.Update();
                    pullStart = DateTime.UtcNow;

                    return ValueTask.CompletedTask;
                }

                if (!stuckDetector.IsMoving())
                {
                    stuckDetector.Unstick();
                }

                if (playerReader.HasTarget && classConfiguration.Approach.GetCooldownRemaining() == 0)
                {
                    input.Approach();
                }
            }
            else
            {
                SendActionEvent(new ActionEventArgs(GoapKey.pulled, true));
            }

            wait.Update();

            return ValueTask.CompletedTask;
        }

        protected bool HasPickedUpAnAdd
        {
            get
            {
                return this.playerReader.Bits.PlayerInCombat && !this.playerReader.Bits.TargetOfTargetIsPlayer && this.playerReader.HealthPercent > 98;
            }
        }

        protected void WaitForWithinMeleeRange(KeyAction item, bool lastCastSuccess)
        {
            stopMoving.Stop();
            wait.Update();

            var start = DateTime.UtcNow;
            var lastKnownHealth = playerReader.HealthCurrent;
            int maxWaitTime = 10;

            Log($"Waiting for the target to reach melee range - max {maxWaitTime}s");

            while (playerReader.HasTarget && !playerReader.IsInMeleeRange && (DateTime.UtcNow - start).TotalSeconds < maxWaitTime)
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

                var success = castingHandler.Cast(item, pullPrevention);
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
                        TargetTargetEnum.Pet);
                if (!timeout)
                {
                    Log($"Entered combat after {elapsedMs}ms");
                }
            }

            return playerReader.Bits.PlayerInCombat;
        }

        private void Log(string s)
        {
            logger.LogInformation($"{nameof(PullTargetGoal)}: {s}");
        }
    }
}