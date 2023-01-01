using Core.GOAP;
using Microsoft.Extensions.Logging;
using System;

namespace Core.Goals;

public sealed class AdhocGoal : GoapGoal
{
    public override float Cost => key.Cost;

    private readonly ILogger logger;
    private readonly ConfigurableInput input;

    private readonly Wait wait;
    private readonly StopMoving stopMoving;
    private readonly AddonReader addonReader;
    private readonly PlayerReader playerReader;

    private readonly KeyAction key;
    private readonly CastingHandler castingHandler;
    private readonly IMountHandler mountHandler;

    private readonly bool? combatMatters;

    public AdhocGoal(KeyAction key, ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, StopMoving stopMoving, CastingHandler castingHandler, IMountHandler mountHandler)
        : base(nameof(AdhocGoal))
    {
        this.logger = logger;
        this.input = input;
        this.wait = wait;
        this.stopMoving = stopMoving;
        this.addonReader = addonReader;
        this.playerReader = addonReader.PlayerReader;
        this.key = key;
        this.castingHandler = castingHandler;
        this.mountHandler = mountHandler;

        if (bool.TryParse(key.InCombat, out bool result))
        {
            AddPrecondition(GoapKey.incombat, result);
            combatMatters = result;
        }

        Keys = new KeyAction[1] { key };
    }

    public override bool CanRun() => key.CanRun();

    public override void OnEnter()
    {
        if (key.BeforeCastDismount && mountHandler.IsMounted())
        {
            mountHandler.Dismount();
            wait.Update();
        }

        castingHandler.UpdateGCD(true);
    }

    public override void Update()
    {
        if (castingHandler.SpellInQueue())
        {
            wait.Update();
            return;
        }

        if ((key.Charge >= 1 && key.CanRun()))
        {
            castingHandler.CastIfReady(key, Interrupt);
            wait.Update();
        }
    }

    private bool Interrupt()
    {
        return combatMatters.HasValue
            ? combatMatters.Value == addonReader.PlayerReader.Bits.PlayerInCombat() && addonReader.DamageTakenCount() > 0
            : addonReader.DamageTakenCount() > 0;
    }
}
