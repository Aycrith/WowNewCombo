<#
.SYNOPSIS
    Restores WoW settings from a previous backup.
    
.DESCRIPTION
    Restores WoW configuration files from a backup created by Backup-WoWSettings.ps1.
    
.PARAMETER BackupFolder
    Path to the backup folder to restore from.
    Required parameter.
    
.PARAMETER WowPath
    Path to the World of Warcraft installation folder.
    Default: Uses the path stored in the backup's restore_info.json
    
.EXAMPLE
    .\Restore-WoWSettings.ps1 -BackupFolder "C:\WowClassicGrindBot\Backups\2026-02-02_14-30-00"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$BackupFolder,
    
    [string]$WowPath = ""
)

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  WoW Settings Restore Utility" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Validate backup folder
if (-not (Test-Path $BackupFolder)) {
    Write-Error "Backup folder not found: $BackupFolder"
    exit 1
}

# Load restore info
$restoreInfoPath = Join-Path $BackupFolder "restore_info.json"
if (Test-Path $restoreInfoPath) {
    $restoreInfo = Get-Content $restoreInfoPath | ConvertFrom-Json
    if ([string]::IsNullOrEmpty($WowPath)) {
        $WowPath = $restoreInfo.WowPath
    }
    Write-Host "Backup Date: $($restoreInfo.BackupDate)" -ForegroundColor Gray
} else {
    if ([string]::IsNullOrEmpty($WowPath)) {
        $WowPath = "C:\Program Files (x86)\World of Warcraft\_anniversary_"
    }
}

Write-Host "Backup From: $BackupFolder" -ForegroundColor Gray
Write-Host "Restore To:  $WowPath" -ForegroundColor Gray
Write-Host ""

# Validate WoW path
if (-not (Test-Path $WowPath)) {
    Write-Error "WoW installation not found: $WowPath"
    exit 1
}

$restored = @()

# Restore Config.wtf
$configBackup = Join-Path $BackupFolder "Config.wtf"
if (Test-Path $configBackup) {
    $dest = Join-Path $WowPath "WTF\Config.wtf"
    Copy-Item -Path $configBackup -Destination $dest -Force
    Write-Host "[OK] Config.wtf restored" -ForegroundColor Green
    $restored += "Config.wtf"
}

# Restore WTF/Account
$accountBackup = Join-Path $BackupFolder "WTF_Account"
if (Test-Path $accountBackup) {
    $dest = Join-Path $WowPath "WTF\Account"
    
    # Create backup of current state before overwriting
    if (Test-Path $dest) {
        $tempBackup = "$dest.restore_backup"
        if (Test-Path $tempBackup) { Remove-Item $tempBackup -Recurse -Force }
        Move-Item $dest $tempBackup
    }
    
    Copy-Item -Path $accountBackup -Destination $dest -Recurse
    Write-Host "[OK] WTF\Account restored" -ForegroundColor Green
    $restored += "WTF\Account"
    
    # Clean up temp backup
    if (Test-Path $tempBackup) { Remove-Item $tempBackup -Recurse -Force }
}

# Restore SavedVariables
$savedVarsBackup = Join-Path $BackupFolder "SavedVariables"
if (Test-Path $savedVarsBackup) {
    $dest = Join-Path $WowPath "WTF\SavedVariables"
    
    if (Test-Path $dest) {
        $tempBackup = "$dest.restore_backup"
        if (Test-Path $tempBackup) { Remove-Item $tempBackup -Recurse -Force }
        Move-Item $dest $tempBackup
    }
    
    Copy-Item -Path $savedVarsBackup -Destination $dest -Recurse
    Write-Host "[OK] WTF\SavedVariables restored" -ForegroundColor Green
    $restored += "WTF\SavedVariables"
    
    if (Test-Path $tempBackup) { Remove-Item $tempBackup -Recurse -Force }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Restore Complete!" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Restored $($restored.Count) items from backup." -ForegroundColor Green
Write-Host ""

Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
