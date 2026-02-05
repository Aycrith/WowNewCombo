<#
.SYNOPSIS
  Smoke test for BlazorServer launch + core endpoints.

.DESCRIPTION
  Starts BlazorServer.exe on a chosen port, waits for /api/health, then verifies
  /api/launch/status responds. Captures stdout/stderr to disk so logs persist
  even if the process crashes.

.NOTES
  Run from repo root:
    pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\SmokeTest-BlazorServer.ps1
#>

#Requires -Version 5.1

param(
    [int]$Port = 5099,
    [int]$TimeoutSeconds = 30,
    [switch]$SimulatePortConflict,
    [switch]$SimulateMissingAddonConfig,
    [switch]$SimulateMissingFrameConfig,
    [switch]$SimulateCorruptedAddonConfig,
    [switch]$SimulateCorruptedFrameConfig
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ScriptRoot {
    return $PSScriptRoot
}

$botRoot = (Resolve-Path -LiteralPath (Join-Path (Get-ScriptRoot) "..")).Path
$logsDir = Join-Path $botRoot "logs"
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null

$ts = Get-Date -Format "yyyyMMdd-HHmmss"
$stdoutPath = Join-Path $logsDir "smoke-blazor-${ts}-stdout.log"
$stderrPath = Join-Path $logsDir "smoke-blazor-${ts}-stderr.log"
$reportPath = Join-Path $logsDir "smoke-blazor-${ts}-report.json"

$exe = Join-Path $botRoot "BlazorServer\\bin\\Release\\net10.0\\BlazorServer.exe"
$wd = Split-Path -Parent $exe

if (-not (Test-Path -LiteralPath $exe)) {
    throw "Missing BlazorServer.exe at $exe. Build with: dotnet build MasterOfPuppets.sln -c Release"
}

$baseUrl = "http://localhost:$Port"
$listener = $null
$process = $null
$ok = $false
$health = $null
$launch = $null
$botStatus = $null
$failure = $null
$fileBackups = @()

function Backup-ForRestore {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Mode,
        [Parameter(Mandatory = $true)][string]$BackupPath
    )

    $fileBackups += [ordered]@{
        Path = $Path
        Mode = $Mode
        BackupPath = $BackupPath
    }
}

function Simulate-FileMissing {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) { return }

    $bak = "$Path.bak_smoke_missing_$ts"
    Move-Item -LiteralPath $Path -Destination $bak -Force
    Backup-ForRestore -Path $Path -Mode "missing" -BackupPath $bak
}

function Simulate-FileCorrupted {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) { return }

    $bak = "$Path.bak_smoke_corrupt_$ts"
    Copy-Item -LiteralPath $Path -Destination $bak -Force
    Set-Content -LiteralPath $Path -Encoding UTF8 -Value "{"
    Backup-ForRestore -Path $Path -Mode "corrupt" -BackupPath $bak
}

function Restore-Files {
    foreach ($b in $fileBackups) {
        try {
            if ($b.Mode -eq "missing") {
                if (Test-Path -LiteralPath $b.BackupPath) {
                    Move-Item -LiteralPath $b.BackupPath -Destination $b.Path -Force
                }
            } elseif ($b.Mode -eq "corrupt") {
                if (Test-Path -LiteralPath $b.BackupPath) {
                    Copy-Item -LiteralPath $b.BackupPath -Destination $b.Path -Force
                    Remove-Item -LiteralPath $b.BackupPath -Force -ErrorAction SilentlyContinue
                }
            }
        } catch { }
    }
}

function Assert-LaunchCheckStatus {
    param(
        [Parameter(Mandatory = $true)]$Launch,
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][int[]]$AllowedStatuses
    )

    $check = $Launch.Checks | Where-Object { $_.Title -eq $Title } | Select-Object -First 1
    if (-not $check) {
        throw "Missing '$Title' check in /api/launch/status payload"
    }

    $status = [int]$check.Status
    if (-not ($AllowedStatuses -contains $status)) {
        throw "Unexpected '$Title' status=$status. Allowed: $($AllowedStatuses -join ', ')"
    }
}

try {
    if ($SimulatePortConflict) {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
    }

    $env:Startup__WebUIPort = "$Port"
    $env:Startup__AutoOpenBrowser = "false"

    $addonCfg = Join-Path $wd "addon_config.json"
    $frameCfg = Join-Path $wd "frame_config.json"

    if ($SimulateMissingAddonConfig) { Simulate-FileMissing -Path $addonCfg }
    if ($SimulateMissingFrameConfig) { Simulate-FileMissing -Path $frameCfg }
    if ($SimulateCorruptedAddonConfig) { Simulate-FileCorrupted -Path $addonCfg }
    if ($SimulateCorruptedFrameConfig) { Simulate-FileCorrupted -Path $frameCfg }

    $process = Start-Process -FilePath $exe -WorkingDirectory $wd -PassThru `
        -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -NoNewWindow

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        try {
            $health = Invoke-RestMethod -Uri "$baseUrl/api/health" -TimeoutSec 2
            if ($health -and $health.Status -eq "OK") {
                $ok = $true
                break
            }
        } catch {
            # keep waiting
        }

        try {
            $process.Refresh()
            if ($process.HasExited) { break }
        } catch { }
    }

    if ($ok) {
        $launch = Invoke-RestMethod -Uri "$baseUrl/api/launch/status" -TimeoutSec 10
        if (-not $launch) {
            throw "/api/launch/status returned empty payload"
        }

        if ($SimulateMissingFrameConfig) { Assert-LaunchCheckStatus -Launch $launch -Title "Frames" -AllowedStatuses @(4) }
        if ($SimulateMissingAddonConfig) { Assert-LaunchCheckStatus -Launch $launch -Title "Frames" -AllowedStatuses @(4) }
        if ($SimulateCorruptedFrameConfig) { Assert-LaunchCheckStatus -Launch $launch -Title "Frames" -AllowedStatuses @(3, 4) }
        if ($SimulateCorruptedAddonConfig) { Assert-LaunchCheckStatus -Launch $launch -Title "Add-ons" -AllowedStatuses @(1, 2, 3, 4) }

        $botStatus = Invoke-RestMethod -Uri "$baseUrl/api/bot/status" -TimeoutSec 5
        if (-not $botStatus) {
            throw "/api/bot/status returned empty payload"
        }
    } else {
        try {
            $process.Refresh()
            if ($process.HasExited) {
                $failure = "Process exited (code=$($process.ExitCode)) before health became OK."
            } else {
                $failure = "Timed out waiting for /api/health to become OK."
            }
        } catch {
            $failure = "Timed out waiting for /api/health to become OK."
        }
    }
}
catch {
    $failure = $_.Exception.Message
}
finally {
    if ($listener) {
        try { $listener.Stop() } catch { }
    }

    if ($process) {
        try {
            $process.Refresh()
            if (-not $process.HasExited) {
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            }
        } catch { }
    }

    Remove-Item Env:Startup__WebUIPort -ErrorAction SilentlyContinue
    Remove-Item Env:Startup__AutoOpenBrowser -ErrorAction SilentlyContinue
    Restore-Files
}

$payload = [ordered]@{
    Timestamp = (Get-Date).ToString("o")
    Success = ($ok -and -not $SimulatePortConflict)
    SimulatePortConflict = [bool]$SimulatePortConflict
    SimulateMissingAddonConfig = [bool]$SimulateMissingAddonConfig
    SimulateMissingFrameConfig = [bool]$SimulateMissingFrameConfig
    SimulateCorruptedAddonConfig = [bool]$SimulateCorruptedAddonConfig
    SimulateCorruptedFrameConfig = [bool]$SimulateCorruptedFrameConfig
    Port = $Port
    BaseUrl = $baseUrl
    TimeoutSeconds = $TimeoutSeconds
    Process = [ordered]@{
        Pid = if ($process) { $process.Id } else { $null }
    }
    Health = $health
    LaunchStatus = $launch
    BotStatus = $botStatus
    StdOutPath = $stdoutPath
    StdErrPath = $stderrPath
    Error = $failure
}

$payload | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPath -Encoding UTF8

if ($failure) {
    Write-Host "FAIL: $failure"
    Write-Host "Report: $reportPath"
    exit 1
}

Write-Host "OK"
Write-Host "Report: $reportPath"
exit 0
