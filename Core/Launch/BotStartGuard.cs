using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SixLabors.ImageSharp;

using Game;
using SharedLib;

using WinAPI;

namespace Core.Launch;

public interface IBotStartGuard
{
    LaunchReadinessSnapshot Evaluate(ClassConfiguration? classConfig, RouteInfo? routeInfo);
}

public sealed class BotStartGuard : IBotStartGuard
{
    private readonly ILogger<BotStartGuard> logger;
    private readonly LaunchOptions options;

    private readonly LaunchOverrideState overrides;

    private readonly WowProcess wowProcess;
    private readonly AddonValidator addonValidator;
    private readonly AddonConfigurator addonConfigurator;
    private readonly IAddonReader addonReader;

    private readonly KeyBindingsReader keyBindingsReader;
    private readonly ActionBarTextureReader actionBarTextureReader;
    private readonly ActionBarSlotValidator actionBarSlotValidator;

    private readonly IPPather pather;
    private readonly DataConfig dataConfig;

    private readonly object cacheLock = new();
    private DateTimeOffset lastAddonValidationUtc = DateTimeOffset.MinValue;
    private AddonValidationResult? cachedAddonValidation;

    public BotStartGuard(
        ILogger<BotStartGuard> logger,
        IOptions<LaunchOptions> options,
        LaunchOverrideState overrides,
        WowProcess wowProcess,
        AddonValidator addonValidator,
        AddonConfigurator addonConfigurator,
        IAddonReader addonReader,
        KeyBindingsReader keyBindingsReader,
        ActionBarTextureReader actionBarTextureReader,
        ActionBarSlotValidator actionBarSlotValidator,
        IPPather pather,
        DataConfig dataConfig)
    {
        this.logger = logger;
        this.options = options.Value;
        this.overrides = overrides;
        this.wowProcess = wowProcess;
        this.addonValidator = addonValidator;
        this.addonConfigurator = addonConfigurator;
        this.addonReader = addonReader;
        this.keyBindingsReader = keyBindingsReader;
        this.actionBarTextureReader = actionBarTextureReader;
        this.actionBarSlotValidator = actionBarSlotValidator;
        this.pather = pather;
        this.dataConfig = dataConfig;
    }

    public LaunchReadinessSnapshot Evaluate(ClassConfiguration? classConfig, RouteInfo? routeInfo)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        LaunchOverrideSnapshot overrideSnapshot = new(
            overrides.AllowStartWithWarnings,
            overrides.SkipNavigationChecks,
            overrides.SkipKeybindingChecks,
            overrides.SkipActionBarChecks);

        List<LaunchSubsystemCheck> checks =
        [
            CheckNavigation(now, overrideSnapshot),
            CheckWoW(now),
            CheckAddons(now),
            CheckFrames(now),
            CheckAddonHandshake(now),
            CheckProfile(now, classConfig),
            CheckRoute(now, routeInfo),
            CheckKeyBindings(now, classConfig, overrideSnapshot),
            CheckActionBar(now, classConfig, overrideSnapshot)
        ];

        bool hasBlocking = checks.Any(c => c.IsBlocking);

        bool strictReady = checks
            .Where(c => c.IsRequired)
            .All(c => c.Status is LaunchStatus.Ok or LaunchStatus.Skipped);

        bool canStart = !hasBlocking;

        return new LaunchReadinessSnapshot(
            IsLaunchReady: strictReady,
            CanStartBot: canStart,
            TimestampUtc: now,
            Checks: checks,
            Overrides: overrideSnapshot);
    }

    private LaunchSubsystemCheck CheckNavigation(DateTimeOffset now, LaunchOverrideSnapshot overrideSnapshot)
    {
        bool required = !overrideSnapshot.SkipNavigationChecks;

        if (!required)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.Navigation,
                LaunchStatus.Skipped,
                "Navigation",
                "Skipped by override",
                IsRequired: false,
                IsBlocking: false,
                TimestampUtc: now);
        }

        try
        {
            if (pather is RemotePathingAPIV3 remoteV3)
            {
                bool ok = remoteV3.PingServer();
                return new LaunchSubsystemCheck(
                    LaunchSubsystem.Navigation,
                    ok ? LaunchStatus.Ok : LaunchStatus.Error,
                    "Navigation",
                    ok ? "RemoteV3 connected" : "RemoteV3 not connected (Navigation server)",
                    IsRequired: true,
                    IsBlocking: !ok,
                    TimestampUtc: now,
                    FixHint: ok ? null : "Start AmeisenNavigationServer.exe (port 47110) then restart BlazorServer",
                    NavigateTo: "/Settings");
            }

            if (pather is RemotePathingAPI remoteV1)
            {
                bool ok = TryTcpConnect(remoteV1.Client.BaseAddress);
                return new LaunchSubsystemCheck(
                    LaunchSubsystem.Navigation,
                    ok ? LaunchStatus.Ok : LaunchStatus.Error,
                    "Navigation",
                    ok ? "RemoteV1 connected (PathingAPI)" : "RemoteV1 not reachable (PathingAPI)",
                    IsRequired: true,
                    IsBlocking: !ok,
                    TimestampUtc: now,
                    FixHint: ok ? null : "Start PathingAPI.exe (port 5001) then restart BlazorServer",
                    NavigateTo: "/Settings");
            }

            // Local pathing
            bool mpqOk = HasAnyMpqFiles();
            LaunchStatus status = mpqOk ? LaunchStatus.Ok : LaunchStatus.Warning;

            bool blocks = !mpqOk && !overrideSnapshot.AllowStartWithWarnings;
            return new LaunchSubsystemCheck(
                LaunchSubsystem.Navigation,
                status,
                "Navigation",
                mpqOk ? "Local pathing ready (MPQ OK)" : "Local pathing degraded (MPQ missing)",
                IsRequired: true,
                IsBlocking: blocks,
                TimestampUtc: now,
                FixHint: mpqOk ? null : "Download MPQ files (Json/MPQ) or enable Remote pathing",
                NavigateTo: "/Settings");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[BotStartGuard] Navigation check failed");
            return new LaunchSubsystemCheck(
                LaunchSubsystem.Navigation,
                LaunchStatus.Error,
                "Navigation",
                $"Navigation check error: {ex.Message}",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now);
        }
    }

    private static bool TryTcpConnect(Uri? baseAddress)
    {
        if (baseAddress == null)
            return false;

        try
        {
            using TcpClient client = new();
            Task connect = client.ConnectAsync(baseAddress.Host, baseAddress.Port);
            return connect.Wait(TimeSpan.FromMilliseconds(350));
        }
        catch
        {
            return false;
        }
    }

    private bool HasAnyMpqFiles()
    {
        try
        {
            string mpqDir = dataConfig.MPQ;
            if (!Directory.Exists(mpqDir))
                return false;

            return Directory.EnumerateFiles(mpqDir, "*.MPQ", SearchOption.TopDirectoryOnly).Any() ||
                   Directory.EnumerateFiles(mpqDir, "*.mpq", SearchOption.TopDirectoryOnly).Any();
        }
        catch
        {
            return false;
        }
    }

    private LaunchSubsystemCheck CheckWoW(DateTimeOffset now)
    {
        if (!wowProcess.IsRunning)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.WoWProcess,
                LaunchStatus.Error,
                "WoW Client",
                "WoW process not detected",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "Launch WoW and log in to your character");
        }

        if (wowProcess.MainWindowHandle == IntPtr.Zero)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.WoWProcess,
                LaunchStatus.Warning,
                "WoW Client",
                "WoW detected but window handle not ready yet",
                IsRequired: true,
                IsBlocking: !overrides.AllowStartWithWarnings,
                TimestampUtc: now,
                FixHint: "Wait for the game window to fully load");
        }

        return new LaunchSubsystemCheck(
            LaunchSubsystem.WoWProcess,
            LaunchStatus.Ok,
            "WoW Client",
            $"WoW running (PID {wowProcess.Id})",
            IsRequired: true,
            IsBlocking: false,
            TimestampUtc: now);
    }

    private LaunchSubsystemCheck CheckAddons(DateTimeOffset now)
    {
        try
        {
            if (!wowProcess.IsRunning || string.IsNullOrWhiteSpace(wowProcess.Path))
            {
                return new LaunchSubsystemCheck(
                    LaunchSubsystem.Addons,
                    LaunchStatus.Pending,
                    "Add-ons",
                    "Waiting for WoW to be running (install path unknown)",
                    IsRequired: true,
                    IsBlocking: true,
                    TimestampUtc: now,
                    FixHint: "Start WoW before validating add-ons");
            }

            AddonValidationResult result = GetCachedAddonValidation(now);
            if (result.IsValid && !result.HasWarnings)
            {
                return new LaunchSubsystemCheck(
                    LaunchSubsystem.Addons,
                    LaunchStatus.Ok,
                    "Add-ons",
                    "Add-ons validated",
                    IsRequired: true,
                    IsBlocking: false,
                    TimestampUtc: now,
                    NavigateTo: "/AddonConfiguration");
            }

            if (result.IsValid && result.HasWarnings)
            {
                return new LaunchSubsystemCheck(
                    LaunchSubsystem.Addons,
                    LaunchStatus.Warning,
                    "Add-ons",
                    $"Validated with warnings: {result.GetSummary()}",
                    IsRequired: true,
                    IsBlocking: !overrides.AllowStartWithWarnings,
                    TimestampUtc: now,
                    FixHint: "Open Addon Config and address warnings",
                    NavigateTo: "/AddonConfiguration");
            }

            return new LaunchSubsystemCheck(
                LaunchSubsystem.Addons,
                LaunchStatus.Error,
                "Add-ons",
                $"Validation failed: {result.GetSummary()}",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "Open Addon Config and install/update DataToColor",
                NavigateTo: "/AddonConfiguration");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[BotStartGuard] Addon validation threw");
            return new LaunchSubsystemCheck(
                LaunchSubsystem.Addons,
                LaunchStatus.Error,
                "Add-ons",
                $"Addon validation error: {ex.Message}",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                NavigateTo: "/AddonConfiguration");
        }
    }

    private AddonValidationResult GetCachedAddonValidation(DateTimeOffset now)
    {
        const int cacheSeconds = 10;

        lock (cacheLock)
        {
            if (cachedAddonValidation != null && (now - lastAddonValidationUtc).TotalSeconds < cacheSeconds)
            {
                return cachedAddonValidation;
            }

            lastAddonValidationUtc = now;
            cachedAddonValidation = addonValidator.Validate();
            return cachedAddonValidation;
        }
    }

    private LaunchSubsystemCheck CheckFrames(DateTimeOffset now)
    {
        if (!AddonConfig.Exists())
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.Frames,
                LaunchStatus.Error,
                "Frames",
                "Addon config missing",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "Complete Addon Configuration",
                NavigateTo: "/AddonConfiguration");
        }

        if (!FrameConfig.Exists())
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.Frames,
                LaunchStatus.Error,
                "Frames",
                "Frame config missing",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "Complete Frame Configuration",
                NavigateTo: "/FrameConfiguration");
        }

        try
        {
            Version? installVersion = addonConfigurator.GetInstallVersion();
            if (installVersion == null || wowProcess.MainWindowHandle == IntPtr.Zero)
            {
                return new LaunchSubsystemCheck(
                    LaunchSubsystem.Frames,
                    LaunchStatus.Warning,
                    "Frames",
                    "Frame config exists (unable to verify against window/addon version yet)",
                    IsRequired: true,
                    IsBlocking: !overrides.AllowStartWithWarnings,
                    TimestampUtc: now,
                    NavigateTo: "/FrameConfiguration");
            }

            NativeMethods.GetWindowRect(wowProcess.MainWindowHandle, out Rectangle rect);
            bool valid = FrameConfig.IsValid(rect, installVersion);

            return new LaunchSubsystemCheck(
                LaunchSubsystem.Frames,
                valid ? LaunchStatus.Ok : LaunchStatus.Error,
                "Frames",
                valid ? "Frame config valid" : "Frame config invalid (window size or addon version changed)",
                IsRequired: true,
                IsBlocking: !valid,
                TimestampUtc: now,
                FixHint: valid ? null : "Re-run Frame Configuration (then Restart server)",
                NavigateTo: "/FrameConfiguration");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[BotStartGuard] Frame validation failed");
            return new LaunchSubsystemCheck(
                LaunchSubsystem.Frames,
                LaunchStatus.Warning,
                "Frames",
                $"Frame validation error: {ex.Message}",
                IsRequired: true,
                IsBlocking: !overrides.AllowStartWithWarnings,
                TimestampUtc: now,
                NavigateTo: "/FrameConfiguration");
        }
    }

    private LaunchSubsystemCheck CheckAddonHandshake(DateTimeOffset now)
    {
        if (!wowProcess.IsRunning)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.AddonHandshake,
                LaunchStatus.Pending,
                "Addon Handshake",
                "Waiting for WoW to be running",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now);
        }

        if (addonReader is not AddonReader fullReader)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.AddonHandshake,
                LaunchStatus.Pending,
                "Addon Handshake",
                "Waiting for full addon reader (restart after configuration)",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "Complete Addon + Frame config, then Restart server");
        }

        int globalTime = fullReader.GlobalTime.Value;
        int ageMs = fullReader.GlobalTime.ElapsedMs();

        if (globalTime <= 3)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.AddonHandshake,
                LaunchStatus.Pending,
                "Addon Handshake",
                "Waiting for live addon data (enter world and ensure DataToColor pixels are visible)",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "Enter world; ensure addon pixels are visible; run /reload if needed",
                NavigateTo: "/FrameConfiguration");
        }

        if (ageMs > options.AddonHandshakeMaxStalenessMs)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.AddonHandshake,
                LaunchStatus.Warning,
                "Addon Handshake",
                $"Addon data is stale ({ageMs}ms since last tick)",
                IsRequired: true,
                IsBlocking: !overrides.AllowStartWithWarnings,
                TimestampUtc: now,
                FixHint: "Ensure WoW is foreground; verify frames and addon pixels",
                NavigateTo: "/RawPlayerReader");
        }

        return new LaunchSubsystemCheck(
            LaunchSubsystem.AddonHandshake,
            LaunchStatus.Ok,
            "Addon Handshake",
            $"Live data OK (t={globalTime}, age={ageMs}ms)",
            IsRequired: true,
            IsBlocking: false,
            TimestampUtc: now);
    }

    private static LaunchSubsystemCheck CheckProfile(DateTimeOffset now, ClassConfiguration? classConfig)
    {
        if (classConfig == null)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.Profile,
                LaunchStatus.Error,
                "Profile",
                "No class profile loaded",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "Select and load a class profile",
                NavigateTo: "/");
        }

        return new LaunchSubsystemCheck(
            LaunchSubsystem.Profile,
            LaunchStatus.Ok,
            "Profile",
            $"Loaded: {classConfig.FileName}",
            IsRequired: true,
            IsBlocking: false,
            TimestampUtc: now);
    }

    private static LaunchSubsystemCheck CheckRoute(DateTimeOffset now, RouteInfo? routeInfo)
    {
        if (routeInfo == null)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.Route,
                LaunchStatus.Error,
                "Route",
                "Route manager not initialized (load profile)",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "Load a class profile (creates RouteInfo)");
        }

        int count = routeInfo.Route?.Length ?? 0;
        if (count <= 1)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.Route,
                LaunchStatus.Error,
                "Route",
                "Route has no points",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "Load/choose a Path profile (route JSON)");
        }

        return new LaunchSubsystemCheck(
            LaunchSubsystem.Route,
            LaunchStatus.Ok,
            "Route",
            $"Loaded ({count} points)",
            IsRequired: true,
            IsBlocking: false,
            TimestampUtc: now);
    }

    private LaunchSubsystemCheck CheckKeyBindings(DateTimeOffset now, ClassConfiguration? classConfig, LaunchOverrideSnapshot overrideSnapshot)
    {
        bool required = !overrideSnapshot.SkipKeybindingChecks;
        if (!required)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.KeyBindings,
                LaunchStatus.Skipped,
                "Key Bindings",
                "Skipped by override",
                IsRequired: false,
                IsBlocking: false,
                TimestampUtc: now,
                NavigateTo: "/KeyBindings");
        }

        if (classConfig == null)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.KeyBindings,
                LaunchStatus.Pending,
                "Key Bindings",
                "Waiting for class profile (expected bindings unknown)",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "Load a class profile first",
                NavigateTo: "/");
        }

        if (!keyBindingsReader.IsInitialized)
        {
            var (totalReads, nonZero, consecutiveZeros) = keyBindingsReader.GetReadStats();
            return new LaunchSubsystemCheck(
                LaunchSubsystem.KeyBindings,
                LaunchStatus.Pending,
                "Key Bindings",
                $"Reading in-game bindings... (reads={totalReads}, nonZero={nonZero}, zeros={consecutiveZeros})",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "Enter world and run /dcbindings (or use Key Bindings page)",
                NavigateTo: "/KeyBindings");
        }

        List<KeyAction> expected = GetExpectedKeyActions(classConfig);
        List<BindingMismatch> mismatches = keyBindingsReader.GetMismatches(expected);

        if (mismatches.Count == 0)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.KeyBindings,
                LaunchStatus.Ok,
                "Key Bindings",
                $"Verified ({keyBindingsReader.Count} bindings)",
                IsRequired: true,
                IsBlocking: false,
                TimestampUtc: now,
                NavigateTo: "/KeyBindings");
        }

        return new LaunchSubsystemCheck(
            LaunchSubsystem.KeyBindings,
            LaunchStatus.Error,
            "Key Bindings",
            $"{mismatches.Count} mismatch(es) vs profile",
            IsRequired: true,
            IsBlocking: true,
            TimestampUtc: now,
            FixHint: "Open Key Bindings page and apply defaults (/dcbindings, /dcactions)",
            NavigateTo: "/KeyBindings");
    }

    private LaunchSubsystemCheck CheckActionBar(DateTimeOffset now, ClassConfiguration? classConfig, LaunchOverrideSnapshot overrideSnapshot)
    {
        bool required = !overrideSnapshot.SkipActionBarChecks;
        if (!required)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.ActionBar,
                LaunchStatus.Skipped,
                "Action Bar",
                "Skipped by override",
                IsRequired: false,
                IsBlocking: false,
                TimestampUtc: now,
                NavigateTo: "/KeyBindings");
        }

        if (classConfig == null)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.ActionBar,
                LaunchStatus.Pending,
                "Action Bar",
                "Waiting for class profile",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now);
        }

        if (!actionBarTextureReader.IsInitialized)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.ActionBar,
                LaunchStatus.Pending,
                "Action Bar",
                "Textures not initialized (enter world / wait for sync)",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "Enter world and wait; then Sync Actionbar on Key Bindings page",
                NavigateTo: "/KeyBindings");
        }

        int issueCount = actionBarSlotValidator.GetIssueCount(classConfig);
        if (issueCount == 0)
        {
            return new LaunchSubsystemCheck(
                LaunchSubsystem.ActionBar,
                LaunchStatus.Ok,
                "Action Bar",
                "Validated",
                IsRequired: true,
                IsBlocking: false,
                TimestampUtc: now,
                NavigateTo: "/KeyBindings");
        }

        return new LaunchSubsystemCheck(
            LaunchSubsystem.ActionBar,
            LaunchStatus.Error,
            "Action Bar",
            $"{issueCount} issue(s) detected",
            IsRequired: true,
            IsBlocking: true,
            TimestampUtc: now,
            FixHint: "Open Key Bindings and resolve missing action bar slots",
            NavigateTo: "/KeyBindings");
    }

    private static List<KeyAction> GetExpectedKeyActions(ClassConfiguration classConfig)
    {
        List<KeyAction> result = [];

        foreach ((string _, KeyActions keyActions) in classConfig.GetByType<KeyActions>())
        {
            result.AddRange(keyActions.Sequence);
        }

        return result;
    }
}
