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

    // Closed-loop control constants
    private const int MAX_TURN_RETRIES = 3;
    private const float TURN_TOLERANCE_RADIANS = 0.15f;  // ~8.6 degrees
    private const int TURN_VERIFICATION_DELAY_MS = 50;
    private const int MAX_TOTAL_TURN_DURATION_MS = 3000;

    private readonly ILogger<PlayerDirection> logger;
    private readonly ConfigurableInput input;
    private readonly PlayerReader playerReader;
    private readonly CancellationToken token;

    // Tracking for turn verification
    private int consecutiveFailedTurns;

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
        // Use closed-loop control with verification
        SetDirectionWithVerification(targetDir, token);
    }

    /// <summary>
    /// Sets direction with closed-loop control - verifies turn completed and retries if needed
    /// </summary>
    private void SetDirectionWithVerification(float targetDir, CancellationToken token)
    {
        float initialDir = playerReader.Direction;
        float currentDiff = CalculateAngleDifference(targetDir, initialDir);

        if (currentDiff <= TURN_TOLERANCE_RADIANS)
        {
            // Already facing target
            consecutiveFailedTurns = 0;
            return;
        }

        long turnStartTime = GetTimestamp();
        int retryCount = 0;

        while (retryCount < MAX_TURN_RETRIES)
        {
            float currentDir = playerReader.Direction;
            float diff = CalculateAngleDifference(targetDir, currentDir);

            if (diff <= TURN_TOLERANCE_RADIANS)
            {
                // Turn successful
                consecutiveFailedTurns = 0;
                LogTurnSuccess(logger, targetDir, retryCount);
                return;
            }

            // Check total turn time
            if (GetElapsedTime(turnStartTime).TotalMilliseconds > MAX_TOTAL_TURN_DURATION_MS)
            {
                logger.LogWarning($"[PlayerDirection] Turn to {targetDir:F2} exceeded max duration ({MAX_TOTAL_TURN_DURATION_MS}ms), aborting");
                consecutiveFailedTurns++;
                return;
            }

            // Execute turn
            ConsoleKey turnKey = GetDirectionKeyToPress(targetDir, currentDir);
            int duration = CalculateTurnDuration(diff);

            if (retryCount > 0)
            {
                logger.LogDebug($"[PlayerDirection] Turn retry {retryCount}: diff={diff:F2}rad, duration={duration}ms");
            }

            input.PressFixed(turnKey, duration, token);

            // Wait for turn to execute with verification
            WaitForTurn(duration, token);

            retryCount++;
        }

        // Max retries reached
        float finalDiff = CalculateAngleDifference(targetDir, playerReader.Direction);
        if (finalDiff > TURN_TOLERANCE_RADIANS)
        {
            consecutiveFailedTurns++;
            logger.LogWarning($"[PlayerDirection] Failed to turn to {targetDir:F2} after {MAX_TURN_RETRIES} attempts. " +
                $"Final diff: {finalDiff:F2}rad ({finalDiff * 180 / PI:F1}°)");
        }
        else
        {
            consecutiveFailedTurns = 0;
        }
    }

    /// <summary>
    /// Waits for turn to execute, polling direction to detect completion
    /// </summary>
    private static void WaitForTurn(int expectedDuration, CancellationToken token)
    {
        // Wait at least the expected duration
        int waitTime = Math.Min(expectedDuration + TURN_VERIFICATION_DELAY_MS, 200);

        // Use spin wait for better precision with cancellation support
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < waitTime)
        {
            if (token.IsCancellationRequested)
                return;

            token.WaitHandle.WaitOne(10);
        }
    }

    /// <summary>
    /// Calculates the shortest angle difference between two directions
    /// </summary>
    private static float CalculateAngleDifference(float target, float current)
    {
        float diff = MathF.Abs(target - current) % (2 * PI);
        return diff > PI ? 2 * PI - diff : diff;
    }

    /// <summary>
    /// Calculates turn duration based on angle with safety limits
    /// </summary>
    private static int CalculateTurnDuration(float angleRadians)
    {
        // Base calculation: time to turn at ~180°/second
        int duration = (int)(angleRadians * 1000f / PI);

        // Clamp to reasonable range
        return Math.Clamp(duration, 50, 1000);
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

    /// <summary>
    /// Gets the number of consecutive failed turn attempts
    /// </summary>
    public int GetConsecutiveFailedTurns() => consecutiveFailedTurns;

    /// <summary>
    /// Resets the failed turn counter
    /// </summary>
    public void ResetFailedTurnCounter() => consecutiveFailedTurns = 0;

    private static long GetTimestamp() => System.Diagnostics.Stopwatch.GetTimestamp();

    private static TimeSpan GetElapsedTime(long startTimestamp)
    {
        return System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp);
    }

    [LoggerMessage(
        EventId = 0032,
        Level = LogLevel.Debug,
        Message = "[PlayerDirection] Turn to {targetDir:F2} succeeded after {retryCount} retries")]
    static partial void LogTurnSuccess(ILogger logger, float targetDir, int retryCount);

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