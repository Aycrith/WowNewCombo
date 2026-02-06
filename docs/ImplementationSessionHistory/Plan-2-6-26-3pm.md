Plan: Fix Three Root Cause Failure Chains Blocking Bot Operation
TL;DR: Days of failed bot operation trace to exactly three independent root causes, each cascading into multiple symptoms. (1) PressClearTarget() sends F11 (an arbitrary action bar slot) instead of the upstream Alt-Insert binding that's wired to the addon's /cleartarget macro — this breaks target clearing everywhere; 9 of 13 ForceAggressiveClearTarget callers also lack the execGameCommand fallback. (2) AmeisenNavigationServer v1.8.3.2 crashes with ACCESS_VIOLATION (0xC0000005) on every TCP client connect, exhausting restart limits; PathingAPI zombie processes block port 5001 on restarts. (3) FrameConfig.IsValid() does exact pixel-match resolution comparison; an in-flight window resize, DPI change, or fullscreen↔windowed switch deletes frame_config.json, which collapses the UI to a 5-item setup wizard and hides all features including Dashboard and Combat Rotation.

Steps

Phase 1: Target Clearing (Root Cause 1) — Unblocks combat, blacklisting, route following
Restore upstream PressClearTarget() behavior: In ConfigurableInput.cs:216-219, change PressClearTarget() to call PressRandom(ClearTarget, token) — the configured Alt-Insert binding that goes through SecureButtons.xml → CLICK BindPadMacro:cleartarget → /cleartarget. This is how the upstream repo (Xian55/WowClassicGrindBot) has always done it. Keep PressF11ClearTarget() as a helper but don't use it as the primary path.

Reorder ForceAggressiveClearTarget() stages: In ConfigurableInput.cs:231-282, swap Stage 1 (F11) and Stage 3 (configured binding). The configured binding (Alt-Insert) should be tried first since it's the reliable path. F11 should be a secondary fallback. Stage 4 (/cleartarget via execGameCommand) remains last.

Pass execGameCommand to all callers: Currently only FollowRouteGoal.cs:243, LootGoal.cs:222, SkinningGoal.cs:338, and MailGoal.cs:149 pass it. These 9 callers need ExecGameCommand injected into their constructors and passed to ForceAggressiveClearTarget():

BlacklistTargetGoal.cs:43 — most critical, this is what gets stuck
ReactCastError.cs:240
CombatTracker.cs:96 (two calls)
FleeGoal.cs:112
PullTargetGoal.cs:152 (three calls)
TargetPetTargetGoal.cs:54
TargetFocusTargetGoal.cs:74
FollowFocusGoal.cs:45
Phase 2: Navigation (Root Cause 2) — Unblocks pathfinding, route following
AmeisenNavigationServer crash mitigation: The ACCESS_VIOLATION is in the C++ binary (not our code). Evidence: every crash log shows exit code -1073741819 (0xC0000005), triggered when RemotePathingAPIV3.ObserveConnection() calls AnTcpClient.Connect(). In NavigationServerManager.cs, the IsHealthyAsync() already avoids TCP probing. The fix: in RemotePathingAPIV3.cs, add connection retry with exponential backoff and delay the initial connection attempt by 2-3 seconds after server start to let the server fully initialize before accepting TCP clients.

PathingAPI zombie process cleanup: 12 consecutive crashes in crash-PathingAPI-20260205-042641 show "Failed to bind to address http://127.0.0.1:5001: address already in use". Before starting PathingAPI, kill any existing process on port 5001. Add port cleanup to StartupOrchestrator or LaunchAutoFixService. Same pattern for NavigationServer on port 47110.

NavigationServerManager auto-recovery enhancement: In NavigationServerManager.cs:174-197, the MonitorServerAsync detects crashes but does NOT auto-restart. Add await EnsureRunningAsync() call after detecting server exit in the monitor loop, subject to the existing restart limits and cooldown.

Phase 3: Frame Config / UI (Root Cause 3) — Unblocks Dashboard, Combat Rotation, auto-config
Add resolution tolerance to FrameConfig.IsValid(): In FrameConfig.cs:115, change exact Width == Width && Height == Height comparison to allow ±100px height variance. Width must still match exactly (horizontal pixel positions derive from width). Height variance covers window chrome (title bar ~30-80px). The addon's SetPoint("TOPLEFT", ...) anchorage means frame pixel positions are stable when only the window bottom edge changes.

Add diagnostic logging when config is deleted: In DependencyInjection.cs (the validation block), log the stored rect and current rect before deleting, so this silent failure chain becomes visible.

Expand config-mode nav menu: In MainLayout.razor:191-216, GetConfigNavItems() currently returns only 5 wizard steps. Add Dashboard (/), Launch Wizard (/launch), Settings (/Settings), Combat Rotation (/combat-rotation), Frame Configuration (/FrameConfiguration), Key Bindings (/KeyBindings), and Raw Values (/RawPlayerReader). These pages work without a fully configured system and blocking access to them traps users.

Phase 4: Verification
Unit tests: Run dotnet test CoreUnitTests and dotnet test FrontendUnitTests to verify no regressions.

Live validation sequence (with WoW running at 1920x1008 windowed):

Start BlazorServer → verify Dashboard loads (not config wizard)
Verify Combat Rotation page accessible in nav menu
Verify Navigation server starts and stays running without ACCESS_VIOLATION
Select class profile → verify key bindings and action bar populate
Start bot → verify target clearing works (Alt-Insert, not F11)
Verify blacklisted targets are cleared immediately
Verify route following operates with pathfinder
Verification

dotnet build MasterOfPuppets.sln — 0 errors
dotnet test CoreUnitTests — 161+ passing
dotnet test FrontendUnitTests — 7+ passing
Dashboard loads at http://localhost:5000/ without crashing or showing config wizard
/combat-rotation accessible in sidebar nav
[ClearTarget] log shows "Cleared via configured binding" not "Cleared via F11"
Navigation server process stays alive for >5 minutes after bot start
No "Unable to clear target!" errors in logs
Decisions

Restore upstream binding over F11: The F11 approach was a design decision documented in PRD_F11_TARGET_CLEARING.md, but it requires a specific macro on action bar slot 71 that users aren't guaranteed to configure. The upstream Alt-Insert path through SecureButtons.xml is self-contained and auto-configured by the addon's AutoSetupBindingsIfNeeded().
Height tolerance of ±100px over exact match: addon frames anchor to TOPLEFT, so vertical frame positions are unaffected by window bottom-edge changes. 100px covers all window chrome scenarios without masking genuine resolution changes.
Nav menu expansion over removing config mode entirely: keep the wizard steps visible but add essential pages alongside them, so users are never locked out.
Connection delay for NavigationServer over updating the binary: the C++ ACCESS_VIOLATION is in an external binary we can't easily patch, but delaying the initial connection gives the server time to finish initialization.
Phase ordering: Target clearing first because it's a pure code change with no runtime dependency. Navigation second because it affects the pathing pipeline. Frame config third because it's the least critical operationally (the bot can function once frames are configured, even in config mode).

-----------------------------------------------------------------------------------------------


WORK BY CODEX CLI AGENT:



I’m implementing your four-phase fix set directly in the codebase, starting with auditing the target-clearing flow and each ForceAggressiveClearTarget caller so I can patch behavior first and then move to navigation and frame-config/UI fixes.

Ran rg -n "PressClearTarget|PressF11ClearTarget|ForceAggressiveClearTarget|execGameCommand|IsValid\(|GetConfigNavItems|MonitorServerAsync|ObserveConnection|EnsureRunningAsync|IsHealthyAsync" -S Core Frontend BlazorServer HeadlessServer PathingAPI


Ran git status --short


I located all target-clear call sites and the navigation/frame-config hotspots. Next I’m reading the affected classes to apply minimal, coordinated edits so constructor injections and fallback ordering stay consistent.

Ran Get-Content Core/Startup/NavigationServerManager.cs


Ran Get-Content Core/PPather/RemotePathingAPIV3.cs


Ran Get-Content Core/DependencyInjection.cs


Ran Get-Content Frontend/Shared/MainLayout.razor


Ran Get-Content Core/DataFrame/FrameConfig.cs


Ran Get-Content Core/Input/ConfigurableInput.cs


Ran rg -n "5001|PathingAPI|NavigationServer|port 47110|EnsureRunningAsync\(|StartPathing|Kill.*port|GetActiveTcpListeners|GetProcessesByName|Pathing API|LaunchAutoFixService|StartupOrchestrator" -S Core BlazorServer HeadlessServer PathingAPI


I’ve confirmed the core files and I’m now checking constructor signatures and startup orchestration code to wire ExecGameCommand where missing and add safe port/process cleanup without breaking DI.

Ran Get-Content Core/Goals/BlacklistTargetGoal.cs


Ran Get-Content Core/GoalsComponent/CombatTracker.cs


Ran Get-Content Core/GoalsComponent/ReactCastError.cs


Ran Get-Content Core/Goals/FleeGoal.cs


Ran Get-Content Core/Goals/TargetPetTargetGoal.cs


Ran Get-Content Core/Goals/FollowFocusGoal.cs


Ran Get-Content Core/Goals/TargetFocusTargetGoal.cs


Ran Get-Content Core/Goals/PullTargetGoal.cs


Ran rg -n "new\s+BlacklistTargetGoal|new\s+PullTargetGoal|new\s+TargetPetTargetGoal|new\s+TargetFocusTargetGoal|new\s+FollowFocusGoal|AddSingleton<ReactCastError|AddSingleton<CombatTracker|AddTransient<.*Goal|GoalFactory|CreateInstance" -S Core BlazorServer HeadlessServer


Ran Get-Content Core/Startup/StartupOptions.cs


Ran Get-Content Core/GoalsFactory/GoalFactory.cs


Ran Get-Content Core/Startup/StartupOrchestrator.cs


Ran Get-Content Core/Launch/LaunchAutoFixService.cs


Ran rg -n "PathingAPI|localhost:5001|ProcessStartInfo|dotnet run --project PathingAPI|pathing api|Start.*Pathing|portv1|hostv1|Kestrel|launch pathing|port 5001" -S Core BlazorServer HeadlessServer


Ran Get-Content BlazorServer/StartupHostedService.cs


Ran rg -n "PathingAPI\.exe|PathingAPI.dll|dotnet .*PathingAPI|Process.Start\(.*PathingAPI|AmeisenNavigationServer|NavigationServer|localhost:5001|5001" -S


Ran rg -n "GetActiveTcpListeners|GetActiveTcpConnections|IPGlobalProperties|IPGlobalProperties|NetStat|Get-NetTCPConnection|port cleanup|kill process" -S Core BlazorServer HeadlessServer


Ran rg -n "PathingAPI|5001|AmeisenNavigationServer|47110|Start-.*Pathing|OneClickLauncher|Restart.*Pathing" -S Scripts


Ran rg -n "new\s+ReactCastError|new\s+CombatTracker|new\s+BlacklistTargetGoal|new\s+FleeGoal|new\s+PullTargetGoal|new\s+TargetPetTargetGoal|new\s+TargetFocusTargetGoal|new\s+FollowFocusGoal" -S CoreTests CoreUnitTests FrontendUnitTests


I have enough context and I’m moving to edits now: target-clear behavior/order plus ExecGameCommand injection across the 9 listed callers, then nav resilience/auto-restart and frame-config/menu fixes. After patching I’ll run build/tests to verify.

Edited file
ConfigurableInput.cs
+15
-12

    {
        PressF11ClearTarget(token);
        PressRandom(ClearTarget, token);
    }
    {
        // Stage 1: F11 macro retries.
        for (int attempt = 1; attempt <= 3; attempt++)
        // Stage 1: Configured binding retries.
        if (ClearTarget.ConsoleKey != ConsoleKey.NoName)
        {
            PressF11ClearTarget(token);
            wait.Update();
            if (!bits.Target())
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                logger.LogInformation("[ClearTarget      ] Cleared via F11 (attempt {Attempt})", attempt);
                return true;
                PressRandom(ClearTarget, token);
                wait.Update();
                if (!bits.Target())
                {
                    logger.LogInformation("[ClearTarget      ] Cleared via configured binding (attempt {Attempt})", attempt);
                    return true;
                }
            }

        // Stage 3: Configured binding fallback.
        if (ClearTarget.ConsoleKey != ConsoleKey.NoName)
        // Stage 3: F11 macro retries.
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            PressRandom(ClearTarget, token);
            PressF11ClearTarget(token);
            wait.Update();
            {
                logger.LogInformation("[ClearTarget      ] Cleared via configured binding");
                logger.LogInformation("[ClearTarget      ] Cleared via F11 (attempt {Attempt})", attempt);
                return true;
I’m now wiring ExecGameCommand through each listed caller and switching those ForceAggressiveClearTarget invocations to include the command fallback, while keeping constructor signatures DI-compatible.

Edited file
BlacklistTargetGoal.cs
+5
-3

    private readonly IBlacklist targetBlacklist;
    private readonly ExecGameCommand execGameCommand;

        IBlacklist blacklist,
        Wait wait)
        Wait wait,
        ExecGameCommand execGameCommand)
        : base(nameof(BlacklistTargetGoal))
        this.targetBlacklist = blacklist;
        this.bits = bits;
        this.wait = wait;
        this.execGameCommand = execGameCommand;
    }

        input.ForceAggressiveClearTarget(wait, bits);
        input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
    }
Edited file
ReactCastError.cs
+5
-2

    private readonly SessionStat sessionStat;
    private readonly ExecGameCommand execGameCommand;

        SessionStat sessionStat,
        PlayerDirection direction)
        PlayerDirection direction,
        ExecGameCommand execGameCommand)
    {
        this.sessionStat = sessionStat;
        this.execGameCommand = execGameCommand;
    }
                    input.PressStopAttack();
                    input.ForceAggressiveClearTarget(wait, bits);
                    input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
                }
Edited file
CombatTracker.cs
+9
-3

    private readonly Wait wait;
    private readonly ExecGameCommand execGameCommand;

        ConfigurableInput input,
        AddonBits bits, Wait wait, PlayerReader playerReader, CombatLog combatLog)
        AddonBits bits,
        Wait wait,
        PlayerReader playerReader,
        CombatLog combatLog,
        ExecGameCommand execGameCommand)
    {
        this.combatLog = combatLog;
        this.execGameCommand = execGameCommand;


        input.ForceAggressiveClearTarget(wait, bits);
        input.ForceAggressiveClearTarget(wait, bits, execGameCommand);

        Log($"{nameof(AcquiredTarget)}: No target found after {maxTimeMs}ms");
        input.ForceAggressiveClearTarget(wait, bits);
        input.ForceAggressiveClearTarget(wait, bits, execGameCommand);

Edited file
FleeGoal.cs
+5
-2

    private readonly SafeSpotCollector safeSpotCollector;
    private readonly ExecGameCommand execGameCommand;

        ClassConfiguration classConfig,
        SafeSpotCollector safeSpotCollector)
        SafeSpotCollector safeSpotCollector,
        ExecGameCommand execGameCommand)
        : base(nameof(FleeGoal))
        this.safeSpotCollector = safeSpotCollector;
        this.execGameCommand = execGameCommand;
    }
        {
            input.ForceAggressiveClearTarget(wait, bits);
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
        }
Edited file
PullTargetGoal.cs
+7
-4

    private readonly IBlacklist targetBlacklist;
    private readonly ExecGameCommand execGameCommand;

        StuckDetector stuckDetector, CombatTracker combatTracker,
        ClassConfiguration classConfig)
        ClassConfiguration classConfig,
        ExecGameCommand execGameCommand)
        : base(nameof(PullTargetGoal))
        this.classConfig = classConfig;
        this.execGameCommand = execGameCommand;

            input.PressStopAttack();
            input.ForceAggressiveClearTarget(wait, bits);
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
            Log("Pull taking too long. Clear target and face away!");
                input.PressStopAttack();
                input.ForceAggressiveClearTarget(wait, bits);
                input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
                return;
            input.PressStopAttack();
            input.ForceAggressiveClearTarget(wait, bits);
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
            return;
Edited file
TargetPetTargetGoal.cs
+5
-2

    private readonly Wait wait;
    private readonly ExecGameCommand execGameCommand;

        PlayerReader playerReader, AddonBits bits,
        Wait wait)
        Wait wait,
        ExecGameCommand execGameCommand)
        : base(nameof(TargetPetTargetGoal))
        this.wait = wait;
        this.execGameCommand = execGameCommand;

        {
            input.ForceAggressiveClearTarget(wait, bits);
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
        }
Edited file
TargetFocusTargetGoal.cs
+4
-2

    private readonly Wait wait;
    private readonly ExecGameCommand execGameCommand;

    public TargetFocusTargetGoal(ConfigurableInput input, PlayerReader playerReader,
        AddonBits bits, ClassConfiguration classConfig, Wait wait)
        AddonBits bits, ClassConfiguration classConfig, Wait wait, ExecGameCommand execGameCommand)
        : base(nameof(TargetFocusTargetGoal))
        this.wait = wait;
        this.execGameCommand = execGameCommand;

        {
            input.ForceAggressiveClearTarget(wait, bits);
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
        }
Edited file
FollowFocusGoal.cs
+5
-2

    private readonly Wait wait;
    private readonly ExecGameCommand execGameCommand;

        AddonBits bits,
        Wait wait)
        Wait wait,
        ExecGameCommand execGameCommand)
        : base(nameof(FollowFocusGoal))
        this.wait = wait;
        this.execGameCommand = execGameCommand;

        {
            input.ForceAggressiveClearTarget(wait, bits);
            input.ForceAggressiveClearTarget(wait, bits, execGameCommand);
        }
Edited file
FrameConfig.cs
+5
-1

{
    private const int HeightTolerancePixels = 100;

    private static string GetPath()
            bool sameAddonVersion = config.AddonVersion == addonVersion;
            bool sameRect = config.Rect.Width == rect.Width && config.Rect.Height == rect.Height;
            bool sameWidth = config.Rect.Width == rect.Width;
            bool similarHeight = Math.Abs(config.Rect.Height - rect.Height) <= HeightTolerancePixels;
            bool sameRect = sameWidth && similarHeight;
            return sameAddonVersion && sameVersion && sameRect && config.Frames.Length > 1;
Edited file
DependencyInjection.cs
+27
-0

            {
                Rectangle? storedRect = null;
                Version? storedAddonVersion = null;
                int? storedFrameCount = null;

                try
                {
                    DataFrameConfig storedConfig = FrameConfig.Load();
                    storedRect = storedConfig.Rect;
                    storedAddonVersion = storedConfig.AddonVersion;
                    storedFrameCount = storedConfig.Frames.Length;
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, $"{nameof(FrameConfig)} could not load existing config before delete.");
                }

                if (storedRect.HasValue)
                {
                    log.LogError(
                        $"{nameof(FrameConfig)} mismatch. StoredRect={storedRect.Value} CurrentRect={rect} " +
                        $"StoredAddonVersion={storedAddonVersion} CurrentAddonVersion={installVersion} StoredFrames={storedFrameCount}");
                }
                else
                {
                    log.LogError($"{nameof(FrameConfig)} mismatch. StoredRect=<unavailable> CurrentRect={rect} CurrentAddonVersion={installVersion}");
                }

                // At this point the webpage never loads so fallback to configuration page
Edited file
MainLayout.razor
+16
-10

    {
        navItems = new List<NavItem> {
            new() { Id = "cfg-dashboard", Href = "/", Text = "Dashboard", Match = NavLinkMatch.All },
            new() { Id = "cfg-launch", Href = "/launch", Text = "Launch Wizard" },
            new() { Id = "cfg-settings", Href = "/Settings", Text = "Settings" },
            new() { Id = "cfg-combat-rotation", Href = "/combat-rotation", Text = "Combat Rotation" },
            new() { Id = "cfg-frame", Href = "/FrameConfiguration", Text = "Frame Configuration" },
            new() { Id = "cfg-keybindings", Href = "/KeyBindings", Text = "Key Bindings" },
            new() { Id = "cfg-raw", Href = "/RawPlayerReader", Text = "Raw Values" },
        };

        if (!process.IsRunning)
        {
            navItems = new List<NavItem> {
                new() { Id = "0", IconName = IconName.StopBtn, Text = "Process Not Running" },
            };
            navItems.Insert(0, new() { Id = "cfg-process", IconName = IconName.StopBtn, Text = "Process Not Running" });
            return navItems;

        navItems = new List<NavItem> {
            new() { Id = "1", Text = "1. Restart Game" },
        };
        navItems.Add(new() { Id = "cfg-step-1", Text = "1. Restart Game" });

        {
            navItems.Add(new() { Id = "2", Text = "2. Addon Configuration" });
            navItems.Add(new() { Id = "cfg-step-2", Text = "2. Addon Configuration" });
        }
        {
            navItems.Add(new() { Id = "3", Text = "3. Restart Game" });
            navItems.Add(new() { Id = "4", Text = "4. Enter world" });
            navItems.Add(new() { Id = "5", Text = "5. Frame Configuration" });
            navItems.Add(new() { Id = "cfg-step-3", Text = "3. Restart Game" });
            navItems.Add(new() { Id = "cfg-step-4", Text = "4. Enter world" });
            navItems.Add(new() { Id = "cfg-step-5", Text = "5. Frame Configuration" });
        }
Ran Get-Content SharedLib/StartupConfigPathing.cs


Ran rg --files -g "*StartupConfigPathing*.cs"


Ran Get-Content SharedLib/StartupConfig/StartupConfigPathing.cs


Ran Get-Content PathingAPI/Controllers/PPatherController.cs


Ran Get-Content Scripts/StartPathingAPI.bat


Edited file
RemotePathingAPIV3.cs
+23
-1

    private const float DefaultZFallback = 64f;
    private const int InitialConnectDelayMs = 2500;
    private const int ConnectBackoffMinMs = 500;
        int backoffMs = ConnectBackoffMinMs;
        bool delayNextConnectAttempt = true;
        bool wasConnected = client.IsConnected;

        {
            if (!client.IsConnected)
            bool isConnected = client.IsConnected;
            if (!isConnected)
            {
                if (wasConnected)
                {
                    delayNextConnectAttempt = true;
                }

                if (delayNextConnectAttempt)
                {
                    cts.Token.WaitHandle.WaitOne(InitialConnectDelayMs);
                    if (cts.IsCancellationRequested)
                    {
                        break;
                    }

                    delayNextConnectAttempt = false;
                }

                try

            wasConnected = client.IsConnected;

            int waitMs = client.IsConnected ? watchdogPollMs : backoffMs;
I’m patching NavigationServerManager now for two behaviors: monitor-loop auto-restart on crash and a guarded port-conflict cleanup step before launching AmeisenNavigationServer.exe.

Edited file
NavigationServerManager.cs
+125
-1

using System;
using System.Collections.Generic;
using System.Diagnostics;

            TryTerminateProcessHoldingPort(Port, "AmeisenNavigationServer");

            _process = Process.Start(startInfo);
    {
        if (_monitorTask != null && !_monitorTask.IsCompleted)
        {
            return;
        }

        _monitorCts?.Dispose();
        _monitorCts = new CancellationTokenSource();
                    {
                        _logger.LogWarning("[NavigationServerManager] Server process exited unexpectedly");
                        int exitCode = _process.ExitCode;
                        _logger.LogWarning("[NavigationServerManager] Server process exited unexpectedly (code {Code})", exitCode);

                        _process.Dispose();
                        _process = null;
                        _state.NavigationProcess = null;
                        Status = NavigationServerStatus.Stopped;

                        bool restarted = await EnsureRunningAsync(cancellationToken);
                        if (!restarted)
                        {
                            Status = NavigationServerStatus.Failed;
                        }

                        continue;
                    }

    private void TryTerminateProcessHoldingPort(int port, string expectedProcessName)
    {
        try
        {
            HashSet<int> processIds = GetListeningProcessIds(port);
            foreach (int processId in processIds)
            {
                try
                {
                    Process existing = Process.GetProcessById(processId);
                    string processName = existing.ProcessName;

                    if (!processName.Equals(expectedProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "[NavigationServerManager] Port {Port} is held by PID {PID} ({Name}), expected {Expected}. Skipping termination.",
                            port, processId, processName, expectedProcessName);
                        continue;
                    }

                    _logger.LogWarning(
                        "[NavigationServerManager] Terminating stale {Name} process on port {Port} (PID: {PID})",
                        processName, port, processId);

                    existing.Kill(true);
                    if (!existing.WaitForExit(5000))
                    {
                        _logger.LogWarning(
                            "[NavigationServerManager] Process PID {PID} did not exit after termination request",
                            processId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[NavigationServerManager] Failed to terminate process PID {PID} holding port {Port}",
                        processId, port);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NavigationServerManager] Failed to inspect port ownership for {Port}", port);
        }
    }

    private static HashSet<int> GetListeningProcessIds(int port)
    {
        HashSet<int> processIds = [];

        ProcessStartInfo psi = new()
        {
            FileName = "netstat",
            Arguments = "-ano -p tcp",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? process = Process.Start(psi);
        if (process == null)
        {
            return processIds;
        }

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);

        string[] lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string portNeedle = ":" + port.ToString();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase) ||
                !line.Contains(portNeedle, StringComparison.Ordinal))
            {
                continue;
            }

            string[] parts = line.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                continue;
            }

            string localAddress = parts[1];
            if (!localAddress.EndsWith(portNeedle, StringComparison.Ordinal))
            {
                continue;
            }

            if (int.TryParse(parts[^1], out int processId))
            {
                processIds.Add(processId);
            }
        }

        return processIds;
    }

    public void Dispose()
I’m adding a guarded PathingAPI port cleanup path during startup initialization (local RemoteV1 only, and only when /api/PPather/SelfTest is unhealthy) so we don’t kill healthy servers.

Edited file
StartupOrchestrator.cs
+161
-2

using System.Collections.Generic;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;

    private Task<StageResult> InitializeAsync(CancellationToken ct)
    private async Task<StageResult> InitializeAsync(CancellationToken ct)
    {
        _state.StatusMessage = "Initializing startup orchestration...";

        await CleanupPathingApiPortIfStaleAsync(ct);

        _logger.LogInformation("[StartupOrchestrator] Options: AutoLaunchWoW={AutoLaunch}, AutoConfigFrames={AutoConfig}, NavServer={NavServer}",

        return Task.FromResult(StageResult.Success("Initialization complete"));
        return StageResult.Success("Initialization complete");
    }
    }

    private async Task CleanupPathingApiPortIfStaleAsync(CancellationToken cancellationToken)
    {
        if (_pathing.Type != StartupConfigPathing.Types.RemoteV1)
        {
            return;
        }

        int port = _pathing.portv1 > 0 ? _pathing.portv1 : 5001;
        string host = string.IsNullOrWhiteSpace(_pathing.hostv1) ? "127.0.0.1" : _pathing.hostv1;

        if (!IsLocalHost(host))
        {
            return;
        }

        bool healthy = await IsPathingApiHealthyAsync(host, port, cancellationToken);
        if (healthy)
        {
            return;
        }

        TryTerminateProcessHoldingPort(port, "PathingAPI");
    }

    private async Task<bool> IsPathingApiHealthyAsync(string host, int port, CancellationToken cancellationToken)
    {
        string url = $"http://{host}:{port}/api/PPather/SelfTest";

        try
        {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(2) };
            using HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            string payload = await response.Content.ReadAsStringAsync(cancellationToken);
            return payload.Contains("true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void TryTerminateProcessHoldingPort(int port, string expectedProcessName)
    {
        try
        {
            HashSet<int> processIds = GetListeningProcessIds(port);
            foreach (int processId in processIds)
            {
                try
                {
                    Process process = Process.GetProcessById(processId);
                    string processName = process.ProcessName;
                    if (!processName.Equals(expectedProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "[StartupOrchestrator] Port {Port} is held by PID {PID} ({Name}), expected {Expected}. Skipping termination.",
                            port, processId, processName, expectedProcessName);
                        continue;
                    }

                    _logger.LogWarning(
                        "[StartupOrchestrator] Terminating stale {Name} process on port {Port} (PID: {PID})",
                        processName, port, processId);

                    process.Kill(true);
                    if (!process.WaitForExit(5000))
                    {
                        _logger.LogWarning(
                            "[StartupOrchestrator] Process PID {PID} did not exit after termination request",
                            processId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[StartupOrchestrator] Failed to terminate process PID {PID} holding port {Port}",
                        processId, port);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[StartupOrchestrator] Failed to inspect port ownership for {Port}", port);
        }
    }

    private static HashSet<int> GetListeningProcessIds(int port)
    {
        HashSet<int> processIds = [];

        ProcessStartInfo psi = new()
        {
            FileName = "netstat",
            Arguments = "-ano -p tcp",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process? process = Process.Start(psi);
        if (process == null)
        {
            return processIds;
        }

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);

        string[] lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        string portNeedle = ":" + port.ToString();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase) ||
                !line.Contains(portNeedle, StringComparison.Ordinal))
            {
                continue;
            }

            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                continue;
            }

            string localAddress = parts[1];
            if (!localAddress.EndsWith(portNeedle, StringComparison.Ordinal))
            {
                continue;
            }

            if (int.TryParse(parts[^1], out int processId))
            {
                processIds.Add(processId);
            }
        }

        return processIds;
    }

    private static bool IsLocalHost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}
Edited file
NavigationServerManager.cs
+1
-1


            string[] parts = line.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
Ran Get-Content Core/Startup/StartupOrchestrator.cs -TotalCount 25


Edited file
StartupOrchestrator.cs
+0
-1

using System.Collections.Generic;
using System.Collections.Generic;
using System.Diagnostics;
Ran rg -n "ForceAggressiveClearTarget\(wait, bits\)" -S Core/Goals Core/GoalsComponent


Ran rg -n "ForceAggressiveClearTarget\(" Core/Goals/BlacklistTargetGoal.cs Core/GoalsComponent/ReactCastError.cs Core/GoalsComponent/CombatTracker.cs Core/Goals/FleeGoal.cs Core/Goals/PullTargetGoal.cs Core/Goals/TargetPetTargetGoal.cs Core/Goals/TargetFocusTargetGoal.cs Core/Goals/FollowFocusGoal.cs


I finished the code edits across target clearing, navigation recovery, and frame-config/UI. Next I’m running build and the two test suites you specified to catch compile/runtime regressions.

Ran dotnet build MasterOfPuppets.sln


Build failed on one analyzer error (CA1822) in the new startup helper; I’m applying a minimal fix (marking the helper static) and rerunning build/tests.

Edited file
StartupOrchestrator.cs
+1
-1


    private async Task<bool> IsPathingApiHealthyAsync(string host, int port, CancellationToken cancellationToken)
    private static async Task<bool> IsPathingApiHealthyAsync(string host, int port, CancellationToken cancellationToken)
    {
Ran dotnet build MasterOfPuppets.sln


Ran dotnet test CoreUnitTests


Ran dotnet test FrontendUnitTests


Ran git status --short


Ran rg -n "IsValid\(|HeightTolerancePixels|sameWidth|similarHeight" Core/DataFrame/FrameConfig.cs


Ran rg -n "PressClearTarget\(|ForceAggressiveClearTarget\(|Cleared via configured binding" Core/Input/ConfigurableInput.cs


Ran rg -n "StoredRect|mismatch|FrameConfig.Delete\(\)" Core/DependencyInjection.cs


Ran rg -n "ForceAggressiveClearTarget\(wait, bits, execGameCommand\)|ExecGameCommand" Core/Goals/BlacklistTargetGoal.cs Core/Goals/FleeGoal.cs Core/Goals/PullTargetGoal.cs Core/Goals/TargetPetTargetGoal.cs Core/Goals/TargetFocusTargetGoal.cs Core/Goals/FollowFocusGoal.cs Core/GoalsComponent/CombatTracker.cs Core/GoalsComponent/ReactCastError.cs


Ran rg -n "InitialConnectDelayMs|ObserveConnection\(|delayNextConnectAttempt" Core/PPather/RemotePathingAPIV3.cs


Ran rg -n "GetConfigNavItems\(|Dashboard|combat-rotation|RawPlayerReader|Process Not Running" Frontend/Shared/MainLayout.razor


Ran rg -n "CleanupPathingApiPortIfStaleAsync|IsPathingApiHealthyAsync|TryTerminateProcessHoldingPort|GetListeningProcessIds|InitializeAsync" Core/Startup/StartupOrchestrator.cs


Ran rg -n "TryTerminateProcessHoldingPort|MonitorServerAsync|EnsureRunningAsync\(|StartMonitoring\(|GetListeningProcessIds" Core/Startup/NavigationServerManager.cs


Ran git diff --stat


Worked for 8m 26s
Implemented all four phases in code.

What Changed

Target clear path restored to configured binding first:
PressClearTarget() now uses configured ClearTarget binding (Alt-Insert path), not F11: ConfigurableInput.cs (line 216)
ForceAggressiveClearTarget() order is now:
configured binding retries, 2) ESC, 3) F11 fallback, 4) /cleartarget via ExecGameCommand: ConfigurableInput.cs (line 231)
Injected ExecGameCommand and passed it to ForceAggressiveClearTarget() for all requested callers:
BlacklistTargetGoal.cs (line 19)
ReactCastError.cs (line 33)
CombatTracker.cs (line 32)
FleeGoal.cs (line 35)
PullTargetGoal.cs (line 53)
TargetPetTargetGoal.cs (line 18)
TargetFocusTargetGoal.cs (line 16)
FollowFocusGoal.cs (line 19)
Navigation resilience:
Added initial connection warm-up delay before TCP connect attempts in v3 remote pather (kept exponential backoff): RemotePathingAPIV3.cs (line 334)
Navigation monitor now auto-attempts restart after unexpected server exit: NavigationServerManager.cs (line 386)
Added pre-start port-owner cleanup for navigation port (kills stale AmeisenNavigationServer holder): NavigationServerManager.cs (line 275)
Added startup-time stale PathingAPI cleanup for local RemoteV1 only if /api/PPather/SelfTest is unhealthy: StartupOrchestrator.cs (line 586)
Frame config/UI fixes:
FrameConfig.IsValid() now requires exact width but allows ±100px height tolerance: FrameConfig.cs (line 108)
Added stored/current rect diagnostics before config deletion on mismatch: DependencyInjection.cs (line 435)
Expanded config-mode nav with Dashboard, Launch Wizard, Settings, Combat Rotation, Frame Configuration, Key Bindings, Raw Values (while keeping wizard steps): MainLayout.razor (line 187)
Verification Run

dotnet build MasterOfPuppets.sln passed (0 errors).
dotnet test CoreUnitTests passed: 161/161.
dotnet test FrontendUnitTests passed: 7/7.
Live WoW runtime validation steps (target clear behavior, nav process stability >5 min, dashboard/combat-rotation accessibility in running app) were not runnable in this headless environment.


15 files changed
+416
-47
Undo

Review
Core/DataFrame/FrameConfig.cs
Core/DependencyInjection.cs
Core/Goals/BlacklistTargetGoal.cs
Core/Goals/FleeGoal.cs
Core/Goals/FollowFocusGoal.cs
Core/Goals/PullTargetGoal.cs
Core/Goals/TargetFocusTargetGoal.cs
Core/Goals/TargetPetTargetGoal.cs
Core/GoalsComponent/CombatTracker.cs
Core/GoalsComponent/ReactCastError.cs
Core/Input/ConfigurableInput.cs
Core/PPather/RemotePathingAPIV3.cs
Core/Startup/NavigationServerManager.cs
Core/Startup/StartupOrchestrator.cs
Frontend/Shared/MainLayout.razor
