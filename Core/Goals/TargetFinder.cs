using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using System.Threading;
using System;

namespace Core.Goals
{
    public class TargetFinder
    {
        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly ClassConfiguration classConfig;
        private readonly Wait wait;
        private readonly PlayerReader playerReader;

        private readonly IBlacklist blacklist;
        private readonly NpcNameTargeting npcNameTargeting;

        public TargetFinder(ILogger logger, ConfigurableInput input, ClassConfiguration classConfig, Wait wait, PlayerReader playerReader, IBlacklist blacklist, NpcNameTargeting npcNameTargeting)
        {
            this.logger = logger;
            this.classConfig = classConfig;
            this.input = input;
            this.wait = wait;
            this.playerReader = playerReader;

            this.blacklist = blacklist;
            this.npcNameTargeting = npcNameTargeting;
        }

        public bool Search(NpcNames target, Func<bool> validTarget, CancellationTokenSource cts)
        {
            if (LookForTarget(target, cts))
            {
                if (validTarget() && !blacklist.IsTargetBlacklisted())
                {
                    logger.LogInformation("Has target!");
                    return true;
                }
                else
                {
                    if (!cts.IsCancellationRequested)
                    {
                        logger.LogWarning("Target is invalid!");
                        input.ClearTarget();
                        wait.Update();
                    }
                }
            }

            return false;
        }

        private bool LookForTarget(NpcNames target, CancellationTokenSource cts)
        {
            if (!cts.IsCancellationRequested)
            {
                npcNameTargeting.ChangeNpcType(target);
                input.NearestTarget();
                wait.Update();
            }

            if (!cts.IsCancellationRequested && !classConfig.KeyboardOnly && !playerReader.HasTarget)
            {
                npcNameTargeting.ChangeNpcType(target);
                if (!cts.IsCancellationRequested && npcNameTargeting.NpcCount > 0)
                {
                    npcNameTargeting.TargetingAndClickNpc(true, cts);
                    wait.Update();
                }
            }

            return playerReader.HasTarget;
        }
    }
}
