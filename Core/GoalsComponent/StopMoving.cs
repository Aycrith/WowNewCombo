using System;
using System.Threading;
using System.Numerics;
using SharedLib.Extensions;
using Game;

namespace Core.Goals
{
    public class StopMoving
    {
        private readonly WowProcessInput input;
        private readonly PlayerReader playerReader;
        private readonly CancellationTokenSource cts;
        private readonly Random random;

        private const float MinDist = 0.001f;

        private Vector3 last;
        private float Direction;

        public StopMoving(WowProcessInput input, PlayerReader playerReader, CancellationTokenSource cts)
        {
            this.input = input;
            this.playerReader = playerReader;
            this.cts = cts;
            random = new();
        }

        public void Stop()
        {
            StopForward();
            StopTurn();
        }

        public void StopForward()
        {
            if (last != playerReader.PlayerLocation)
            {
                bool pressedAny = false;

                if (!input.IsKeyDown(input.BackwardKey) &&
                    !input.IsKeyDown(input.ForwardKey) &&
                    last.DistanceXYTo(playerReader.PlayerLocation) >= MinDist)
                {
                    input.KeyPressSleep(input.ForwardKey, random.Next(2, 5), cts);
                    pressedAny = true;
                }

                if (input.IsKeyDown(input.ForwardKey))
                {
                    input.SetKeyState(input.ForwardKey, false, true);
                    pressedAny = true;
                }

                if (input.IsKeyDown(input.BackwardKey))
                {
                    input.SetKeyState(input.BackwardKey, false, true);
                    pressedAny = true;
                }

                if (pressedAny)
                    cts.Token.WaitHandle.WaitOne(random.Next(25, 30));
            }

            last = playerReader.PlayerLocation;
        }

        public void StopTurn()
        {
            if (Direction != playerReader.Direction)
            {
                bool pressedAny = false;

                if (input.IsKeyDown(input.TurnLeftKey))
                {
                    input.SetKeyState(input.TurnLeftKey, false, true);
                    pressedAny = true;
                }

                if (input.IsKeyDown(input.TurnRightKey))
                {
                    input.SetKeyState(input.TurnRightKey, false, true);
                    pressedAny = true;
                }

                if (pressedAny)
                    cts.Token.WaitHandle.WaitOne(1);
            }

            Direction = playerReader.Direction;
        }
    }
}