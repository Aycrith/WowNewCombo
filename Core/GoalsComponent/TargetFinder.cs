using SharedLib.NpcFinder;
using System.Threading;
using System;

namespace Core.Goals
{
    public class TargetFinder
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
        }

        public bool Search(NpcNames target, Func<bool> validTarget, CancellationTokenSource cts)
        {
            return LookForTarget(target, cts) && validTarget();
        }

        private bool LookForTarget(NpcNames target, CancellationTokenSource cts)
        {
            if (!cts.IsCancellationRequested)
            {
                input.NearestTarget();
            }

            if (!cts.IsCancellationRequested && !classConfig.KeyboardOnly && !playerReader.Bits.HasTarget())
            {
                npcNameTargeting.ChangeNpcType(target);
                if (!cts.IsCancellationRequested && npcNameTargeting.NpcCount > 0)
                {
                    npcNameTargeting.TargetingAndClickNpc(true, cts);
                }
            }

            return playerReader.Bits.HasTarget();
        }

    }
}
