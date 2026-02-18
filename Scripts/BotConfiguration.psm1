<#
.SYNOPSIS
    WowClassicGrindBot Configuration Manager
    
.DESCRIPTION
    Handles reading, validating, and updating bot configuration files.
    Provides a unified interface for all bot settings.
#>

#Requires -Version 5.1

class BotConfiguration {
    [string]$BotPath
    [string]$WoWPath
    [int]$WebPort
    [int]$NavPort
    [string]$PathingMode
    [string]$ReaderType
    [bool]$OverlayEnabled
    [bool]$DiagnosticsEnabled
    
    static [BotConfiguration] Load([string]$botPath) {
        $config = [BotConfiguration]::new()
        $config.BotPath = $botPath
        
        # Load appsettings.json
        $appSettingsPath = Join-Path $botPath "BlazorServer\appsettings.json"
        if (Test-Path $appSettingsPath) {
            $appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
            
            $config.WebPort = 5000  # Default
            $config.PathingMode = $appSettings.Pathing.Mode
            $config.NavPort = $appSettings.Pathing.portv3
            $config.ReaderType = $appSettings.Reader.Type
            $config.OverlayEnabled = $appSettings.Overlay.Enabled
            $config.DiagnosticsEnabled = $appSettings.Diagnostics.Enabled
        }
        
        # Try to detect WoW path
        $possibleWoWPaths = @(
            "C:\Program Files (x86)\World of Warcraft\_anniversary_",
            "C:\Program Files (x86)\World of Warcraft\_classic_",
            "C:\Program Files\World of Warcraft\_anniversary_",
            "C:\Program Files\World of Warcraft\_classic_"
        )
        
        foreach ($path in $possibleWoWPaths) {
            if (Test-Path (Join-Path $path "WowClassic.exe")) {
                $config.WoWPath = $path
                break
            }
        }
        
        return $config
    }
    
    [void] Save() {
        $appSettingsPath = Join-Path $this.BotPath "BlazorServer\bin\Release\net10.0\appsettings.json"
        if (Test-Path $appSettingsPath) {
            $appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
            
            $appSettings.Pathing.Mode = $this.PathingMode
            $appSettings.Pathing.portv3 = $this.NavPort
            $appSettings.Reader.Type = $this.ReaderType
            $appSettings.Overlay.Enabled = $this.OverlayEnabled
            $appSettings.Diagnostics.Enabled = $this.DiagnosticsEnabled
            
            $appSettings | ConvertTo-Json -Depth 10 | Set-Content $appSettingsPath
        }
    }
    
    [hashtable] Validate() {
        $issues = @{
            Errors = @()
            Warnings = @()
        }
        
        # Check bot path
        if (-not (Test-Path $this.BotPath)) {
            $issues.Errors += "Bot path does not exist: $($this.BotPath)"
        }
        
        # Check BlazorServer
        $blazorPath = Join-Path $this.BotPath "BlazorServer\bin\Release\net10.0\BlazorServer.exe"
        if (-not (Test-Path $blazorPath)) {
            $issues.Errors += "BlazorServer.exe not found. Run 'dotnet build -c Release'"
        }
        
        # Check WoW path
        if (-not $this.WoWPath -or -not (Test-Path $this.WoWPath)) {
            $issues.Warnings += "WoW path not found or not set"
        }
        
        # Check addons
        if ($this.WoWPath) {
            $dataToColorPath = Join-Path $this.WoWPath "Interface\AddOns\DataToColor"
            if (-not (Test-Path $dataToColorPath)) {
                $issues.Errors += "DataToColor addon not installed"
            }
        }
        
        # Check navigation
        if ($this.PathingMode -eq "RemoteV3") {
            $navPath = Join-Path $this.BotPath "Navigation\AmeisenNavigationServer.exe"
            if (-not (Test-Path $navPath)) {
                $issues.Warnings += "AmeisenNavigation not found - pathfinding may be limited"
            }
            
            $mmapsPath = Join-Path $this.BotPath "Navigation\mmaps"
            $mmapFiles = Get-ChildItem -Path $mmapsPath -Filter "*.map" -ErrorAction SilentlyContinue
            if ($mmapFiles.Count -eq 0) {
                $issues.Warnings += "No MMAP files found - Navigation server won't work"
            }
        }
        
        return $issues
    }
}

function Get-BotConfiguration {
    param(
        [string]$BotPath = "C:\WowClassicGrindBot"
    )
    
    return [BotConfiguration]::Load($BotPath)
}

function Test-BotConfiguration {
    param(
        [string]$BotPath = "C:\WowClassicGrindBot"
    )
    
    $config = Get-BotConfiguration -BotPath $BotPath
    return $config.Validate()
}

function Set-BotWoWPath {
    param(
        [Parameter(Mandatory)]
        [string]$WoWPath,
        [string]$BotPath = "C:\WowClassicGrindBot"
    )
    
    $config = Get-BotConfiguration -BotPath $BotPath
    $config.WoWPath = $WoWPath
    $config.Save()
    
    return $config
}

function Set-BotPathingMode {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("RemoteV3", "Local")]
        [string]$Mode,
        [string]$BotPath = "C:\WowClassicGrindBot"
    )
    
    $config = Get-BotConfiguration -BotPath $BotPath
    $config.PathingMode = $Mode
    $config.Save()
    
    return $config
}

Export-ModuleMember -Function Get-BotConfiguration, Test-BotConfiguration, Set-BotWoWPath, Set-BotPathingMode
