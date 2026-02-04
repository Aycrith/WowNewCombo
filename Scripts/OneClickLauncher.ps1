<#
.SYNOPSIS
  WowClassicGrindBot - Production one-click launcher for Windows.

.DESCRIPTION
  Performs pre-flight checks, installs/validates addons, launches required components
  in the correct sequence, verifies client/server alignment using built-in API tests,
  and keeps processes healthy with monitoring + automatic restart.

  Default behavior is fully autonomous. If the detected character is not the expected
  Level 8 Blood Elf Rogue, the launcher will still bring the stack online but will
  skip automated validation actions that affect gameplay.

.NOTES
  Requires: PowerShell 5.1+, .NET 10 runtime, Windows 10/11
#>

#Requires -Version 5.1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

param(
    [string]$BotRoot = "",
    [int]$WebPort = 5000,
    [int]$PathingApiPort = 5001,
    [int]$NavPort = 47110,

    [bool]$AutoLaunchWoW = $true,
    [bool]$AutoStartBot = $true,
    [bool]$AutoFix = $true,
    [bool]$RunValidation = $true,

    [string]$ExpectedRace = "BloodElf",
    [string]$ExpectedClass = "Rogue",
    [int]$ExpectedLevel = 8,
    [string]$ProfileFileName = "BloodElf_Rogue_Starter_Test.json",

    [ValidateSet("Auto", "Local", "RemoteV1", "RemoteV3")]
    [string]$PathingMode = "Auto",

    [int]$MonitorIntervalSeconds = 10
)

function Get-ScriptRoot {
    $inv = (Get-Variable MyInvocation -Scope 1).Value
    return Split-Path -Parent $inv.MyCommand.Path
}

if ([string]::IsNullOrWhiteSpace($BotRoot)) {
    $BotRoot = Resolve-Path (Join-Path (Get-ScriptRoot) "..") | Select-Object -ExpandProperty Path
} else {
    $BotRoot = Resolve-Path $BotRoot | Select-Object -ExpandProperty Path
}

$logsDir = Join-Path $BotRoot "logs"
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
$logFile = Join-Path $logsDir ("launcher-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"))

function Write-Log {
    param(
        [Parameter(Mandatory)] [string]$Message,
        [ValidateSet("INFO", "OK", "WARN", "ERROR")] [string]$Level = "INFO"
    )

    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $line = "[{0}] [{1}] {2}" -f $ts, $Level, $Message
    $line | Out-File -FilePath $logFile -Encoding utf8 -Append

    switch ($Level) {
        "OK"    { Write-Host $line -ForegroundColor Green }
        "WARN"  { Write-Host $line -ForegroundColor Yellow }
        "ERROR" { Write-Host $line -ForegroundColor Red }
        default { Write-Host $line -ForegroundColor Gray }
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
        "-File", (Join-Path (Get-ScriptRoot) "OneClickLauncher.ps1"),
        "-BotRoot", $BotRoot,
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
        "-RunValidation:$RunValidation"
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
    $hasCore = $runtimes | Where-Object { $_ -match "^Microsoft\\.NETCore\\.App\\s+10\\." } | Select-Object -First 1
    $hasAsp = $runtimes | Where-Object { $_ -match "^Microsoft\\.AspNetCore\\.App\\s+10\\." } | Select-Object -First 1
    if (-not $hasCore -or -not $hasAsp) {
        throw "Missing .NET 10 runtime. Found runtimes:`n$($runtimes -join \"`n\")"
    }
}

function Read-JsonFile {
    param([string]$Path)
    return (Get-Content -Raw -Path $Path -Encoding UTF8) | ConvertFrom-Json
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

        Restart-ElevatedIfNeeded -Reason "Install WoW addon '$name' to $dest"

        Write-Log "Installing addon: $name -> $dst" "INFO"
        New-Item -ItemType Directory -Path $dest -Force | Out-Null
        Copy-Item -Path $src -Destination $dst -Recurse -Force
        Write-Log "Addon installed: $name" "OK"
    }
}

function Ensure-BindPadMinimalXmlEncoding {
    param([string]$WoWPath)

    $bindPadDir = Join-Path $WoWPath "Interface\\AddOns\\BindPadMinimal"
    if (-not (Test-Path $bindPadDir)) { return }

    Restart-ElevatedIfNeeded -Reason "Fix BindPadMinimal encoding under $bindPadDir"

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
        Write-Log "BindPadMinimal encoding fix failed: $($_.Exception.Message)" "WARN"
    }
}

function Test-TcpPort {
    param([string]$Host, [int]$Port, [int]$TimeoutMs = 1000)
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $iar = $client.BeginConnect($Host, $Port, $null, $null)
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

    $psi = @{
        FilePath = $ExePath
        WorkingDirectory = $WorkingDir
        ArgumentList = $Args
        PassThru = $true
        WindowStyle = "Minimized"
    }

    foreach ($k in $Environment.Keys) {
        $env:$k = [string]$Environment[$k]
    }

    # Note: Primary logs are emitted by the services themselves (e.g., out.log). Launcher logs are in $logFile.

    Write-Log "Starting $Name: $ExePath" "INFO"
    $p = Start-Process @psi
    Write-Log "$Name started (PID: $($p.Id))" "OK"
    return $p
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
            $status = Invoke-HttpJson -Method "GET" -Url ("{0}/api/test/status" -f $BaseUrl) -TimeoutSec 10
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

    $pattern = "^(?<race>\\w+)\\s+(?<class>\\w+)\\s+L(?<level>\\d+)$"
    $m = [regex]::Match($CharacterString, $pattern)
    if (-not $m.Success) { return $false }

    $race = $m.Groups["race"].Value
    $class = $m.Groups["class"].Value
    $level = [int]$m.Groups["level"].Value

    return ($race -eq $ExpectedRace -and $class -eq $ExpectedClass -and $level -eq $ExpectedLevel)
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
        $r = Invoke-HttpJson -Method "POST" -Url ("{0}/api/bot/profile/load" -f $BaseUrl) -Body @{ fileName = $FileName } -TimeoutSec 10
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
        $r = Invoke-HttpJson -Method "POST" -Url ("{0}/api/bot/start" -f $BaseUrl) -TimeoutSec 10
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
    Require-File -Path $blazorExe -Hint "Run: dotnet build MasterOfPuppets.sln -c Release"

    $navExe = Join-Path $BotRoot "Navigation\\AmeisenNavigationServer.exe"
    $navDir = Split-Path -Parent $navExe

    $pathingExe = Join-Path $BotRoot "PathingAPI\\bin\\Release\\net10.0\\PathingAPI.exe"
    $pathingDir = Split-Path -Parent $pathingExe

    $wowPath = Resolve-WoWPath
    if (-not $wowPath) {
        throw "Could not detect WoW installation path. Set Startup:WoWPath in BlazorServer\\appsettings.json or install WoW in a standard path."
    }
    Write-Log "WoW path: $wowPath" "OK"

    Ensure-AddonInstalled -WoWPath $wowPath -AddonNames @("DataToColor", "BindPadMinimal", "cTimerBackport", "SoundKitBackport")
    Ensure-BindPadMinimalXmlEncoding -WoWPath $wowPath

    $navAvailable = $false
    $navProcess = $null
    if (Test-Path $navExe) {
        $mmaps = Join-Path $BotRoot "Navigation\\mmaps"
        $hasMmap = Test-Path (Join-Path $mmaps "*.mmap")
        if ($hasMmap) {
            if (-not (Test-TcpPort -Host "127.0.0.1" -Port $NavPort -TimeoutMs 250)) {
                $navProcess = Start-ManagedProcess -Name "NavigationServer" -ExePath $navExe -WorkingDir $navDir
                $managed.NavigationServer.Process = $navProcess
                $managed.NavigationServer.Managed = $true

                $deadline = (Get-Date).AddSeconds(20)
                while ((Get-Date) -lt $deadline) {
                    if (Test-TcpPort -Host "127.0.0.1" -Port $NavPort -TimeoutMs 250) { break }
                    Start-Sleep -Milliseconds 500
                }
            }

            if (Test-TcpPort -Host "127.0.0.1" -Port $NavPort -TimeoutMs 250) {
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
    } else {
        Write-Log "Navigation server not present; skipping." "WARN"
    }

    $pathingApiAvailable = $false
    $pathingProcess = $null
    if (Test-Path $pathingExe) {
        if (-not (Test-TcpPort -Host "127.0.0.1" -Port $PathingApiPort -TimeoutMs 250)) {
            $pathingProcess = Start-ManagedProcess -Name "PathingAPI" -ExePath $pathingExe -WorkingDir $pathingDir
            $managed.PathingAPI.Process = $pathingProcess
            $managed.PathingAPI.Managed = $true
            Start-Sleep -Seconds 2
        }
        if (Test-TcpPort -Host "127.0.0.1" -Port $PathingApiPort -TimeoutMs 250) {
            $pathingApiAvailable = $true
            Write-Log "PathingAPI ready on 127.0.0.1:$PathingApiPort" "OK"
        }
    }

    $resolvedPathingMode = Determine-PathingMode -NavAvailable $navAvailable -PathingApiAvailable $pathingApiAvailable
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
        $blazorProcess = Start-ManagedProcess -Name "BlazorServer" -ExePath $blazorExe -WorkingDir $blazorDir -Environment $envOverrides
        $managed.BlazorServer.Process = $blazorProcess
        $managed.BlazorServer.Managed = $true

        if (-not (Wait-ForHttp -Url "$baseUrl/api/health" -TimeoutSeconds 60)) {
            throw "BlazorServer did not become reachable at $baseUrl within timeout."
        }
    }
    Write-Log "Web UI reachable: $baseUrl" "OK"
    try { Start-Process $baseUrl | Out-Null } catch { }

    Write-Log "Waiting for client alignment (addon comms, frames, data freshness)..." "INFO"
    $ready = Wait-ForSystemReady -BaseUrl $baseUrl -TimeoutSeconds 300
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

    Write-Log "Entering monitor loop (interval: ${MonitorIntervalSeconds}s). Close this window to stop." "INFO"

    $restartBackoff = @{
        "NavigationServer" = 5
        "PathingAPI" = 5
        "BlazorServer" = 5
    }

    while ($true) {
        Start-Sleep -Seconds $MonitorIntervalSeconds

        # Navigation server
        if ($navProcess -and $navAvailable -and $managed.NavigationServer.Managed) {
            if ($navProcess.HasExited -or -not (Test-TcpPort -Host "127.0.0.1" -Port $NavPort -TimeoutMs 250)) {
                Write-Log "Navigation server unhealthy - restart attempt in $($restartBackoff.NavigationServer)s" "WARN"
                Start-Sleep -Seconds $restartBackoff.NavigationServer
                try {
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
            if ($pathingProcess.HasExited -or -not (Test-TcpPort -Host "127.0.0.1" -Port $PathingApiPort -TimeoutMs 250)) {
                Write-Log "PathingAPI unhealthy - restart attempt in $($restartBackoff.PathingAPI)s" "WARN"
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
