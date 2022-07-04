using Core.GOAP;
using Microsoft.Extensions.Logging;
using System;

namespace Core.Goals
{
    public class AdhocGoal : GoapGoal
    {
        public override float Cost => key.Cost;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;

        private readonly Wait wait;
        private readonly StopMoving stopMoving;
        private readonly AddonReader addonReader;
        private readonly PlayerReader playerReader;

        private readonly KeyAction key;
        private readonly CastingHandler castingHandler;
        private readonly MountHandler mountHandler;

        public AdhocGoal(KeyAction key, ILogger logger, ConfigurableInput input, Wait wait, AddonReader addonReader, StopMoving stopMoving, CastingHandler castingHandler, MountHandler mountHandler)
            : base(nameof(AdhocGoal))
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

            if (bool.TryParse(key.InCombat, out bool result))
            {
                AddPrecondition(GoapKey.incombat, result);
            }

            Keys = new KeyAction[1] { key };
        }

        public override bool CanRun() => key.CanRun();

        public override void OnEnter()
        {
            if (mountHandler.IsMounted())
            {
                mountHandler.Dismount();
                wait.Update();
            }

            castingHandler.CastIfReady(key, DangerCombat);

            bool wasDrinkingOrEating = playerReader.Buffs.Drink() || playerReader.Buffs.Food();

            DateTime startTime = DateTime.UtcNow;

            while ((playerReader.Buffs.Drink() || playerReader.Buffs.Food() || playerReader.IsCasting()) && !DangerCombat())
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

        public override void Update()
        {
            if (key.CanRun() && key.Charge > 1)
            {
                castingHandler.Cast(key);
            }

            wait.Update();
        }

        public bool DangerCombat()
        {
            return addonReader.PlayerReader.Bits.PlayerInCombat() &&
                addonReader.CombatCreatureCount > 0;
        }
    }
}
