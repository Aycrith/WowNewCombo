<#
.SYNOPSIS
  Validates BlazorServer launch infrastructure with basic failure simulations.

.DESCRIPTION
  Runs `SmokeTest-BlazorServer.ps1` in a few scenarios to validate:
  - /api/health, /api/launch/status, /api/bot/status respond
  - Port-conflict scenario fails gracefully with captured logs on disk

.NOTES
  Run from repo root:
    pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Validate-BlazorLaunch.ps1
#>

#Requires -Version 5.1

param(
    [int]$BasePort = 5120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Run-Smoke {
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [switch]$SimulatePortConflict
    )

    $pwshArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $PSScriptRoot "SmokeTest-BlazorServer.ps1"),
        "-Port", "$Port",
        "-TimeoutSeconds", "45"
    )

    if ($SimulatePortConflict) {
        $pwshArgs += "-SimulatePortConflict"
    }

    & pwsh @pwshArgs | Out-Host
    $exitCode = $LASTEXITCODE
    if ($null -eq $exitCode) { $exitCode = 0 }
    return [int]$exitCode
}

Write-Host "Scenario: Normal launch"
$code = Run-Smoke -Port $BasePort
if ($code -ne 0) { exit $code }

Write-Host "Scenario: Missing frame_config.json (readiness should report error, server stays up)"
$args = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", (Join-Path $PSScriptRoot "SmokeTest-BlazorServer.ps1"),
    "-Port", "$($BasePort + 2)",
    "-TimeoutSeconds", "45",
    "-SimulateMissingFrameConfig"
)
& pwsh @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Scenario: Missing addon_config.json (readiness should report error, server stays up)"
$args = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", (Join-Path $PSScriptRoot "SmokeTest-BlazorServer.ps1"),
    "-Port", "$($BasePort + 4)",
    "-TimeoutSeconds", "45",
    "-SimulateMissingAddonConfig"
)
& pwsh @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Scenario: Corrupted addon_config.json (server should not crash)"
$args = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", (Join-Path $PSScriptRoot "SmokeTest-BlazorServer.ps1"),
    "-Port", "$($BasePort + 3)",
    "-TimeoutSeconds", "45",
    "-SimulateCorruptedAddonConfig"
)
& pwsh @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Scenario: Corrupted frame_config.json (server should not crash)"
$args = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", (Join-Path $PSScriptRoot "SmokeTest-BlazorServer.ps1"),
    "-Port", "$($BasePort + 5)",
    "-TimeoutSeconds", "45",
    "-SimulateCorruptedFrameConfig"
)
& pwsh @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Scenario: Port conflict"
$code = Run-Smoke -Port ($BasePort + 1) -SimulatePortConflict
if ($code -eq 0) {
    Write-Host "FAIL: Expected port-conflict scenario to fail, but it succeeded."
    exit 1
}

Write-Host "OK"
exit 0
