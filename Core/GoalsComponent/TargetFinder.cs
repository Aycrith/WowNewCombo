using SharedLib.NpcFinder;
using System.Threading;
using System;

namespace Core.Goals;

public sealed class TargetFinder
{
    private const int minMs = 400, maxMs = 1000;

    private readonly ConfigurableInput input;
    private readonly ClassConfiguration classConfig;
    private readonly PlayerReader playerReader;
    private readonly NpcNameTargeting npcNameTargeting;
    private readonly Wait wait;

    private DateTime lastActive;

    public int ElapsedMs => (int)(DateTime.UtcNow - lastActive).TotalMilliseconds;

    public TargetFinder(ConfigurableInput input, ClassConfiguration classConfig,
        PlayerReader playerReader, NpcNameTargeting npcNameTargeting, Wait wait)
    {
        this.classConfig = classConfig;
        this.input = input;
        this.playerReader = playerReader;
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
            return playerReader.Bits.HasTarget();

        if (!ct.IsCancellationRequested &&
            classConfig.TargetNearestTarget.GetRemainingCooldown() == 0)
        {
            input.NearestTarget();
            wait.Update();
        }

        if (!ct.IsCancellationRequested && !classConfig.KeyboardOnly && !playerReader.Bits.HasTarget())
        {
            npcNameTargeting.ChangeNpcType(target);
            npcNameTargeting.WaitForUpdate();

            if (!ct.IsCancellationRequested &&
                npcNameTargeting.NpcCount > 0 &&
                !input.Proc.IsKeyDown(input.Proc.TurnLeftKey) &&
                !input.Proc.IsKeyDown(input.Proc.TurnRightKey))
            {
                npcNameTargeting.AquireNonBlacklisted(ct);
            }
        }

        lastActive = DateTime.UtcNow;

        return playerReader.Bits.HasTarget();
    }

}
