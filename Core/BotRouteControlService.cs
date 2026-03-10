using Core.Goals;
using Core.GOAP;
using Core.Launch;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Core;

public sealed record BotRouteSlotState(
    int Index,
    int PathId,
    string DefaultPathFileName,
    string? OverridePathFileName,
    string EffectivePathFileName,
    bool RequirementsMet,
    bool CanRun,
    bool IsActiveRoute,
    bool PathThereAndBack,
    int ExpectedUiMapId,
    string ExpectedAreaName,
    IReadOnlyList<string> Requirements);

public sealed record BotRouteControlState(
    bool RouteControlAvailable,
    string RuntimeMode,
    bool BotActive,
    string? ProfileName,
    string? CurrentGoal,
    int? ActiveRouteIndex,
    string? ActiveRouteFileName,
    IReadOnlyList<BotRouteSlotState> RouteSlots,
    IReadOnlyList<string> AvailablePathFiles);

public sealed record BotRouteCommandRequest(
    int TargetIndex,
    string? FileName,
    bool ClearOverride,
    bool StopBotFirst,
    bool ResumeBotAfterSwitch);

public sealed record BotRouteCommandResult(
    bool Success,
    string Message,
    bool BotWasActive,
    bool BotWasStopped,
    bool RouteApplied,
    bool ResumeRequested,
    bool ResumeSucceeded,
    string? ResumeBlockedReason,
    BotRouteControlState State);

public interface IBotRouteControlService
{
    BotRouteControlState GetState();

    BotRouteCommandResult Apply(BotRouteCommandRequest request);
}

public sealed class BotRouteControlService : IBotRouteControlService
{
    private readonly ILogger<BotRouteControlService> logger;
    private readonly IBotController botController;
    private readonly IBotStartGuard botStartGuard;

    public BotRouteControlService(
        ILogger<BotRouteControlService> logger,
        IBotController botController,
        IBotStartGuard botStartGuard)
    {
        this.logger = logger;
        this.botController = botController;
        this.botStartGuard = botStartGuard;
    }

    public BotRouteControlState GetState()
    {
        string runtimeMode = BotRuntimeModeHelper.GetRuntimeMode(botController);
        string? currentGoal = GetGoalLabel(botController.GoapAgent?.CurrentGoal);
        string? profileName = string.IsNullOrWhiteSpace(botController.SelectedClassFilename)
            ? null
            : botController.SelectedClassFilename;
        string[] availablePathFiles = botController.PathFiles()
            .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ClassConfiguration? classConfig = botController.ClassConfig;
        if (classConfig == null || classConfig.Paths.Length == 0)
        {
            return new BotRouteControlState(
                RouteControlAvailable: false,
                RuntimeMode: runtimeMode,
                BotActive: botController.IsBotActive,
                ProfileName: profileName,
                CurrentGoal: currentGoal,
                ActiveRouteIndex: null,
                ActiveRouteFileName: null,
                RouteSlots: Array.Empty<BotRouteSlotState>(),
                AvailablePathFiles: availablePathFiles);
        }

        List<BotRouteSlotState> slots = new(classConfig.Paths.Length);
        int? activeRouteIndex = null;

        for (int i = 0; i < classConfig.Paths.Length; i++)
        {
            PathSettings path = classConfig.Paths[i];
            bool requirementsMet = SafeCanRunRequirementsOnly(path);
            bool canRun = SafeCanRun(path);

            if (activeRouteIndex == null && canRun)
            {
                activeRouteIndex = i;
            }

            slots.Add(new BotRouteSlotState(
                Index: i,
                PathId: path.Id,
                DefaultPathFileName: path.PathFilename,
                OverridePathFileName: NormalizeOverride(path.OverridePathFilename),
                EffectivePathFileName: path.FileName,
                RequirementsMet: requirementsMet,
                CanRun: canRun,
                IsActiveRoute: false,
                PathThereAndBack: path.PathThereAndBack,
                ExpectedUiMapId: path.EffectiveExpectedUIMapId,
                ExpectedAreaName: path.ExpectedAreaName,
                Requirements: path.Requirements.ToArray()));
        }

        if (activeRouteIndex == null && botController.SelectedPathFilename.Count > 0)
        {
            activeRouteIndex = botController.SelectedPathFilename.Keys
                .Where(index => index >= 0 && index < slots.Count)
                .OrderBy(static index => index)
                .Cast<int?>()
                .FirstOrDefault();
        }

        if (activeRouteIndex == null && slots.Count > 0)
        {
            activeRouteIndex = 0;
        }

        if (activeRouteIndex != null)
        {
            BotRouteSlotState slot = slots[activeRouteIndex.Value];
            slots[activeRouteIndex.Value] = slot with { IsActiveRoute = true };
        }

        string? activeRouteFileName = activeRouteIndex != null
            ? slots[activeRouteIndex.Value].EffectivePathFileName
            : null;

        return new BotRouteControlState(
            RouteControlAvailable: true,
            RuntimeMode: runtimeMode,
            BotActive: botController.IsBotActive,
            ProfileName: profileName,
            CurrentGoal: currentGoal,
            ActiveRouteIndex: activeRouteIndex,
            ActiveRouteFileName: activeRouteFileName,
            RouteSlots: slots,
            AvailablePathFiles: availablePathFiles);
    }

    public BotRouteCommandResult Apply(BotRouteCommandRequest request)
    {
        BotRouteControlState stateBefore = GetState();
        if (!stateBefore.RouteControlAvailable)
        {
            return Failure("Load a class profile with at least one route before switching goals.", request, stateBefore);
        }

        if (request.TargetIndex < 0 || request.TargetIndex >= stateBefore.RouteSlots.Count)
        {
            return Failure($"Route slot {request.TargetIndex} is out of range.", request, stateBefore);
        }

        string? normalizedFileName = NormalizeFileName(request.FileName);
        if (!request.ClearOverride && string.IsNullOrWhiteSpace(normalizedFileName))
        {
            return Failure("A route file is required when ClearOverride is false.", request, stateBefore);
        }

        if (!request.ClearOverride &&
            !stateBefore.AvailablePathFiles.Any(file => string.Equals(file, normalizedFileName, StringComparison.OrdinalIgnoreCase)))
        {
            return Failure($"Route file '{normalizedFileName}' was not found.", request, stateBefore);
        }

        bool botWasActive = botController.IsBotActive;
        bool botWasStopped = false;

        if (botWasActive)
        {
            if (!request.StopBotFirst)
            {
                return Failure("Bot is active. Stop it first or set StopBotFirst=true before switching routes.", request, stateBefore, botWasActive);
            }

            botController.ToggleBotStatus("RouteControlSwitchStop");
            botWasStopped = true;

            if (botController.IsBotActive)
            {
                BotRouteControlState failedStopState = GetState();
                return new BotRouteCommandResult(
                    Success: false,
                    Message: "Bot stop request did not deactivate the active session.",
                    BotWasActive: botWasActive,
                    BotWasStopped: false,
                    RouteApplied: false,
                    ResumeRequested: request.ResumeBotAfterSwitch,
                    ResumeSucceeded: false,
                    ResumeBlockedReason: null,
                    State: failedStopState);
            }
        }

        BotRouteSlotState targetSlot = stateBefore.RouteSlots[request.TargetIndex];
        bool useProfileDefault = request.ClearOverride ||
            string.Equals(normalizedFileName, targetSlot.DefaultPathFileName, StringComparison.OrdinalIgnoreCase);

        Dictionary<int, string> overrides = CloneOverrides(botController.SelectedPathFilename);
        if (useProfileDefault)
        {
            overrides.Remove(request.TargetIndex);
        }
        else
        {
            overrides[request.TargetIndex] = normalizedFileName!;
        }

        botController.LoadPathProfile(overrides);

        BotRouteControlState stateAfterApply = GetState();
        string expectedEffectivePath = useProfileDefault ? targetSlot.DefaultPathFileName : normalizedFileName!;
        bool routeApplied = stateAfterApply.RouteSlots.Count > request.TargetIndex &&
            string.Equals(
                stateAfterApply.RouteSlots[request.TargetIndex].EffectivePathFileName,
                expectedEffectivePath,
                StringComparison.OrdinalIgnoreCase);

        if (!routeApplied)
        {
            logger.LogWarning(
                "[BotRouteControl  ] Route switch rejected for slot {SlotIndex}. Requested={RequestedFile} Default={DefaultFile}",
                request.TargetIndex,
                normalizedFileName ?? "<default>",
                targetSlot.DefaultPathFileName);

            return new BotRouteCommandResult(
                Success: false,
                Message: "The selected route was not accepted by the current profile.",
                BotWasActive: botWasActive,
                BotWasStopped: botWasStopped,
                RouteApplied: false,
                ResumeRequested: request.ResumeBotAfterSwitch,
                ResumeSucceeded: false,
                ResumeBlockedReason: null,
                State: stateAfterApply);
        }

        bool resumeSucceeded = false;
        string? resumeBlockedReason = null;
        BotRouteControlState finalState = stateAfterApply;

        if (request.ResumeBotAfterSwitch)
        {
            LaunchReadinessSnapshot readiness = botStartGuard.Evaluate(botController.ClassConfig, botController.RouteInfo);
            if (!readiness.CanStartBot)
            {
                resumeBlockedReason = BuildBlockingReason(readiness);
            }
            else
            {
                botController.ToggleBotStatus("RouteControlSwitchResume");
                resumeSucceeded = botController.IsBotActive;
                finalState = GetState();

                if (!resumeSucceeded)
                {
                    resumeBlockedReason = string.IsNullOrWhiteSpace(botController.LastDeactivateReason)
                        ? "Bot did not resume after the route switch."
                        : botController.LastDeactivateReason;
                }
            }
        }

        string message = BuildResultMessage(
            useProfileDefault,
            request.TargetIndex,
            expectedEffectivePath,
            botWasStopped,
            request.ResumeBotAfterSwitch,
            resumeSucceeded,
            resumeBlockedReason);

        return new BotRouteCommandResult(
            Success: true,
            Message: message,
            BotWasActive: botWasActive,
            BotWasStopped: botWasStopped,
            RouteApplied: true,
            ResumeRequested: request.ResumeBotAfterSwitch,
            ResumeSucceeded: resumeSucceeded,
            ResumeBlockedReason: resumeBlockedReason,
            State: finalState);
    }

    private static string BuildResultMessage(
        bool useProfileDefault,
        int targetIndex,
        string effectivePath,
        bool botWasStopped,
        bool resumeRequested,
        bool resumeSucceeded,
        string? resumeBlockedReason)
    {
        string action = useProfileDefault
            ? $"Route slot {targetIndex} reverted to the profile default '{effectivePath}'."
            : $"Route slot {targetIndex} switched to '{effectivePath}'.";

        if (resumeRequested)
        {
            if (resumeSucceeded)
            {
                return $"{action} Bot resumed on the new route.";
            }

            return string.IsNullOrWhiteSpace(resumeBlockedReason)
                ? $"{action} Bot remained stopped after the switch."
                : $"{action} Bot remained stopped: {resumeBlockedReason}";
        }

        return botWasStopped
            ? $"{action} Bot remains stopped."
            : action;
    }

    private static BotRouteCommandResult Failure(
        string message,
        BotRouteCommandRequest request,
        BotRouteControlState state,
        bool botWasActive = false)
    {
        return new BotRouteCommandResult(
            Success: false,
            Message: message,
            BotWasActive: botWasActive,
            BotWasStopped: false,
            RouteApplied: false,
            ResumeRequested: request.ResumeBotAfterSwitch,
            ResumeSucceeded: false,
            ResumeBlockedReason: null,
            State: state);
    }

    private static Dictionary<int, string> CloneOverrides(Dictionary<int, string> overrides)
    {
        Dictionary<int, string> clone = new(overrides.Count);
        foreach (KeyValuePair<int, string> pair in overrides)
        {
            clone[pair.Key] = pair.Value;
        }

        return clone;
    }

    private static string? NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return fileName.Trim();
    }

    private static string? NormalizeOverride(string? overridePath)
    {
        return string.IsNullOrWhiteSpace(overridePath) ? null : overridePath;
    }

    private static string? GetGoalLabel(GoapGoal? goal)
    {
        if (goal == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(goal.DisplayName))
        {
            return goal.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(goal.Name))
        {
            return goal.Name;
        }

        return goal.GetType().Name;
    }

    private static string BuildBlockingReason(LaunchReadinessSnapshot readiness)
    {
        string blocking = string.Join(" | ",
            readiness.Checks
                .Where(static check => check.IsBlocking)
                .Select(static check => $"{check.Title}: {check.Message}"));

        return string.IsNullOrWhiteSpace(blocking)
            ? "Launch readiness blocked the resume request."
            : blocking;
    }

    private static bool SafeCanRun(PathSettings path)
    {
        try
        {
            return path.CanRun();
        }
        catch
        {
            return false;
        }
    }

    private static bool SafeCanRunRequirementsOnly(PathSettings path)
    {
        try
        {
            return path.CanRunRequirementsOnly();
        }
        catch
        {
            return false;
        }
    }
}
