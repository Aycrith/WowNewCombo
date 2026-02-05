<#
.SYNOPSIS
  Validates OneClickLauncher pre-flight behavior using `.wow_mock`.

.DESCRIPTION
  Runs OneClickLauncher.ps1 in `-DryRun` mode across several simulated scenarios:
  - Normal: `.wow_mock` with free ports
  - Port conflict: WebPort occupied
  - WoW-path corruption: missing client exe

  The goal is to validate readiness gating and fail-fast error messages without
  starting any real services or requiring a live WoW process.

.NOTES
  Run from repo root:
    pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Validate-OneClickLauncher.ps1
#>

#Requires -Version 5.1

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ScriptRoot {
    return $PSScriptRoot
}

function Get-FreePort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return $listener.LocalEndpoint.Port
    } finally {
        $listener.Stop()
    }
}

$botRoot = (Resolve-Path -LiteralPath (Join-Path (Get-ScriptRoot) "..")).Path
$launcher = Join-Path $botRoot "Scripts\\OneClickLauncher.ps1"
$wowMock = Join-Path $botRoot ".wow_mock"

if (-not (Test-Path -LiteralPath $launcher)) {
    throw "Missing launcher script: $launcher"
}
if (-not (Test-Path -LiteralPath $wowMock)) {
    throw "Missing .wow_mock directory: $wowMock"
}

function Run-LauncherDryRun {
    param(
        [Parameter(Mandatory)][string]$WoWPath,
        [Parameter(Mandatory)][int]$WebPort,
        [Parameter(Mandatory)][int]$PathingApiPort,
        [Parameter(Mandatory)][int]$NavPort
    )

    & pwsh -NoProfile -ExecutionPolicy Bypass -File $launcher `
        -ShowDashboard:$false `
        -DryRun:$true `
        -AutoFix:$true `
        -RunValidation:$false `
        -WoWPathOverride $WoWPath `
        -WebPort $WebPort `
        -PathingApiPort $PathingApiPort `
        -NavPort $NavPort 2>$null | Out-Null

    return $LASTEXITCODE
}

Write-Host "Scenario: Normal (.wow_mock)"
$web = Get-FreePort
$api = Get-FreePort
$nav = Get-FreePort
$code = Run-LauncherDryRun -WoWPath $wowMock -WebPort $web -PathingApiPort $api -NavPort $nav
if ($code -ne 0) { throw "FAIL: Normal dryrun exit code=$code" }

Write-Host "Scenario: Port conflict (WebPort occupied)"
$conflict = Get-FreePort
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $conflict)
$listener.Start()
try {
    $code = Run-LauncherDryRun -WoWPath $wowMock -WebPort $conflict -PathingApiPort (Get-FreePort) -NavPort (Get-FreePort)
    if ($code -eq 0) { throw "FAIL: Expected port-conflict scenario to fail, but it succeeded." }
} finally {
    $listener.Stop()
}

Write-Host "Scenario: WoW path corruption (missing exe)"
$bad = Join-Path $env:TEMP ("wow_mock_invalid_{0}" -f ([Guid]::NewGuid().ToString("n")))
New-Item -ItemType Directory -Path $bad -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $bad "Interface") -Force | Out-Null
try {
    $code = Run-LauncherDryRun -WoWPath $bad -WebPort (Get-FreePort) -PathingApiPort (Get-FreePort) -NavPort (Get-FreePort)
    if ($code -eq 0) { throw "FAIL: Expected invalid WoW path to fail, but it succeeded." }
} finally {
    Remove-Item -LiteralPath $bad -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Scenario: Permissions (AddOns folder not writable)"
$ro = Join-Path $env:TEMP ("wow_mock_ro_{0}" -f ([Guid]::NewGuid().ToString("n")))
Copy-Item -LiteralPath $wowMock -Destination $ro -Recurse -Force
$addons = Join-Path $ro "Interface\\AddOns"
$dtc = Join-Path $addons "DataToColor"
if (Test-Path -LiteralPath $dtc) {
    Remove-Item -LiteralPath $dtc -Recurse -Force -ErrorAction SilentlyContinue
}

try {
    New-Item -ItemType File -Path (Join-Path $addons ".wow_mock_deny_write") -Force | Out-Null
    $code = Run-LauncherDryRun -WoWPath $ro -WebPort (Get-FreePort) -PathingApiPort (Get-FreePort) -NavPort (Get-FreePort)
    if ($code -eq 0) { throw "FAIL: Expected permission scenario to fail, but it succeeded." }
} finally {
    Remove-Item -LiteralPath $ro -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "OK"
exit 0
