using Core.GOAP;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Core.Goals
{
    public class ParallelGoal : GoapGoal
    {
        public override float Cost => 3f;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly StopMoving stopMoving;
        private readonly Wait wait;
        private readonly PlayerReader playerReader;
        private readonly CastingHandler castingHandler;
        private readonly MountHandler mountHandler;

        private static bool None() => false;

        private bool castSuccess;

        public ParallelGoal(ILogger logger, ConfigurableInput input, Wait wait, PlayerReader playerReader, StopMoving stopMoving, ClassConfiguration classConfig, CastingHandler castingHandler, MountHandler mountHandler)
            : base(nameof(ParallelGoal))
        {
            this.logger = logger;
            this.input = input;
            this.stopMoving = stopMoving;
            this.wait = wait;
            this.playerReader = playerReader;
            this.castingHandler = castingHandler;
            this.mountHandler = mountHandler;

            AddPrecondition(GoapKey.incombat, false);

            Keys = classConfig.Parallel.Sequence;
        }

        public override bool CanRun()
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                if (Keys[i].CanRun())
                    return true;
            }
            return false;
        }

        public override void OnEnter()
        {
            if (mountHandler.IsMounted())
            {
                mountHandler.Dismount();
                wait.Update();
            }

            castingHandler.UpdateGCD(true);

            for (int i = 0; i < Keys.Length; i++)
            {
                if (Keys[i].BeforeCastStop)
                {
                    stopMoving.Stop();
                    wait.Update();
                    break;
                }
            }
        }

        public override void Update()
        {
            if (castingHandler.SpellInQueue())
            {
                wait.Update();
                return;
            }

            if (!castSuccess)
            {
                Cast();
                wait.Update();
            }
        }

        public override void OnExit()
        {
            castSuccess = false;
        }

        private void Cast()
        {
            Parallel.For(0, Keys.Length, Execute);

            if (castSuccess)
            {
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

                    if ((DateTime.UtcNow - startTime).TotalSeconds > 30)
                    {
                        logger.LogInformation($"Waited (30s) long enough for {Name}");
                        break;
                    }
                }

                if (wasDrinkingOrEating)
                {
                    input.StandUp();
                }
            }
        }

        private void Execute(int i)
        {
            if (castingHandler.CastIfReady(Keys[i], None))
            {
                Keys[i].ResetCooldown();
                Keys[i].SetClicked();

                castSuccess = true;
            }
        }
    }
}