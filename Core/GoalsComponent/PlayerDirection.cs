using Core.GOAP;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Extensions;

using System;
using System.Numerics;
using System.Threading;

using static System.MathF;

#pragma warning disable 162

namespace Core;

public sealed partial class PlayerDirection
{
    private const bool debug = false;

    public const int DefaultIgnoreDistance = 10;

    private readonly ILogger<PlayerDirection> logger;
    private readonly ConfigurableInput input;
    private readonly PlayerReader playerReader;
    private readonly CancellationToken token;

    public PlayerDirection(ILogger<PlayerDirection> logger,
        CancellationTokenSource<GoapAgent> cts,
        ConfigurableInput input, PlayerReader playerReader)
    {
        this.logger = logger;
        this.token = cts.Token;
        this.input = input;
        this.playerReader = playerReader;
    }

    public void SetDirection(float targetDir, Vector3 map)
    {
        SetDirection(targetDir, map, DefaultIgnoreDistance, token);
    }

    public void SetDirection(float targetDir, Vector3 world, float ignoreDistance, CancellationToken token)
    {
        float distance = playerReader.WorldPos.WorldDistanceXYTo(world);
        if (distance < ignoreDistance)
        {
            if (debug)
                LogDebugClose(logger, distance, ignoreDistance);

            return;
        }

        if (debug)
            LogDebugSetDirection(logger, playerReader.Direction, targetDir, distance);

        SetDirection(targetDir, token);
    }

    public void SetDirection(float targetDir, CancellationToken token = default)
    {
        // Simple single-press turn matching upstream pattern.
        // Navigation.AdjustHeading() runs every tick and will correct
        // any residual error on the next frame — no retry loop needed.
        float diff = TurnAmount(targetDir);
        if (diff < PI / 35f) // same threshold as Navigation.minAngleToTurn
            return;

        ConsoleKey turnKey = GetDirectionKeyToPress(targetDir);
        int duration = TurnDuration(targetDir);
        input.PressFixed(turnKey, duration, token);
    }

    private float TurnAmount(float targetDir)
    {
        float result = (Tau + targetDir - playerReader.Direction) % Tau;
        return result > PI
            ? Tau - result
            : result;
    }

    private int TurnDuration(float targetDir)
    {
        return (int)(TurnAmount(targetDir) * 1000f / PI);
    }

    private ConsoleKey GetDirectionKeyToPress(float desiredDirection, float? currentDirection = null)
    {
        float current = currentDirection ?? playerReader.Direction;
        return (Tau + desiredDirection - current) % Tau < PI
            ? input.TurnLeftKey
            : input.TurnRightKey;
    }



    #region Logging

    [LoggerMessage(
        EventId = 0030,
        Level = LogLevel.Debug,
        Message = "SetDirection: Too close, ignored direction change. {distance} < {ignoreDistance}")]
    static partial void LogDebugClose(ILogger logger, float distance, float ignoreDistance);

    [LoggerMessage(
        EventId = 0031,
        Level = LogLevel.Debug,
        Message = "SetDirection: {direction:0.000} -> {desiredDirection:0.000} - {distance:0.000}")]
    static partial void LogDebugSetDirection(ILogger logger, float direction, float desiredDirection, float distance);

    #endregion
}
