using Core.GOAP;
using Microsoft.Extensions.Logging;
using System;

namespace Core.Goals
{
    public class AdhocGoal : GoapGoal
    {
        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly Wait wait;
        private readonly StopMoving stopMoving;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;
        
        private readonly KeyAction key;
        private readonly CastingHandler castingHandler;
        private readonly MountHandler mountHandler;
        public override float CostOfPerformingAction => key.Cost;

        private readonly Func<bool> dangerCombat;

        public override string Name => Keys.Length == 0 ? base.Name : Keys[0].Name;

        public AdhocGoal(ILogger logger, ConfigurableInput input, Wait wait, KeyAction key, AddonReader addonReader, StopMoving stopMoving, CastingHandler castingHandler, MountHandler mountHandler)
        {
            this.logger = logger;
            this.input = input;
            this.wait = wait;
            this.stopMoving = stopMoving;
            this.addonReader = addonReader;
            this.playerReader = addonReader.PlayerReader;
            this.key = key;
            this.castingHandler = castingHandler;
            this.mountHandler = mountHandler;

            dangerCombat = () => addonReader.PlayerReader.Bits.PlayerInCombat() &&
                addonReader.CombatCreatureCount > 0;

            if (key.InCombat == "false")
            {
                AddPrecondition(GoapKey.incombat, false);
            }
            else if (key.InCombat == "true")
            {
                AddPrecondition(GoapKey.incombat, true);
            }

            Keys = new KeyAction[1] { key };
        }

        public override bool CheckIfActionCanRun() => key.CanRun();

        public override void OnEnter()
        {
            if (mountHandler.IsMounted())
            {
                mountHandler.Dismount();
                wait.Update();
            }

            castingHandler.CastIfReady(key, dangerCombat);

            bool wasDrinkingOrEating = playerReader.Buffs.Drink() || playerReader.Buffs.Food();

            DateTime startTime = DateTime.UtcNow;

            while ((playerReader.Buffs.Drink() || playerReader.Buffs.Food() || playerReader.IsCasting()) && !dangerCombat())
            {
                wait.Update();

                if (playerReader.Buffs.Drink())
                {
                    if (playerReader.ManaPercentage() > 98) { break; }
                }
                else if (playerReader.Buffs.Food() && !key.Requirements.Contains("Well Fed"))
                {
                    if (playerReader.HealthPercent() > 98) { break; }
                }
                else if (!key.CanRun())
                {
                    break;
                }

                if ((DateTime.UtcNow - startTime).TotalSeconds > 25)
                {
                    logger.LogInformation($"Waited (25s) long enough for {key.Name}");
                    break;
                }
            }

            if (wasDrinkingOrEating)
            {
                input.Stop();
            }

            wait.Update();
        }

        public override void PerformAction()
        {
            if (key.CanRun() && key.Charge > 1)
            {
                castingHandler.Cast(key);
            }

            wait.Update();
        }
    }
}
