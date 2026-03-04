#Requires -Version 7.0

<#
.SYNOPSIS
Autonomous Warlock Leveling Orchestrator
Runs as a PowerShell script in the background, polling bot APIs and making decisions.

.EXAMPLE
PS> pwsh -NoProfile -File Scripts/Orchestrate-Warlock-AI.ps1
#>

param(
    [string]$BaseUrl = "http://localhost:5000",
    [int]$PollIntervalSeconds = 30,
    [string]$SessionFile = "logs/warlock-ai-session.json"
)

$ErrorActionPreference = "Continue"
Set-StrictMode -Version Latest

$script:session = @{}
$script:baseUrl = $BaseUrl
$script:trainerLevels = @(10, 20, 30, 40, 42, 50, 60, 62, 66)

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $prefix = "[$timestamp] [$Level]"
    Write-Host "$prefix $Message"
}

function Load-SessionState {
    if (Test-Path $SessionFile) {
        try {
            $script:session = Get-Content $SessionFile -Raw | ConvertFrom-Json -AsHashtable
            Write-Log "Loaded session state from $SessionFile"
        }
        catch {
            Write-Log "Failed to load session: $_" "WARN"
            Initialize-SessionState
        }
    }
    else {
        Initialize-SessionState
    }
}

function Initialize-SessionState {
    $script:session = @{
        StartedAt = (Get-Date -AsUTC).ToString("o")
        LastPollAt = (Get-Date -AsUTC).ToString("o")
        EstimatedLevel = 1
        TotalKills = 0
        TotalDeaths = 0
        State = "RUNNING"
        LastGoal = ""
        SameGoalCount = 0
        TrainersPaused = @()
        RecoveryAttempts = 0
        PollCount = 0
        StatusHistory = @()
    }
}

function Save-SessionState {
    try {
        $script:session["LastPollAt"] = (Get-Date -AsUTC).ToString("o")
        $jsonPath = Resolve-Path $SessionFile -ErrorAction SilentlyContinue
        if (-not $jsonPath) {
            $jsonPath = Join-Path (Get-Location) $SessionFile
        }
        $script:session | ConvertTo-Json -Depth 10 | Set-Content $jsonPath -Force
    }
    catch {
        Write-Log "Failed to save session: $_" "WARN"
    }
}

function Poll-BotAPI {
    try {
        Write-Log "Polling bot status..." "DEBUG"

        # Get core status
        $status = Invoke-RestMethod -Uri "$script:baseUrl/api/bot/status" -Method Get -TimeoutSec 5 -ErrorAction Stop
        $stats = Invoke-RestMethod -Uri "$script:baseUrl/api/session/stats" -Method Get -TimeoutSec 5 -ErrorAction Stop
        $snapshot = Invoke-RestMethod -Uri "$script:baseUrl/api/test/snapshot" -Method Get -TimeoutSec 5 -ErrorAction Stop
        $goap = Invoke-RestMethod -Uri "$script:baseUrl/api/troubleshoot/goap" -Method Get -TimeoutSec 5 -ErrorAction Stop

        return @{
            Status = $status
            Stats = $stats
            Snapshot = $snapshot
            Goap = $goap
            Success = $true
        }
    }
    catch {
        Write-Log "API poll failed: $_" "ERROR"
        return @{ Success = $false; Error = $_.ToString() }
    }
}

function Evaluate-State {
    param([hashtable]$Data)

    if (-not $Data.Success) {
        Write-Log "Bot API unreachable - attempting restart" "WARN"
        return @{ Action = "RESTART_BOT" }
    }

    $currentGoal = $Data.Status.currentGoal
    $isDead = $Data.Snapshot.data.snapshot.dead
    $kills = $Data.Stats.kills
    $deaths = $Data.Stats.deaths
    $level = $Data.Snapshot.data.snapshot.level
    $kph = $Data.Stats.killsPerHour

    # Death detection
    if ($isDead) {
        return @{ Action = "DEATH_DETECTED"; Level = $level }
    }

    # No plan detection
    if ($Data.Goap.status -eq "NoGoal" -or $Data.Goap.noPlanEvents.Count -gt 0) {
        return @{ Action = "NO_PLAN"; GoalStatus = $Data.Goap.status }
    }

    # Stuck detection (same goal too long)
    if ($script:session.LastGoal -eq $currentGoal) {
        $script:session.SameGoalCount++
        if ($script:session.SameGoalCount -gt 5) {
            return @{ Action = "STUCK_DETECTED"; Goal = $currentGoal; Count = $script:session.SameGoalCount }
        }
    }
    else {
        $script:session.SameGoalCount = 1
        $script:session.LastGoal = $currentGoal
    }

    # Trainer level detection
    if ($level -in $script:trainerLevels) {
        if ($level -notin $script:session.TrainersPaused) {
            return @{ Action = "TRAINER_PAUSE"; Level = $level }
        }
    }

    # Low kill rate (stuck in bad zone)
    if ($kph -lt 3 -and $script:session.PollCount -gt 3) {
        return @{ Action = "LOW_KPH"; KPH = $kph }
    }

    # Normal operation
    $script:session.TotalKills = $kills
    $script:session.TotalDeaths = $deaths
    $script:session.EstimatedLevel = $level

    return @{ Action = "CONTINUE"; Level = $level; Kills = $kills; KPH = [math]::Round($kph, 1) }
}

function Execute-Action {
    param([hashtable]$Decision)

    switch ($Decision.Action) {
        "CONTINUE" {
            Write-Log "✓ Running normally - L$($Decision.Level) | Kills: $($Decision.Kills) | KPH: $($Decision.KPH)" "INFO"
            return $true
        }

        "DEATH_DETECTED" {
            Write-Log "💀 DEATH DETECTED at Level $($Decision.Level)" "ERROR"
            Write-Log "Waiting for player resurrection..." "WARN"
            $script:session.TotalDeaths++
            # Pause and wait for user action
            return $false
        }

        "TRAINER_PAUSE" {
            Write-Log "⚠️ TRAINER LEVEL $($Decision.Level) - Pausing bot" "WARN"
            Write-Log "Please visit trainer and confirm when ready to resume" "WARN"

            # Killswitch to pause
            try {
                $body = @{ active = $true } | ConvertTo-Json
                Invoke-RestMethod -Uri "$script:baseUrl/api/features/killswitch" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 5 | Out-Null
            }
            catch { Write-Log "Killswitch failed: $_" "WARN" }

            $script:session.TrainersPaused += $Decision.Level
            return $false
        }

        "STUCK_DETECTED" {
            Write-Log "🔁 STUCK - Same goal for $($Decision.Count) polls: $($Decision.Goal)" "WARN"
            Write-Log "Attempting recovery via diagnostics" "INFO"

            try {
                Invoke-RestMethod -Uri "$script:baseUrl/api/diagnostics/fix/all" -Method Post -TimeoutSec 10 | Out-Null
                Write-Log "Recovery request sent" "INFO"
            }
            catch { Write-Log "Diagnostics failed: $_" "WARN" }

            $script:session.RecoveryAttempts++
            return $true
        }

        "NO_PLAN" {
            Write-Log "⚠️ NO PLAN - Status: $($Decision.GoalStatus)" "WARN"
            Write-Log "Attempting recovery" "INFO"

            try {
                Invoke-RestMethod -Uri "$script:baseUrl/api/diagnostics/fix/all" -Method Post -TimeoutSec 10 | Out-Null
            }
            catch { Write-Log "Diagnostics failed: $_" "WARN" }

            return $true
        }

        "RESTART_BOT" {
            Write-Log "🔴 API UNREACHABLE - Bot restart needed" "ERROR"
            return $false
        }

        default {
            Write-Log "Unknown action: $($Decision.Action)" "WARN"
            return $true
        }
    }
}

# ============= MAIN LOOP =============

Load-SessionState

Write-Log "Starting Warlock AI Orchestrator - L1 to 70" "INFO"
Write-Log "Base URL: $script:baseUrl" "INFO"
Write-Log "Poll interval: ${PollIntervalSeconds}s" "INFO"
Write-Log "Trainer pause levels: $($script:trainerLevels -join ', ')" "INFO"
Write-Log ""

while ($true) {
    $script:session.PollCount++
    Write-Log "Poll #$($script:session.PollCount) [$((Get-Date).ToString('HH:mm:ss'))]" "DEBUG"

    # Poll bot
    $pollData = Poll-BotAPI

    # Evaluate state
    $decision = Evaluate-State $pollData

    # Execute action
    $continueRunning = Execute-Action $decision

    # Save session state
    Save-SessionState

    # Add to history
    $historyEntry = "$(Get-Date -Format 'HH:mm:ss'): $($decision.Action)"
    $script:session.StatusHistory += $historyEntry
    if ($script:session.StatusHistory.Count -gt 100) {
        $script:session.StatusHistory = $script:session.StatusHistory[-100..-1]
    }

    if (-not $continueRunning) {
        Write-Log "Paused or critical event - waiting for external action" "WARN"
        # Wait for manual intervention
        Start-Sleep -Seconds 10
    }
    else {
        Start-Sleep -Seconds $PollIntervalSeconds
    }
}
