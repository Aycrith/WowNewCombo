#Requires -Version 5.1
<#
.SYNOPSIS
  One-click launcher for Blood Elf Warlock autonomous leveling.

.DESCRIPTION
  Launches both the orchestrator and optional dashboard in the correct order.
  Handles all setup, validation, and process management.

.EXAMPLES
  pwsh -NoProfile -ExecutionPolicy Bypass -File Scripts\Launch-WarlockLeveling.ps1
  pwsh -NoProfile -ExecutionPolicy Bypass -File Scripts\Launch-WarlockLeveling.ps1 -OpenDashboard
  pwsh -NoProfile -ExecutionPolicy Bypass -File Scripts\Launch-WarlockLeveling.ps1 -AutoConfirmTrainer
#>

param(
    [string]$Profile = "BloodElf_Warlock_1-70_TBC.json",
    [int]$ResumeFromLevel = 0,
    [switch]$OpenDashboard,
    [switch]$AutoConfirmTrainer,
    [switch]$VerboseLogging,
    [string]$BotRoot = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================================
# Setup
# ============================================================================

if ([string]::IsNullOrWhiteSpace($BotRoot)) {
    $BotRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
}

$logDir = Join-Path $BotRoot "logs"
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

$COLOR_OK = "Green"
$COLOR_WARN = "Yellow"
$COLOR_ERROR = "Red"
$COLOR_INFO = "Cyan"

function Write-H1([string]$Message) {
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor $COLOR_INFO
    Write-Host "║ $($Message.PadRight(58)) ║" -ForegroundColor $COLOR_INFO
    Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor $COLOR_INFO
}

function Write-Ok([string]$Message) {
    Write-Host "[ OK  ] $Message" -ForegroundColor $COLOR_OK
}

function Write-Warn([string]$Message) {
    Write-Host "[WARN ] $Message" -ForegroundColor $COLOR_WARN
}

function Write-Err([string]$Message) {
    Write-Host "[ERR  ] $Message" -ForegroundColor $COLOR_ERROR
}

function Write-Info([string]$Message) {
    Write-Host "[INFO ] $Message" -ForegroundColor $COLOR_INFO
}

# ============================================================================
# Preflight Checks
# ============================================================================

Write-H1 "WARLOCK LEVELING - PREFLIGHT CHECKS"

Write-Info "Verifying build..."
$buildOutput = & dotnet build $BotRoot\MasterOfPuppets.sln -c Release 2>&1 | Select-String -Pattern "error|Error"

if ($buildOutput) {
    Write-Err "Build failed! Check errors above and fix before continuing."
    Write-Host ""
    exit 1
}

Write-Ok "Build succeeded (0 errors)"

Write-Info "Verifying profile file..."
$profilePath = Join-Path $BotRoot "Json\class\$Profile"

if (-not (Test-Path $profilePath)) {
    Write-Err "Profile not found: $profilePath"
    Write-Host ""
    exit 1
}

Write-Ok "Profile found: $Profile"

Write-Info "Killing existing dotnet processes..."
Get-Process | Where-Object { $_.ProcessName -like "*dotnet*" -and $_.ProcessName -ne [System.Diagnostics.Process]::GetCurrentProcess().ProcessName } | Stop-Process -Force -ErrorAction SilentlyContinue

Write-Ok "Cleaned up old processes"

Write-Info "Verifying paths..."
$requiredDirs = @(
    (Join-Path $BotRoot "Json\class"),
    (Join-Path $BotRoot "Json\path"),
    (Join-Path $BotRoot "Scripts"),
    (Join-Path $BotRoot "logs")
)

foreach ($dir in $requiredDirs) {
    if (-not (Test-Path $dir)) {
        Write-Err "Required directory not found: $dir"
        exit 1
    }
}

Write-Ok "All required directories verified"

Write-Info "Verifying WoW setup..."
Write-Warn "Checklist:"
Write-Warn "  [ ] Character is Blood Elf Warlock"
Write-Warn "  [ ] Action bars are configured (keys 2,4,5,6,7,8,9,0,-,=,F1,F2,F3,N0-N9,C)"
Write-Warn "  [ ] DataToColor addon is loaded (type /reload)"
Write-Warn "  [ ] WoW is running and visible"
Write-Host ""
Read-Host "Press Enter when all items are complete"

# ============================================================================
# Launch Services
# ============================================================================

Write-H1 "LAUNCHING BOT SERVICES"

Write-Info "Starting BlazorServer and bot..."
$agentOutput = & pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $BotRoot "Scripts\Agent-BotControl.ps1") `
    -Action StartAndValidate `
    -Profile $Profile `
    -BypassActionBar

Write-Host $agentOutput

# Check if startup was successful
if ($agentOutput -like "*ERR*") {
    Write-Err "Failed to start bot services. Check output above."
    exit 1
}

Write-Ok "Bot services started successfully"

# ============================================================================
# Launch Orchestrator
# ============================================================================

Write-H1 "LAUNCHING ORCHESTRATOR"

Write-Info "Preparing orchestrator..."
Write-Info "Profile: $Profile"
Write-Info "Trainer Auto-Confirm: $(if ($AutoConfirmTrainer) { 'YES' } else { 'NO' })"
Write-Info "Verbose Logging: $(if ($VerboseLogging) { 'YES' } else { 'NO' })"
Write-Info "Dashboard: $(if ($OpenDashboard) { 'YES (separate terminal)' } else { 'NO' })"
Write-Host ""

# Build orchestrator arguments
$orchestratorArgs = @(
    "-NoProfile"
    "-ExecutionPolicy", "Bypass"
    "-File", (Join-Path $BotRoot "Scripts\Orchestrate-WarlockLeveling.ps1")
    "-Profile", $Profile
)

if ($ResumeFromLevel -gt 0) {
    $orchestratorArgs += "-ResumeFromLevel", $ResumeFromLevel
}

if ($AutoConfirmTrainer) {
    $orchestratorArgs += "-AutoConfirmTrainer"
}

if ($VerboseLogging) {
    $orchestratorArgs += "-VerboseLogging"
}

# Launch orchestrator in this terminal
if ($OpenDashboard) {
    Write-Info "Launching dashboard in new window..."
    $dashboardArgs = @(
        "-NoProfile"
        "-ExecutionPolicy", "Bypass"
        "-File", (Join-Path $BotRoot "Scripts\WarlockDashboard.ps1")
    )
    Start-Process -FilePath "pwsh" -ArgumentList $dashboardArgs -WindowStyle Normal
    Start-Sleep -Seconds 2
    Write-Ok "Dashboard launched in separate window"
}

Write-H1 "ORCHESTRATOR STARTING"

# Start orchestrator (this blocks)
& pwsh @orchestratorArgs

# If we get here, orchestrator was stopped manually
Write-Host ""
Write-H1 "SESSION ENDED"
Write-Info "Check logs at: $logDir"
Write-Host ""
