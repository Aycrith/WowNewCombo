using Core.GOAP;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;

namespace Core.Goals
{
    public partial class WaitForGathering : GoapGoal
    {
        public override float CostOfPerformingAction => 17;

        private const int Timeout = 5000;

        private readonly ILogger logger;
        private readonly Wait wait;
        private readonly PlayerReader playerReader;
        private readonly StopMoving stopMoving;
        private readonly Stopwatch stopWatch;

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
        private int lastKnownCast;

        public WaitForGathering(ILogger logger, Wait wait, PlayerReader playerReader, StopMoving stopMoving)
        {
            this.logger = logger;
            this.wait = wait;
            this.playerReader = playerReader;
            this.stopMoving = stopMoving;
            this.stopWatch = new();

            AddPrecondition(GoapKey.gathering, true);
        }

        public override void OnEnter()
        {
            stopMoving.Stop();
            wait.Update();

            while (playerReader.Bits.IsFalling())
            {
                wait.Update();
            }

            LogOnEnter(logger);
        }

        public override void OnExit()
        {
            state = CastState.None;
            lastKnownCast = 0;

            LogState(logger, state);

            stopWatch.Reset();
            stopWatch.Stop();
        }

        public override void PerformAction()
        {
            switch (state)
            {
                case CastState.None:
                    CheckCastStarted(false);
                    break;
                case CastState.Casting:
                    if (!playerReader.IsCasting())
                    {
                        wait.Update();
                        if (playerReader.LastUIError == UI_ERROR.ERR_SPELL_FAILED_S)
                        {
                            state = CastState.Failed;
                            LogFailed(logger, state, Timeout);
                        }
                        else
                        {
                            if (miningSpells.Contains(lastKnownCast))
                            {
                                state = CastState.WaitUserInput;
                                LogSuccessMining(logger, CastState.Success, state, Timeout);
                                stopWatch.Restart();
                                wait.Update();
                            }
                            else
                            {
                                state = CastState.Success;
                                LogState(logger, state);
                            }
                        }
                    }
                    break;
                case CastState.Failed:
                    stopWatch.Restart();
                    state = CastState.WaitUserInput;
                    LogFailed(logger, state, Timeout);
                    wait.Update();
                    break;
                case CastState.Success:
                case CastState.Abort:
                    SendActionEvent(new ActionEventArgs(GoapKey.gathering, false));
                    break;
                case CastState.WaitUserInput:
                    CheckCastStarted(true);

                    if (stopWatch.ElapsedMilliseconds > Timeout)
                    {
                        SendActionEvent(new ActionEventArgs(GoapKey.gathering, false));
                    }
                    break;
            }

            wait.Update();
        }

        private void CheckCastStarted(bool restartTimer)
        {
            if (playerReader.IsCasting() &&
                (herbSpells.Contains(playerReader.CastSpellId.Value) ||
                miningSpells.Contains(playerReader.CastSpellId.Value)))
            {
                lastKnownCast = playerReader.CastSpellId.Value;
                state = CastState.Casting;

                LogState(logger, state);

                if (restartTimer)
                {
                    stopWatch.Reset();
                    stopWatch.Stop();
                }
            }

            if (playerReader.Bits.IsFalling())
            {
                state = CastState.Abort;
                LogState(logger, state);
            }
        }


        #region Logging

        [LoggerMessage(
            EventId = 101,
            Level = LogLevel.Information,
            Message = "{state}")]
        static partial void LogState(ILogger logger, CastState state);

        [LoggerMessage(
            EventId = 102,
            Level = LogLevel.Warning,
            Message = "Waiting indefinitely for [Gathering cast to start] or [Press Jump to Abort]")]
        static partial void LogOnEnter(ILogger logger);

        [LoggerMessage(
            EventId = 103,
            Level = LogLevel.Error,
            Message = "{state} -- Waiting(max {Timeout} ms) for [Gathering cast to start] or [Press Jump to Abort]")]
        static partial void LogFailed(ILogger logger, CastState state, int Timeout);

        [LoggerMessage(
            EventId = 104,
            Level = LogLevel.Information,
            Message = "{success} -> {state} Waiting(max {Timeout} ms) for [More Mining cast] or [Press Jump to Abort]")]
        static partial void LogSuccessMining(ILogger logger, CastState success, CastState state, int Timeout);

        #endregion
    }
}
