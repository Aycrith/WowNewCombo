<#
.SYNOPSIS
    Configure WowClassicGrindBot Pathfinder Settings
    
.DESCRIPTION
    This script configures the pathfinder backend for WowClassicGrindBot.
    It supports three backends with automatic fallback:
    
    1. RemoteV3 (AmeisenNavigation) - Best quality, TCP server, uses MMAPs
    2. RemoteV1 (PathingAPI) - Good quality, out-of-process, uses MPQ
    3. Local (PPather) - Basic, in-process, uses MPQ
    
.PARAMETER Backend
    The primary pathfinder backend to use: RemoteV3, RemoteV1, or Local
    
.PARAMETER V3Host
    Host address for AmeisenNavigation server (default: 127.0.0.1)
    
.PARAMETER V3Port
    Port for AmeisenNavigation server (default: 47110)
    
.PARAMETER V1Host
    Host address for PathingAPI server (default: localhost)
    
.PARAMETER V1Port
    Port for PathingAPI server (default: 5001)
    
.PARAMETER EnableVisualizer
    Enable path visualization (requires PathingAPI running)
    
.EXAMPLE
    .\Configure-Pathfinder.ps1 -Backend RemoteV3
    
.EXAMPLE
    .\Configure-Pathfinder.ps1 -Backend Local
#>

param(
    [ValidateSet("RemoteV3", "RemoteV1", "Local")]
    [string]$Backend = "RemoteV3",
    
    [string]$V3Host = "127.0.0.1",
    [int]$V3Port = 47110,
    
    [string]$V1Host = "localhost",
    [int]$V1Port = 5001,
    
    [switch]$EnableVisualizer
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  WowClassicGrindBot Pathfinder Configuration" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

$AppSettingsPath = "C:\WowClassicGrindBot\BlazorServer\appsettings.json"
$AppSettingsDevPath = "C:\WowClassicGrindBot\BlazorServer\appsettings.Development.json"
$ReleasePath = "C:\WowClassicGrindBot\BlazorServer\bin\Release\net10.0\appsettings.json"

# Read current settings
if (Test-Path $AppSettingsPath) {
    $settings = Get-Content $AppSettingsPath -Raw | ConvertFrom-Json
} else {
    Write-Host "[ERROR] appsettings.json not found!" -ForegroundColor Red
    exit 1
}

Write-Host "Current Configuration:" -ForegroundColor Yellow
Write-Host "  Mode: $($settings.Pathing.Mode)"
Write-Host "  V3 Host: $($settings.Pathing.hostv3):$($settings.Pathing.portv3)"
Write-Host "  V1 Host: $($settings.Pathing.hostv1):$($settings.Pathing.portv1)"
Write-Host ""

# Update pathing settings
$settings.Pathing.Mode = $Backend
$settings.Pathing.hostv3 = $V3Host
$settings.Pathing.portv3 = $V3Port
$settings.Pathing.hostv1 = $V1Host
$settings.Pathing.portv1 = $V1Port
$settings.Pathing | Add-Member -NotePropertyName "PathVisualizer" -NotePropertyValue $EnableVisualizer.IsPresent -Force

# Convert back to JSON with proper formatting
$jsonContent = $settings | ConvertTo-Json -Depth 10

# Save to all locations
$jsonContent | Set-Content $AppSettingsPath -Encoding UTF8
Write-Host "[OK] Updated: $AppSettingsPath" -ForegroundColor Green

if (Test-Path $ReleasePath) {
    $jsonContent | Set-Content $ReleasePath -Encoding UTF8
    Write-Host "[OK] Updated: $ReleasePath" -ForegroundColor Green
}

Write-Host ""
Write-Host "New Configuration:" -ForegroundColor Green
Write-Host "  Primary Backend: $Backend"
Write-Host "  V3 (AmeisenNavigation): ${V3Host}:${V3Port}"
Write-Host "  V1 (PathingAPI): ${V1Host}:${V1Port}"
Write-Host "  Path Visualizer: $($EnableVisualizer.IsPresent)"
Write-Host ""

# Provide setup instructions based on backend
switch ($Backend) {
    "RemoteV3" {
        Write-Host "=== RemoteV3 (AmeisenNavigation) Setup ===" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "AmeisenNavigation provides the BEST pathfinding quality!" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Requirements:"
        Write-Host "  1. AmeisenNavigationServer.exe (already downloaded to Navigation folder)"
        Write-Host "  2. MMAP files (TrinityCore format) in Navigation\mmaps folder"
        Write-Host ""
        Write-Host "MMAP Sources:"
        Write-Host "  - Extract using TrinityCore tools (recommended)"
        Write-Host "  - Download pre-extracted MMAPs from wowhead/trinity forums"
        Write-Host ""
        Write-Host "To start the navigation server:"
        Write-Host "  C:\WowClassicGrindBot\Navigation\StartNavigationServer.bat"
        Write-Host ""
        Write-Host "Fallback Chain: RemoteV3 -> RemoteV1 -> Local"
    }
    
    "RemoteV1" {
        Write-Host "=== RemoteV1 (PathingAPI) Setup ===" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "PathingAPI runs as a separate process using MPQ data."
        Write-Host ""
        Write-Host "Requirements:"
        Write-Host "  1. expansion.MPQ file (~1.8GB) in C:\WowClassicGrindBot\Json\MPQ"
        Write-Host ""
        Write-Host "MPQ Download:"
        Write-Host "  https://mega.nz/folder/GipyXCyR#-cT2SLwsN01fBD63HJKF7w"
        Write-Host ""
        Write-Host "To start PathingAPI:"
        Write-Host "  C:\WowClassicGrindBot\Scripts\StartPathingAPI.bat"
        Write-Host ""
        Write-Host "Fallback Chain: RemoteV1 -> Local"
    }
    
    "Local" {
        Write-Host "=== Local (PPather) Setup ===" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "PPather runs in-process and is the simplest option."
        Write-Host ""
        Write-Host "Requirements:"
        Write-Host "  1. expansion.MPQ file (~1.8GB) in C:\WowClassicGrindBot\Json\MPQ"
        Write-Host ""
        Write-Host "MPQ Download:"
        Write-Host "  https://mega.nz/folder/GipyXCyR#-cT2SLwsN01fBD63HJKF7w"
        Write-Host ""
        Write-Host "No additional server required. Bot will use PPather directly."
    }
}

Write-Host ""
Write-Host "Configuration complete!" -ForegroundColor Green
