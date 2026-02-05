<#
.SYNOPSIS
  WowClassicGrindBot - Production one-click launcher for Windows.

.DESCRIPTION
  Performs pre-flight checks, installs/validates addons, launches required components
  in the correct sequence, and opens the Launch Wizard (/launch) for staged readiness
  checks before bot activation.

  By default the launcher does NOT auto-start the bot. It keeps services online with
  monitoring + automatic restart while you complete the Launch Wizard checklist.
  Legacy automation switches (AutoFix/AutoStartBot/RunValidation) are available for
  advanced users, but are disabled by default.

.NOTES
  Requires: PowerShell 5.1+, .NET 10 runtime, Windows 10/11
#>

#Requires -Version 5.1

param(
    [string]$BotRoot = "",
    [string]$WoWPathOverride = "",
    [int]$WebPort = 5000,
    [int]$PathingApiPort = 5001,
    [int]$NavPort = 47110,

    [bool]$ShowDashboard = $true,
    [int]$DashboardTailLines = 400,
    [int]$DashboardRefreshMs = 500,

    [bool]$AutoLaunchWoW = $true,
    [bool]$AutoStartBot = $false,
    [bool]$AutoFix = $false,
    [bool]$RunValidation = $false,

    [string]$ExpectedRace = "BloodElf",
    [string]$ExpectedClass = "Rogue",
    [int]$ExpectedLevel = 8,
    [string]$ProfileFileName = "BloodElf_Rogue_Starter_Test.json",

    [ValidateSet("Auto", "Local", "RemoteV1", "RemoteV3")]
    [string]$PathingMode = "Auto",

    [int]$MonitorIntervalSeconds = 10,
    [bool]$ExitAfterStartup = $false,
    [int]$AlignmentTimeoutSeconds = 300,
    [bool]$EnableNavigationServer = $true,
    [int]$NavigationMaxRestarts = 2,
    [int]$NavigationRestartWindowSeconds = 300
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ScriptRoot {
    return $PSScriptRoot
}

if ([string]::IsNullOrWhiteSpace($BotRoot)) {
    $BotRoot = (Resolve-Path -LiteralPath (Join-Path (Get-ScriptRoot) "..")).Path
} else {
    $BotRoot = (Resolve-Path -LiteralPath $BotRoot).Path
}

$logsDir = Join-Path $BotRoot "logs"
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
$logFile = Join-Path $logsDir ("launcher-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
$latestLogFile = Join-Path $logsDir "launcher-latest.log"
$runId = [Guid]::NewGuid().ToString("n")

if ($ShowDashboard -and -not $env:WCG_LAUNCHER_DASHBOARD) {
    try {
        $env:WCG_LAUNCHER_DASHBOARD = "1"
        $dash = Join-Path $PSScriptRoot "LauncherDashboard.ps1"
        if (Test-Path -LiteralPath $dash) {
            Start-Process -FilePath "powershell" -ArgumentList @(
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-File", $dash,
                "-LogPath", $latestLogFile,
                "-LogsDir", $logsDir,
                "-TailLines", "$DashboardTailLines",
                "-RefreshMs", "$DashboardRefreshMs"
            ) | Out-Null
        }
    } catch {
        # Dashboard is optional; never fail launcher startup because of UI.
    }
}

function Write-Log {
    param(
        [Parameter(Mandatory)] [string]$Message,
        [ValidateSet("INFO", "OK", "WARN", "ERROR")] [string]$Level = "INFO"
    )

    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $line = "[{0}] [{1}] {2}" -f $ts, $Level, $Message
    $line | Out-File -FilePath $logFile -Encoding utf8 -Append
    $line | Out-File -FilePath $latestLogFile -Encoding utf8 -Append

    switch ($Level) {
        "OK"    { Write-Host $line -ForegroundColor Green }
        "WARN"  { Write-Host $line -ForegroundColor Yellow }
        "ERROR" { Write-Host $line -ForegroundColor Red }
        default { Write-Host $line -ForegroundColor Gray }
    }
}

function Get-LastLines {
    param(
        [Parameter(Mandatory)][string]$Path,
        [int]$Tail = 200
    )

    try {
        if (-not (Test-Path -LiteralPath $Path)) { return @() }
        return @(Get-Content -LiteralPath $Path -Tail $Tail -ErrorAction Stop)
    } catch {
        return @()
    }
}

function Write-CrashReport {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$ExePath,
        [string[]]$Args = @(),
        [hashtable]$Environment = @{},
        [System.Diagnostics.Process]$Process = $null,
        [string]$Reason = ""
    )

    $ts = Get-Date -Format "yyyyMMdd-HHmmss"
    $reportPath = Join-Path $logsDir ("crash-{0}-{1}-{2}.json" -f $Name, $ts, $runId)

    $exitCode = $null
    $pid = $null
    $stdoutPath = $null
    $stderrPath = $null

    if ($Process) {
        try {
            $Process.Refresh()
            $pid = $Process.Id
            $stdoutPath = $Process.StdOutPath
            $stderrPath = $Process.StdErrPath
            if ($Process.HasExited) {
                try { $exitCode = $Process.ExitCode } catch { }
            }
        } catch { }
    }

    $payload = [ordered]@{
        Timestamp = (Get-Date).ToString("o")
        RunId = $runId
        Reason = $Reason
        Service = $Name
        ExePath = $ExePath
        Args = $Args
        Environment = $Environment
        Process = [ordered]@{
            Pid = $pid
            ExitCode = $exitCode
            StdOutPath = $stdoutPath
            StdErrPath = $stderrPath
        }
        Host = [ordered]@{
            User = $env:USERNAME
            Computer = $env:COMPUTERNAME
            OS = [System.Environment]::OSVersion.VersionString
            DotNet = (Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue)
            CurrentDirectory = (Get-Location).Path
        }
        LastStdOut = if ($stdoutPath) { Get-LastLines -Path $stdoutPath -Tail 200 } else { @() }
        LastStdErr = if ($stderrPath) { Get-LastLines -Path $stderrPath -Tail 200 } else { @() }
        LauncherLog = $logFile
    }

    try {
        Write-JsonFile -Path $reportPath -Value $payload
        Write-Log "Crash report written: $reportPath" "ERROR"
    } catch {
        Write-Log "Failed to write crash report: $($_.Exception.Message)" "ERROR"
    }
}

function Test-IsAdmin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    return $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Restart-ElevatedIfNeeded {
    param([string]$Reason)

    if (Test-IsAdmin) { return }

    Write-Log "Elevation required: $Reason" "WARN"
    Write-Log "Requesting UAC elevation..." "INFO"

    $ps = if (Get-Command pwsh -ErrorAction SilentlyContinue) { "pwsh" } else { "powershell" }
    $args = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $PSScriptRoot "OneClickLauncher.ps1"),
        "-BotRoot", $BotRoot,
        "-WoWPathOverride", $WoWPathOverride,
        "-WebPort", $WebPort,
        "-PathingApiPort", $PathingApiPort,
        "-NavPort", $NavPort,
        "-ExpectedRace", $ExpectedRace,
        "-ExpectedClass", $ExpectedClass,
        "-ExpectedLevel", $ExpectedLevel,
        "-ProfileFileName", $ProfileFileName,
        "-PathingMode", $PathingMode,
        "-MonitorIntervalSeconds", $MonitorIntervalSeconds,
        "-AutoLaunchWoW:$AutoLaunchWoW",
        "-AutoStartBot:$AutoStartBot",
        "-AutoFix:$AutoFix",
        "-RunValidation:$RunValidation",
        "-ExitAfterStartup:$ExitAfterStartup",
        "-AlignmentTimeoutSeconds", $AlignmentTimeoutSeconds,
        "-EnableNavigationServer:$EnableNavigationServer",
        "-NavigationMaxRestarts", $NavigationMaxRestarts,
        "-NavigationRestartWindowSeconds", $NavigationRestartWindowSeconds
    )

    Start-Process -FilePath $ps -ArgumentList $args -Verb RunAs | Out-Null
    exit 0
}

function Require-File {
    param([string]$Path, [string]$Hint)
    if (-not (Test-Path $Path)) {
        throw "Missing required file: $Path. $Hint"
    }
}

function Ensure-ReleaseBuild {
    param(
        [Parameter(Mandatory)][string]$SolutionPath,
        [Parameter(Mandatory)][string]$ExpectedOutputPath
    )

    if (Test-Path $ExpectedOutputPath) {
        return
    }

    Write-Log "Missing build output: $ExpectedOutputPath" "WARN"
    Write-Log "Attempting to build Release artifacts..." "INFO"

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet not found. Install .NET 10 SDK or use prebuilt binaries."
    }

    & dotnet build $SolutionPath -c Release

    if (-not (Test-Path $ExpectedOutputPath)) {
        throw "Build completed but expected output still missing: $ExpectedOutputPath"
    }

    Write-Log "Release build artifacts generated" "OK"
}

function Get-DotNetRuntimes {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        return @()
    }
    $out = & dotnet --list-runtimes 2>$null
    if (-not $out) { return @() }
    return $out
}

function Assert-DotNet10 {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw ".NET not found. Install .NET 10 Runtime/SDK and re-run."
    }

    $runtimes = Get-DotNetRuntimes
    $hasCore = $runtimes | Where-Object { $_ -match "^Microsoft\.NETCore\.App\s+10\." } | Select-Object -First 1
    $hasAsp = $runtimes | Where-Object { $_ -match "^Microsoft\.AspNetCore\.App\s+10\." } | Select-Object -First 1
    if (-not $hasCore -or -not $hasAsp) {
        throw "Missing .NET 10 runtime. Found runtimes:`n$($runtimes -join "`n")"
    }
}

function Read-JsonFile {
    param([string]$Path)
    return (Get-Content -Raw -Path $Path -Encoding UTF8) | ConvertFrom-Json
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][object]$Value
    )

    $json = $Value | ConvertTo-Json -Depth 10
    $dir = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    Set-Content -LiteralPath $Path -Value $json -Encoding UTF8
}

function Ensure-DataConfigJson {
    param(
        [Parameter(Mandatory)][string]$TargetDirectory
    )

    $target = Join-Path $TargetDirectory "data_config.json"
    $rootPath = Join-Path $BotRoot "json"

    # Keep this file correct regardless of where the process is started from.
    # DataConfig.Load() treats Root as a path base for "dbc", "class", etc.
    $payload = @{
        Version = 14
        Root = $rootPath
    }

    try {
        Write-JsonFile -Path $target -Value $payload
        Write-Log "Ensured data_config.json for service: $target (Root=$rootPath)" "OK"
    } catch {
        Write-Log "Failed to write service data_config.json: $target ($($_.Exception.Message))" "WARN"
    }
}

function Try-GetWoWPathFromRunningProcess {
    try {
        $p = Get-CimInstance Win32_Process -Filter "Name='WowClassic.exe'" -ErrorAction Stop | Select-Object -First 1
        if ($null -ne $p -and $p.ExecutablePath) {
            return Split-Path -Parent $p.ExecutablePath
        }
    } catch {
        return $null
    }
    return $null
}

function Resolve-WoWPath {
    $fromProc = Try-GetWoWPathFromRunningProcess
    if ($fromProc) { return $fromProc }

    $appSettings = Join-Path $BotRoot "BlazorServer\\appsettings.json"
    if (Test-Path $appSettings) {
        try {
            $json = Read-JsonFile -Path $appSettings
            if ($json.Startup -and $json.Startup.WoWPath) {
                $candidate = [string]$json.Startup.WoWPath
                if (Test-Path (Join-Path $candidate "WowClassic.exe")) {
                    return $candidate
                }
            }
        } catch { }
    }

    $candidates = @(
        "C:\\Program Files (x86)\\World of Warcraft\\_anniversary_",
        "C:\\Program Files (x86)\\World of Warcraft\\_classic_",
        "C:\\Program Files\\World of Warcraft\\_anniversary_",
        "C:\\Program Files\\World of Warcraft\\_classic_"
    )

    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c "WowClassic.exe")) { return $c }
    }

    return $null
}

function Ensure-AddonInstalled {
    param(
        [Parameter(Mandatory)][string]$WoWPath,
        [Parameter(Mandatory)][string[]]$AddonNames
    )

    $dest = Join-Path $WoWPath "Interface\\AddOns"
    $srcRoot = Join-Path $BotRoot "Addons"

    foreach ($name in $AddonNames) {
        $src = Join-Path $srcRoot $name
        $dst = Join-Path $dest $name

        if (-not (Test-Path $src)) {
            Write-Log "Addon source missing in repo: $src" "WARN"
            continue
        }

        if (Test-Path $dst) {
            Write-Log "Addon present: $name" "OK"
            continue
        }

        try {
            Write-Log "Installing addon: $name -> $dst" "INFO"
            New-Item -ItemType Directory -Path $dest -Force | Out-Null
            Copy-Item -Path $src -Destination $dst -Recurse -Force -ErrorAction Stop
            Write-Log "Addon installed: $name" "OK"
        }
        catch [System.UnauthorizedAccessException] {
            Restart-ElevatedIfNeeded -Reason "Install WoW addon '$name' to $dest"
            throw
        }
    }
}

function Ensure-BindPadMinimalXmlEncoding {
    param([string]$WoWPath)

    $bindPadDir = Join-Path $WoWPath "Interface\\AddOns\\BindPadMinimal"
    if (-not (Test-Path $bindPadDir)) { return }

    try {
        Write-Log "Ensuring BindPadMinimal files use ASCII encoding (no BOM)..." "INFO"

        $tocContent = @"
## Interface: 20505
## Title: BindPadMinimal
## Version: 1.0
## Author: WowClassicGrindBot
## Notes: Minimal BindPad replacement providing BindPadMacro button for DataToColor

BindPadMinimal.xml
"@

        $xmlContent = "<Ui xmlns=""http://www.blizzard.com/wow/ui/"">`n<Button name=""BindPadMacro"" inherits=""SecureActionButtonTemplate""/>`n<Button name=""BindPadKey"" inherits=""SecureActionButtonTemplate""/>`n</Ui>"

        $tocPath = Join-Path $bindPadDir "BindPadMinimal.toc"
        $xmlPath = Join-Path $bindPadDir "BindPadMinimal.xml"

        [System.IO.File]::WriteAllText($tocPath, $tocContent, [System.Text.Encoding]::ASCII)
        [System.IO.File]::WriteAllText($xmlPath, $xmlContent, [System.Text.Encoding]::ASCII)

        Write-Log "BindPadMinimal encoding ensured" "OK"
    } catch {
        if ($_.Exception -is [System.UnauthorizedAccessException]) {
            Restart-ElevatedIfNeeded -Reason "Fix BindPadMinimal encoding under $bindPadDir"
            throw
        }
        Write-Log "BindPadMinimal encoding fix failed: $($_.Exception.Message)" "WARN"
    }
}

function Test-TcpPort {
    param([string]$Address, [int]$Port, [int]$TimeoutMs = 1000)
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $iar = $client.BeginConnect($Address, $Port, $null, $null)
        if (-not $iar.AsyncWaitHandle.WaitOne($TimeoutMs, $false)) {
            $client.Close()
            return $false
        }
        $client.EndConnect($iar)
        $client.Close()
        return $true
    } catch {
        return $false
    }
}

function Test-LocalTcpListenPort {
    param([int]$Port)

    try {
        $listeners = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
        foreach ($ep in $listeners) {
            if ($ep.Port -eq $Port) { return $true }
        }
        return $false
    } catch {
        return $false
    }
}

function Find-FreeTcpPort {
    param([int]$StartPort, [int]$MaxTries = 50)

    for ($i = 0; $i -lt $MaxTries; $i++) {
        $p = $StartPort + $i
        if (-not (Test-TcpPort -Address "127.0.0.1" -Port $p -TimeoutMs 150)) {
            return $p
        }
    }

    throw "Unable to find a free TCP port starting at $StartPort"
}

function Start-ManagedProcess {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$ExePath,
        [Parameter(Mandatory)][string]$WorkingDir,
        [string[]]$Args = @(),
        [hashtable]$Environment = @{},
        [string]$StdOutPath = "",
        [string]$StdErrPath = ""
    )

    Require-File -Path $ExePath -Hint "Build the solution (dotnet build -c Release) or restore binaries."

    if ([string]::IsNullOrWhiteSpace($StdOutPath)) {
        $StdOutPath = Join-Path $logsDir ("{0}-{1}-{2}-stdout.log" -f $Name, (Get-Date -Format "yyyyMMdd-HHmmss"), $runId)
    }
    if ([string]::IsNullOrWhiteSpace($StdErrPath)) {
        $StdErrPath = Join-Path $logsDir ("{0}-{1}-{2}-stderr.log" -f $Name, (Get-Date -Format "yyyyMMdd-HHmmss"), $runId)
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $StdOutPath) -Force | Out-Null
    New-Item -ItemType Directory -Path (Split-Path -Parent $StdErrPath) -Force | Out-Null

    $psi = @{
        FilePath = $ExePath
        WorkingDirectory = $WorkingDir
        ArgumentList = $Args
        PassThru = $true
        WindowStyle = "Minimized"
        RedirectStandardOutput = $StdOutPath
        RedirectStandardError = $StdErrPath
        NoNewWindow = $true
    }

    $previousEnv = @{}
    foreach ($k in $Environment.Keys) {
        $envPath = "Env:$k"
        $existing = Test-Path $envPath
        $previousEnv[$k] = @{
            Exists = $existing
            Value = if ($existing) { (Get-Item $envPath).Value } else { $null }
        }

        Set-Item -Path $envPath -Value ([string]$Environment[$k])
    }

    # Note: Primary logs are emitted by the services themselves (e.g., out.log). Launcher logs are in $logFile.

    try {
        Write-Log "Starting ${Name}: $ExePath" "INFO"
        $p = Start-Process @psi
        try {
            $p | Add-Member -NotePropertyName "StdOutPath" -NotePropertyValue $StdOutPath -Force
            $p | Add-Member -NotePropertyName "StdErrPath" -NotePropertyValue $StdErrPath -Force
        } catch { }
        Write-Log "$Name started (PID: $($p.Id))" "OK"
        return $p
    }
    finally {
        foreach ($k in $previousEnv.Keys) {
            $envPath = "Env:$k"
            $prev = $previousEnv[$k]
            if ($prev.Exists) {
                Set-Item -Path $envPath -Value ([string]$prev.Value)
            } else {
                Remove-Item -Path $envPath -ErrorAction SilentlyContinue
            }
        }
    }
}

function Invoke-HttpJson {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Url,
        [object]$Body = $null,
        [int]$TimeoutSec = 5
    )

    $irm = @{
        Method = $Method
        Uri = $Url
        TimeoutSec = $TimeoutSec
        ErrorAction = "Stop"
    }
    if ($null -ne $Body) {
        $irm.Body = ($Body | ConvertTo-Json -Depth 10)
        $irm.ContentType = "application/json"
    }

    return Invoke-RestMethod @irm
}

function Test-IsHttp404 {
    param([Parameter(Mandatory)]$ErrorRecord)

    try {
        $ex = $ErrorRecord.Exception
        if ($ex -and $ex.Response -and $ex.Response.StatusCode) {
            return ([int]$ex.Response.StatusCode) -eq 404
        }
        if ($ex -is [System.Net.WebException] -and $ex.Response -and $ex.Response.StatusCode) {
            return ([int]$ex.Response.StatusCode) -eq 404
        }
    } catch { }

    try {
        return [string]$ErrorRecord.Exception.Message -match "\\b404\\b"
    } catch {
        return $false
    }
}

function Invoke-HttpJsonWithFallbackOn404 {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$PrimaryUrl,
        [Parameter(Mandatory)][string]$FallbackUrl,
        [object]$Body = $null,
        [int]$TimeoutSec = 5
    )

    try {
        return Invoke-HttpJson -Method $Method -Url $PrimaryUrl -Body $Body -TimeoutSec $TimeoutSec
    } catch {
        if (-not (Test-IsHttp404 -ErrorRecord $_)) {
            throw
        }
        return Invoke-HttpJson -Method $Method -Url $FallbackUrl -Body $Body -TimeoutSec $TimeoutSec
    }
}

function Wait-ForHttp {
    param(
        [Parameter(Mandatory)][string]$Url,
        [int]$TimeoutSeconds = 90
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $null = Invoke-HttpJson -Method "GET" -Url $Url -TimeoutSec 3
            return $true
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }
    return $false
}

function Wait-ForSystemReady {
    param(
        [string]$BaseUrl,
        [int]$TimeoutSeconds = 300
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $status = Invoke-HttpJson -Method "GET" -Url ("{0}/api/test/status" -f $BaseUrl) -TimeoutSec 3
            if ($status -and $status.Success -eq $true) {
                $allPassed = $true
                foreach ($c in $status.Checks) {
                    if ($c.Passed -ne $true) { $allPassed = $false; break }
                }
                if ($allPassed) { return $status }
            }
        } catch { }

        Start-Sleep -Seconds 2
    }
    return $null
}

function Get-CharacterString {
    param([object]$TestStatus)
    try { return [string]$TestStatus.Data.character } catch { return "" }
}

function Test-ExpectedCharacter {
    param([string]$CharacterString)

    if ([string]::IsNullOrWhiteSpace($CharacterString)) { return $false }

    $actual = $CharacterString.Trim()
    $expected = ("{0} {1} L{2}" -f $ExpectedRace, $ExpectedClass, $ExpectedLevel).Trim()
    if ($actual.Equals($expected, [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    $pattern = "^(?<race>\\w+)\\s+(?<class>\\w+)\\s+L(?<level>\\d+)$"
    $m = [regex]::Match($actual, $pattern)
    if (-not $m.Success) { return $false }

    $race = $m.Groups["race"].Value
    $class = $m.Groups["class"].Value
    $level = [int]$m.Groups["level"].Value

    return (
        $race.Equals($ExpectedRace, [StringComparison]::OrdinalIgnoreCase) -and
        $class.Equals($ExpectedClass, [StringComparison]::OrdinalIgnoreCase) -and
        $level -eq $ExpectedLevel
    )
}

function Try-AutoFix {
    param([string]$BaseUrl)
    try {
        $r = Invoke-HttpJson -Method "POST" -Url ("{0}/api/diagnostics/fix/all" -f $BaseUrl) -TimeoutSec 30
        Write-Log "Auto-fix: $($r.Message)" "OK"
        return $true
    } catch {
        Write-Log "Auto-fix failed: $($_.Exception.Message)" "WARN"
        return $false
    }
}

function Try-LoadProfile {
    param([string]$BaseUrl, [string]$FileName)
    try {
        $r = Invoke-HttpJsonWithFallbackOn404 `
            -Method "POST" `
            -PrimaryUrl ("{0}/api/bot/profile/load" -f $BaseUrl) `
            -FallbackUrl ("{0}/api/BotApi/profile/load" -f $BaseUrl) `
            -Body @{ fileName = $FileName } `
            -TimeoutSec 10
        Write-Log "Profile load: $($r.Message)" "OK"
        return $true
    } catch {
        Write-Log "Profile load failed: $($_.Exception.Message)" "WARN"
        return $false
    }
}

function Try-StartBot {
    param([string]$BaseUrl)
    try {
        $r = Invoke-HttpJsonWithFallbackOn404 `
            -Method "POST" `
            -PrimaryUrl ("{0}/api/bot/start" -f $BaseUrl) `
            -FallbackUrl ("{0}/api/BotApi/start" -f $BaseUrl) `
            -TimeoutSec 10
        Write-Log $r.Message "OK"
        return $true
    } catch {
        Write-Log "Start bot failed: $($_.Exception.Message)" "WARN"
        return $false
    }
}

function Run-ValidationSuite {
    param([string]$BaseUrl)

    $endpoints = @(
        @{ Name = "Frames"; Url = "{0}/api/test/frames" -f $BaseUrl; Method = "GET" },
        @{ Name = "Snapshot"; Url = "{0}/api/test/snapshot" -f $BaseUrl; Method = "GET" },
        @{ Name = "WASD Movement"; Url = "{0}/api/test/movement/wasd" -f $BaseUrl; Method = "POST" },
        @{ Name = "Combat Cycle"; Url = "{0}/api/test/combat/cycle" -f $BaseUrl; Method = "POST" }
    )

    foreach ($e in $endpoints) {
        try {
            Write-Log "Validation: $($e.Name)..." "INFO"
            $r = Invoke-HttpJson -Method $e.Method -Url $e.Url -TimeoutSec 120
            if ($r.Success -eq $true) {
                Write-Log "Validation PASS: $($e.Name) ($($r.Message))" "OK"
            } else {
                Write-Log "Validation FAIL: $($e.Name) ($($r.Error))" "WARN"
            }
        } catch {
            Write-Log "Validation ERROR: $($e.Name) ($($_.Exception.Message))" "WARN"
        }
    }
}

function Determine-PathingMode {
    param([bool]$NavAvailable, [bool]$PathingApiAvailable)

    if ($PathingMode -ne "Auto") { return $PathingMode }
    if ($NavAvailable) { return "RemoteV3" }
    if ($PathingApiAvailable) { return "RemoteV1" }
    return "Local"
}

Write-Log "WowClassicGrindBot One-Click Launcher" "INFO"
Write-Log "BotRoot: $BotRoot" "INFO"
Write-Log "Log: $logFile" "INFO"

$managed = [ordered]@{
    NavigationServer = @{ Process = $null; Managed = $false }
    PathingAPI = @{ Process = $null; Managed = $false }
    BlazorServer = @{ Process = $null; Managed = $false }
}

function Stop-Managed {
    foreach ($k in @("BlazorServer", "PathingAPI", "NavigationServer")) {
        $entry = $managed[$k]
        if (-not $entry.Managed) { continue }
        $p = $entry.Process
        if ($null -eq $p) { continue }
        try {
            $p.Refresh()
            if (-not $p.HasExited) {
                Write-Log "Stopping $k (PID: $($p.Id))" "INFO"
                Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
            }
        } catch { }
    }
}

$null = Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action { Stop-Managed } | Out-Null

try {
    Assert-DotNet10
    Write-Log ".NET 10 runtime present" "OK"

    $blazorExe = Join-Path $BotRoot "BlazorServer\\bin\\Release\\net10.0\\BlazorServer.exe"
    $blazorDir = Split-Path -Parent $blazorExe

    $sln = Join-Path $BotRoot "MasterOfPuppets.sln"
    Require-File -Path $sln -Hint "Repository is incomplete."
    Ensure-ReleaseBuild -SolutionPath $sln -ExpectedOutputPath $blazorExe
    Ensure-DataConfigJson -TargetDirectory $blazorDir

    $navExe = Join-Path $BotRoot "Navigation\\AmeisenNavigationServer.exe"
    $navDir = Split-Path -Parent $navExe

    $pathingExe = Join-Path $BotRoot "PathingAPI\\bin\\Release\\net10.0\\PathingAPI.exe"
    $pathingDir = Split-Path -Parent $pathingExe

    $wowPath = $null
    if (-not [string]::IsNullOrWhiteSpace($WoWPathOverride)) {
        $wowPath = $WoWPathOverride
        Write-Log "WoW path override: $wowPath" "WARN"
    }

    if (-not $wowPath) {
        $wowPath = Resolve-WoWPath
    }
    if (-not $wowPath) {
        throw "Could not detect WoW installation path. Set Startup:WoWPath in BlazorServer\\appsettings.json or install WoW in a standard path."
    }
    Write-Log "WoW path: $wowPath" "OK"

    Ensure-AddonInstalled -WoWPath $wowPath -AddonNames @("DataToColor", "BindPadMinimal", "cTimerBackport", "SoundKitBackport")
    Ensure-BindPadMinimalXmlEncoding -WoWPath $wowPath

    $navAvailable = $false
    $navProcess = $null
    $navRestartHistory = New-Object System.Collections.Generic.List[datetime]
    $navDisabledForSession = $false

    if (-not $EnableNavigationServer) {
        Write-Log "Navigation server disabled via -EnableNavigationServer:$false; continuing with fallback pathing." "WARN"
    } elseif (-not (Test-Path $navExe)) {
        Write-Log "Navigation server not found at: $navExe (skipping)" "WARN"
    } else {
        $mmaps = Join-Path $BotRoot "Navigation\\mmaps"
        $hasMmap = Test-Path (Join-Path $mmaps "*.mmap")
        if ($hasMmap) {
            if (-not (Test-LocalTcpListenPort -Port $NavPort)) {
                $navProcess = Start-ManagedProcess -Name "NavigationServer" -ExePath $navExe -WorkingDir $navDir
                $managed.NavigationServer.Process = $navProcess
                $managed.NavigationServer.Managed = $true

                $deadline = (Get-Date).AddSeconds(20)
                while ((Get-Date) -lt $deadline) {
                    if (Test-LocalTcpListenPort -Port $NavPort) { break }
                    Start-Sleep -Milliseconds 500
                }
            }

            if (Test-LocalTcpListenPort -Port $NavPort) {
                $navAvailable = $true
                Write-Log "Navigation server ready on 127.0.0.1:$NavPort" "OK"
            } else {
                Write-Log "Navigation server unavailable; continuing with fallback pathing." "WARN"
                if ($navProcess -and -not $navProcess.HasExited) {
                    try { Stop-Process -Id $navProcess.Id -Force } catch { }
                }
            }
        } else {
            Write-Log "MMAP files missing; skipping navigation server." "WARN"
        }
    }

    $pathingApiAvailable = $false
    $pathingProcess = $null
    if (Test-Path $pathingExe) {
        Ensure-DataConfigJson -TargetDirectory (Split-Path -Parent $pathingExe)
        if (-not (Test-TcpPort -Address "127.0.0.1" -Port $PathingApiPort -TimeoutMs 250)) {
            $pathingProcess = Start-ManagedProcess -Name "PathingAPI" -ExePath $pathingExe -WorkingDir $pathingDir
            $managed.PathingAPI.Process = $pathingProcess
            $managed.PathingAPI.Managed = $true
            Start-Sleep -Seconds 2
        }
        if (Test-TcpPort -Address "127.0.0.1" -Port $PathingApiPort -TimeoutMs 250) {
            $pathingApiAvailable = $true
            Write-Log "PathingAPI ready on 127.0.0.1:$PathingApiPort" "OK"
        }
    }

    # If nav server is crashy, avoid RemoteV3. We'll still start it if enabled, but prefer stability.
    $resolvedPathingMode = Determine-PathingMode -NavAvailable $navAvailable -PathingApiAvailable $pathingApiAvailable
    if ($resolvedPathingMode -eq "RemoteV3" -and -not $navAvailable) {
        $resolvedPathingMode = if ($pathingApiAvailable) { "RemoteV1" } else { "Local" }
    }
    Write-Log "Pathing mode: $resolvedPathingMode" "OK"

    $envOverrides = @{
        "Startup__AutoLaunchWoW" = $AutoLaunchWoW
        "Startup__AutoStartNavigationServer" = $false
        "Startup__AutoOpenBrowser" = $false
        "Startup__EnableHealthMonitoring" = $false
        "Startup__WebUIPort" = $WebPort
        "Startup__NavigationServerPort" = $NavPort
        "Startup__WoWPath" = $wowPath
        "Pathing__Mode" = $resolvedPathingMode
        "Pathing__portv1" = $PathingApiPort
        "Pathing__portv3" = $NavPort
    }

    $baseUrl = "http://localhost:$WebPort"

    $existingHealth = $null
    try { $existingHealth = Invoke-HttpJson -Method "GET" -Url "$baseUrl/api/health" -TimeoutSec 2 } catch { }

    if ($existingHealth -and $existingHealth.Status -eq "OK") {
        Write-Log "BlazorServer already running at $baseUrl (reusing existing instance)" "OK"
        $managed.BlazorServer.Managed = $false
    } else {
        if (Test-TcpPort -Address "127.0.0.1" -Port $WebPort -TimeoutMs 150) {
            $newPort = Find-FreeTcpPort -StartPort ($WebPort + 1)
            Write-Log "Port $WebPort is in use. Switching Web UI port to $newPort" "WARN"
            $WebPort = $newPort
            $envOverrides["Startup__WebUIPort"] = $WebPort
            $baseUrl = "http://localhost:$WebPort"
        }

        $blazorProcess = Start-ManagedProcess -Name "BlazorServer" -ExePath $blazorExe -WorkingDir $blazorDir -Environment $envOverrides
        $managed.BlazorServer.Process = $blazorProcess
        $managed.BlazorServer.Managed = $true

        if (-not (Wait-ForHttp -Url "$baseUrl/api/health" -TimeoutSeconds 60)) {
            Write-CrashReport -Name "BlazorServer" -ExePath $blazorExe -Args @() -Environment $envOverrides -Process $blazorProcess -Reason "Web UI health endpoint did not become reachable within timeout."
            throw "BlazorServer did not become reachable at $baseUrl within timeout."
        }
    }
    Write-Log "Web UI reachable: $baseUrl" "OK"

    $wizardUrl = "$baseUrl/launch"
    Write-Log "Launch Wizard: $wizardUrl" "OK"
    try { Start-Process $wizardUrl | Out-Null } catch { }

    $navSummary =
        if (-not $EnableNavigationServer) { "Disabled" }
        elseif ($navDisabledForSession) { "Disabled (restart limit)" }
        elseif ($navAvailable) { "Enabled (127.0.0.1:$NavPort)" }
        else { "Enabled (unavailable)" }

    $pathingSummary = if ($pathingApiAvailable) { "Up (127.0.0.1:$PathingApiPort)" } else { "Down" }
    Write-Log "Service summary: WebUI=$baseUrl | PathingMode=$resolvedPathingMode | PathingAPI=$pathingSummary | Nav=$navSummary" "OK"

    Write-Log "Complete the Launch Wizard before starting the bot (Start Bot is disabled until all required checks are green)." "INFO"

    if ($AutoFix -or $AutoStartBot -or $RunValidation) {
        Write-Log "Advanced automation enabled (AutoFix=$AutoFix, AutoStartBot=$AutoStartBot, RunValidation=$RunValidation)" "WARN"

        Write-Log "Waiting for client alignment (addon comms, frames, data freshness)..." "INFO"
        $ready = Wait-ForSystemReady -BaseUrl $baseUrl -TimeoutSeconds $AlignmentTimeoutSeconds
        if (-not $ready) {
            Write-Log "System not fully ready within timeout. Keeping services online for manual inspection." "WARN"
        } else {
            $char = Get-CharacterString -TestStatus $ready
            Write-Log "Aligned character: $char" "OK"

            $isExpected = Test-ExpectedCharacter -CharacterString $char
            if (-not $isExpected) {
                Write-Log "Expected '$ExpectedRace $ExpectedClass L$ExpectedLevel' but got '$char' - skipping gameplay-affecting automation." "WARN"
            } else {
                if ($AutoFix) { $null = Try-AutoFix -BaseUrl $baseUrl }
                $null = Try-LoadProfile -BaseUrl $baseUrl -FileName $ProfileFileName

                if ($AutoStartBot) { $null = Try-StartBot -BaseUrl $baseUrl }
                if ($RunValidation) { Run-ValidationSuite -BaseUrl $baseUrl }
            }
        }
    }

    if ($ExitAfterStartup) {
        Write-Log "ExitAfterStartup=true; exiting before monitor loop." "INFO"
        return
    }

    Write-Log "Entering monitor loop (interval: ${MonitorIntervalSeconds}s). Close this window to stop." "INFO"

    $restartBackoff = @{
        "NavigationServer" = 5
        "PathingAPI" = 5
        "BlazorServer" = 5
    }

    while ($true) {
        Start-Sleep -Seconds $MonitorIntervalSeconds

        # Navigation server
        if ($navProcess -and $navAvailable -and $managed.NavigationServer.Managed -and -not $navDisabledForSession) {
            $portOk = Test-LocalTcpListenPort -Port $NavPort
            $exited = $false
            $exitCode = $null

            try {
                $navProcess.Refresh()
                $exited = $navProcess.HasExited
                if ($exited) {
                    try { $exitCode = $navProcess.ExitCode } catch { }
                }
            } catch { }

            if ($exited -or -not $portOk) {
                $cause = if ($exited) { "exited (code=$exitCode)" } else { "port not responding" }
                Write-Log "Navigation server unhealthy ($cause) - restart attempt in $($restartBackoff.NavigationServer)s" "WARN"

                if ($exited) {
                    Write-CrashReport -Name "NavigationServer" -ExePath $navExe -Process $navProcess -Reason "Process exited in monitor loop."
                }

                $navRestartHistory.Add((Get-Date)) | Out-Null
                $windowStart = (Get-Date).AddSeconds(-1 * $NavigationRestartWindowSeconds)
                $recentRestarts = @($navRestartHistory | Where-Object { $_ -ge $windowStart }).Count

                if ($recentRestarts -gt $NavigationMaxRestarts) {
                    $navDisabledForSession = $true
                    Write-Log "Navigation server restart limit exceeded ($recentRestarts in ${NavigationRestartWindowSeconds}s). Disabling for this session." "WARN"

                    if ($navProcess) {
                        try {
                            $navProcess.Refresh()
                            if (-not $navProcess.HasExited) {
                                try { Stop-Process -Id $navProcess.Id -Force -ErrorAction SilentlyContinue } catch { }
                            }
                        } catch { }
                    }

                    # If we started BlazorServer using RemoteV3, restart it with stable pathing.
                    if ($resolvedPathingMode -eq "RemoteV3") {
                        $fallback = if ($pathingApiAvailable) { "RemoteV1" } else { "Local" }
                        Write-Log "Restarting BlazorServer with fallback Pathing mode: $fallback" "WARN"

                        $envOverrides["Pathing__Mode"] = $fallback
                        $resolvedPathingMode = $fallback

                        try {
                            if ($managed.BlazorServer.Managed -and $managed.BlazorServer.Process) {
                                try { Stop-Process -Id $managed.BlazorServer.Process.Id -Force -ErrorAction SilentlyContinue } catch { }
                            }

                            $blazorProcess = Start-ManagedProcess -Name "BlazorServer" -ExePath $blazorExe -WorkingDir $blazorDir -Environment $envOverrides
                            $managed.BlazorServer.Process = $blazorProcess
                            $managed.BlazorServer.Managed = $true

                            if (-not (Wait-ForHttp -Url "$baseUrl/api/health" -TimeoutSeconds 60)) {
                                Write-Log "BlazorServer fallback restart did not become reachable." "ERROR"
                            } else {
                                Write-Log "BlazorServer restarted and reachable with Pathing mode: $fallback" "OK"
                            }
                        } catch {
                            Write-Log "Failed to restart BlazorServer after nav disable: $($_.Exception.Message)" "ERROR"
                        }
                    }

                    continue
                }

                Start-Sleep -Seconds $restartBackoff.NavigationServer

                try {
                    if (-not $exited) {
                        try { Stop-Process -Id $navProcess.Id -Force -ErrorAction SilentlyContinue } catch { }
                    }

                    $navProcess = Start-ManagedProcess -Name "NavigationServer" -ExePath $navExe -WorkingDir $navDir
                    $managed.NavigationServer.Process = $navProcess
                    $restartBackoff.NavigationServer = [Math]::Min($restartBackoff.NavigationServer * 2, 60)
                } catch {
                    Write-Log "Navigation server restart failed: $($_.Exception.Message)" "WARN"
                }
            } else {
                $restartBackoff.NavigationServer = 5
            }
        }

        # PathingAPI
        if ($pathingProcess -and $managed.PathingAPI.Managed) {
            if ($pathingProcess.HasExited -or -not (Test-TcpPort -Address "127.0.0.1" -Port $PathingApiPort -TimeoutMs 250)) {
                Write-Log "PathingAPI unhealthy - restart attempt in $($restartBackoff.PathingAPI)s" "WARN"

                if ($pathingProcess.HasExited) {
                    Write-CrashReport -Name "PathingAPI" -ExePath $pathingExe -Process $pathingProcess -Reason "Process exited in monitor loop."
                }

                Start-Sleep -Seconds $restartBackoff.PathingAPI
                try {
                    $pathingProcess = Start-ManagedProcess -Name "PathingAPI" -ExePath $pathingExe -WorkingDir $pathingDir
                    $managed.PathingAPI.Process = $pathingProcess
                    $restartBackoff.PathingAPI = [Math]::Min($restartBackoff.PathingAPI * 2, 60)
                } catch {
                    Write-Log "PathingAPI restart failed: $($_.Exception.Message)" "WARN"
                }
            } else {
                $restartBackoff.PathingAPI = 5
            }
        }

        # BlazorServer (process + HTTP)
        $httpOk = $true
        try { $null = Invoke-HttpJson -Method "GET" -Url "$baseUrl/api/health" -TimeoutSec 3 } catch { $httpOk = $false }

        if ($managed.BlazorServer.Managed) {
            $blazorProcess = $managed.BlazorServer.Process
        }

        if ($managed.BlazorServer.Managed -and ($blazorProcess.HasExited -or -not $httpOk)) {
            Write-Log "BlazorServer unhealthy (process exited=$($blazorProcess.HasExited), httpOk=$httpOk) - restart attempt in $($restartBackoff.BlazorServer)s" "WARN"

            if ($blazorProcess.HasExited) {
                Write-CrashReport -Name "BlazorServer" -ExePath $blazorExe -Environment $envOverrides -Process $blazorProcess -Reason "Process exited in monitor loop."
            }

            Start-Sleep -Seconds $restartBackoff.BlazorServer
            try {
                if (-not $blazorProcess.HasExited) { try { Stop-Process -Id $blazorProcess.Id -Force } catch { } }
                $blazorProcess = Start-ManagedProcess -Name "BlazorServer" -ExePath $blazorExe -WorkingDir $blazorDir -Environment $envOverrides
                $managed.BlazorServer.Process = $blazorProcess
                $restartBackoff.BlazorServer = [Math]::Min($restartBackoff.BlazorServer * 2, 60)
            } catch {
                Write-Log "BlazorServer restart failed: $($_.Exception.Message)" "ERROR"
            }
        } else {
            $restartBackoff.BlazorServer = 5
        }
    }
}
catch {
    Write-Log $_.Exception.Message "ERROR"
    Write-Log "See launcher log: $logFile" "ERROR"
    throw
}
finally {
    Stop-Managed
}
