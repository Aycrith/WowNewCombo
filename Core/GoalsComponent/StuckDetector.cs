using Core.GOAP;
using Core.Goals;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Extensions;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

using static System.Diagnostics.Stopwatch;

#pragma warning disable 162

namespace Core;

public enum UnstuckState
{
    None,
    InitialAttempt,      // Stop, turn random, move
    StrafeAttempt,       // Strafe left/right
    ReverseAttempt,      // Move backward then forward
    PathClearAttempt,    // Clear route and find nearest reachable point
    EmergencyEscape      // Use hearthstone or teleport
}

public sealed class StuckEventData
{
    public Vector3 Position { get; init; }
    public float Direction { get; init; }
    public UnstuckState State { get; init; }
    public double DurationMs { get; init; }
    public bool IsSpinning { get; init; }
    public int AttemptCount { get; init; }
    public DateTime Timestamp { get; init; }
    public string? AdditionalInfo { get; init; }
}

public sealed class StuckDetector : IGoapEventListener
{
    private const bool debug = false;

    private const float MIN_RANGE_DIFF = 2f;
    private const float MIN_DISTANCE = 0.2f;
    private const float MAX_RANGE = 999999;
    private const double UNSTUCK_AFTER_MS = 2000;
    private const double ACTION_STUCK_TIME = 3000;

    private const double SPIN_CHECK_INTERVAL_MS = 500;
    private const float SPIN_THRESHOLD_RADIANS = 2.0f;
    private const int SPIN_DETECTION_COUNT = 3;
    private const int HEADING_HISTORY_SIZE = 10;

    private static readonly Dictionary<UnstuckState, double> StateTimeouts = new()
    {
        { UnstuckState.InitialAttempt, 3000 },
        { UnstuckState.StrafeAttempt, 4000 },
        { UnstuckState.ReverseAttempt, 5000 },
        { UnstuckState.PathClearAttempt, 6000 },
        { UnstuckState.EmergencyEscape, 10000 }
    };

    private readonly ILogger<StuckDetector> logger;
    private readonly ConfigurableInput input;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly PlayerDirection playerDirection;
    private readonly StopMoving stopMoving;
    private readonly IScreenCapture screenCapture;

    private Vector3 worldTarget;
    private float prevDistance = MAX_RANGE;
    private long startTime;
    private long attemptTime;
    private long lastJumpTime;

    private float prevDirection;
    private float accumulatedRotationDelta;
    private long lastDirectionCheckTime;
    private int spinDetectionCounter;
    private readonly Queue<float> headingHistory = new(HEADING_HISTORY_SIZE);

    private UnstuckState currentUnstuckState = UnstuckState.None;
    private long unstuckStateEnterTime;
    private Vector3? lastAttemptPosition;
    private int unstuckAttemptCount;
    private DateTime stuckDetectedTimestamp;

    public event Action<StuckEventData>? OnStuckDetected;

    public double ActionDurationMs => GetElapsedTime(startTime).TotalMilliseconds;
    private double UnstuckMs => GetElapsedTime(attemptTime).TotalMilliseconds;
    public UnstuckState CurrentState => currentUnstuckState;
    public bool IsCurrentlyStuck => currentUnstuckState != UnstuckState.None;
    public bool IsSpinningDetected => spinDetectionCounter >= SPIN_DETECTION_COUNT;

    public StuckDetector(ILogger<StuckDetector> logger, ConfigurableInput input,
        AddonBits bits, PlayerReader playerReader, PlayerDirection playerDirection,
        StopMoving stopMoving, IScreenCapture screenCapture)
    {
        this.logger = logger;
        this.input = input;
        this.bits = bits;
        this.playerReader = playerReader;
        this.playerDirection = playerDirection;
        this.stopMoving = stopMoving;
        this.screenCapture = screenCapture;

        Reset();
    }

    public void OnGoapEvent(GoapEventArgs e)
    {
        if (e is ResumeEvent)
        {
            Reset();
        }
    }

    public void SetTargetLocation(Vector3 worldTarget)
    {
        if (this.worldTarget != worldTarget)
        {
            this.worldTarget = worldTarget;
            Reset();
        }
    }

    public void Reset()
    {
        attemptTime = GetTimestamp();
        startTime = GetTimestamp();
        lastJumpTime = 0;
        prevDistance = MAX_RANGE;
        prevDirection = playerReader.Direction;
        accumulatedRotationDelta = 0;
        spinDetectionCounter = 0;
        headingHistory.Clear();
        currentUnstuckState = UnstuckState.None;
        unstuckAttemptCount = 0;
        lastAttemptPosition = null;
    }

    public void Update(CancellationToken token = default)
    {
        if (bits.Falling())
            return;

        UpdateSpinDetection();

        if (debug)
            logger.LogDebug($"[StuckDetector] Duration: {ActionDurationMs:F0}ms, Unstuck: {UnstuckMs:F0}ms ago, State: {currentUnstuckState}, Spinning: {IsSpinningDetected}");

        if (currentUnstuckState != UnstuckState.None)
        {
            ProcessActiveUnstuckState(token);
            return;
        }

        bool shouldTriggerUnstuck = UnstuckMs > UNSTUCK_AFTER_MS || IsSpinningDetected;

        if (shouldTriggerUnstuck)
        {
            TriggerUnstuck(token);
        }
    }

    private void UpdateSpinDetection()
    {
        double elapsed = GetElapsedTime(lastDirectionCheckTime).TotalMilliseconds;
        if (elapsed < SPIN_CHECK_INTERVAL_MS)
            return;

        float currentDir = playerReader.Direction;
        float delta = MathF.Abs(currentDir - prevDirection);

        if (delta > MathF.PI)
            delta = 2 * MathF.PI - delta;

        accumulatedRotationDelta += delta;

        headingHistory.Enqueue(currentDir);
        if (headingHistory.Count > HEADING_HISTORY_SIZE)
            headingHistory.Dequeue();

        float distance = playerReader.WorldPos.WorldDistanceXYTo(worldTarget);
        bool positionChanged = MathF.Abs(distance - prevDistance) > MIN_DISTANCE;

        if (accumulatedRotationDelta > SPIN_THRESHOLD_RADIANS && !positionChanged)
        {
            spinDetectionCounter++;

            if (spinDetectionCounter == SPIN_DETECTION_COUNT)
            {
                logger.LogWarning($"[StuckDetector] SPINNING DETECTED! Rotation delta: {accumulatedRotationDelta:F2} rad, Position unchanged");
                CollectStuckData("Spinning detected - high rotation without movement");
            }
        }
        else if (positionChanged)
        {
            if (spinDetectionCounter > 0)
            {
                logger.LogDebug($"[StuckDetector] Reset spin detection - position changed");
            }
            spinDetectionCounter = 0;
            accumulatedRotationDelta = 0;
        }

        prevDirection = currentDir;
        lastDirectionCheckTime = GetTimestamp();
    }

    private void ProcessActiveUnstuckState(CancellationToken token)
    {
        double stateDuration = GetElapsedTime(unstuckStateEnterTime).TotalMilliseconds;

        if (HasUnstuckSucceeded())
        {
            logger.LogInformation($"[StuckDetector] Successfully unstuck from state {currentUnstuckState} after {stateDuration:F0}ms");
            CollectStuckData($"Recovery successful - exited state {currentUnstuckState}");
            Reset();
            return;
        }

        if (stateDuration > StateTimeouts[currentUnstuckState])
        {
            logger.LogWarning($"[StuckDetector] State {currentUnstuckState} timed out after {stateDuration:F0}ms, escalating...");
            EscalateUnstuckState(token);
        }
    }

    private void TriggerUnstuck(CancellationToken token)
    {
        currentUnstuckState = UnstuckState.InitialAttempt;
        unstuckStateEnterTime = GetTimestamp();
        lastAttemptPosition = playerReader.WorldPos;
        stuckDetectedTimestamp = DateTime.UtcNow;
        unstuckAttemptCount++;

        logger.LogWarning($"[StuckDetector] TRIGGERING UNSTUCK - State: {currentUnstuckState}, Attempt: {unstuckAttemptCount}, Spinning: {IsSpinningDetected}");

        CollectStuckData($"Unstuck triggered - InitialAttempt");
        ExecuteUnstuckState(token);
    }

    private void EscalateUnstuckState(CancellationToken token)
    {
        currentUnstuckState = currentUnstuckState switch
        {
            UnstuckState.None => UnstuckState.InitialAttempt,
            UnstuckState.InitialAttempt => UnstuckState.StrafeAttempt,
            UnstuckState.StrafeAttempt => UnstuckState.ReverseAttempt,
            UnstuckState.ReverseAttempt => UnstuckState.PathClearAttempt,
            UnstuckState.PathClearAttempt => UnstuckState.EmergencyEscape,
            _ => UnstuckState.EmergencyEscape
        };

        unstuckStateEnterTime = GetTimestamp();
        lastAttemptPosition = playerReader.WorldPos;
        unstuckAttemptCount++;

        logger.LogWarning($"[StuckDetector] ESCALATING TO {currentUnstuckState} (attempt {unstuckAttemptCount})");
        CollectStuckData($"Escalated to {currentUnstuckState}");
        ExecuteUnstuckState(token);
    }

    private bool HasUnstuckSucceeded()
    {
        if (lastAttemptPosition == null)
            return false;

        float movedDistance = playerReader.WorldPos.WorldDistanceXYTo(lastAttemptPosition.Value);
        return movedDistance > MIN_RANGE_DIFF;
    }

    private void ExecuteUnstuckState(CancellationToken token)
    {
        switch (currentUnstuckState)
        {
            case UnstuckState.InitialAttempt:
                ExecuteInitialUnstuck(token);
                break;
            case UnstuckState.StrafeAttempt:
                ExecuteStrafeUnstuck(token);
                break;
            case UnstuckState.ReverseAttempt:
                ExecuteReverseUnstuck(token);
                break;
            case UnstuckState.PathClearAttempt:
                ExecutePathClearUnstuck(token);
                break;
            case UnstuckState.EmergencyEscape:
                ExecuteEmergencyEscape(token);
                break;
        }

        attemptTime = GetTimestamp();
    }

    private void ExecuteInitialUnstuck(CancellationToken token)
    {
        stopMoving.Stop();

        int turnDuration = Random.Shared.Next(500) + 350;
        logger.LogInformation($"[StuckDetector] Initial attempt: Turning for {turnDuration}ms");
        input.TurnRandomDir(turnDuration, token);

        token.WaitHandle.WaitOne(turnDuration + 100);

        ConsoleKey moveKey = Random.Shared.Next(100) >= 25 ? input.ForwardKey : input.BackwardKey;
        int moveDuration = Random.Shared.Next(750) + 1000;
        logger.LogInformation($"[StuckDetector] Initial attempt: Moving {(moveKey == input.ForwardKey ? "forward" : "backward")} for {moveDuration}ms");
        input.PressFixed(moveKey, moveDuration, token);

        TryJump(token);
    }

    private void ExecuteStrafeUnstuck(CancellationToken token)
    {
        stopMoving.Stop();

        bool strafeRight = Random.Shared.Next(2) == 0;
        int strafeDuration = 1500 + Random.Shared.Next(1000);

        logger.LogInformation($"[StuckDetector] Strafe attempt: Strafing {(strafeRight ? "right" : "left")} for {strafeDuration}ms");

        if (strafeRight)
            input.PressFixed(input.StrafeRightKey, strafeDuration, token);
        else
            input.PressFixed(input.StrafeLeftKey, strafeDuration, token);

        TryJump(token);

        token.WaitHandle.WaitOne(200);

        int forwardDuration = 1000 + Random.Shared.Next(500);
        logger.LogInformation($"[StuckDetector] Strafe attempt: Moving forward for {forwardDuration}ms");
        input.PressFixed(input.ForwardKey, forwardDuration, token);
    }

    private void ExecuteReverseUnstuck(CancellationToken token)
    {
        stopMoving.Stop();

        int backupDuration = 2000 + Random.Shared.Next(1000);
        logger.LogInformation($"[StuckDetector] Reverse attempt: Backing up for {backupDuration}ms");
        input.PressFixed(input.BackwardKey, backupDuration, token);

        token.WaitHandle.WaitOne(200);

        int turnDuration = 800 + Random.Shared.Next(400);
        logger.LogInformation($"[StuckDetector] Reverse attempt: Turning for {turnDuration}ms");
        input.TurnRandomDir(turnDuration, token);

        token.WaitHandle.WaitOne(turnDuration + 100);

        int forwardDuration = 2000 + Random.Shared.Next(1000);
        logger.LogInformation($"[StuckDetector] Reverse attempt: Moving forward for {forwardDuration}ms");
        input.PressFixed(input.ForwardKey, forwardDuration, token);
    }

    private void ExecutePathClearUnstuck(CancellationToken token)
    {
        logger.LogWarning("[StuckDetector] PathClear attempt: Stopping all movement and clearing navigation state");

        stopMoving.Stop();

        input.TurnRandomDir(2000, token);

        token.WaitHandle.WaitOne(500);

        logger.LogInformation($"[StuckDetector] PathClear attempt: Moving forward for 3s to escape obstacle");
        input.PressFixed(input.ForwardKey, 3000, token);
    }

    private void ExecuteEmergencyEscape(CancellationToken token)
    {
        logger.LogError("[StuckDetector] EMERGENCY ESCAPE: Bot has been stuck for an extended period. Manual intervention may be required.");

        CollectStuckData("Emergency escape - manual intervention may be required");

        stopMoving.Stop();

        for (int i = 0; i < 3; i++)
        {
            input.PressJump(token);
            token.WaitHandle.WaitOne(500);
        }

        input.TurnRandomDir(3000, token);
        token.WaitHandle.WaitOne(100);
        input.PressFixed(input.ForwardKey, 5000, token);
    }

    private void TryJump(CancellationToken token)
    {
        if (bits.Flying())
            return;

        if (lastJumpTime != 0 && GetElapsedTime(lastJumpTime).TotalMilliseconds < 900)
            return;

        logger.LogDebug("[StuckDetector] Jumping");
        input.PressJump(token);
        lastJumpTime = GetTimestamp();
    }

    private void CollectStuckData(string reason)
    {
        try
        {
            var eventData = new StuckEventData
            {
                Position = playerReader.WorldPos,
                Direction = playerReader.Direction,
                State = currentUnstuckState,
                DurationMs = ActionDurationMs,
                IsSpinning = IsSpinningDetected,
                AttemptCount = unstuckAttemptCount,
                Timestamp = DateTime.UtcNow,
                AdditionalInfo = reason
            };

            OnStuckDetected?.Invoke(eventData);

            logger.LogInformation($"[StuckDetector] DATA COLLECTION - Pos: {eventData.Position}, Dir: {eventData.Direction:F2}, " +
                $"State: {eventData.State}, Duration: {eventData.DurationMs:F0}ms, Spinning: {eventData.IsSpinning}, Reason: {reason}");

            screenCapture?.Request();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[StuckDetector] Failed to collect stuck data");
        }
    }

    public bool IsGettingCloser()
    {
        float distance = playerReader.WorldPos.WorldDistanceXYTo(worldTarget);
        if (distance <= prevDistance - MIN_RANGE_DIFF)
        {
            if (currentUnstuckState != UnstuckState.None)
            {
                logger.LogInformation($"[StuckDetector] Movement detected during recovery - distance decreased from {prevDistance:F2} to {distance:F2}");
            }
            Reset();
            prevDistance = distance;
            return true;
        }

        return ActionDurationMs < ACTION_STUCK_TIME;
    }

    public bool IsMoving()
    {
        float distance = playerReader.WorldPos.WorldDistanceXYTo(worldTarget);
        if (MathF.Abs(distance - prevDistance) > MIN_DISTANCE)
        {
            if (currentUnstuckState != UnstuckState.None)
            {
                logger.LogInformation($"[StuckDetector] Movement detected during recovery");
            }
            Reset();
            prevDistance = distance;
            return true;
        }

        return ActionDurationMs < ACTION_STUCK_TIME;
    }

    public bool IsOscillating()
    {
        if (headingHistory.Count < HEADING_HISTORY_SIZE)
            return false;

        int directionChanges = 0;
        float prevDelta = 0;

        var headings = headingHistory.ToArray();
        for (int i = 1; i < headings.Length; i++)
        {
            float delta = headings[i] - headings[i - 1];

            if (prevDelta != 0 && Math.Sign(delta) != Math.Sign(prevDelta) && Math.Abs(delta) > 0.1f)
            {
                directionChanges++;
            }
            prevDelta = delta;
        }

        return directionChanges > 5;
    }
}
