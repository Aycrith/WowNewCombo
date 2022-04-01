using System;
using System.Threading;

namespace Core.Goals
{
    public class StopMoving : IDisposable
    {
        private readonly ConfigurableInput input;
        private readonly PlayerReader playerReader;
        private readonly CancellationTokenSource cts;

        private const double MinDist = 0.01;

        private float XCoord;
        private float YCoord;
        private float Direction;

        public StopMoving(ConfigurableInput input, PlayerReader playerReader)
        {
            this.input = input;
            this.playerReader = playerReader;
            this.cts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            cts.Dispose();
        }

        public void Stop()
        {
            StopForward();
            StopTurn();
        }

        public void StopForward()
        {
            if (XCoord != playerReader.XCoord || YCoord != playerReader.YCoord)
            {
                if (!input.IsKeyDown(input.BackwardKey) && !input.IsKeyDown(input.ForwardKey) &&
                    (MathF.Abs(XCoord - playerReader.XCoord) > MinDist || MathF.Abs(YCoord - playerReader.YCoord) > MinDist))
                {
                    //"StopForward - Cancel interact"
                    input.SetKeyState(input.ForwardKey, true);
                    cts.Token.WaitHandle.WaitOne(2);
                }

                input.SetKeyState(input.ForwardKey, false);
                cts.Token.WaitHandle.WaitOne(2);
                //"StopForward"
                input.SetKeyState(input.BackwardKey, false);
                cts.Token.WaitHandle.WaitOne(10);
            }

            this.XCoord = playerReader.XCoord;
            this.YCoord = playerReader.YCoord;
        }

        public void StopTurn()
        {
            if (Direction != playerReader.Direction)
            {
                input.SetKeyState(input.TurnLeftKey, false);
                //"StopTurn"
                input.SetKeyState(input.TurnRightKey, false);
                cts.Token.WaitHandle.WaitOne(1);
            }

            this.Direction = playerReader.Direction;
        }
    }
}