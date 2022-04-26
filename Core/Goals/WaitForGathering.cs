using Core.GOAP;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Core.Goals
{
    public class WaitForGathering : GoapGoal
    {
        public override float CostOfPerformingAction => 17;

        private const int Timeout = 5000;

        private readonly ILogger logger;
        private readonly Wait wait;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly Stopwatch stopWatch;

        private int lastKnownCast;

        private readonly List<int> herbSpells = new()
        {
            2366,
            2368,
            3570,
            11993,
            28695,
        };

        private readonly List<int> miningSpells = new()
        {
            2575,
            2576,
            3564,
            10248,
            29354
        };

        private enum CastState
        {
            None,
            Casting,
            Failed,
            Abort,
            Success,
            WaitUserInput,
        }
        private CastState state;

        public WaitForGathering(ILogger logger, Wait wait, PlayerReader playerReader, StopMoving stopMoving)
        {
            this.logger = logger;
            this.wait = wait;
            this.playerReader = playerReader;
            this.stopMoving = stopMoving;
            this.stopWatch = new();

            AddPrecondition(GoapKey.gathering, true);
        }

        public override ValueTask OnEnter()
        {
            stopMoving.Stop();
            wait.Update(1);

            while (playerReader.Bits.IsFalling)
            {
                wait.Update(1);
            }

            logger.LogInformation("Waiting indefinitely for [Gathering cast to start] or [Press Jump to Abort]");

            return base.OnEnter();
        }

        public override ValueTask OnExit()
        {
            logger.LogInformation($"{state} -- Exit");

            state = CastState.None;
            lastKnownCast = 0;

            stopWatch.Reset();
            stopWatch.Stop();

            return base.OnExit();
        }

        public override ValueTask PerformAction()
        {
            switch (state)
            {
                case CastState.None:
                    if (playerReader.IsCasting &&
                        (herbSpells.Contains(playerReader.CastSpellId.Value) ||
                        miningSpells.Contains(playerReader.CastSpellId.Value)))
                    {
                        lastKnownCast = playerReader.CastSpellId.Value;
                        state = CastState.Casting;

                        logger.LogInformation(state.ToString());
                    }

                    if (playerReader.Bits.IsFalling)
                    {
                        state = CastState.Abort;
                        logger.LogInformation(state.ToString());
                    }
                    break;
                case CastState.Casting:
                    if (!playerReader.IsCasting)
                    {
                        if (playerReader.LastUIErrorMessage == UI_ERROR.ERR_SPELL_FAILED_S)
                        {
                            state = CastState.Failed;
                            logger.LogInformation($"{state} -- Waiting(max {Timeout} ms) for [Gathering cast to start] or [Press Jump to Abort]");
                        }
                        else
                        {
                            if (miningSpells.Contains(lastKnownCast))
                            {
                                state = CastState.WaitUserInput;
                                logger.LogInformation($"{CastState.Success} -> {state} Waiting(max {Timeout} ms) for [More Mining cast] or [Press Jump to Abort]");
                                stopWatch.Restart();
                                wait.Update(1);
                            }
                            else
                            {
                                state = CastState.Success;
                                logger.LogInformation(state.ToString());
                            }
                        }
                    }
                    break;
                case CastState.Failed:
                    stopWatch.Restart();
                    state = CastState.WaitUserInput;
                    logger.LogInformation($"{state} -- Waiting(max {Timeout} ms) for [Gathering cast to start] or [Press Jump to Abort]");
                    wait.Update(1);
                    break;
                case CastState.Success:
                case CastState.Abort:
                    SendActionEvent(new ActionEventArgs(GoapKey.gathering, false));
                    break;
                case CastState.WaitUserInput:
                    if (playerReader.IsCasting &&
                        (herbSpells.Contains(playerReader.CastSpellId.Value) ||
                        miningSpells.Contains(playerReader.CastSpellId.Value)))
                    {
                        lastKnownCast = playerReader.CastSpellId.Value;
                        state = CastState.Casting;

                        logger.LogInformation(state.ToString());

                        stopWatch.Reset();
                        stopWatch.Stop();
                    }

                    if (playerReader.Bits.IsFalling)
                    {
                        state = CastState.Abort;
                        logger.LogInformation(state.ToString());
                    }

                    if (stopWatch.ElapsedMilliseconds > Timeout)
                    {
                        SendActionEvent(new ActionEventArgs(GoapKey.gathering, false));
                    }
                    break;
            }

            wait.Update(1);
            return ValueTask.CompletedTask;
        }
    }
}
