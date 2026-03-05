#Requires -Version 5.1
<#
.SYNOPSIS
  Autonomous leveling orchestrator for Blood Elf Warlock 1-70 TBC.

.DESCRIPTION
  Manages the complete leveling lifecycle with state machine-driven recovery,
  automatic trainer pause at critical spell thresholds, death/stuck detection,
  and session persistence across crashes.

  State Machine:
    IDLE → STARTING → RUNNING ──┬→ TRAINER_PAUSE → RUNNING
                                 ├→ DEATH_RECOVERY → RUNNING
                                 ├→ STUCK_RECOVERY → RUNNING
                                 │                 → INTERVENTION_REQUIRED
                                 └→ ZONE_TRANSITION → RUNNING (auto-resolves)

.EXAMPLES
  pwsh -File Scripts/Orchestrate-WarlockLeveling.ps1 -Profile BloodElf_Warlock_1-70_TBC.json
  pwsh -File Scripts/Orchestrate-WarlockLeveling.ps1 -ResumeFromLevel 24
  pwsh -File Scripts/Orchestrate-WarlockLeveling.ps1 -AutoConfirmTrainer
#>
[CmdletBinding()]
param(
    [string]$Profile = "BloodElf_Warlock_1-70_TBC.json",
    [int]$ResumeFromLevel = 0,
    [string]$BotRoot = "",
    [string]$BaseUrl = "http://localhost:5000",
    [int]$PollIntervalSeconds = 15,
    [switch]$AutoConfirmTrainer,
    [switch]$VerboseLogging,
    [Alias("h", "?")][switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

if ($Help)
{
    Write-Host "Usage: Orchestrate-WarlockLeveling.ps1 [-Profile <json>] [-ResumeFromLevel <n>] [-BaseUrl <url>] [-PollIntervalSeconds <n>] [-AutoConfirmTrainer] [-VerboseLogging] [-BotRoot <path>]" -ForegroundColor Cyan
    Write-Host "Runs the autonomous warlock leveling state machine." -ForegroundColor Gray
    return
}

# ============================================================================
# Configuration
# ============================================================================

$MAX_SAME_GOAL_TICKS         = 20   # 5 min (15s × 20) no-progress before stuck recovery
$MAX_DEATH_WAIT_TICKS        = 40   # 10 min (15s × 40) max death wait
$MAX_STUCK_RECOVERY_ATTEMPTS = 3    # escalate to INTERVENTION_REQUIRED after 3 failed attempts
$MAX_API_FAILURES            = 5    # restart BlazorServer after 5 consecutive API failures
$KILL_RATE_WARN_THRESHOLD    = 3    # kills/hr warning floor
$BLAZOR_RESTART_TIMEOUT      = 30   # seconds to wait for port 5000 to respond

# Critical trainer levels where new rotation-changing spells are learned
$CRITICAL_TRAINER_LEVELS = @(10, 20, 30, 40, 42, 50, 60, 62, 66)

# All trainer levels for convenience visits
$ALL_TRAINER_LEVELS = @(10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40, 42, 44, 46, 48, 50, 54, 58, 60, 62, 66, 70)

# Trainer locations by level range
$TRAINER_LOCATIONS = @{
    "1-10"   = "Silvermoon City, Sunfury Spire area"
    "10-20"  = "Silvermoon City or Undercity (Arcane Vaults)"
    "20-60"  = "Undercity (Arcane Vaults) or Orgrimmar (Valley of Spirits)"
    "60-70"  = "Thrallmar, Hellfire Peninsula"
}

# ============================================================================
# Initialization
# ============================================================================

if ([string]::IsNullOrWhiteSpace($BotRoot)) {
    $BotRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
} else {
    $BotRoot = (Resolve-Path -LiteralPath $BotRoot).Path
}

$logsDir = Join-Path $BotRoot "logs"
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null

$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logFile = Join-Path $logsDir "warlock-orchestrator-$runStamp.log"
$sessionStateFile = Join-Path $logsDir "warlock-session-state.json"

$sessionState = @{
    StartedAt           = (Get-Date).ToUniversalTime()
    LastSavedAt         = (Get-Date).ToUniversalTime()
    LastKnownLevel      = if ($ResumeFromLevel -gt 0) { $ResumeFromLevel } else { 1 }
    TotalKills          = 0
    TotalDeaths         = 0
    State               = "STARTING"
    LeveledUpAt         = @{}
    SameGoalTickCount   = 0
    LastGoal            = ""
    StuckRecoveryCount  = 0
    ApiFailureCount     = 0
}

# ============================================================================
# Logging and Output
# ============================================================================

function Write-Log([string]$Message, [string]$Level = "INFO") {
    $timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $logMessage = "[$timestamp] [$Level] $Message"
    Add-Content -Path $logFile -Value $logMessage -ErrorAction SilentlyContinue

    if ($VerboseLogging -or $Level -ne "DEBUG") {
        switch ($Level) {
            "OK"       { Write-Host $logMessage -ForegroundColor Green }
            "WARN"     { Write-Host $logMessage -ForegroundColor Yellow }
            "ERROR"    { Write-Host $logMessage -ForegroundColor Red }
            "DEBUG"    { Write-Host $logMessage -ForegroundColor Gray }
            default    { Write-Host $logMessage -ForegroundColor White }
        }
    }
}

function Show-Dashboard([object]$Snapshot) {
    $uptime = (Get-Date) - $sessionState.StartedAt
    $uptimeStr = "{0:D2}h {1:D2}m {2:D2}s" -f $uptime.Hours, $uptime.Minutes, $uptime.Seconds

    $killRate = 0
    if ($uptime.TotalHours -gt 0) {
        $killRate = [math]::Round($sessionState.TotalKills / $uptime.TotalHours, 1)
    }

    $nextTrainer = $null
    foreach ($level in $CRITICAL_TRAINER_LEVELS) {
        if ($level -gt $snapshot.Level) {
            $nextTrainer = $level
            break
        }
    }
    $nextTrainerStr = if ($nextTrainer) { "Level $nextTrainer (+$($nextTrainer - $snapshot.Level) levels)" } else { "COMPLETE" }

    # Clear and redraw
    Clear-Host
    Write-Host ""
    Write-Host "=== Blood Elf Warlock - Autonomous Leveling ===" -ForegroundColor Cyan
    Write-Host "State:        $($sessionState.State)" -ForegroundColor Cyan
    Write-Host "Uptime:       $uptimeStr" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Level:        $($snapshot.Level) / 70" -ForegroundColor Yellow
    Write-Host "Zone:         $($snapshot.Zone ?? 'Unknown')" -ForegroundColor Yellow
    Write-Host "Health:       $($snapshot.Health ?? 'N/A')%" -ForegroundColor Yellow
    Write-Host "Mana:         $($snapshot.Mana ?? 'N/A')%" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Current Goal: $($snapshot.CurrentGoal ?? 'None')" -ForegroundColor Green
    Write-Host "Kills:        $($sessionState.TotalKills) ($($killRate)/hr)" -ForegroundColor Green
    Write-Host "Deaths:       $($sessionState.TotalDeaths)" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next Trainer: $nextTrainerStr" -ForegroundColor Magenta
    Write-Host "Last Event:   $($snapshot.LastEvent ?? '[idle]')" -ForegroundColor Gray
    Write-Host ""
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host ""
}

# ============================================================================
# API Communication
# ============================================================================

function Invoke-OrchestratorApi([string]$Method, [string]$Path, $Body = $null) {
    try {
        $uri = "$BaseUrl$Path"
        $params = @{
            Uri        = $uri
            Method     = $Method
            TimeoutSec = 10
        }

        if ($null -ne $Body) {
            $params["ContentType"] = "application/json"
            $params["Body"] = ($Body | ConvertTo-Json -Depth 12)
        }

        return Invoke-RestMethod @params
    }
    catch {
        Write-Log "API ERROR: $($_.Exception.Message)" "ERROR"
        $sessionState.ApiFailureCount++
        return $null
    }
}

function Get-BotSnapshot() {
    $session = Invoke-OrchestratorApi "GET" "/api/session"
    $test = Invoke-OrchestratorApi "GET" "/api/test/snapshot"

    if ($null -eq $session -or $null -eq $test) {
        return $null
    }

    return @{
        Level         = $session.Level ?? 1
        CurrentGoal   = $session.CurrentGoal ?? "None"
        Health        = $test.Health ?? 100
        Mana          = $test.Mana ?? 100
        Zone          = $test.Zone
        IsDead        = $test.IsDead -eq $true
        NoPlan        = $session.NoPlan -eq $true
        Kills         = $session.Kills ?? 0
        Deaths        = $session.Deaths ?? 0
        IsActive      = $session.IsActive -eq $true
        LastEvent     = "[$(Get-Date -Format 'HH:mm:ss')] Snapshot retrieved"
    }
}

function Test-ServiceHealth() {
    try {
        $health = Invoke-RestMethod -Uri "$BaseUrl/api/health" -TimeoutSec 5
        return $health -ne $null
    }
    catch {
        return $false
    }
}

# ============================================================================
# Core State Machine Functions
# ============================================================================

function Test-LevelUp([object]$Snapshot) {
    if ($Snapshot.Level -gt $sessionState.LastKnownLevel) {
        $levelDiff = $Snapshot.Level - $sessionState.LastKnownLevel
        Write-Log "LEVEL UP! $($sessionState.LastKnownLevel) → $($Snapshot.Level)" "OK"

        for ($i = $sessionState.LastKnownLevel + 1; $i -le $Snapshot.Level; $i++) {
            $sessionState.LeveledUpAt[$i] = (Get-Date).ToUniversalTime()
        }

        $sessionState.LastKnownLevel = $Snapshot.Level
        $sessionState.SameGoalTickCount = 0
        Save-SessionState

        return $true
    }
    return $false
}

function Invoke-TrainerPause([int]$Level) {
    Write-Log "*** TRAINER PAUSE AT LEVEL $Level ***" "WARN"
    $sessionState.State = "TRAINER_PAUSE"
    Save-SessionState

    # Determine trainer location
    $trainerLocation = if ($Level -le 10) {
        $TRAINER_LOCATIONS["1-10"]
    }
    elseif ($Level -le 20) {
        $TRAINER_LOCATIONS["10-20"]
    }
    elseif ($Level -lt 60) {
        $TRAINER_LOCATIONS["20-60"]
    }
    else {
        $TRAINER_LOCATIONS["60-70"]
    }

    Write-Log "Trainer Location: $trainerLocation" "WARN"
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
    Write-Host "║                     TRAINER PAUSE                          ║" -ForegroundColor Magenta
    Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
    Write-Host "Level: $Level" -ForegroundColor Yellow
    Write-Host "Trainer Location: $trainerLocation" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please visit your class trainer and learn new spells." -ForegroundColor Cyan
    Write-Host "Press Enter when done..." -ForegroundColor Cyan

    if ($AutoConfirmTrainer) {
        Write-Host "[AUTO-CONFIRMED - resuming in 10 seconds]" -ForegroundColor Gray
        Start-Sleep -Seconds 10
    }
    else {
        Read-Host | Out-Null
    }

    # Resume bot
    $null = Invoke-OrchestratorApi "POST" "/api/features/killswitch" @{ active = $false }
    $null = Invoke-OrchestratorApi "POST" "/api/bot/start"

    $sessionState.State = "RUNNING"
    $sessionState.SameGoalTickCount = 0
    Save-SessionState

    Write-Log "Trainer pause completed, resuming at level $Level" "OK"
}

function Invoke-StuckRecovery([string]$CurrentGoal) {
    $sessionState.State = "STUCK_RECOVERY"
    $sessionState.StuckRecoveryCount++
    Save-SessionState

    Write-Log "STUCK RECOVERY ATTEMPT #$($sessionState.StuckRecoveryCount) - Last goal: $CurrentGoal" "WARN"

    # Get diagnostics
    $diag = Invoke-OrchestratorApi "GET" "/api/troubleshoot"
    if ($diag) {
        Write-Log "Diagnostics: $($diag.recommendations -join ', ')" "DEBUG"
    }

    # Apply fixes
    $null = Invoke-OrchestratorApi "POST" "/api/diagnostics/fix/all"

    # Restart bot
    $null = Invoke-OrchestratorApi "POST" "/api/bot/stop"
    Start-Sleep -Seconds 5
    $null = Invoke-OrchestratorApi "POST" "/api/bot/start"

    # Wait for new plan (max 60 seconds)
    $waitTicks = 0
    $newGoal = $CurrentGoal
    while ($waitTicks -lt 4 -and $newGoal -eq $CurrentGoal) {
        Start-Sleep -Seconds 15
        $snapshot = Get-BotSnapshot
        if ($snapshot) {
            $newGoal = $snapshot.CurrentGoal
        }
        $waitTicks++
    }

    if ($newGoal -eq $CurrentGoal) {
        Write-Log "STUCK RECOVERY FAILED - No new goal after restart" "ERROR"

        if ($sessionState.StuckRecoveryCount -ge $MAX_STUCK_RECOVERY_ATTEMPTS) {
            $sessionState.State = "INTERVENTION_REQUIRED"
            Save-SessionState
            Write-Log "MAX RECOVERY ATTEMPTS EXCEEDED - Manual intervention needed" "ERROR"
            return $false
        }
    }
    else {
        Write-Log "STUCK RECOVERY SUCCESS - New goal: $newGoal" "OK"
        $sessionState.StuckRecoveryCount = 0
        $sessionState.SameGoalTickCount = 0
        $sessionState.State = "RUNNING"
        Save-SessionState
        return $true
    }

    return $false
}

function Invoke-DeathRecovery() {
    $sessionState.State = "DEATH_RECOVERY"
    Save-SessionState

    Write-Log "DEATH DETECTED - Waiting for resurrection..." "WARN"
    $deathWaitTicks = 0

    while ($deathWaitTicks -lt $MAX_DEATH_WAIT_TICKS) {
        Start-Sleep -Seconds 15
        $snapshot = Get-BotSnapshot

        if ($snapshot -and -not $snapshot.IsDead) {
            Write-Log "Resurrection confirmed" "OK"
            $sessionState.TotalDeaths++
            $sessionState.State = "RUNNING"
            $sessionState.SameGoalTickCount = 0
            Save-SessionState
            return
        }

        $deathWaitTicks++
        Write-Log "Still dead... waiting ($deathWaitTicks/$MAX_DEATH_WAIT_TICKS)" "DEBUG"
    }

    Write-Log "DEATH TIMEOUT - Manual resurrection needed" "ERROR"
    $sessionState.State = "INTERVENTION_REQUIRED"
    Save-SessionState
}

function Invoke-ServiceRestart() {
    Write-Log "Attempting BlazorServer restart..." "WARN"

    # Kill existing process
    Get-Process | Where-Object { $_.ProcessName -match "dotnet" } | Stop-Process -Force -ErrorAction SilentlyContinue

    Start-Sleep -Seconds 5

    # Start new process
    $blazorPath = Join-Path $BotRoot "BlazorServer"
    $process = Start-Process -FilePath "dotnet" -ArgumentList "run --project BlazorServer -c Release" -WorkingDirectory $BotRoot -PassThru

    Write-Log "BlazorServer process started (PID: $($process.Id))" "INFO"

    # Wait for health
    $healthWaitTicks = 0
    while ($healthWaitTicks -lt ($BLAZOR_RESTART_TIMEOUT / 5)) {
        Start-Sleep -Seconds 5

        if (Test-ServiceHealth) {
            Write-Log "BlazorServer is healthy" "OK"
            $sessionState.ApiFailureCount = 0

            # Reload profile
            $null = Invoke-OrchestratorApi "POST" "/api/bot/profile/load" @{ profile = $Profile }
            $null = Invoke-OrchestratorApi "POST" "/api/bot/start"

            return $true
        }

        $healthWaitTicks++
    }

    Write-Log "BlazorServer failed to start - manual intervention required" "ERROR"
    return $false
}

# ============================================================================
# Session Persistence
# ============================================================================

function Save-SessionState() {
    $sessionState.LastSavedAt = (Get-Date).ToUniversalTime()

    $state = $sessionState | ConvertTo-Json -Depth 12
    Set-Content -Path $sessionStateFile -Value $state -ErrorAction SilentlyContinue
}

function Load-SessionState() {
    if (Test-Path $sessionStateFile) {
        try {
            $loaded = Get-Content $sessionStateFile | ConvertFrom-Json

            # Merge loaded state
            $script:sessionState.LastKnownLevel = $loaded.LastKnownLevel
            $script:sessionState.TotalKills = $loaded.TotalKills
            $script:sessionState.TotalDeaths = $loaded.TotalDeaths
            $script:sessionState.LeveledUpAt = $loaded.LeveledUpAt

            Write-Log "Session state restored: Level $($loaded.LastKnownLevel), Kills $($loaded.TotalKills), Deaths $($loaded.TotalDeaths)" "OK"
        }
        catch {
            Write-Log "Failed to load session state: $($_.Exception.Message)" "WARN"
        }
    }
}

# ============================================================================
# Main Loop
# ============================================================================

function Start-OrchestratorLoop() {
    Write-Log "===== WARLOCK LEVELING ORCHESTRATOR STARTED =====" "OK"
    Write-Log "Profile: $Profile" "INFO"
    Write-Log "Starting Level: $($sessionState.LastKnownLevel)" "INFO"
    Write-Log "Poll Interval: ${PollIntervalSeconds}s" "INFO"

    Load-SessionState

    $sessionState.State = "RUNNING"
    Save-SessionState

    # Main poll loop
    while ($true) {
        try {
            # Get snapshot
            $snapshot = Get-BotSnapshot

            if ($null -eq $snapshot) {
                $sessionState.ApiFailureCount++

                if ($sessionState.ApiFailureCount -ge $MAX_API_FAILURES) {
                    Write-Log "API failure threshold reached - attempting service restart" "WARN"

                    if (Invoke-ServiceRestart) {
                        $sessionState.ApiFailureCount = 0
                    }
                    else {
                        $sessionState.State = "INTERVENTION_REQUIRED"
                        Save-SessionState
                    }
                }

                Show-Dashboard @{ Level = $sessionState.LastKnownLevel; Zone = "Unknown"; Health = 0; Mana = 0; CurrentGoal = "API_OFFLINE" }
                Start-Sleep -Seconds $PollIntervalSeconds
                continue
            }

            $sessionState.ApiFailureCount = 0  # Reset on successful call

            # Check for level up
            Test-LevelUp $snapshot | Out-Null

            # Update kill/death counts
            $sessionState.TotalKills = [math]::Max($sessionState.TotalKills, $snapshot.Kills)
            $sessionState.TotalDeaths = [math]::Max($sessionState.TotalDeaths, $snapshot.Deaths)

            # Handle state transitions
            switch ($sessionState.State) {
                "RUNNING" {
                    # Check for critical events
                    if ($snapshot.IsDead) {
                        Invoke-DeathRecovery
                    }
                    elseif ($snapshot.NoPlan) {
                        Write-Log "No plan detected - applying diagnostics" "WARN"
                        $null = Invoke-OrchestratorApi "POST" "/api/diagnostics/fix/all"
                    }
                    else {
                        # Check for stuck (same goal too long)
                        if ($snapshot.CurrentGoal -eq $sessionState.LastGoal) {
                            $sessionState.SameGoalTickCount++

                            if ($sessionState.SameGoalTickCount -ge $MAX_SAME_GOAL_TICKS) {
                                Invoke-StuckRecovery $snapshot.CurrentGoal | Out-Null
                            }
                        }
                        else {
                            $sessionState.LastGoal = $snapshot.CurrentGoal
                            $sessionState.SameGoalTickCount = 0
                        }

                        # Check for trainer pause
                        if ($CRITICAL_TRAINER_LEVELS -contains $snapshot.Level -and $snapshot.Level -notin $sessionState.LeveledUpAt.Keys) {
                            # Only pause once per level
                            if ($sessionState.LeveledUpAt.ContainsKey($snapshot.Level)) {
                                # Already paused, skip
                            }
                            else {
                                Invoke-TrainerPause $snapshot.Level
                            }
                        }
                    }
                }

                "INTERVENTION_REQUIRED" {
                    Write-Host ""
                    Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Red
                    Write-Host "║         MANUAL INTERVENTION REQUIRED                      ║" -ForegroundColor Red
                    Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Red
                    Write-Host ""
                    Write-Host "Check logs at: $logFile" -ForegroundColor Yellow
                    Write-Host "Press Ctrl+C to exit." -ForegroundColor Yellow
                    Write-Host ""
                }
            }

            # Update dashboard
            Show-Dashboard $snapshot
            Save-SessionState
        }
        catch {
            Write-Log "Loop error: $($_.Exception.Message)" "ERROR"
        }

        Start-Sleep -Seconds $PollIntervalSeconds
    }
}

# ============================================================================
# Entry Point
# ============================================================================

Write-Log "Script initialized, starting main loop..." "OK"
Start-OrchestratorLoop
