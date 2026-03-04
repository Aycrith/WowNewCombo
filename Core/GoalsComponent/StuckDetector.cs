using Core.FeatureFlags;
using Core.GOAP;
using Core.Goals;
using Core.GoalsComponent;

using Microsoft.Extensions.Logging;

using SharedLib;
using SharedLib.Data;
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
    BreadcrumbBacktrack, // Walk back along recorded position trail
    PathClearAttempt,    // Clear route and find nearest reachable point
    EmergencyEscape      // Use hearthstone or teleport
}

public sealed class StuckEventData
{
    public Vector3 Position { get; init; }
    public float MapX { get; init; }
    public float MapY { get; init; }
    public int MapId { get; init; }
    public int UIMapId { get; init; }
    public string Zone { get; init; } = string.Empty;
    public float Direction { get; init; }
    public UnstuckState State { get; init; }
    public double DurationMs { get; init; }
    public bool IsSpinning { get; init; }
    public int AttemptCount { get; init; }
    public DateTime Timestamp { get; init; }
    public StuckTriggerReason? TriggerReason { get; init; }
    public string? AdditionalInfo { get; init; }
}

public sealed class StuckDetector : IGoapEventListener
{
    private const bool debug = false;

    private const float DEFAULT_MIN_RANGE_DIFF = 2f;
    private const float DEFAULT_MIN_DISTANCE = 0.2f;
    private const float MAX_RANGE = 999999;
    private const double DEFAULT_UNSTUCK_AFTER_MS = 2000;
    private const double DEFAULT_ACTION_STUCK_TIME = 3000;

    private const double SPIN_CHECK_INTERVAL_MS = 500;
    private const float SPIN_THRESHOLD_RADIANS = 2.0f;
    private const int SPIN_DETECTION_COUNT = 3;
    private const int HEADING_HISTORY_SIZE = 10;
    private const float RepeatHotspotRadius = 10f;
    private static readonly TimeSpan RepeatHotspotWindow = TimeSpan.FromSeconds(120);

    private static readonly Dictionary<UnstuckState, double> StateTimeouts = new()
    {
        { UnstuckState.InitialAttempt, 3000 },
        { UnstuckState.StrafeAttempt, 4000 },
        { UnstuckState.ReverseAttempt, 5000 },
        { UnstuckState.BreadcrumbBacktrack, 8000 },
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
    private readonly BreadcrumbTracker? breadcrumbTracker;
    private readonly FeatureFlagService? featureFlagService;

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
    private Vector3 lastStuckTriggerPosition = Vector3.Zero;
    private DateTime lastStuckTriggerUtc = DateTime.MinValue;
    private int repeatedHotspotCount;
    private StuckTriggerReason? lastTriggerReason;
    private DateTime? lastTriggerUtc;
    private int? lastPredictiveRisk;
    private readonly Queue<StuckTriggerSample> recentTriggers = new();
    private const int RecentTriggerHistoryLimit = 20;

    public event Action<StuckEventData>? OnStuckDetected;

    public double ActionDurationMs => GetElapsedTime(startTime).TotalMilliseconds;
    private double UnstuckMs => GetElapsedTime(attemptTime).TotalMilliseconds;
    public double CurrentUnstuckDurationMs => UnstuckMs;
    public UnstuckState CurrentState => currentUnstuckState;
    public bool IsCurrentlyStuck => currentUnstuckState != UnstuckState.None;
    public bool IsSpinningDetected => spinDetectionCounter >= SPIN_DETECTION_COUNT;
    public StuckTriggerReason? LastTriggerReason => lastTriggerReason;
    public DateTime? LastTriggerUtc => lastTriggerUtc;
    public int? LastPredictiveRisk => lastPredictiveRisk;

    /// <summary>
    /// Indicates whether enhanced stuck recovery (breadcrumb backtracking) is available.
    /// </summary>
    public bool IsEnhancedRecoveryAvailable =>
        breadcrumbTracker != null &&
        (featureFlagService?.IsEnhancedStuckRecoveryEnabled ?? true);

    public StuckDetector(ILogger<StuckDetector> logger, ConfigurableInput input,
        AddonBits bits, PlayerReader playerReader, PlayerDirection playerDirection,
        StopMoving stopMoving, IScreenCapture screenCapture,
        BreadcrumbTracker? breadcrumbTracker = null,
        FeatureFlagService? featureFlagService = null)
    {
        this.logger = logger;
        this.input = input;
        this.bits = bits;
        this.playerReader = playerReader;
        this.playerDirection = playerDirection;
        this.stopMoving = stopMoving;
        this.screenCapture = screenCapture;
        this.breadcrumbTracker = breadcrumbTracker;
        this.featureFlagService = featureFlagService;

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
        lastPredictiveRisk = null;
    }

    public void Update(CancellationToken token = default)
    {
        Update(token, null);
    }

    public void Update(CancellationToken token, StuckTriggerReason? externalTriggerHint)
    {
        if (bits.Falling())
            return;

        // Record current position for breadcrumb backtracking (only when not stuck)
        if (currentUnstuckState == UnstuckState.None && IsEnhancedRecoveryAvailable)
        {
            breadcrumbTracker!.RecordPosition(playerReader.WorldPos);
        }

        UpdateSpinDetection();

        if (debug)
            logger.LogDebug($"[StuckDetector] Duration: {ActionDurationMs:F0}ms, Unstuck: {UnstuckMs:F0}ms ago, State: {currentUnstuckState}, Spinning: {IsSpinningDetected}");

        if (currentUnstuckState != UnstuckState.None)
        {
            ProcessActiveUnstuckState(token);
            return;
        }

        bool predictiveTriggered = IsPredictiveStuckRiskTriggered();
        bool shouldTriggerUnstuck;
        StuckTriggerReason? triggerReason = null;

        if (externalTriggerHint == StuckTriggerReason.ExternalOscillationNotify)
        {
            shouldTriggerUnstuck = true;
            triggerReason = StuckTriggerReason.ExternalOscillationNotify;
        }
        else if (predictiveTriggered)
        {
            shouldTriggerUnstuck = true;
            triggerReason = StuckTriggerReason.PredictiveRisk;
        }
        else if (IsSpinningDetected)
        {
            shouldTriggerUnstuck = true;
            triggerReason = StuckTriggerReason.SpinDetected;
        }
        else if (UnstuckMs > GetUnstuckAfterMsThreshold())
        {
            shouldTriggerUnstuck = true;
            triggerReason = StuckTriggerReason.TimeoutNoProgress;
        }
        else
        {
            shouldTriggerUnstuck = false;
        }

        if (shouldTriggerUnstuck)
        {
            if (predictiveTriggered)
            {
                logger.LogWarning(
                    "[StuckDetector] Predictive stuck risk exceeded threshold; forcing recovery early.");
            }

            TriggerUnstuck(token, triggerReason ?? StuckTriggerReason.TimeoutNoProgress);
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
        bool positionChanged = MathF.Abs(distance - prevDistance) > GetMinDistanceThreshold();

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

    private void TriggerUnstuck(CancellationToken token, StuckTriggerReason triggerReason)
    {
        RecordTrigger(triggerReason);
        currentUnstuckState = GetInitialUnstuckState(playerReader.WorldPos);
        unstuckStateEnterTime = GetTimestamp();
        lastAttemptPosition = playerReader.WorldPos;
        stuckDetectedTimestamp = DateTime.UtcNow;
        unstuckAttemptCount++;

        logger.LogWarning(
            "[StuckDetector] TRIGGERING UNSTUCK - Trigger: {TriggerReason}, State: {State}, Attempt: {Attempt}, RepeatedHotspotCount: {RepeatedHotspotCount}, Spinning: {IsSpinning}",
            triggerReason,
            currentUnstuckState,
            unstuckAttemptCount,
            repeatedHotspotCount,
            IsSpinningDetected);

        CollectStuckData($"Unstuck triggered - {currentUnstuckState}");
        ExecuteUnstuckState(token);
    }

    private void RecordTrigger(StuckTriggerReason triggerReason)
    {
        DateTime now = DateTime.UtcNow;
        lastTriggerReason = triggerReason;
        lastTriggerUtc = now;

        recentTriggers.Enqueue(new StuckTriggerSample(
            TimestampUtc: now,
            Reason: triggerReason,
            ActionDurationMs: ActionDurationMs,
            State: currentUnstuckState,
            PredictiveRisk: lastPredictiveRisk));

        while (recentTriggers.Count > RecentTriggerHistoryLimit)
        {
            recentTriggers.Dequeue();
        }
    }

    private void EscalateUnstuckState(CancellationToken token)
    {
        currentUnstuckState = currentUnstuckState switch
        {
            UnstuckState.None => UnstuckState.InitialAttempt,
            UnstuckState.InitialAttempt => UnstuckState.StrafeAttempt,
            UnstuckState.StrafeAttempt => UnstuckState.ReverseAttempt,
            // Use breadcrumb backtracking if available, otherwise skip to path clear
            UnstuckState.ReverseAttempt => IsEnhancedRecoveryAvailable
                ? UnstuckState.BreadcrumbBacktrack
                : UnstuckState.PathClearAttempt,
            UnstuckState.BreadcrumbBacktrack => UnstuckState.PathClearAttempt,
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
        return movedDistance > GetRangeDiffThreshold();
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
            case UnstuckState.BreadcrumbBacktrack:
                ExecuteBreadcrumbBacktrack(token);
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

    private void ExecuteBreadcrumbBacktrack(CancellationToken token)
    {
        if (breadcrumbTracker == null)
        {
            logger.LogWarning("[StuckDetector] Breadcrumb backtrack requested but tracker unavailable");
            return;
        }

        stopMoving.Stop();

        // Try to find a position to backtrack to (3-5 steps back along the trail)
        int preferredSteps = GetBacktrackSteps();
        BreadcrumbEntry? backtrackEntry = breadcrumbTracker.GetBacktrackPosition(preferredSteps);
        if (backtrackEntry == null)
            backtrackEntry = breadcrumbTracker.GetBacktrackPosition(preferredSteps + 2);

        if (backtrackEntry == null)
        {
            logger.LogInformation("[StuckDetector] No valid backtrack position found in breadcrumb trail");
            return;
        }

        logger.LogInformation(
            "[StuckDetector] Breadcrumb backtrack: Walking to {Target} ({Distance}y away)",
            backtrackEntry.Value.Position,
            playerReader.WorldPos.WorldDistanceXYTo(backtrackEntry.Value.Position));

        // Turn toward the backtrack position
        Vector3 targetPos = backtrackEntry.Value.Position;

        // Use map coordinates for heading calculation
        Vector3 playerM = WorldMapAreaDB.ToMap_FlipXY(playerReader.WorldPos, playerReader.WorldMapArea);
        Vector3 targetM = WorldMapAreaDB.ToMap_FlipXY(targetPos, playerReader.WorldMapArea);
        float heading = DirectionCalculator.CalculateMapHeading(playerM, targetM);
        float angleDiff = Math.Abs(heading - playerReader.Direction);
        if (angleDiff > MathF.PI) angleDiff = 2 * MathF.PI - angleDiff;

        // Turn to face the target
        int turnDuration = (int)(angleDiff * 500 / MathF.PI);
        turnDuration = Math.Clamp(turnDuration, 200, 1500);

        if (heading > playerReader.Direction)
            input.PressFixed(input.TurnLeftKey, turnDuration, token);
        else
            input.PressFixed(input.TurnRightKey, turnDuration, token);

        token.WaitHandle.WaitOne(turnDuration + 100);

        // Walk toward the target
        float distance = playerReader.WorldPos.WorldDistanceXYTo(targetPos);
        int walkDuration = (int)(distance * 150); // ~6.7 yards/sec walking speed
        walkDuration = Math.Clamp(walkDuration, 1000, 5000);

        logger.LogInformation("[StuckDetector] Breadcrumb backtrack: Walking for {Duration}ms", walkDuration);
        input.PressFixed(input.ForwardKey, walkDuration, token);

        // Clear the breadcrumb trail since we've backtracked
        breadcrumbTracker.Clear();
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
                MapX = playerReader.MapX,
                MapY = playerReader.MapY,
                MapId = playerReader.MapId,
                UIMapId = playerReader.UIMapId.Value,
                Zone = playerReader.WorldMapArea.AreaName,
                Direction = playerReader.Direction,
                State = currentUnstuckState,
                DurationMs = ActionDurationMs,
                IsSpinning = IsSpinningDetected,
                AttemptCount = unstuckAttemptCount,
                Timestamp = DateTime.UtcNow,
                TriggerReason = lastTriggerReason,
                AdditionalInfo = reason
            };

            OnStuckDetected?.Invoke(eventData);

            logger.LogInformation($"[StuckDetector] DATA COLLECTION - Pos: {eventData.Position}, Dir: {eventData.Direction:F2}, " +
                $"State: {eventData.State}, Duration: {eventData.DurationMs:F0}ms, Spinning: {eventData.IsSpinning}, Trigger: {eventData.TriggerReason}, Reason: {reason}");

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
        if (distance <= prevDistance - GetRangeDiffThreshold())
        {
            if (currentUnstuckState != UnstuckState.None)
            {
                logger.LogInformation($"[StuckDetector] Movement detected during recovery - distance decreased from {prevDistance:F2} to {distance:F2}");
            }
            Reset();
            prevDistance = distance;
            return true;
        }

        return ActionDurationMs < GetActionStuckTimeThreshold();
    }

    public bool IsMoving()
    {
        float distance = playerReader.WorldPos.WorldDistanceXYTo(worldTarget);
        if (MathF.Abs(distance - prevDistance) > GetMinDistanceThreshold())
        {
            if (currentUnstuckState != UnstuckState.None)
            {
                logger.LogInformation($"[StuckDetector] Movement detected during recovery");
            }
            Reset();
            prevDistance = distance;
            return true;
        }

        return ActionDurationMs < GetActionStuckTimeThreshold();
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

    private bool IsPredictiveStuckRiskTriggered()
    {
        if (breadcrumbTracker == null || featureFlagService == null)
        {
            lastPredictiveRisk = null;
            return false;
        }

        FeatureFlagsOptions currentFlags = featureFlagService.Current;
        StuckSensitivityOptions options = currentFlags.StuckSensitivity;

        if (!options.Enabled || !options.EnablePredictiveDetection || breadcrumbTracker.Count < 6)
        {
            lastPredictiveRisk = null;
            return false;
        }

        int threshold = Math.Clamp(options.PredictiveRiskThreshold, 20, 95);
        int risk = breadcrumbTracker.CalculateStuckRisk();
        lastPredictiveRisk = risk;

        // Avoid firing predictive recovery instantly after a reset.
        if (UnstuckMs < GetUnstuckAfterMsThreshold() * 0.5)
        {
            return false;
        }

        return risk >= threshold;
    }

    private UnstuckState GetInitialUnstuckState(Vector3 currentPosition)
    {
        DateTime now = DateTime.UtcNow;
        bool repeatedHotspot =
            lastStuckTriggerUtc != DateTime.MinValue &&
            (now - lastStuckTriggerUtc) <= RepeatHotspotWindow &&
            WorldDistanceXY(currentPosition, lastStuckTriggerPosition) <= RepeatHotspotRadius;

        repeatedHotspotCount = repeatedHotspot ? repeatedHotspotCount + 1 : 0;
        lastStuckTriggerPosition = currentPosition;
        lastStuckTriggerUtc = now;

        if (repeatedHotspotCount >= 2 && IsEnhancedRecoveryAvailable)
        {
            return UnstuckState.BreadcrumbBacktrack;
        }

        if (repeatedHotspotCount >= 1)
        {
            return UnstuckState.StrafeAttempt;
        }

        return UnstuckState.InitialAttempt;
    }

    private static float WorldDistanceXY(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private float GetRangeDiffThreshold()
    {
        if (featureFlagService == null)
        {
            return DEFAULT_MIN_RANGE_DIFF;
        }

        StuckSensitivityOptions options = featureFlagService.Current.StuckSensitivity;
        if (!options.Enabled)
        {
            return DEFAULT_MIN_RANGE_DIFF;
        }

        float minDistance = Math.Clamp(options.MinDistance, 0.05f, 1.5f);
        return Math.Clamp(minDistance * 6f, 0.4f, 2.5f);
    }

    private float GetMinDistanceThreshold()
    {
        if (featureFlagService == null)
        {
            return DEFAULT_MIN_DISTANCE;
        }

        StuckSensitivityOptions options = featureFlagService.Current.StuckSensitivity;
        if (!options.Enabled)
        {
            return DEFAULT_MIN_DISTANCE;
        }

        return Math.Clamp(options.MinDistance, 0.05f, 1.5f);
    }

    private double GetUnstuckAfterMsThreshold()
    {
        if (featureFlagService == null)
        {
            return DEFAULT_UNSTUCK_AFTER_MS;
        }

        StuckSensitivityOptions options = featureFlagService.Current.StuckSensitivity;
        if (!options.Enabled)
        {
            return DEFAULT_UNSTUCK_AFTER_MS;
        }

        return Math.Clamp(options.UnstuckAfterMs, 750, 10_000);
    }

    private double GetActionStuckTimeThreshold()
    {
        if (featureFlagService == null)
        {
            return DEFAULT_ACTION_STUCK_TIME;
        }

        StuckSensitivityOptions options = featureFlagService.Current.StuckSensitivity;
        if (!options.Enabled)
        {
            return DEFAULT_ACTION_STUCK_TIME;
        }

        double baseWindow = Math.Clamp(options.UnstuckAfterMs + 1200, 1_500, 15_000);
        float multiplier = Math.Clamp(options.ApproachTimeoutMultiplier, 0.5f, 3f);
        return baseWindow * multiplier;
    }

    private int GetBacktrackSteps()
    {
        if (featureFlagService == null)
        {
            return 3;
        }

        return Math.Clamp(featureFlagService.Current.StuckRecoveryV2.BacktrackSteps, 1, 12);
    }

    public StuckDetectorRuntimeSnapshot GetRuntimeSnapshot()
    {
        return new StuckDetectorRuntimeSnapshot(
            CurrentState: currentUnstuckState,
            IsCurrentlyStuck: IsCurrentlyStuck,
            ActionDurationMs: ActionDurationMs,
            UnstuckMs: CurrentUnstuckDurationMs,
            IsSpinningDetected: IsSpinningDetected,
            RangeDiffThreshold: GetRangeDiffThreshold(),
            MinDistanceThreshold: GetMinDistanceThreshold(),
            UnstuckAfterMsThreshold: GetUnstuckAfterMsThreshold(),
            ActionStuckTimeThreshold: GetActionStuckTimeThreshold(),
            LastTriggerReason: lastTriggerReason,
            LastTriggerUtc: lastTriggerUtc,
            LastPredictiveRisk: lastPredictiveRisk,
            RecentTriggers: recentTriggers.ToArray());
    }
}
