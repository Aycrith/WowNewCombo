using System;
using System.Collections.Generic;

namespace Core.Startup;

/// <summary>
/// Represents the stages of the startup orchestration process.
/// </summary>
public enum StartupStage
{
    /// <summary>Startup has not begun.</summary>
    NotStarted = 0,

    /// <summary>Initializing orchestrator and loading configuration.</summary>
    Initializing = 1,

    /// <summary>Discovering WoW installation path.</summary>
    DiscoveringWoW = 2,

    /// <summary>Validating and installing required addons.</summary>
    ValidatingAddons = 3,

    /// <summary>Starting the navigation server.</summary>
    StartingNavigationServer = 4,

    /// <summary>Launching or detecting WoW process.</summary>
    LaunchingWoW = 5,

    /// <summary>Waiting for user to log in and enter world.</summary>
    WaitingForCharacter = 6,

    /// <summary>Configuring pixel reading frames.</summary>
    ConfiguringFrames = 7,

    /// <summary>Performing final validation.</summary>
    FinalValidation = 8,

    /// <summary>Startup complete and ready.</summary>
    Ready = 9,

    /// <summary>Startup failed.</summary>
    Failed = -1
}

/// <summary>
/// Result of executing a single startup stage.
/// </summary>
public enum StageResultType
{
    /// <summary>Stage completed successfully.</summary>
    Success,

    /// <summary>Stage was skipped (not needed or disabled).</summary>
    Skipped,

    /// <summary>Stage completed with warnings but can continue.</summary>
    Warning,

    /// <summary>Stage is waiting for external condition (e.g., user action).</summary>
    Waiting,

    /// <summary>Stage failed but can be retried.</summary>
    Retry,

    /// <summary>Stage failed fatally - cannot continue.</summary>
    Failed
}

/// <summary>
/// Result from executing a startup stage.
/// </summary>
public sealed class StageResult
{
    public StageResultType Type { get; }
    public string Message { get; }
    public Exception? Exception { get; }

    private StageResult(StageResultType type, string message, Exception? exception = null)
    {
        Type = type;
        Message = message;
        Exception = exception;
    }

    public bool IsSuccess => Type == StageResultType.Success || Type == StageResultType.Skipped;
    public bool CanContinue => Type != StageResultType.Failed;
    public bool ShouldRetry => Type == StageResultType.Retry;
    public bool IsWaiting => Type == StageResultType.Waiting;

    public static StageResult Success(string message = "Completed successfully")
        => new(StageResultType.Success, message);

    public static StageResult Skipped(string message = "Skipped")
        => new(StageResultType.Skipped, message);

    public static StageResult Warning(string message)
        => new(StageResultType.Warning, message);

    public static StageResult Waiting(string message)
        => new(StageResultType.Waiting, message);

    public static StageResult Retry(string message)
        => new(StageResultType.Retry, message);

    public static StageResult Failed(string message, Exception? ex = null)
        => new(StageResultType.Failed, message, ex);

    public override string ToString() => $"[{Type}] {Message}";
}

/// <summary>
/// Event args for stage completion events.
/// </summary>
public sealed class StageCompletedEventArgs : EventArgs
{
    public StartupStage Stage { get; }
    public StageResult Result { get; }
    public TimeSpan Duration { get; }

    public StageCompletedEventArgs(StartupStage stage, StageResult result, TimeSpan duration)
    {
        Stage = stage;
        Result = result;
        Duration = duration;
    }
}

/// <summary>
/// Final result of the complete startup orchestration.
/// </summary>
public sealed class StartupResult
{
    public bool IsSuccess { get; }
    public StartupStage FinalStage { get; }
    public string Message { get; }
    public TimeSpan TotalDuration { get; }
    public IReadOnlyList<(StartupStage Stage, StageResult Result)> StageResults { get; }

    public StartupResult(
        bool isSuccess,
        StartupStage finalStage,
        string message,
        TimeSpan totalDuration,
        IReadOnlyList<(StartupStage, StageResult)> stageResults)
    {
        IsSuccess = isSuccess;
        FinalStage = finalStage;
        Message = message;
        TotalDuration = totalDuration;
        StageResults = stageResults;
    }

    public static StartupResult CreateSuccess(
        TimeSpan duration,
        IReadOnlyList<(StartupStage, StageResult)> stageResults)
        => new(true, StartupStage.Ready, "Startup completed successfully", duration, stageResults);

    public static StartupResult CreateFailure(
        StartupStage failedStage,
        string message,
        TimeSpan duration,
        IReadOnlyList<(StartupStage, StageResult)> stageResults)
        => new(false, failedStage, message, duration, stageResults);
}
