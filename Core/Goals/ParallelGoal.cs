using Core.GOAP;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Extensions;

namespace Core.Goals
{
    public class ParallelGoal : GoapGoal
    {
        public override float CostOfPerformingAction => 3f;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly StopMoving stopMoving;
        private readonly Wait wait;
        private readonly PlayerReader playerReader;

        private readonly CastingHandler castingHandler;
        private readonly MountHandler mountHandler;

        public ParallelGoal(ILogger logger, ConfigurableInput input, Wait wait, PlayerReader playerReader, StopMoving stopMoving, List<KeyAction> keysConfig, CastingHandler castingHandler, MountHandler mountHandler)
        {
            this.logger = logger;
            this.input = input;

            this.stopMoving = stopMoving;
            this.wait = wait;
            this.playerReader = playerReader;

            this.castingHandler = castingHandler;
            this.mountHandler = mountHandler;

            AddPrecondition(GoapKey.incombat, false);

            keysConfig.ForEach(key => Keys.Add(key));
        }

        public override bool CheckIfActionCanRun()
        {
            return Keys.Any(key => key.CanRun());
        }

        public override async void OnEnter()
        {
            if (Keys.Any(k => k.StopBeforeCast))
            {
                stopMoving.Stop();
                wait.Update();

                if (mountHandler.IsMounted())
                {
                    mountHandler.Dismount();
                    wait.Update();
                }
            }

            await AsyncExt.Loop(Keys, (KeyAction key) =>
            {
                var pressed = castingHandler.CastIfReady(key, () => false);
                key.ResetCooldown();
                key.SetClicked();
                return Task.CompletedTask;
            });

            bool wasDrinkingOrEating = playerReader.Buffs.Drink() || playerReader.Buffs.Food();

            DateTime startTime = DateTime.UtcNow;
            while ((playerReader.Buffs.Drink() || playerReader.Buffs.Food() || playerReader.IsCasting()) && !playerReader.Bits.PlayerInCombat())
            {
                wait.Update();

                if (playerReader.Buffs.Drink() && playerReader.Buffs.Food())
                {
                    if (playerReader.ManaPercentage() > 98 && playerReader.HealthPercent() > 98) { break; }
                }
                else if (playerReader.Buffs.Drink())
                {
                    if (playerReader.ManaPercentage() > 98) { break; }
                }
                else if (playerReader.Buffs.Food())
                {
                    if (playerReader.HealthPercent() > 98) { break; }
                }

                if ((DateTime.UtcNow - startTime).TotalSeconds >= 25)
                {
                    logger.LogInformation($"Waited (25s) long enough for {Name}");
                    break;
                }
            }

            if (wasDrinkingOrEating)
            {
                input.StandUp();
            }
        }

        public override void PerformAction()
        {
        }
    }
}