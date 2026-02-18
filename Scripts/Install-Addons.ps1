#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs WowClassicGrindBot addons to WoW using symbolic links.
    
.DESCRIPTION
    Creates symbolic links from the WoW AddOns folder to the bot's addon folder.
    This keeps all files in the bot installation directory while making them
    available to WoW. Requires Administrator privileges.
    
.PARAMETER WowPath
    Path to the World of Warcraft installation folder.
    Default: "C:\Program Files (x86)\World of Warcraft\_anniversary_"
    
.PARAMETER BotPath
    Path to the WowClassicGrindBot installation folder.
    Default: "C:\WowClassicGrindBot"
    
.EXAMPLE
    .\Install-Addons.ps1
    
.EXAMPLE
    .\Install-Addons.ps1 -WowPath "D:\Games\World of Warcraft\_classic_"
#>

param(
    [string]$WowPath = "C:\Program Files (x86)\World of Warcraft\_anniversary_",
    [string]$BotPath = "C:\WowClassicGrindBot"
)

$ErrorActionPreference = "Stop"

# Validate paths
$wowAddonsPath = Join-Path $WowPath "Interface\AddOns"
$botAddonsPath = Join-Path $BotPath "Addons"

if (-not (Test-Path $wowAddonsPath)) {
    Write-Error "WoW AddOns folder not found: $wowAddonsPath"
    exit 1
}

if (-not (Test-Path $botAddonsPath)) {
    Write-Error "Bot Addons folder not found: $botAddonsPath"
    exit 1
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  WowClassicGrindBot Addon Installer" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "WoW Path:     $WowPath" -ForegroundColor Gray
Write-Host "Bot Path:     $BotPath" -ForegroundColor Gray
Write-Host "WoW AddOns:   $wowAddonsPath" -ForegroundColor Gray
Write-Host "Bot AddOns:   $botAddonsPath" -ForegroundColor Gray
Write-Host ""

# Get list of addons to install
$addons = Get-ChildItem -Path $botAddonsPath -Directory | Select-Object -ExpandProperty Name

Write-Host "Found $($addons.Count) addon(s) to install:" -ForegroundColor Yellow
$addons | ForEach-Object { Write-Host "  - $_" -ForegroundColor White }
Write-Host ""

$installed = 0
$skipped = 0
$failed = 0

foreach ($addon in $addons) {
    $source = Join-Path $botAddonsPath $addon
    $target = Join-Path $wowAddonsPath $addon
    
    if (Test-Path $target) {
        $item = Get-Item $target
        if ($item.LinkType -eq "SymbolicLink") {
            Write-Host "[SKIP] $addon - Symlink already exists" -ForegroundColor Yellow
        } else {
            Write-Host "[WARN] $addon - Folder exists (not a symlink), skipping..." -ForegroundColor Red
        }
        $skipped++
        continue
    }
    
    try {
        New-Item -ItemType SymbolicLink -Path $target -Target $source | Out-Null
        Write-Host "[OK]   $addon - Symlink created" -ForegroundColor Green
        $installed++
    }
    catch {
        Write-Host "[FAIL] $addon - $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Installation Complete" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Installed: $installed" -ForegroundColor Green
Write-Host "  Skipped:   $skipped" -ForegroundColor Yellow
Write-Host "  Failed:    $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Gray" })
Write-Host ""

if ($failed -gt 0) {
    Write-Host "Some addons failed to install. Make sure you're running as Administrator." -ForegroundColor Red
    exit 1
}

Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
