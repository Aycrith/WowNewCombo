using Core.GOAP;
using Microsoft.Extensions.Logging;
using SharedLib.NpcFinder;
using System;

namespace Core.Goals;

public sealed class PullTargetGoal : GoapGoal, IGoapEventListener
{
    public override float Cost => 7f;

    private const int AcquireTargetTimeMs = 5000;

    private readonly ILogger<PullTargetGoal> logger;
    private readonly ConfigurableInput input;
    private readonly ClassConfiguration classConfig;
    private readonly Wait wait;
    private readonly AddonReader addonReader;
    private readonly PlayerReader playerReader;
    private readonly StopMoving stopMoving;
    private readonly StuckDetector stuckDetector;
    private readonly NpcNameTargeting npcNameTargeting;
    private readonly CastingHandler castingHandler;
    private readonly IMountHandler mountHandler;
    private readonly CombatUtil combatUtil;
    private readonly IBlacklist targetBlacklist;

    private readonly KeyAction? approachKey;
    private readonly Action approachAction;

    private readonly bool requiresNpcNameFinder;

    private DateTime pullStart;

    private double PullDurationMs => (DateTime.UtcNow - pullStart).TotalMilliseconds;

    public PullTargetGoal(ILogger<PullTargetGoal> logger, ConfigurableInput input,
        Wait wait, AddonReader addonReader, IBlacklist blacklist,
        StopMoving stopMoving, CastingHandler castingHandler,
        IMountHandler mountHandler, NpcNameTargeting npcNameTargeting,
        StuckDetector stuckDetector, CombatUtil combatUtil,
        ClassConfiguration classConfig)
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
        this.targetBlacklist = blacklist;
        this.classConfig = classConfig;

        Keys = classConfig.Pull.Sequence;

        approachAction = DefaultApproach;

        for (int i = 0; i < Keys.Length; i++)
        {
            KeyAction keyAction = Keys[i];

            if (keyAction.Name.Equals(input.Approach.Name, StringComparison.OrdinalIgnoreCase))
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

        castingHandler.UpdateGCD();

        if (mountHandler.IsMounted())
        {
            mountHandler.Dismount();
        }

        if (Keys.Length != 0 && input.StopAttack.GetRemainingCooldown() == 0)
        {
            Log("Stop auto interact!");
            input.PressStopAttack();
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
        if (e.GetType() == typeof(ResumeEvent))
        {
            pullStart = DateTime.UtcNow;
        }
    }

    public override void Update()
    {
        wait.Update();

        if (addonReader.DamageDoneCount() > 0)
        {
            SendGoapEvent(new GoapStateEvent(GoapKey.pulled, true));
            return;
        }

        if (PullDurationMs > 15_000)
        {
            input.PressClearTarget();
            Log("Pull taking too long. Clear target and face away!");
            input.TurnRandomDir(1000);
            return;
        }

        if (classConfig.AutoPetAttack &&
            playerReader.Bits.HasPet() && !playerReader.PetHasTarget())
        {
            if (input.PetAttack.GetRemainingCooldown() == 0)
                input.PressPetAttack();
        }

        bool castAny = false;
        bool spellInQueue = false;
        for (int i = 0; i < Keys.Length; i++)
        {
            KeyAction keyAction = Keys[i];

            if (keyAction.Name.Equals(input.Approach.Name, StringComparison.OrdinalIgnoreCase))
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
                input.PressStopAttack();
                input.PressClearTarget();
                wait.Update();
                return;
            }
        }

        if (!castAny && !spellInQueue && !playerReader.IsCasting())
        {
            if (combatUtil.EnteredCombat())
            {
                if (wait.Until(AcquireTargetTimeMs, CombatLogChanged) >= 0)
                {
                    if (addonReader.DamageTakenCount() > 0 && !playerReader.Bits.TargetInCombat())
                    {
                        stopMoving.Stop();

                        input.PressClearTarget();
                        wait.Update();

                        combatUtil.AquiredTarget(AcquireTargetTimeMs);
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
            addonReader.DamageDoneCount() > 0 ||
            addonReader.DamageTakenCount() > 0 ||
            playerReader.TargetTarget is
            UnitsTarget.Me or
            UnitsTarget.Pet or
            UnitsTarget.PartyOrPet;
    }

    private void DefaultApproach()
    {
        if (input.Approach.GetRemainingCooldown() == 0)
        {
            if (!stuckDetector.IsMoving())
            {
                stuckDetector.Update();
            }

            input.PressApproach();
        }
    }

    private void ConditionalApproach()
    {
        if (approachKey != null && (approachKey.CanRun() || approachKey.GetRemainingCooldown() > 0))
        {
            if (approachKey.GetRemainingCooldown() == 0)
            {
                input.PressApproach();
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
            UnitsTarget.Me or
            UnitsTarget.Pet or
            UnitsTarget.PartyOrPet ||
            addonReader.CombatLog.DamageDoneGuid.ElapsedMs() < CastingHandler.GCD ||
            playerReader.IsInMeleeRange();
    }

    private bool PullPrevention()
    {
        return targetBlacklist.Is() &&
            playerReader.TargetTarget is not
            UnitsTarget.None or
            UnitsTarget.Me or
            UnitsTarget.Pet or
            UnitsTarget.PartyOrPet;
    }

    private void Log(string text)
    {
        logger.LogInformation(text);
    }
}