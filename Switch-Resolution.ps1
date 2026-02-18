<#
.SYNOPSIS
    Switch between resolution-specific frame configurations.
    
.DESCRIPTION
    Lists available frame_config_{W}x{H}.json files and lets you activate one
    as the active frame_config.json. The bot will auto-detect on startup, but
    this script is useful for manual switching or pre-staging a config before launch.

.PARAMETER Resolution
    Target resolution in WxH format (e.g. "1920x1080", "3840x2160").
    If omitted, shows an interactive menu.

.EXAMPLE
    .\Switch-Resolution.ps1
    .\Switch-Resolution.ps1 -Resolution 1920x1080
    .\Switch-Resolution.ps1 3840x2160
#>
param(
    [Parameter(Position = 0)]
    [string]$Resolution
)

$ErrorActionPreference = "Stop"

# Find configs in both BlazorServer project dir and bin output
$projectDir = Join-Path $PSScriptRoot "BlazorServer"
$binDir = Join-Path $projectDir "bin\Debug\net10.0"

function Get-ResolutionConfigs($dir) {
    if (-not (Test-Path $dir)) { return @() }
    Get-ChildItem -Path $dir -Filter "frame_config_*x*.json" | ForEach-Object {
        if ($_.BaseName -match 'frame_config_(\d+)x(\d+)') {
            [PSCustomObject]@{
                Width      = [int]$Matches[1]
                Height     = [int]$Matches[2]
                Label      = "$($Matches[1])x$($Matches[2])"
                Path       = $_.FullName
                Directory  = $dir
            }
        }
    }
}

# Gather from project directory
$configs = @(Get-ResolutionConfigs $projectDir)

if ($configs.Count -eq 0) {
    Write-Host "No resolution-specific configs found in $projectDir" -ForegroundColor Yellow
    Write-Host "Run the bot with AutoConfigureFrames=true at each resolution to generate them." -ForegroundColor Yellow
    exit 1
}

# Get current active config info
$activeConfig = $null
$activeLabel = "none"
$activePath = Join-Path $projectDir "frame_config.json"
if (Test-Path $activePath) {
    $active = Get-Content $activePath | ConvertFrom-Json
    $activeLabel = "$($active.Rect.Width)x$($active.Rect.Height)"
    $activeConfig = $active
}

Write-Host ""
Write-Host "=== Frame Config Resolution Switcher ===" -ForegroundColor Cyan
Write-Host "Active config: $activeLabel" -ForegroundColor Green
Write-Host ""

# Interactive selection if no parameter
if (-not $Resolution) {
    Write-Host "Available resolutions:" -ForegroundColor White
    for ($i = 0; $i -lt $configs.Count; $i++) {
        $marker = if ($configs[$i].Label -eq $activeLabel) { " (active)" } else { "" }
        Write-Host "  [$($i + 1)] $($configs[$i].Label)$marker"
    }
    Write-Host ""
    $choice = Read-Host "Select resolution (1-$($configs.Count))"
    
    if ($choice -match '^\d+$' -and [int]$choice -ge 1 -and [int]$choice -le $configs.Count) {
        $selected = $configs[[int]$choice - 1]
    } else {
        Write-Host "Invalid selection." -ForegroundColor Red
        exit 1
    }
} else {
    $selected = $configs | Where-Object { $_.Label -eq $Resolution } | Select-Object -First 1
    if (-not $selected) {
        Write-Host "No config found for resolution: $Resolution" -ForegroundColor Red
        Write-Host "Available: $($configs.Label -join ', ')" -ForegroundColor Yellow
        exit 1
    }
}

if ($selected.Label -eq $activeLabel) {
    Write-Host "Resolution $($selected.Label) is already active." -ForegroundColor Yellow
    exit 0
}

# Copy resolution config to active config
$sourcePath = $selected.Path
$destPath = Join-Path $projectDir "frame_config.json"

Write-Host "Switching to $($selected.Label)..." -ForegroundColor White
Copy-Item -Path $sourcePath -Destination $destPath -Force
Write-Host "  Updated: $destPath" -ForegroundColor Gray

# Also copy to bin output if it exists
if (Test-Path $binDir) {
    $binDest = Join-Path $binDir "frame_config.json"
    Copy-Item -Path $sourcePath -Destination $binDest -Force
    Write-Host "  Updated: $binDest" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Switched to $($selected.Label) successfully!" -ForegroundColor Green
Write-Host "Restart BlazorServer to apply the new config." -ForegroundColor Yellow
