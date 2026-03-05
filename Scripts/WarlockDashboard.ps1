#Requires -Version 5.1
<#
.SYNOPSIS
  Lightweight read-only status dashboard for Blood Elf Warlock leveling.

.DESCRIPTION
  Displays real-time bot status in a separate terminal. Read-only - no mutations.
  Polls every 10 seconds and refreshes the display.

  Run in a second terminal alongside Orchestrate-WarlockLeveling.ps1:
    Terminal 1: Orchestrator (state machine, recovery, trainer pauses)
    Terminal 2: Dashboard (monitoring only)

.EXAMPLES
  pwsh -File Scripts/WarlockDashboard.ps1
  pwsh -File Scripts/WarlockDashboard.ps1 -BaseUrl http://localhost:5000 -RefreshSeconds 10
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5000",
    [int]$RefreshSeconds = 10,
    [Alias("h", "?")][switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

if ($Help)
{
    Write-Host "Usage: WarlockDashboard.ps1 [-BaseUrl <url>] [-RefreshSeconds <n>]" -ForegroundColor Cyan
    Write-Host "Opens a read-only live status dashboard for warlock leveling." -ForegroundColor Gray
    return
}

$COLOR_HEALTHY = "Green"
$COLOR_WARNING = "Yellow"
$COLOR_ERROR = "Red"
$COLOR_INFO = "Cyan"
$COLOR_STAT = "White"

function Get-DashboardData() {
    try {
        $session = Invoke-RestMethod -Uri "$BaseUrl/api/session" -TimeoutSec 5 -ErrorAction SilentlyContinue
        $health = Invoke-RestMethod -Uri "$BaseUrl/api/health" -TimeoutSec 5 -ErrorAction SilentlyContinue
        $snapshot = Invoke-RestMethod -Uri "$BaseUrl/api/test/snapshot" -TimeoutSec 5 -ErrorAction SilentlyContinue

        if ($null -eq $session) {
            return @{
                Status = "OFFLINE"
                Level = 0
                Zone = "Unknown"
                Health = 0
                Mana = 0
                CurrentGoal = "Service Unavailable"
                Kills = 0
                Deaths = 0
                IsActive = $false
            }
        }

        return @{
            Status = if ($session.IsActive) { "ACTIVE" } else { "PAUSED" }
            Level = $session.Level ?? 0
            Zone = $snapshot.Zone ?? "Unknown"
            Health = $snapshot.Health ?? 0
            Mana = $snapshot.Mana ?? 0
            CurrentGoal = $session.CurrentGoal ?? "None"
            Kills = $session.Kills ?? 0
            Deaths = $session.Deaths ?? 0
            IsActive = $session.IsActive
            IsDead = $snapshot.IsDead -eq $true
        }
    }
    catch {
        return @{
            Status = "ERROR"
            Level = 0
            Zone = "Connection Failed"
            Health = 0
            Mana = 0
            CurrentGoal = $_.Exception.Message
            Kills = 0
            Deaths = 0
            IsActive = $false
        }
    }
}

function Show-DashboardUi([object]$Data, [datetime]$StartTime) {
    Clear-Host

    $uptime = (Get-Date) - $StartTime
    $uptimeStr = "{0:D2}h {1:D2}m" -f $uptime.Hours, $uptime.Minutes

    $killRate = 0
    if ($uptime.TotalHours -gt 0) {
        $killRate = [math]::Round($Data.Kills / $uptime.TotalHours, 1)
    }

    $statusColor = switch ($Data.Status) {
        "ACTIVE" { $COLOR_HEALTHY }
        "PAUSED" { $COLOR_WARNING }
        "OFFLINE" { $COLOR_ERROR }
        default { $COLOR_STAT }
    }

    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor $COLOR_INFO
    Write-Host "║   WARLOCK LEVELING DASHBOARD - READ ONLY                  ║" -ForegroundColor $COLOR_INFO
    Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor $COLOR_INFO
    Write-Host ""

    Write-Host "Status:" -ForegroundColor $COLOR_STAT -NoNewline
    Write-Host " $($Data.Status)" -ForegroundColor $statusColor
    Write-Host "Uptime:" -ForegroundColor $COLOR_STAT -NoNewline
    Write-Host " $uptimeStr" -ForegroundColor $COLOR_INFO
    Write-Host ""

    Write-Host "Level:" -ForegroundColor $COLOR_STAT -NoNewline
    Write-Host " $($Data.Level) / 70" -ForegroundColor $COLOR_STAT
    Write-Host "Zone:" -ForegroundColor $COLOR_STAT -NoNewline
    Write-Host " $($Data.Zone)" -ForegroundColor $COLOR_STAT
    Write-Host ""

    $healthColor = if ($Data.Health -gt 50) { $COLOR_HEALTHY } elseif ($Data.Health -gt 25) { $COLOR_WARNING } else { $COLOR_ERROR }
    $manaColor = if ($Data.Mana -gt 50) { $COLOR_HEALTHY } elseif ($Data.Mana -gt 25) { $COLOR_WARNING } else { $COLOR_ERROR }

    Write-Host "Health:" -ForegroundColor $COLOR_STAT -NoNewline
    Write-Host " $($Data.Health)%" -ForegroundColor $healthColor
    Write-Host "Mana:" -ForegroundColor $COLOR_STAT -NoNewline
    Write-Host " $($Data.Mana)%" -ForegroundColor $manaColor
    Write-Host ""

    Write-Host "Current Goal:" -ForegroundColor $COLOR_STAT -NoNewline
    Write-Host " $($Data.CurrentGoal)" -ForegroundColor $COLOR_INFO
    Write-Host ""

    Write-Host "Kills:" -ForegroundColor $COLOR_STAT -NoNewline
    Write-Host " $($Data.Kills)" -ForegroundColor $COLOR_HEALTHY -NoNewline
    Write-Host " ($($killRate)/hr)" -ForegroundColor $COLOR_STAT
    Write-Host "Deaths:" -ForegroundColor $COLOR_STAT -NoNewline
    Write-Host " $($Data.Deaths)" -ForegroundColor (if ($Data.Deaths -gt 0) { $COLOR_WARNING } else { $COLOR_HEALTHY })
    Write-Host ""

    if ($Data.IsDead) {
        Write-Host "WARNING: Character is DEAD" -ForegroundColor $COLOR_ERROR
        Write-Host ""
    }

    Write-Host "Last Updated: $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor $COLOR_STAT
    Write-Host "Refresh Interval: ${RefreshSeconds}s (next update in..." -ForegroundColor $COLOR_STAT -NoNewline

    for ($i = $RefreshSeconds; $i -gt 0; $i--) {
        Write-Host " $i" -ForegroundColor $COLOR_INFO -NoNewline
        Start-Sleep -Seconds 1
    }

    Write-Host ")" -ForegroundColor $COLOR_STAT
}

# Main loop
$startTime = Get-Date

while ($true) {
    try {
        $data = Get-DashboardData
        Show-DashboardUi $data $startTime
    }
    catch {
        Clear-Host
        Write-Host "Dashboard Error: $($_.Exception.Message)" -ForegroundColor $COLOR_ERROR
        Start-Sleep -Seconds $RefreshSeconds
    }
}
