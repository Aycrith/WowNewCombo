using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using System.Threading;
using SharedLib.Extensions;
using System.Runtime.CompilerServices;

#pragma warning disable 162

namespace Core
{
    public sealed partial class PlayerDirection : IDisposable
    {
        private const bool debug = false;

        private const float RADIAN = MathF.PI * 2;
        private const int DefaultIgnoreDistance = 10;

        private readonly ILogger logger;
        private readonly ConfigurableInput input;
        private readonly PlayerReader playerReader;
        private readonly CancellationTokenSource _cts;

        public PlayerDirection(ILogger logger, ConfigurableInput input, PlayerReader playerReader)
        {
            this.logger = logger;
            this.input = input;
            this.playerReader = playerReader;
            _cts = new();
        }

        public void Dispose()
        {
            _cts.Cancel();
        }

        public void SetDirection(float desiredDirection, Vector3 point)
        {
            SetDirection(desiredDirection, point, DefaultIgnoreDistance, _cts);
        }

        public void SetDirection(float desiredDirection, Vector3 point, int ignoreDistance, CancellationTokenSource cts)
        {
            float distance = playerReader.PlayerLocation.DistanceXYTo(point);
            if (distance < ignoreDistance)
            {
                if (debug)
                    LogDebugClose(logger, distance, ignoreDistance);

                return;
            }

            if (debug)
                LogDebugSetDirection(logger, playerReader.Direction, desiredDirection, distance);

            input.Proc.KeyPressSleep(GetDirectionKeyToPress(desiredDirection),
                TurnDuration(desiredDirection),
                cts);
        }

        private float TurnAmount(float desiredDirection)
        {
            var result = (RADIAN + desiredDirection - playerReader.Direction) % RADIAN;
            if (result > MathF.PI) { result = RADIAN - result; }
            return result;
        }

        private int TurnDuration(float desiredDirection)
        {
            return (int)(TurnAmount(desiredDirection) * 1000 / MathF.PI);
        }

        private ConsoleKey GetDirectionKeyToPress(float desiredDirection)
        {
            return (RADIAN + desiredDirection - playerReader.Direction) % RADIAN < MathF.PI
                ? input.Proc.TurnLeftKey : input.Proc.TurnRightKey;
        }

        #region Logging

        [LoggerMessage(
            EventId = 30,
            Level = LogLevel.Debug,
            Message = "SetDirection: Too close, ignored direction change. {distance} < {ignoreDistance}")]
        static partial void LogDebugClose(ILogger logger, float distance, float ignoreDistance);

        [LoggerMessage(
            EventId = 31,
            Level = LogLevel.Debug,
            Message = "SetDirection: {direction:0.000} -> {desiredDirection:0.000} - {distance:0.000}")]
        static partial void LogDebugSetDirection(ILogger logger, float direction, float desiredDirection, float distance);

        #endregion
    }
}