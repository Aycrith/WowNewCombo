#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Uninstalls WowClassicGrindBot addons from WoW by removing symbolic links.
    
.DESCRIPTION
    Removes symbolic links for bot addons from the WoW AddOns folder.
    Only removes symlinks that point to the bot installation - will not
    delete any actual addon folders. Requires Administrator privileges.
    
.PARAMETER WowPath
    Path to the World of Warcraft installation folder.
    Default: "C:\Program Files (x86)\World of Warcraft\_anniversary_"
    
.PARAMETER BotPath
    Path to the WowClassicGrindBot installation folder.
    Default: "C:\WowClassicGrindBot"
    
.PARAMETER Full
    If specified, also removes the entire bot installation folder.
    Use with caution!
    
.EXAMPLE
    .\Uninstall-Bot.ps1
    
.EXAMPLE
    .\Uninstall-Bot.ps1 -Full
#>

param(
    [string]$WowPath = "C:\Program Files (x86)\World of Warcraft\_anniversary_",
    [string]$BotPath = "C:\WowClassicGrindBot",
    [switch]$Full
)

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  WowClassicGrindBot Uninstaller" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "WoW Path: $WowPath" -ForegroundColor Gray
Write-Host "Bot Path: $BotPath" -ForegroundColor Gray
Write-Host ""

$wowAddonsPath = Join-Path $WowPath "Interface\AddOns"

if (-not (Test-Path $wowAddonsPath)) {
    Write-Error "WoW AddOns folder not found: $wowAddonsPath"
    exit 1
}

# Find and remove symlinks pointing to bot addons
$botAddonsPath = Join-Path $BotPath "Addons"
$symlinksRemoved = 0

Write-Host "Scanning for bot addon symlinks..." -ForegroundColor Yellow
Write-Host ""

Get-ChildItem -Path $wowAddonsPath -Directory | ForEach-Object {
    $item = $_
    if ($item.LinkType -eq "SymbolicLink") {
        $target = $item.Target
        if ($target -like "$BotPath*" -or $target -like "$botAddonsPath*") {
            Write-Host "[REMOVE] $($item.Name) -> $target" -ForegroundColor Red
            Remove-Item -Path $item.FullName -Force
            $symlinksRemoved++
        }
    }
}

Write-Host ""
Write-Host "Removed $symlinksRemoved addon symlink(s)" -ForegroundColor $(if ($symlinksRemoved -gt 0) { "Green" } else { "Gray" })
Write-Host ""

if ($Full) {
    Write-Host "============================================" -ForegroundColor Red
    Write-Host "  FULL UNINSTALL REQUESTED" -ForegroundColor Red
    Write-Host "============================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "This will DELETE the entire bot installation at:" -ForegroundColor Yellow
    Write-Host "  $BotPath" -ForegroundColor White
    Write-Host ""
    Write-Host "This includes:" -ForegroundColor Yellow
    Write-Host "  - All class profiles and paths" -ForegroundColor White
    Write-Host "  - All configuration files" -ForegroundColor White
    Write-Host "  - All backups stored in the bot folder" -ForegroundColor White
    Write-Host "  - MPQ pathfinding data" -ForegroundColor White
    Write-Host ""
    
    $confirm = Read-Host "Type 'DELETE' to confirm full removal"
    
    if ($confirm -eq "DELETE") {
        Write-Host ""
        Write-Host "Removing $BotPath..." -ForegroundColor Red
        
        if (Test-Path $BotPath) {
            Remove-Item -Path $BotPath -Recurse -Force
            Write-Host "[OK] Bot installation removed" -ForegroundColor Green
        } else {
            Write-Host "[SKIP] Bot folder not found" -ForegroundColor Yellow
        }
    } else {
        Write-Host ""
        Write-Host "Full removal cancelled." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Uninstall Complete" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

if (-not $Full) {
    Write-Host "Note: Bot files remain at $BotPath" -ForegroundColor Gray
    Write-Host "      Run with -Full to completely remove." -ForegroundColor Gray
    Write-Host ""
}

Write-Host "Your WoW installation has been cleaned up." -ForegroundColor Green
Write-Host "No bot files remain in your WoW folder." -ForegroundColor Green
Write-Host ""

Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
