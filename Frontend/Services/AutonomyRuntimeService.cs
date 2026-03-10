using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Core.Autonomy;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Frontend.Services;

public sealed class AutonomyRuntimeService
{
    public const string DefaultSupervisorId = "autonomy-live-prep-warlock";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<AutonomyRuntimeService> logger;
    private readonly IHostEnvironment env;

    public AutonomyRuntimeService(ILogger<AutonomyRuntimeService> logger, IHostEnvironment env)
    {
        this.logger = logger;
        this.env = env;
    }

    public string GetSupervisorRoot(string supervisorId)
    {
        return Path.Combine(
            env.ContentRootPath,
            "logs",
            "autonomous-supervisor",
            string.IsNullOrWhiteSpace(supervisorId) ? DefaultSupervisorId : supervisorId);
    }

    public string GetControlRoot(string supervisorId)
    {
        return Path.Combine(GetSupervisorRoot(supervisorId), "control");
    }

    public AutonomyRunState GetRunState(string supervisorId = DefaultSupervisorId)
    {
        string supervisorRoot = GetSupervisorRoot(supervisorId);
        AutonomyRunState state = ReadJson<AutonomyRunState>(Path.Combine(supervisorRoot, "state.json")) ?? new AutonomyRunState();
        state.SupervisorId = string.IsNullOrWhiteSpace(state.SupervisorId) ? supervisorId : state.SupervisorId;

        KillSwitchState? persistedKillSwitch = ReadJson<KillSwitchState>(Path.Combine(GetControlRoot(supervisorId), "kill-switch.json"));
        if (persistedKillSwitch != null)
        {
            state.KillSwitchState = persistedKillSwitch;
        }

        LiveWindowState? persistedLiveWindow = ReadJson<LiveWindowState>(Path.Combine(GetControlRoot(supervisorId), "live-window.json"));
        if (persistedLiveWindow != null)
        {
            state.LiveWindowState = persistedLiveWindow;
        }

        return state;
    }

    public IReadOnlyList<AutonomyIncident> GetIncidents(string supervisorId = DefaultSupervisorId)
    {
        string supervisorRoot = GetSupervisorRoot(supervisorId);
        return ReadJson<List<AutonomyIncident>>(Path.Combine(supervisorRoot, "incidents-latest.json")) ??
            [];
    }

    public IReadOnlyList<AutonomyRunSummary> GetRuns(string supervisorId = DefaultSupervisorId, int limit = 10)
    {
        string supervisorRoot = GetSupervisorRoot(supervisorId);
        List<AutonomyRunSummary>? runs = ReadJson<List<AutonomyRunSummary>>(Path.Combine(supervisorRoot, "runs-latest.json"));
        if (runs != null)
        {
            return runs.Take(Math.Max(limit, 1)).ToList();
        }

        return GetRunState(supervisorId).RecentRuns.Take(Math.Max(limit, 1)).ToList();
    }

    public IReadOnlyList<ArtifactRef> GetArtifacts(string supervisorId, string incidentId)
    {
        AutonomyIncident? incident = GetIncidents(supervisorId)
            .FirstOrDefault(i => string.Equals(i.Id, incidentId, StringComparison.OrdinalIgnoreCase));

        return incident?.Artifacts ?? [];
    }

    public JsonElement? GetLatestStatus(string supervisorId = DefaultSupervisorId)
    {
        string path = Path.Combine(GetSupervisorRoot(supervisorId), "status-latest.json");
        if (!File.Exists(path))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    public (bool PauseRequested, bool StopRequested, KillSwitchState KillSwitchState, LiveWindowState LiveWindowState) ApplyControl(
        string supervisorId,
        string command,
        string? reason,
        string? source)
    {
        string controlRoot = GetControlRoot(supervisorId);
        Directory.CreateDirectory(controlRoot);

        string pausePath = Path.Combine(controlRoot, "pause.flag");
        string stopPath = Path.Combine(controlRoot, "stop.flag");
        string killSwitchPath = Path.Combine(controlRoot, "kill-switch.json");
        string liveWindowPath = Path.Combine(controlRoot, "live-window.json");

        string normalized = command.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "pause":
                File.WriteAllText(pausePath, DateTimeOffset.UtcNow.ToString("O"));
                break;

            case "resume":
                DeleteIfExists(pausePath);
                break;

            case "stop":
                File.WriteAllText(stopPath, DateTimeOffset.UtcNow.ToString("O"));
                break;

            case "clearstop":
            case "acknowledgestop":
                DeleteIfExists(stopPath);
                break;

            case "enablekillswitch":
                WriteJson(killSwitchPath, new KillSwitchState
                {
                    Enabled = true,
                    Reason = reason ?? "API request",
                    Source = source ?? "api",
                    UpdatedUtc = DateTimeOffset.UtcNow
                });
                break;

            case "disablekillswitch":
                WriteJson(killSwitchPath, new KillSwitchState
                {
                    Enabled = false,
                    Reason = reason ?? "API request",
                    Source = source ?? "api",
                    UpdatedUtc = DateTimeOffset.UtcNow
                });
                break;

            case "enablelivewindow":
            case "armlivewindow":
                WriteJson(liveWindowPath, new LiveWindowState
                {
                    Enabled = true,
                    Reason = reason ?? "Operator armed live window.",
                    Source = source ?? "api",
                    UpdatedUtc = DateTimeOffset.UtcNow
                });
                break;

            case "disablelivewindow":
            case "disarmlivewindow":
                WriteJson(liveWindowPath, new LiveWindowState
                {
                    Enabled = false,
                    Reason = reason ?? "Operator disarmed live window.",
                    Source = source ?? "api",
                    UpdatedUtc = DateTimeOffset.UtcNow
                });
                break;

            case "resetprep":
            case "resetpreplane":
                DeleteIfExists(pausePath);
                DeleteIfExists(stopPath);
                WriteJson(killSwitchPath, new KillSwitchState
                {
                    Enabled = false,
                    Reason = reason ?? "Prep lane reset.",
                    Source = source ?? "api",
                    UpdatedUtc = DateTimeOffset.UtcNow
                });
                WriteJson(liveWindowPath, new LiveWindowState
                {
                    Enabled = false,
                    Reason = reason ?? "Prep lane reset.",
                    Source = source ?? "api",
                    UpdatedUtc = DateTimeOffset.UtcNow
                });
                break;

            default:
                throw new InvalidOperationException($"Unsupported autonomy control command '{command}'.");
        }

        KillSwitchState killSwitchState = ReadJson<KillSwitchState>(killSwitchPath) ?? new KillSwitchState
        {
            Enabled = false,
            Reason = string.Empty,
            Source = string.Empty
        };

        LiveWindowState liveWindowState = ReadJson<LiveWindowState>(liveWindowPath) ?? new LiveWindowState
        {
            Enabled = false,
            Reason = string.Empty,
            Source = string.Empty
        };

        return (
            PauseRequested: File.Exists(pausePath),
            StopRequested: File.Exists(stopPath),
            KillSwitchState: killSwitchState,
            LiveWindowState: liveWindowState);
    }

    private static T? ReadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static void WriteJson<T>(string path, T value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
