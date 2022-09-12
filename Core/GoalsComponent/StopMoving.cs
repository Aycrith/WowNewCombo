using System;
using System.Threading;
using System.Numerics;
using SharedLib.Extensions;
using Game;

namespace Core.Goals
{
    public sealed class StopMoving
    {
        private readonly WowProcessInput input;
        private readonly PlayerReader playerReader;
        private readonly CancellationToken ct;

        private const float MinDist = 0.001f;

        private Vector3 mapPos;
        private float direction;

        public StopMoving(WowProcessInput input, PlayerReader playerReader, CancellationTokenSource cts)
        {
            this.input = input;
            this.playerReader = playerReader;
            ct = cts.Token;
        }

        public void Stop()
        {
            StopForward();
            StopTurn();
        }

        public void StopForward()
        {
            if (mapPos != playerReader.MapPos)
            {
                bool pressedAny = false;

                if (!input.IsKeyDown(input.BackwardKey) &&
                    !input.IsKeyDown(input.ForwardKey) &&
                    mapPos.MapDistanceXYTo(playerReader.MapPos) >= MinDist)
                {
                    input.KeyPressSleep(input.ForwardKey, Random.Shared.Next(2, 5), ct);
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
                    ct.WaitHandle.WaitOne(Random.Shared.Next(25, 30));
            }

            mapPos = playerReader.MapPos;
        }

        public void StopTurn()
        {
            if (direction != playerReader.Direction)
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
                    ct.WaitHandle.WaitOne(1);
            }

            direction = playerReader.Direction;
        }
    }
}