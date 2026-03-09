using Core;
using Core.Addon;
using Core.GOAP;
using Core.Goals;
using Core.Launch;

using Game;

using Microsoft.Extensions.Logging.Abstractions;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FrontendUnitTests.Controllers;

internal sealed class FakeBotController : IBotController
{
    public bool IsBotActive { get; set; }
    public string? LastDeactivateReason { get; set; }
    public DateTime? LastDeactivateUtc { get; set; }
    public string SelectedClassFilename { get; set; } = string.Empty;
    public string? LoadClassProfileAppliedSelection { get; set; }
    public ClassConfiguration? LoadClassProfileAppliedConfig { get; set; }
    public Exception? LoadClassProfileException { get; set; }
    public bool LoadClassProfileLeavesNullConfig { get; set; }
    public Dictionary<int, string> SelectedPathFilename { get; set; } = [];
    public ClassConfiguration? ClassConfig { get; set; }
    public GoapAgent? GoapAgent { get; set; }
    public RouteInfo? RouteInfo { get; set; }
    public double AvgScreenLatency { get; set; }
    public double AvgNPCLatency { get; set; }
    public IEnumerable<string> ClassFileList { get; set; } = Array.Empty<string>();
    public IEnumerable<string> PathFileList { get; set; } = Array.Empty<string>();

    public event Action? ProfileLoaded;
    public event Action? StatusChanged;

    public IEnumerable<string> ClassFiles() => ClassFileList;

    public IEnumerable<string> PathFiles() => PathFileList;

    public void LoadClassProfile(string classFilename)
    {
        if (LoadClassProfileException != null)
        {
            throw LoadClassProfileException;
        }

        SelectedClassFilename = LoadClassProfileAppliedSelection ?? classFilename;
        if (LoadClassProfileAppliedConfig != null)
        {
            ClassConfig = LoadClassProfileAppliedConfig;
        }
        else if (LoadClassProfileLeavesNullConfig)
        {
            ClassConfig = null;
        }
        else
        {
            ClassConfig = new ClassConfiguration
            {
                FileName = SelectedClassFilename
            };
        }

        ProfileLoaded?.Invoke();
    }

    public void LoadPathProfile(Dictionary<int, string> pathFilenames)
    {
        SelectedPathFilename = pathFilenames;
        ProfileLoaded?.Invoke();
    }

    public void MinimapNodeFound()
    {
    }

    public void OverrideClassConfig(ClassConfiguration classConfig)
    {
        ClassConfig = classConfig;
    }

    public void RecordDeactivateReason(string reason)
    {
        LastDeactivateReason = reason;
        LastDeactivateUtc = DateTime.UtcNow;
    }

    public ClassConfiguration ResolveLoadedProfile() => ClassConfig ?? new ClassConfiguration();

    public void SaveClassConfig()
    {
    }

    public void Shutdown()
    {
    }

    public void ToggleBotStatus(string? reason = null)
    {
        IsBotActive = !IsBotActive;
        if (!IsBotActive && !string.IsNullOrWhiteSpace(reason))
        {
            RecordDeactivateReason(reason);
        }

        StatusChanged?.Invoke();
    }
}

internal sealed class FakeBotStartGuard : IBotStartGuard
{
    public LaunchReadinessSnapshot Snapshot { get; set; } = new(
        IsLaunchReady: true,
        CanStartBot: true,
        TimestampUtc: DateTimeOffset.UtcNow,
        Checks: Array.Empty<LaunchSubsystemCheck>(),
        Overrides: new LaunchOverrideSnapshot(false, false, new Dictionary<LaunchSubsystem, LaunchSubsystemBypass>()));

    public LaunchReadinessSnapshot Evaluate(ClassConfiguration? classConfig, RouteInfo? routeInfo) => Snapshot;
}

internal sealed class TestGoal(string name = nameof(TestGoal)) : GoapGoal(name)
{
    public override float Cost => 1f;
}

internal sealed class TestRouteProviderGoal(string name, Vector3[] mapRoute, DateTime? lastActive = null) : GoapGoal(name), IRouteProvider
{
    private readonly Vector3[] mapRoute = mapRoute;

    public override float Cost => 1f;

    public DateTime LastActive { get; } = lastActive ?? DateTime.UtcNow;

    public Vector3[] MapRoute() => mapRoute;

    public Vector3[] PathingRoute() => mapRoute;

    public bool HasNext() => mapRoute.Length > 0;

    public Vector3 NextMapPoint() => mapRoute.Length > 0 ? mapRoute[0] : Vector3.Zero;
}

internal sealed class FakeAddonReader : IAddonReader
{
    public double AvgUpdateLatency => 0;
    public string TargetName => string.Empty;
    public ManualResetEventSlim DataReady { get; } = new(false);
    public event Action? AddonDataChanged;

    public void FullReset()
    {
    }

    public void SessionReset()
    {
    }

    public void Update()
    {
        DataReady.Set();
    }

    public void UpdateUI()
    {
        AddonDataChanged?.Invoke();
    }
}

internal static class TestGoapAgentFactory
{
    public static GoapAgent Create(
        SessionStat sessionStat,
        bool active,
        GoapGoal? currentGoal = null,
        GoapGoal[]? availableGoals = null,
        BitVector32? worldState = null)
    {
        GoapAgent agent = (GoapAgent)RuntimeHelpers.GetUninitializedObject(typeof(GoapAgent));

        SetField(agent, "active", active);
        SetAutoProperty(agent, "SessionStat", sessionStat);
        SetAutoProperty(agent, "CurrentGoal", currentGoal);
        SetAutoProperty(agent, "AvailableGoals", availableGoals ?? Array.Empty<GoapGoal>());
        SetAutoProperty(agent, "Plan", new Stack<GoapGoal>());
        SetAutoProperty(agent, "WorldState", worldState ?? new BitVector32());
        SetAutoProperty(agent, "State", new GoapAgentState());

        return agent;
    }

    private static void SetAutoProperty(object target, string propertyName, object? value)
    {
        FieldInfo? field = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException($"Backing field for property '{propertyName}' was not found.");
        }

        field.SetValue(target, value);
    }

    private static void SetField(object target, string fieldName, object? value)
    {
        FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        }

        field.SetValue(target, value);
    }
}

internal static class TestSessionStatFactory
{
    public static SessionStat Create(int kills, int deaths, TimeSpan uptime)
    {
        SessionStat stats = new()
        {
            Kills = kills,
            Deaths = deaths,
            StartTime = Stopwatch.GetTimestamp() - (long)(uptime.TotalSeconds * Stopwatch.Frequency)
        };

        return stats;
    }
}

internal static class TestConfigBotControllerFactory
{
    public static ConfigBotController Create(CancellationTokenSource cts)
    {
        return new ConfigBotController(
            NullLogger.Instance,
            new FakeAddonReader(),
            new NullWowScreen(),
            cts);
    }
}
