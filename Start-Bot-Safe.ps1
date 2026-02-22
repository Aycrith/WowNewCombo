#!/usr/bin/env pwsh
# Back-compat wrapper for the unified agent CLI workflow.

param(
    [string]$Profile = "BloodElf_Rogue_8-60_TBC.json",
    [string]$Base = "http://localhost:5000",
    [switch]$NoMonitor,
    [switch]$AllowStartWithWarnings,
    [switch]$SkipCharacterGate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$agentCtl = Join-Path $root "Scripts\Agent-BotControl.ps1"

if (-not (Test-Path -LiteralPath $agentCtl))
{
    throw "Missing script: $agentCtl"
}

$args = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $agentCtl,
    "-Action", "Start",
    "-Profile", $Profile,
    "-BaseUrl", $Base
)

if (-not $NoMonitor)
{
    $args += "-StartMonitor"
}

if ($AllowStartWithWarnings)
{
    $args += "-AllowStartWithWarnings"
}

if ($SkipCharacterGate)
{
    $args += "-SkipCharacterGate"
}

& pwsh @args
exit $LASTEXITCODE
