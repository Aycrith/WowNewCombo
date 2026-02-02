<#
.SYNOPSIS
    Backs up WoW configuration files and addon settings before bot usage.
    
.DESCRIPTION
    Creates timestamped backups of:
    - WTF/Config.wtf (WoW settings)
    - WTF/Account/ (addon settings, keybindings)
    - Interface/AddOns/ addon list state
    
    Backups are stored in C:\WowClassicGrindBot\Backups\
    
.PARAMETER WowPath
    Path to the World of Warcraft installation folder.
    Default: "C:\Program Files (x86)\World of Warcraft\_anniversary_"
    
.EXAMPLE
    .\Backup-WoWSettings.ps1
    
.EXAMPLE
    .\Backup-WoWSettings.ps1 -WowPath "D:\Games\World of Warcraft\_classic_"
#>

param(
    [string]$WowPath = "C:\Program Files (x86)\World of Warcraft\_anniversary_",
    [string]$BotPath = "C:\WowClassicGrindBot"
)

$ErrorActionPreference = "Stop"

# Create backup folder with timestamp
$timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$backupRoot = Join-Path $BotPath "Backups"
$backupFolder = Join-Path $backupRoot $timestamp

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  WoW Settings Backup Utility" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "WoW Path:     $WowPath" -ForegroundColor Gray
Write-Host "Backup To:    $backupFolder" -ForegroundColor Gray
Write-Host ""

# Validate WoW path
if (-not (Test-Path $WowPath)) {
    Write-Error "WoW installation not found: $WowPath"
    exit 1
}

# Create backup directory
New-Item -ItemType Directory -Force -Path $backupFolder | Out-Null
Write-Host "Created backup folder: $backupFolder" -ForegroundColor Green
Write-Host ""

$backedUp = @()

# Backup Config.wtf
$configWtf = Join-Path $WowPath "WTF\Config.wtf"
if (Test-Path $configWtf) {
    $dest = Join-Path $backupFolder "Config.wtf"
    Copy-Item -Path $configWtf -Destination $dest
    Write-Host "[OK] Config.wtf backed up" -ForegroundColor Green
    $backedUp += "Config.wtf"
} else {
    Write-Host "[SKIP] Config.wtf not found" -ForegroundColor Yellow
}

# Backup WTF/Account folder (contains addon settings)
$wtfAccount = Join-Path $WowPath "WTF\Account"
if (Test-Path $wtfAccount) {
    $dest = Join-Path $backupFolder "WTF_Account"
    Copy-Item -Path $wtfAccount -Destination $dest -Recurse
    $size = (Get-ChildItem -Path $dest -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Host "[OK] WTF\Account backed up ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
    $backedUp += "WTF\Account"
} else {
    Write-Host "[SKIP] WTF\Account not found" -ForegroundColor Yellow
}

# Backup SavedVariables
$savedVars = Join-Path $WowPath "WTF\SavedVariables"
if (Test-Path $savedVars) {
    $dest = Join-Path $backupFolder "SavedVariables"
    Copy-Item -Path $savedVars -Destination $dest -Recurse
    $size = (Get-ChildItem -Path $dest -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Host "[OK] WTF\SavedVariables backed up ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
    $backedUp += "WTF\SavedVariables"
} else {
    Write-Host "[SKIP] WTF\SavedVariables not found" -ForegroundColor Yellow
}

# Create addon list snapshot
$addonsPath = Join-Path $WowPath "Interface\AddOns"
if (Test-Path $addonsPath) {
    $addons = Get-ChildItem -Path $addonsPath -Directory | ForEach-Object {
        $item = $_
        $isSymlink = $null -ne $item.LinkType
        [PSCustomObject]@{
            Name = $item.Name
            IsSymlink = $isSymlink
            Target = if ($isSymlink) { $item.Target } else { "" }
        }
    }
    $addons | Export-Csv -Path (Join-Path $backupFolder "AddOnsList.csv") -NoTypeInformation
    Write-Host "[OK] AddOns list saved ($($addons.Count) addons)" -ForegroundColor Green
    $backedUp += "AddOnsList.csv"
}

# Create restore info file
$restoreInfo = @{
    BackupDate = $timestamp
    WowPath = $WowPath
    BotPath = $BotPath
    BackedUpItems = $backedUp
}
$restoreInfo | ConvertTo-Json | Set-Content (Join-Path $backupFolder "restore_info.json")

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Backup Complete!" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Backed up $($backedUp.Count) items to:" -ForegroundColor Green
Write-Host "  $backupFolder" -ForegroundColor White
Write-Host ""
Write-Host "To restore, run:" -ForegroundColor Yellow
Write-Host "  .\Restore-WoWSettings.ps1 -BackupFolder `"$backupFolder`"" -ForegroundColor White
Write-Host ""

Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
