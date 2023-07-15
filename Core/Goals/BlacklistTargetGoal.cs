namespace Core.Goals;

public sealed class BlacklistTargetGoal : GoapGoal
{
    public override float Cost => 2;

    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly ConfigurableInput input;
    private readonly IBlacklist targetBlacklist;

    public BlacklistTargetGoal(PlayerReader playerReader,
        AddonBits bits,
        ConfigurableInput input, IBlacklist blacklist)
        : base(nameof(BlacklistTargetGoal))
    {
        this.playerReader = playerReader;
        this.bits = bits;
        this.input = input;
        this.targetBlacklist = blacklist;
        this.bits = bits;
    }

    public override bool CanRun()
    {
        return bits.Target() && targetBlacklist.Is();
    }

    public override void OnEnter()
    {
        if (playerReader.PetTarget() ||
            playerReader.IsCasting() ||
            bits.Auto_Attack() || bits.AutoShot() ||
            bits.Shoot())
            input.PressStopAttack();

        input.PressClearTarget();
    }
}
