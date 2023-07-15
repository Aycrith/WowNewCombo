using SharedLib.NpcFinder;
using System.Threading;
using System;

namespace Core.Goals;

public sealed class TargetFinder
{
    private const int minMs = 400, maxMs = 1000;

    private readonly ConfigurableInput input;
    private readonly ClassConfiguration classConfig;
    private readonly AddonBits bits;
    private readonly NpcNameTargeting npcNameTargeting;
    private readonly Wait wait;

    private DateTime lastActive;

    public int ElapsedMs => (int)(DateTime.UtcNow - lastActive).TotalMilliseconds;

    public TargetFinder(ConfigurableInput input, ClassConfiguration classConfig,
        AddonBits bits, NpcNameTargeting npcNameTargeting, Wait wait)
    {
        this.classConfig = classConfig;
        this.input = input;
        this.bits = bits;
        this.npcNameTargeting = npcNameTargeting;
        this.wait = wait;

        lastActive = DateTime.UtcNow;
    }

    public void Reset()
    {
        npcNameTargeting.ChangeNpcType(NpcNames.None);
        npcNameTargeting.Reset();
    }

    public bool Search(NpcNames target, Func<bool> validTarget, CancellationToken ct)
    {
        return LookForTarget(target, ct) && validTarget();
    }

    private bool LookForTarget(NpcNames target, CancellationToken ct)
    {
        if (ElapsedMs < Random.Shared.Next(minMs, maxMs))
            return bits.Target();

        if (!ct.IsCancellationRequested &&
            classConfig.TargetNearestTarget.GetRemainingCooldown() == 0)
        {
            input.PressNearestTarget();
            wait.Update();
        }

        if (!ct.IsCancellationRequested && !input.KeyboardOnly && !bits.Target())
        {
            npcNameTargeting.ChangeNpcType(target);
            npcNameTargeting.WaitForUpdate();

            if (!ct.IsCancellationRequested &&
                npcNameTargeting.NpcCount > 0 &&
                !input.IsKeyDown(input.TurnLeftKey) &&
                !input.IsKeyDown(input.TurnRightKey))
            {
                npcNameTargeting.AquireNonBlacklisted(ct);
            }
        }

        lastActive = DateTime.UtcNow;

        return bits.Target();
    }

}
