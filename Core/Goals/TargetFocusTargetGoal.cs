using Core.GOAP;

namespace Core.Goals;

public sealed class TargetFocusTargetGoal : GoapGoal
{
    public override float Cost => 10f;

    private readonly ConfigurableInput input;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly Wait wait;

    public TargetFocusTargetGoal(ConfigurableInput input, PlayerReader playerReader,
        AddonBits bits, ClassConfiguration classConfig, Wait wait)
        : base(nameof(TargetFocusTargetGoal))
    {
        this.input = input;
        this.playerReader = playerReader;
        this.bits = bits;
        this.wait = wait;

        if (classConfig.Loot)
        {
            AddPrecondition(GoapKey.incombat, false);
        }

        AddPrecondition(GoapKey.hasfocus, true);
        AddPrecondition(GoapKey.focushastarget, true);
    }

    public override void OnEnter()
    {
        input.PressTargetFocus();
        wait.Update();
    }

    public override void Update()
    {
        if (bits.FocusTargetCanBeHostile())
        {
            if (bits.FocusTargetInCombat())
            {
                input.PressTargetFocus();
                input.PressTargetOfTarget();
                wait.Update();
            }
        }
        else if (playerReader.SpellInRange.FocusTarget_Trade)
        {
            input.PressTargetFocus();
            input.PressTargetOfTarget();
            input.PressInteract();
            wait.Update();
        }

        wait.Update();
    }

    public override void OnExit()
    {
        if (!bits.FocusHasTarget())
        {
            input.PressClearTarget();
            wait.Update();
        }
    }
}
