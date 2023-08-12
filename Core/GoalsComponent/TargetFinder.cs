using SharedLib.NpcFinder;
using System.Threading;
using System;

namespace Core.Goals;

public sealed class TargetFinder
{
    private const int waitMs = 200;

    private readonly ConfigurableInput input;
    private readonly AddonBits bits;
    private readonly NpcNameTargeting npcNameTargeting;
    private readonly Wait wait;

    private DateTime lastActive;

    public int ElapsedMs =>
        (int)(DateTime.UtcNow - lastActive).TotalMilliseconds;

    public TargetFinder(ConfigurableInput input,
        AddonBits bits, NpcNameTargeting npcNameTargeting, Wait wait)
    {
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

    public bool Search(
        NpcNames target, Func<bool> validTarget, CancellationToken ct)
    {
        return LookForTarget(target, ct) && validTarget();
    }

    private bool LookForTarget(
        NpcNames target, CancellationToken ct)
    {
        if (ElapsedMs < waitMs)
            return bits.Target();

        if (input.TargetNearestTarget.GetRemainingCooldown() == 0)
        {
            lastActive = DateTime.UtcNow;
            input.PressNearestTarget();
            wait.Update();
        }

        if (!ct.IsCancellationRequested &&
            !input.KeyboardOnly && !bits.Target())
        {
            npcNameTargeting.ChangeNpcType(target);
            npcNameTargeting.WaitForUpdate();

            if (ct.IsCancellationRequested)
                return false;

            if (npcNameTargeting.FoundAny() &&
                !input.IsKeyDown(input.TurnLeftKey) &&
                !input.IsKeyDown(input.TurnRightKey))
            {
                lastActive = DateTime.UtcNow;
                return npcNameTargeting.AcquireNonBlacklisted(ct);
            }
        }

        return bits.Target();
    }

}
