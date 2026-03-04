using SharedLib.NpcFinder;

using System;
using System.Threading;

namespace Core.Goals;

public sealed class TargetFinder
{
    private const int waitMs = 200;
    private const int blacklistBackoffMs = 2000;

    private readonly ConfigurableInput input;
    private readonly AddonBits bits;
    private readonly NpcNameTargeting npcNameTargeting;
    private readonly Wait wait;

    private DateTime lastActive;
    private DateTime lastBlacklistedAt;

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
        lastBlacklistedAt = DateTime.MinValue;
    }

    public void Reset()
    {
        npcNameTargeting.ChangeNpcType(NpcNames.None);
        npcNameTargeting.Reset();
        lastBlacklistedAt = DateTime.MinValue;
    }

    public static int BlacklistBackoffMs => blacklistBackoffMs;

    public void NotifyBlacklistedResult()
    {
        lastBlacklistedAt = DateTime.UtcNow;
    }

    public bool Search(
        NpcNames target, Func<bool> validTarget, CancellationToken token)
    {
        return LookForTarget(target, token) && validTarget();
    }

    private bool LookForTarget(
        NpcNames target, CancellationToken token)
    {
        int effectiveWait = (int)(DateTime.UtcNow - lastBlacklistedAt).TotalMilliseconds < blacklistBackoffMs
            ? blacklistBackoffMs
            : waitMs;

        if (ElapsedMs < effectiveWait)
            return bits.Target();

        if (!input.TargetNearestTarget.OnCooldown())
        {
            lastActive = DateTime.UtcNow;
            input.PressNearestTarget(token);
            wait.Update(token);
        }

        if (!token.IsCancellationRequested &&
            !input.KeyboardOnly && !bits.Target())
        {
            npcNameTargeting.ChangeNpcType(target);
            npcNameTargeting.WaitForUpdate(token);

            if (token.IsCancellationRequested)
                return false;

            if (npcNameTargeting.FoundAny() &&
                !input.IsKeyDown(input.TurnLeftKey) &&
                !input.IsKeyDown(input.TurnRightKey))
            {
                lastActive = DateTime.UtcNow;
                return npcNameTargeting.AcquireNonBlacklisted(token);
            }
        }

        return bits.Target();
    }

}
