using Core.GOAP;
using Microsoft.Extensions.Logging;
using SharedLib.NpcFinder;
using System;

namespace Core.Goals
{
    public class PullTargetGoal : GoapGoal, IGoapEventListener
    {
        public override float Cost => 7f;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly Wait wait;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly StuckDetector stuckDetector;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly CastingHandler castingHandler;
        private readonly MountHandler mountHandler;
        private readonly CombatUtil combatUtil;
        private readonly IBlacklist blacklist;

        private readonly Random random;

        private readonly KeyAction? approachKey;
        private readonly Action approachAction;

        private readonly bool requiresNpcNameFinder;

        private DateTime pullStart;

        private double PullDurationMs => (DateTime.UtcNow - pullStart).TotalMilliseconds;

        public PullTargetGoal(ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, IBlacklist blacklist, StopMoving stopMoving, CastingHandler castingHandler, MountHandler mountHandler, NpcNameTargeting npcNameTargeting, StuckDetector stuckDetector, CombatUtil combatUtil)
            : base(nameof(PullTargetGoal))
        {
            this.logger = logger;
            this.input = input;
            this.wait = wait;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.stopMoving = stopMoving;
            this.castingHandler = castingHandler;
            this.mountHandler = mountHandler;
            this.npcNameTargeting = npcNameTargeting;
            this.stuckDetector = stuckDetector;
            this.combatUtil = combatUtil;
            this.blacklist = blacklist;

            random = new();

            Keys = input.ClassConfig.Pull.Sequence;

            approachAction = DefaultApproach;

            for (int i = 0; i < Keys.Length; i++)
            {
                KeyAction keyAction = Keys[i];

                if (keyAction.Name.Equals(input.ClassConfig.Approach.Name, StringComparison.OrdinalIgnoreCase))
                {
                    approachAction = ConditionalApproach;
                    approachKey = keyAction;
                }

                if (keyAction.Requirements.Contains(RequirementFactory.AddVisible))
                {
                    requiresNpcNameFinder = true;
                }
            }

            AddPrecondition(GoapKey.incombat, false);
            AddPrecondition(GoapKey.hastarget, true);
            AddPrecondition(GoapKey.targetisalive, true);
            AddPrecondition(GoapKey.targethostile, true);
            AddPrecondition(GoapKey.pulled, false);
            AddPrecondition(GoapKey.withinpullrange, true);

            AddEffect(GoapKey.pulled, true);
        }

        public override void OnEnter()
        {
            combatUtil.Update();
            wait.Update();
            stuckDetector.Reset();

            castingHandler.UpdateGCD(true);

            if (mountHandler.IsMounted())
            {
                mountHandler.Dismount();
            }

            if (Keys.Length != 0 && input.ClassConfig.StopAttack.GetCooldownRemaining() == 0)
            {
                Log("Stop auto interact!");
                input.StopAttack();
                wait.Update();
            }

            if (requiresNpcNameFinder)
            {
                npcNameTargeting.ChangeNpcType(NpcNames.Enemy);
            }

            pullStart = DateTime.UtcNow;
        }

        public override void OnExit()
        {
            if (requiresNpcNameFinder)
            {
                npcNameTargeting.ChangeNpcType(NpcNames.None);
            }
        }

        public void OnGoapEvent(GoapEventArgs e)
        {
            if (e is ResumeEvent)
            {
                pullStart = DateTime.UtcNow;
            }
        }

        public override void Update()
        {
            wait.Update();

            if (addonReader.DamageDoneCount > 0)
            {
                SendGoapEvent(new GoapStateEvent(GoapKey.pulled, true));
                return;
            }

            if (PullDurationMs > 15_000)
            {
                input.ClearTarget();
                Log("Pull taking too long. Clear target and face away!");
                input.Proc.KeyPress(random.Next(2) == 0 ? input.Proc.TurnLeftKey : input.Proc.TurnRightKey, 1000);
                return;
            }

            if (playerReader.Bits.HasPet() && !playerReader.PetHasTarget)
            {
                if (input.ClassConfig.PetAttack.GetCooldownRemaining() == 0)
                    input.PetAttack();
            }

            bool castAny = false;
            bool spellInQueue = false;
            for (int i = 0; i < Keys.Length; i++)
            {
                KeyAction keyAction = Keys[i];

                if (keyAction.Name.Equals(input.ClassConfig.Approach.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!keyAction.CanRun())
                {
                    continue;
                }

                spellInQueue = castingHandler.SpellInQueue();
                if (spellInQueue)
                {
                    break;
                }

                if (castAny = castingHandler.Cast(keyAction, PullPrevention))
                {

                }
                else if (PullPrevention() &&
                    (playerReader.IsCasting() ||
                     playerReader.Bits.SpellOn_AutoAttack() ||
                     playerReader.Bits.SpellOn_AutoShot() ||
                     playerReader.Bits.SpellOn_Shoot()))
                {
                    Log("Preventing pulling possible tagged target!");
                    input.StopAttack();
                    input.ClearTarget();
                    wait.Update();
                    return;
                }
            }

            if (!castAny && !spellInQueue && !playerReader.IsCasting())
            {
                if (combatUtil.EnteredCombat())
                {
                    (bool t, double e) = wait.Until(5000, CombatLogChanged);
                    if (!t)
                    {
                        if (addonReader.DamageTakenCount > 0 && !playerReader.Bits.TargetInCombat())
                        {
                            stopMoving.Stop();

                            input.ClearTarget();
                            wait.Update();

                            combatUtil.AquiredTarget(5000);
                            return;
                        }

                        SendGoapEvent(new GoapStateEvent(GoapKey.pulled, true));
                        return;
                    }
                }
                else if (playerReader.Bits.PlayerInCombat())
                {
                    SendGoapEvent(new GoapStateEvent(GoapKey.pulled, true));
                    return;
                }

                approachAction();
            }
        }

        private bool CombatLogChanged()
        {
            return
                playerReader.Bits.TargetInCombat() ||
                addonReader.DamageDoneCount > 0 ||
                addonReader.DamageTakenCount > 0 ||
                playerReader.TargetTarget is
                TargetTargetEnum.Me or
                TargetTargetEnum.Pet or
                TargetTargetEnum.PartyOrPet;
        }

        private void DefaultApproach()
        {
            if (input.ClassConfig.Approach.GetCooldownRemaining() == 0)
            {
                if (!stuckDetector.IsMoving())
                {
                    stuckDetector.Update();
                }

                input.Approach();
            }
        }

        private void ConditionalApproach()
        {
            if (approachKey != null && (approachKey.CanRun() || approachKey.GetCooldownRemaining() > 0))
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
            }
        }

        private bool SuccessfulPull()
        {
            return playerReader.TargetTarget is
                        TargetTargetEnum.Me or
                        TargetTargetEnum.Pet or
                        TargetTargetEnum.PartyOrPet ||
                        addonReader.CombatLog.DamageDoneGuid.ElapsedMs() < CastingHandler.GCD ||
                        playerReader.IsInMeleeRange();
        }

        private bool PullPrevention()
        {
            return blacklist.IsTargetBlacklisted() &&
                playerReader.TargetTarget is not
                TargetTargetEnum.None or
                TargetTargetEnum.Me or
                TargetTargetEnum.Pet or
                TargetTargetEnum.PartyOrPet;
        }

        private void Log(string text)
        {
            logger.LogInformation(text);
        }
    }
}