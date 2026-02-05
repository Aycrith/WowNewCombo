using System;
using System.Collections.Generic;
using System.Diagnostics;
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

public interface ILaunchReadinessCacheInvalidator
{
    void InvalidateAddonValidation();
    void PrimeAddonValidation(AddonValidationResult result, string reason);
}

public sealed class BotStartGuard : IBotStartGuard, ILaunchReadinessCacheInvalidator
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
    private Task<AddonValidationResult>? addonValidationTask;
    private DateTimeOffset addonValidationStartedUtc = DateTimeOffset.MinValue;

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
        LaunchOverrideSnapshot overrideSnapshot = overrides.Snapshot();

        if (options.EvaluateTimeoutMs <= 0)
        {
            List<LaunchSubsystemCheck> checksNoTimeout =
            [
                Timed(now, LaunchSubsystem.Navigation, () => CheckNavigation(now, overrideSnapshot)),
                Timed(now, LaunchSubsystem.WoWProcess, () => CheckWoW(now)),
                Timed(now, LaunchSubsystem.Addons, () => CheckAddons(now)),
                Timed(now, LaunchSubsystem.Frames, () => CheckFrames(now)),
                Timed(now, LaunchSubsystem.AddonHandshake, () => CheckAddonHandshake(now)),
                Timed(now, LaunchSubsystem.Profile, () => CheckProfile(now, classConfig)),
                Timed(now, LaunchSubsystem.Route, () => CheckRoute(now, routeInfo)),
                Timed(now, LaunchSubsystem.KeyBindings, () => CheckKeyBindings(now, classConfig, overrideSnapshot)),
                Timed(now, LaunchSubsystem.ActionBar, () => CheckActionBar(now, classConfig, overrideSnapshot))
            ];

            return CreateSnapshot(now, overrideSnapshot, checksNoTimeout);
        }

        (LaunchSubsystem subsystem, Func<LaunchSubsystemCheck> factory)[] factories =
        [
            (LaunchSubsystem.Navigation, () => CheckNavigation(now, overrideSnapshot)),
            (LaunchSubsystem.WoWProcess, () => CheckWoW(now)),
            (LaunchSubsystem.Addons, () => CheckAddons(now)),
            (LaunchSubsystem.Frames, () => CheckFrames(now)),
            (LaunchSubsystem.AddonHandshake, () => CheckAddonHandshake(now)),
            (LaunchSubsystem.Profile, () => CheckProfile(now, classConfig)),
            (LaunchSubsystem.Route, () => CheckRoute(now, routeInfo)),
            (LaunchSubsystem.KeyBindings, () => CheckKeyBindings(now, classConfig, overrideSnapshot)),
            (LaunchSubsystem.ActionBar, () => CheckActionBar(now, classConfig, overrideSnapshot))
        ];

        Task<LaunchSubsystemCheck>[] tasks = new Task<LaunchSubsystemCheck>[factories.Length];
        for (int i = 0; i < factories.Length; i++)
        {
            LaunchSubsystem subsystem = factories[i].subsystem;
            Func<LaunchSubsystemCheck> factory = factories[i].factory;
            tasks[i] = Task.Run(() => Timed(now, subsystem, factory));
        }

        try
        {
            Task.WaitAll(tasks, millisecondsTimeout: options.EvaluateTimeoutMs);
        }
        catch (AggregateException ex)
        {
            // Individual task failures are converted to error checks below.
            logger.LogDebug(ex, "[BotStartGuard] One or more readiness checks faulted");
        }

        List<LaunchSubsystemCheck> checks = new(capacity: factories.Length);
        for (int i = 0; i < factories.Length; i++)
        {
            LaunchSubsystem subsystem = factories[i].subsystem;
            Task<LaunchSubsystemCheck> task = tasks[i];

            if (task.IsCompletedSuccessfully)
            {
                checks.Add(task.Result);
                continue;
            }

            if (task.IsFaulted)
            {
                Exception? inner = task.Exception?.GetBaseException() ?? task.Exception;
                string msg = inner == null ? "Unknown error" : inner.Message;

                logger.LogWarning(inner, "[BotStartGuard] Readiness check faulted: {Subsystem}", subsystem);
                checks.Add(new LaunchSubsystemCheck(
                    subsystem,
                    LaunchStatus.Error,
                    GetTimeoutTitle(subsystem),
                    $"Check failed: {msg}",
                    IsRequired: true,
                    IsBlocking: true,
                    TimestampUtc: now,
                    FixHint: "Open logs for details",
                    NavigateTo: GetTimeoutNavigateTo(subsystem)));
                continue;
            }

            // Timed out (or still running)
            logger.LogWarning(
                "[BotStartGuard] Readiness check timed out: {Subsystem} (>{Timeout}ms)",
                subsystem,
                options.EvaluateTimeoutMs);

            checks.Add(new LaunchSubsystemCheck(
                subsystem,
                LaunchStatus.Error,
                GetTimeoutTitle(subsystem),
                $"Timed out while evaluating readiness (>{options.EvaluateTimeoutMs}ms)",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: "This usually indicates a stuck dependency (WoW reader, addon handshake, or file IO). Check logs and try Restart Server.",
                NavigateTo: GetTimeoutNavigateTo(subsystem)));
        }

        return CreateSnapshot(now, overrideSnapshot, checks);
    }

    private LaunchReadinessSnapshot CreateSnapshot(
        DateTimeOffset now,
        LaunchOverrideSnapshot overrideSnapshot,
        List<LaunchSubsystemCheck> checks)
    {
        List<LaunchSubsystemCheck> effectiveChecks = ApplyOverrides(now, overrideSnapshot, checks);
        bool hasBlocking = effectiveChecks.Any(c => c.IsBlocking);

        bool strictReady = effectiveChecks
            .Where(c => c.IsRequired)
            .All(c => c.Status is LaunchStatus.Ok or LaunchStatus.Skipped);

        bool canStart = !hasBlocking;

        return new LaunchReadinessSnapshot(
            IsLaunchReady: strictReady,
            CanStartBot: canStart,
            TimestampUtc: now,
            Checks: effectiveChecks,
            Overrides: overrideSnapshot);
    }

    private List<LaunchSubsystemCheck> ApplyOverrides(
        DateTimeOffset now,
        LaunchOverrideSnapshot overrideSnapshot,
        List<LaunchSubsystemCheck> checks)
    {
        if (!overrideSnapshot.EmergencyBypassAll && overrideSnapshot.Bypasses.Count == 0)
        {
            return checks;
        }

        List<LaunchSubsystemCheck> updated = new(capacity: checks.Count);
        foreach (LaunchSubsystemCheck check in checks)
        {
            if (!IsBypassed(overrideSnapshot, check.Subsystem, out LaunchSubsystemBypass? bypass))
            {
                updated.Add(check);
                continue;
            }

            if (check.Subsystem == LaunchSubsystem.Addons &&
                !overrideSnapshot.EmergencyBypassAll &&
                (check.Status != LaunchStatus.Ok || check.IsBlocking))
            {
                try
                {
                    // Safeguard: only allow bypass if a synchronous validation confirms add-ons are actually OK.
                    AddonValidationResult forced = addonValidator.Validate();
                    PrimeAddonValidation(forced, "Bypass safeguard");

                    if (!forced.IsValid || forced.HasWarnings)
                    {
                        updated.Add(check with
                        {
                            Status = LaunchStatus.Error,
                            IsBlocking = true,
                            Message = $"Add-on bypass refused: {forced.GetSummary()}",
                            TimestampUtc = now,
                            FixHint = "Open Addon Config, install/update DataToColor, then Refresh",
                            NavigateTo = "/AddonConfiguration"
                        });
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[BotStartGuard] Add-on bypass safeguard failed");
                    updated.Add(check);
                    continue;
                }
            }

            // Safeguard: only emergency bypass can suppress genuine errors.
            if (check.Status == LaunchStatus.Error && !overrideSnapshot.EmergencyBypassAll)
            {
                updated.Add(check);
                continue;
            }

            string reason = bypass?.Reason ?? "Emergency bypass";
            updated.Add(check with
            {
                Status = LaunchStatus.Ok,
                IsBlocking = false,
                Message = $"{check.Message} (OVERRIDDEN: {reason})",
                TimestampUtc = now,
                IsOverridden = true
            });
        }

        return updated;
    }

    private static bool IsBypassed(
        LaunchOverrideSnapshot snapshot,
        LaunchSubsystem subsystem,
        out LaunchSubsystemBypass? bypass)
    {
        if (snapshot.EmergencyBypassAll)
        {
            bypass = null;
            return true;
        }

        if (snapshot.Bypasses.TryGetValue(subsystem, out LaunchSubsystemBypass found) && found.Enabled)
        {
            bypass = found;
            return true;
        }

        bypass = null;
        return false;
    }

    private static string GetTimeoutTitle(LaunchSubsystem subsystem) => subsystem switch
    {
        LaunchSubsystem.Navigation => "Navigation",
        LaunchSubsystem.WoWProcess => "WoW Client",
        LaunchSubsystem.Addons => "Add-ons",
        LaunchSubsystem.Frames => "Frames",
        LaunchSubsystem.AddonHandshake => "Addon Handshake",
        LaunchSubsystem.Profile => "Profile",
        LaunchSubsystem.Route => "Route",
        LaunchSubsystem.KeyBindings => "Key Bindings",
        LaunchSubsystem.ActionBar => "Action Bar",
        _ => subsystem.ToString()
    };

    private static string? GetTimeoutNavigateTo(LaunchSubsystem subsystem) => subsystem switch
    {
        LaunchSubsystem.Navigation => "/Settings",
        LaunchSubsystem.Addons => "/AddonConfiguration",
        LaunchSubsystem.Frames => "/FrameConfiguration",
        LaunchSubsystem.AddonHandshake => "/RawPlayerReader",
        LaunchSubsystem.Profile => "/",
        LaunchSubsystem.Route => "/",
        LaunchSubsystem.KeyBindings => "/KeyBindings",
        LaunchSubsystem.ActionBar => "/KeyBindings",
        _ => null
    };

    private LaunchSubsystemCheck Timed(DateTimeOffset now, LaunchSubsystem subsystem, Func<LaunchSubsystemCheck> factory)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("[BotStartGuard] Check start {Subsystem}", subsystem);
        }

        var sw = Stopwatch.StartNew();
        LaunchSubsystemCheck check = factory();
        sw.Stop();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("[BotStartGuard] Check end {Subsystem} ({Elapsed}ms)", subsystem, sw.ElapsedMilliseconds);
        }

        if (sw.ElapsedMilliseconds > options.SlowCheckThresholdMs)
        {
            logger.LogWarning(
                "[BotStartGuard] Slow check {Subsystem} took {Elapsed}ms",
                subsystem,
                sw.ElapsedMilliseconds);
        }

        return check;
    }

    public void InvalidateAddonValidation()
    {
        lock (cacheLock)
        {
            cachedAddonValidation = null;
            addonValidationTask = null;
            addonValidationStartedUtc = DateTimeOffset.MinValue;
            lastAddonValidationUtc = DateTimeOffset.MinValue;
        }
    }

    public void PrimeAddonValidation(AddonValidationResult result, string reason)
    {
        lock (cacheLock)
        {
            cachedAddonValidation = result;
            lastAddonValidationUtc = DateTimeOffset.UtcNow;
            addonValidationTask = null;
            addonValidationStartedUtc = DateTimeOffset.MinValue;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("[BotStartGuard] Primed addon validation cache ({Reason})", reason);
        }
    }

    private LaunchSubsystemCheck CheckNavigation(DateTimeOffset now, LaunchOverrideSnapshot overrideSnapshot)
    {
        bool required = !overrideSnapshot.EmergencyBypassAll &&
                        !overrideSnapshot.Bypasses.ContainsKey(LaunchSubsystem.Navigation);

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

            if (!AddonConfig.Exists())
            {
                return new LaunchSubsystemCheck(
                    LaunchSubsystem.Addons,
                    LaunchStatus.Error,
                    "Add-ons",
                    "addon_config.json missing",
                    IsRequired: true,
                    IsBlocking: true,
                    TimestampUtc: now,
                    FixHint: "Run Auto Fix or complete Addon Configuration",
                    NavigateTo: "/AddonConfiguration");
            }

            if (!TryGetCachedAddonValidation(now, out AddonValidationResult result))
            {
                return new LaunchSubsystemCheck(
                    LaunchSubsystem.Addons,
                    LaunchStatus.Pending,
                    "Add-ons",
                    "Validating add-ons...",
                    IsRequired: true,
                    IsBlocking: true,
                    TimestampUtc: now,
                    FixHint: "Wait for validation to complete",
                    NavigateTo: "/AddonConfiguration");
            }
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

    private bool TryGetCachedAddonValidation(DateTimeOffset now, out AddonValidationResult result)
    {
        const int cacheSeconds = 3;
        const int maxValidationSeconds = 30;

        lock (cacheLock)
        {
            if (cachedAddonValidation != null && (now - lastAddonValidationUtc).TotalSeconds < cacheSeconds)
            {
                result = cachedAddonValidation;
                return true;
            }

            if (addonValidationTask == null || addonValidationTask.IsCompleted)
            {
                addonValidationTask = Task.Run(() => addonValidator.Validate());
                addonValidationStartedUtc = now;
            }

            if (addonValidationTask.IsFaulted)
            {
                Exception? ex = addonValidationTask.Exception?.GetBaseException();
                cachedAddonValidation = new AddonValidationResult();
                cachedAddonValidation.AddError("Addon validation exception", ex?.Message ?? "Unknown error");
                lastAddonValidationUtc = now;
                addonValidationTask = null;
                addonValidationStartedUtc = DateTimeOffset.MinValue;
                result = cachedAddonValidation;
                return true;
            }

            if (addonValidationTask.IsCanceled)
            {
                cachedAddonValidation = new AddonValidationResult();
                cachedAddonValidation.AddError("Addon validation cancelled", "Validation task was cancelled");
                lastAddonValidationUtc = now;
                addonValidationTask = null;
                addonValidationStartedUtc = DateTimeOffset.MinValue;
                result = cachedAddonValidation;
                return true;
            }

            if (!addonValidationTask.IsCompleted)
            {
                if (addonValidationStartedUtc != DateTimeOffset.MinValue &&
                    (now - addonValidationStartedUtc).TotalSeconds > 1 &&
                    cachedAddonValidation == null)
                {
                    try
                    {
                        logger.LogWarning("[BotStartGuard] Addon validation pending >1s; forcing synchronous validation");
                        cachedAddonValidation = addonValidator.Validate();
                        lastAddonValidationUtc = now;
                        addonValidationTask = null;
                        addonValidationStartedUtc = DateTimeOffset.MinValue;
                        result = cachedAddonValidation;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "[BotStartGuard] Forced synchronous addon validation failed");
                        cachedAddonValidation = new AddonValidationResult();
                        cachedAddonValidation.AddError("Addon validation exception", ex.Message);
                        lastAddonValidationUtc = now;
                        addonValidationTask = null;
                        addonValidationStartedUtc = DateTimeOffset.MinValue;
                        result = cachedAddonValidation;
                        return true;
                    }
                }

                if (addonValidationStartedUtc != DateTimeOffset.MinValue &&
                    (now - addonValidationStartedUtc).TotalSeconds > maxValidationSeconds)
                {
                    cachedAddonValidation = new AddonValidationResult();
                    cachedAddonValidation.AddError("Addon validation timeout", $"Validation did not complete within {maxValidationSeconds}s");
                    lastAddonValidationUtc = now;
                    addonValidationTask = null;
                    addonValidationStartedUtc = DateTimeOffset.MinValue;
                    result = cachedAddonValidation;
                    return true;
                }

                if (cachedAddonValidation != null)
                {
                    result = cachedAddonValidation;
                    return true;
                }

                result = new AddonValidationResult();
                return false;
            }

            try
            {
                cachedAddonValidation = addonValidationTask.GetAwaiter().GetResult();
                lastAddonValidationUtc = now;
                result = cachedAddonValidation;
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[BotStartGuard] Addon validation task failed");
                cachedAddonValidation = new AddonValidationResult();
                cachedAddonValidation.AddError("Addon validation exception", ex.Message);
                lastAddonValidationUtc = now;
                addonValidationTask = null;
                addonValidationStartedUtc = DateTimeOffset.MinValue;
                result = cachedAddonValidation;
                return true;
            }
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
        bool required = !overrideSnapshot.EmergencyBypassAll &&
                        !overrideSnapshot.Bypasses.ContainsKey(LaunchSubsystem.KeyBindings);
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
            string command = GetAddonCommand();
            var (totalReads, nonZero, consecutiveZeros) = keyBindingsReader.GetReadStats();
            return new LaunchSubsystemCheck(
                LaunchSubsystem.KeyBindings,
                LaunchStatus.Pending,
                "Key Bindings",
                $"Reading in-game bindings... (reads={totalReads}, nonZero={nonZero}, zeros={consecutiveZeros})",
                IsRequired: true,
                IsBlocking: true,
                TimestampUtc: now,
                FixHint: $"Enter world and run /{command}bindings (or use Key Bindings page)",
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
            FixHint: $"Open Key Bindings page and apply defaults (/{GetAddonCommand()}bindings, /{GetAddonCommand()}actions)",
            NavigateTo: "/KeyBindings");
    }

    private string GetAddonCommand()
    {
        try
        {
            string cmd = addonConfigurator.Config.Command?.Trim() ?? string.Empty;
            if (cmd.StartsWith("/", StringComparison.Ordinal))
            {
                cmd = cmd.TrimStart('/');
            }
            return string.IsNullOrWhiteSpace(cmd) ? "dc" : cmd;
        }
        catch
        {
            return "dc";
        }
    }

    private LaunchSubsystemCheck CheckActionBar(DateTimeOffset now, ClassConfiguration? classConfig, LaunchOverrideSnapshot overrideSnapshot)
    {
        bool required = !overrideSnapshot.EmergencyBypassAll &&
                        !overrideSnapshot.Bypasses.ContainsKey(LaunchSubsystem.ActionBar);
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
