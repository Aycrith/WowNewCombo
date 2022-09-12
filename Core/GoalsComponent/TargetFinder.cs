using SharedLib.NpcFinder;
using System.Threading;
using System;

namespace Core.Goals
{
    public sealed class TargetFinder
    {
        private readonly ConfigurableInput input;
        private readonly ClassConfiguration classConfig;
        private readonly PlayerReader playerReader;
        private readonly NpcNameTargeting npcNameTargeting;

        public TargetFinder(ConfigurableInput input, ClassConfiguration classConfig, PlayerReader playerReader, NpcNameTargeting npcNameTargeting)
        {
            this.classConfig = classConfig;
            this.input = input;
            this.playerReader = playerReader;
            this.npcNameTargeting = npcNameTargeting;
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
            if (!ct.IsCancellationRequested)
            {
                input.NearestTarget();
            }

            if (!ct.IsCancellationRequested && !classConfig.KeyboardOnly && !playerReader.Bits.HasTarget())
            {
                npcNameTargeting.ChangeNpcType(target);
                if (!ct.IsCancellationRequested && npcNameTargeting.NpcCount > 0)
                {
                    npcNameTargeting.AquireNonBlacklisted(ct);
                }
            }

            return playerReader.Bits.HasTarget();
        }

    }
}
