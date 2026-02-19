#!/usr/bin/env pwsh
# Monitor-Bot.ps1
# Real-time bot monitoring dashboard — polls /api/troubleshoot every 10 seconds.
# Usage:  .\Monitor-Bot.ps1 [-Interval 10] [-Once]
# Ctrl+C to stop.

param(
    [int]    $Interval = 10,     # Seconds between polls
    [switch] $Once              # Print one reading and exit
)

$BASE = "http://localhost:5000"

function Get-ColoredStatus([string]$s) {
    $colors = @{ "Active"="Green"; "IDLE"="Yellow"; "degraded"="Red"; "RUNNING"="Green" }
    $c = $colors[$s]
    if ($c) { return $c } else { return "White" }
}

function Format-Bool([bool]$v, [string]$label) {
    $color = if ($v) { "Green" } else { "Red" }
    $sym   = if ($v) { "✓ " } else { "✗ " }
    Write-Host -NoNewline "$sym$label" -ForegroundColor $color
}

function Read-BotState {
    try {
        $wc = New-Object System.Net.WebClient
        $raw = $wc.DownloadString("$BASE/api/troubleshoot")
        return $raw | ConvertFrom-Json
    } catch {
        return $null
    }
}

function Read-Health {
    try {
        $wc2 = New-Object System.Net.WebClient
        return $wc2.DownloadString("$BASE/api/health") | ConvertFrom-Json
    } catch {
        return $null
    }
}

function Show-Dashboard {
    param($state, $health)

    $ts = Get-Date -Format "HH:mm:ss"
    $uptime = if ($health) { $health.app.uptime } else { "?" }

    Clear-Host
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  BOT MONITOR  [$ts]  Uptime: $uptime  (Ctrl+C to stop)" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan

    if ($null -eq $state) {
        Write-Host "  ✗ BlazorServer not responding at $BASE" -ForegroundColor Red
        return
    }

    # ── Status row ───────────────────────────────────────────────────────
    $statusColor = if ($state.bot.isActive) { "Green" } else { "Yellow" }
    $statusText  = if ($state.bot.isActive) { "ACTIVE" } else { "IDLE" }
    Write-Host ""
    Write-Host -NoNewline "  Status: " 
    Write-Host -NoNewline "[$statusText]" -ForegroundColor $statusColor
    Write-Host -NoNewline "  Profile: "
    $profileText = if ($state.bot.profile) { $state.bot.profile } else { "(none)" }
    $profileColor = if ($state.bot.profile) { "White" } else { "Red" }
    Write-Host "$profileText" -ForegroundColor $profileColor

    # ── GOAP row ─────────────────────────────────────────────────────────
    $goapColor = if ($state.goap.status -eq "Active") { "Green" } elseif ($state.goap.status -eq "NotInitialized") { "Red" } else { "Yellow" }
    Write-Host -NoNewline "  GOAP: "
    Write-Host -NoNewline "$($state.goap.status)" -ForegroundColor $goapColor
    if ($state.goap.currentGoalDisplayName) {
        Write-Host -NoNewline "  Goal: " 
        Write-Host -NoNewline "$($state.goap.currentGoalDisplayName)" -ForegroundColor Cyan
    }
    if ($state.goap.recentNoPlanCount -gt 0) {
        Write-Host -NoNewline "  " 
        Write-Host "NoPlan: $($state.goap.recentNoPlanCount)" -ForegroundColor Red
    } else {
        Write-Host ""
    }

    # ── Diagnostics row ──────────────────────────────────────────────────
    $d = $state.diagnostics
    Write-Host -NoNewline "  Diag: "
    Format-Bool $d.keybindingsInitialized "Keybinds"
    Write-Host -NoNewline "  "
    Format-Bool $d.addonResponding "Addon"
    Write-Host -NoNewline "  "
    Format-Bool $d.actionBarInitialized "ActionBar"
    Write-Host -NoNewline "  Latency: "

    $latMs = [math]::Round($d.lastScreenLatencyMs, 1)
    $latColor = if ($latMs -lt 10) { "Green" } elseif ($latMs -lt 20) { "Yellow" } else { "Red" }
    Write-Host "${latMs}ms" -ForegroundColor $latColor

    # ── Nav server ───────────────────────────────────────────────────────
    Write-Host -NoNewline "  NavSrv: "
    if ($health -and $health.startup.isNavigationServerRunning) {
        Write-Host "Running (port 47110)" -ForegroundColor Green
    } else {
        Write-Host "NOT running" -ForegroundColor Red
    }

    # ── KeyBindings count (from health) ──────────────────────────────────
    try {
        $wc3 = New-Object System.Net.WebClient
        $kb = $wc3.DownloadString("$BASE/api/health") | ConvertFrom-Json
        # KeyBindings count is not in health, poll troubleshoot issue list
    } catch {}

    # ── Issues ───────────────────────────────────────────────────────────
    if ($state.summary.issueCount -gt 0) {
        Write-Host ""
        Write-Host "  ⚠ Issues ($($state.summary.issueCount)):" -ForegroundColor Yellow
        foreach ($issue in $state.summary.issues) {
            Write-Host "    • $issue" -ForegroundColor Yellow
        }
    }

    # ── Recent log lines ─────────────────────────────────────────────────
    if ($state.recentLogs -and $state.recentLogs.Count -gt 0) {
        Write-Host ""
        Write-Host "  Recent log:" -ForegroundColor DarkGray
        $state.recentLogs | Select-Object -Last 5 | ForEach-Object {
            $color = switch ($_.level) {
                "Error"   { "Red" }
                "Warning" { "Yellow" }
                default   { "DarkGray" }
            }
            Write-Host "    $($_.timestamp) [$($_.level)] $($_.message)" -ForegroundColor $color
        }
    }

    # ── Recommendations ──────────────────────────────────────────────────
    $highRec = $state.recommendations | Where-Object { $_.severity -eq "high" }
    if ($highRec) {
        Write-Host ""
        Write-Host "  ⛔ Actions needed:" -ForegroundColor Red
        foreach ($r in $highRec) {
            Write-Host "    [$($r.category)] $($r.message)" -ForegroundColor Red
            Write-Host "    → $($r.action)" -ForegroundColor DarkRed
        }
    }

    Write-Host ""
    Write-Host "───────────────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host "  Next poll in ${Interval}s  |  Interval: $Interval s" -ForegroundColor DarkGray
}

# ── Main loop ─────────────────────────────────────────────────────────────
if ($Once) {
    $s = Read-BotState
    $h = Read-Health
    Show-Dashboard $s $h
    exit
}

while ($true) {
    $s = Read-BotState
    $h = Read-Health
    Show-Dashboard $s $h
    Start-Sleep -Seconds $Interval
}
