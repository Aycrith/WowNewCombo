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
        private readonly Wait wait;
        private readonly NpcNameTargeting npcNameTargeting;

        public TargetFinder(ConfigurableInput input, ClassConfiguration classConfig, Wait wait, PlayerReader playerReader, NpcNameTargeting npcNameTargeting)
        {
            this.classConfig = classConfig;
            this.input = input;
            this.wait = wait;
            this.playerReader = playerReader;
            this.npcNameTargeting = npcNameTargeting;
        }

        public void Reset()
        {
            npcNameTargeting.ChangeNpcType(NpcNames.None);
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
                    if (npcNameTargeting.InteractFirst(ct))
                        wait.Update();
                }
            }

            return playerReader.Bits.HasTarget();
        }

    }
}
