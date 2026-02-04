<#
.SYNOPSIS
    WowClassicGrindBot Service Monitor
    
.DESCRIPTION
    Monitors and manages bot services with auto-restart capabilities.
    Can run as a background watchdog process.
#>

#Requires -Version 5.1

param(
    [switch]$Monitor,
    [switch]$Status,
    [switch]$StartAll,
    [switch]$StopAll,
    [int]$CheckIntervalSeconds = 30,
    [string]$BotPath = "C:\WowClassicGrindBot"
)

$script:Services = @{
    Navigation = @{
        Name = "AmeisenNavigationServer"
        DisplayName = "Navigation Server"
        Path = Join-Path $BotPath "Navigation\AmeisenNavigationServer.exe"
        WorkingDir = Join-Path $BotPath "Navigation"
        Port = 47110
        Required = $false
        AutoRestart = $true
    }
    BlazorServer = @{
        Name = "BlazorServer"
        DisplayName = "Bot Server"
        Path = Join-Path $BotPath "BlazorServer\bin\Release\net10.0\BlazorServer.exe"
        WorkingDir = Join-Path $BotPath "BlazorServer\bin\Release\net10.0"
        Port = 5000
        Required = $true
        AutoRestart = $true
    }
    WoW = @{
        Name = "WowClassic"
        DisplayName = "WoW Classic"
        Required = $false
        AutoRestart = $false
    }
}

function Get-ServiceStatus {
    param([string]$ServiceName)
    
    $process = Get-Process -Name $ServiceName -ErrorAction SilentlyContinue
    
    if ($process) {
        return @{
            Running = $true
            PID = $process.Id
            CPU = $process.CPU
            Memory = [math]::Round($process.WorkingSet64 / 1MB, 2)
            StartTime = $process.StartTime
        }
    }
    
    return @{
        Running = $false
        PID = $null
        CPU = 0
        Memory = 0
        StartTime = $null
    }
}

function Start-Service {
    param([string]$ServiceKey)
    
    $service = $script:Services[$ServiceKey]
    
    if (-not $service.Path -or -not (Test-Path $service.Path)) {
        Write-Warning "Service executable not found: $($service.DisplayName)"
        return $false
    }
    
    $status = Get-ServiceStatus -ServiceName $service.Name
    if ($status.Running) {
        Write-Host "[$($service.DisplayName)] Already running (PID: $($status.PID))" -ForegroundColor Green
        return $true
    }
    
    Write-Host "[$($service.DisplayName)] Starting..." -ForegroundColor Cyan
    
    try {
        Start-Process -FilePath $service.Path -WorkingDirectory $service.WorkingDir -WindowStyle Minimized
        Start-Sleep -Seconds 2
        
        $status = Get-ServiceStatus -ServiceName $service.Name
        if ($status.Running) {
            Write-Host "[$($service.DisplayName)] Started (PID: $($status.PID))" -ForegroundColor Green
            return $true
        } else {
            Write-Host "[$($service.DisplayName)] Failed to start" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "[$($service.DisplayName)] Error: $_" -ForegroundColor Red
        return $false
    }
}

function Stop-Service {
    param([string]$ServiceKey)
    
    $service = $script:Services[$ServiceKey]
    $status = Get-ServiceStatus -ServiceName $service.Name
    
    if (-not $status.Running) {
        Write-Host "[$($service.DisplayName)] Not running" -ForegroundColor Yellow
        return $true
    }
    
    Write-Host "[$($service.DisplayName)] Stopping (PID: $($status.PID))..." -ForegroundColor Cyan
    
    try {
        Stop-Process -Name $service.Name -Force -ErrorAction Stop
        Write-Host "[$($service.DisplayName)] Stopped" -ForegroundColor Green
        return $true
    } catch {
        Write-Host "[$($service.DisplayName)] Error stopping: $_" -ForegroundColor Red
        return $false
    }
}

function Show-AllStatus {
    Write-Host ""
    Write-Host "  ┌──────────────────────────────────────────────────────────────────────┐" -ForegroundColor Cyan
    Write-Host "  │                    SERVICE STATUS                                    │" -ForegroundColor Cyan
    Write-Host "  ├──────────────────────────────────────────────────────────────────────┤" -ForegroundColor Cyan
    Write-Host "  │  Service              Status      PID       Memory     Uptime        │" -ForegroundColor Cyan
    Write-Host "  ├──────────────────────────────────────────────────────────────────────┤" -ForegroundColor Cyan
    
    foreach ($key in $script:Services.Keys) {
        $service = $script:Services[$key]
        $status = Get-ServiceStatus -ServiceName $service.Name
        
        $statusText = if ($status.Running) { "RUNNING" } else { "STOPPED" }
        $statusColor = if ($status.Running) { "Green" } else { "Red" }
        $pidText = if ($status.PID) { $status.PID.ToString().PadLeft(6) } else { "  N/A " }
        $memText = if ($status.Memory) { "$($status.Memory.ToString('0.0').PadLeft(6)) MB" } else { "    N/A   " }
        $uptimeText = if ($status.StartTime) { 
            $uptime = (Get-Date) - $status.StartTime
            "$($uptime.Hours.ToString().PadLeft(2))h $($uptime.Minutes.ToString().PadLeft(2))m"
        } else { "   N/A    " }
        
        $displayName = $service.DisplayName.PadRight(18)
        
        Write-Host "  │  $displayName" -NoNewline -ForegroundColor White
        Write-Host "$($statusText.PadRight(10))" -NoNewline -ForegroundColor $statusColor
        Write-Host "  $pidText   $memText  $uptimeText │" -ForegroundColor White
    }
    
    Write-Host "  └──────────────────────────────────────────────────────────────────────┘" -ForegroundColor Cyan
    Write-Host ""
}

function Start-AllServices {
    Write-Host ""
    Write-Host "Starting all services..." -ForegroundColor Cyan
    Write-Host ""
    
    # Start Navigation first
    if ($script:Services.Navigation.Path) {
        Start-Service -ServiceKey "Navigation"
        Start-Sleep -Seconds 2
    }
    
    # Then BlazorServer
    Start-Service -ServiceKey "BlazorServer"
    
    Write-Host ""
    Show-AllStatus
}

function Stop-AllServices {
    Write-Host ""
    Write-Host "Stopping all services..." -ForegroundColor Cyan
    Write-Host ""
    
    Stop-Service -ServiceKey "BlazorServer"
    Stop-Service -ServiceKey "Navigation"
    
    Write-Host ""
    Show-AllStatus
}

function Start-Monitor {
    Write-Host ""
    Write-Host "Starting service monitor (check interval: ${CheckIntervalSeconds}s)..." -ForegroundColor Cyan
    Write-Host "Press Ctrl+C to stop monitoring." -ForegroundColor Yellow
    Write-Host ""
    
    while ($true) {
        $timestamp = Get-Date -Format "HH:mm:ss"
        
        foreach ($key in $script:Services.Keys) {
            $service = $script:Services[$key]
            
            if (-not $service.AutoRestart) { continue }
            if (-not $service.Path) { continue }
            
            $status = Get-ServiceStatus -ServiceName $service.Name
            
            if (-not $status.Running) {
                Write-Host "[$timestamp] $($service.DisplayName) is down - attempting restart..." -ForegroundColor Yellow
                Start-Service -ServiceKey $key
            }
        }
        
        Start-Sleep -Seconds $CheckIntervalSeconds
    }
}

# Main execution
if ($Status) {
    Show-AllStatus
} elseif ($StartAll) {
    Start-AllServices
} elseif ($StopAll) {
    Stop-AllServices
} elseif ($Monitor) {
    Start-Monitor
} else {
    Write-Host ""
    Write-Host "WowClassicGrindBot Service Monitor" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  -Status              Show status of all services"
    Write-Host "  -StartAll            Start all services"
    Write-Host "  -StopAll             Stop all services"
    Write-Host "  -Monitor             Start continuous monitoring with auto-restart"
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  -CheckIntervalSeconds  Interval between health checks (default: 30)"
    Write-Host "  -BotPath               Path to bot installation"
    Write-Host ""
}
