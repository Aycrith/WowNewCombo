<#
.SYNOPSIS
    WowClassicGrindBot Troubleshooter
    
.DESCRIPTION
    Diagnoses common issues and provides solutions for bot problems.
#>

#Requires -Version 5.1

param(
    [string]$BotPath = "C:\WowClassicGrindBot",
    [switch]$AutoFix
)

$script:Issues = @()
$script:Warnings = @()

function Write-Header {
    try { Clear-Host } catch { }
    Write-Host ""
    Write-Host "  ╔═══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  ║              WowClassicGrindBot - Troubleshooter                      ║" -ForegroundColor Cyan
    Write-Host "  ╚═══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

function Add-Issue {
    param(
        [string]$Category,
        [string]$Problem,
        [string]$Solution,
        [scriptblock]$Fix = $null
    )
    
    $script:Issues += @{
        Category = $Category
        Problem = $Problem
        Solution = $Solution
        Fix = $Fix
    }
}

function Add-Warning {
    param(
        [string]$Category,
        [string]$Message
    )
    
    $script:Warnings += @{
        Category = $Category
        Message = $Message
    }
}

function Test-DotNetRuntime {
    Write-Host "  Checking .NET Runtime..." -ForegroundColor Gray
    
    try {
        $version = dotnet --version 2>$null
        if (-not $version) {
            Add-Issue -Category ".NET" -Problem ".NET SDK/Runtime not found" `
                -Solution "Install .NET 10.0 SDK from https://dotnet.microsoft.com/download/dotnet/10.0"
            return $false
        }
        
        if ([version]$version -lt [version]"10.0") {
            Add-Issue -Category ".NET" -Problem ".NET version $version is too old (need 10.0+)" `
                -Solution "Update to .NET 10.0 SDK from https://dotnet.microsoft.com/download/dotnet/10.0"
            return $false
        }
        
        Write-Host "     ✅ .NET $version installed" -ForegroundColor Green
        return $true
    } catch {
        Add-Issue -Category ".NET" -Problem "Error checking .NET: $_" `
            -Solution "Ensure .NET SDK is properly installed and in PATH"
        return $false
    }
}

function Test-BotInstallation {
    Write-Host "  Checking Bot Installation..." -ForegroundColor Gray
    
    if (-not (Test-Path $BotPath)) {
        Add-Issue -Category "Installation" -Problem "Bot folder not found at $BotPath" `
            -Solution "Clone the repository to $BotPath or update the BotPath parameter"
        return $false
    }
    
    $blazorPath = Join-Path $BotPath "BlazorServer\bin\Release\net10.0\BlazorServer.exe"
    if (-not (Test-Path $blazorPath)) {
        Add-Issue -Category "Installation" -Problem "BlazorServer.exe not built" `
            -Solution "Run 'dotnet build -c Release' in the bot folder" `
            -Fix {
                Push-Location $BotPath
                dotnet build -c Release
                Pop-Location
            }
        return $false
    }
    
    Write-Host "     ✅ Bot installation verified" -ForegroundColor Green
    return $true
}

function Test-WoWInstallation {
    Write-Host "  Checking WoW Installation..." -ForegroundColor Gray
    
    $possiblePaths = @(
        "C:\Program Files (x86)\World of Warcraft\_anniversary_",
        "C:\Program Files (x86)\World of Warcraft\_classic_",
        "C:\Program Files\World of Warcraft\_anniversary_"
    )
    
    $wowPath = $null
    foreach ($path in $possiblePaths) {
        if (Test-Path (Join-Path $path "WowClassic.exe")) {
            $wowPath = $path
            break
        }
    }
    
    if (-not $wowPath) {
        Add-Warning -Category "WoW" -Message "WoW Classic installation not found in standard locations"
        return $false
    }
    
    Write-Host "     ✅ WoW found at: $wowPath" -ForegroundColor Green
    $script:WoWPath = $wowPath
    return $true
}

function Test-Addons {
    Write-Host "  Checking Required Addons..." -ForegroundColor Gray
    
    if (-not $script:WoWPath) {
        Add-Warning -Category "Addons" -Message "Cannot check addons - WoW path unknown"
        return $false
    }
    
    $addonsPath = Join-Path $script:WoWPath "Interface\AddOns"
    $requiredAddons = @("DataToColor")
    
    $allInstalled = $true
    foreach ($addon in $requiredAddons) {
        $addonPath = Join-Path $addonsPath $addon
        if (-not (Test-Path $addonPath)) {
            $sourceAddon = Join-Path $BotPath "Addons\$addon"
            Add-Issue -Category "Addons" -Problem "$addon addon not installed" `
                -Solution "Copy $addon from $sourceAddon to $addonPath" `
                -Fix {
                    if (Test-Path $sourceAddon) {
                        Copy-Item -Path $sourceAddon -Destination $addonPath -Recurse -Force
                    }
                }
            $allInstalled = $false
        } else {
            Write-Host "     ✅ $addon addon installed" -ForegroundColor Green
        }
    }
    
    return $allInstalled
}

function Test-NavigationServer {
    Write-Host "  Checking Navigation System..." -ForegroundColor Gray
    
    $navPath = Join-Path $BotPath "Navigation\AmeisenNavigationServer.exe"
    $mmapsPath = Join-Path $BotPath "Navigation\mmaps"
    
    if (-not (Test-Path $navPath)) {
        Add-Warning -Category "Navigation" -Message "AmeisenNavigationServer not found - will use local pathfinding"
    } else {
        $mmapFiles = Get-ChildItem -Path $mmapsPath -Filter "*.map" -ErrorAction SilentlyContinue
        if ($mmapFiles.Count -eq 0) {
            Add-Warning -Category "Navigation" -Message "No MMAP files found - Navigation Server won't work properly"
        } else {
            Write-Host "     ✅ Navigation Server ready ($($mmapFiles.Count) map files)" -ForegroundColor Green
        }
    }
    
    $mpqPath = Join-Path $BotPath "Json\MPQ\expansion.MPQ"
    if (-not (Test-Path $mpqPath)) {
        Add-Warning -Category "Navigation" -Message "expansion.MPQ not found - local pathfinder may have issues"
    } else {
        Write-Host "     ✅ MPQ file present for local pathfinding" -ForegroundColor Green
    }
}

function Test-ProcessState {
    Write-Host "  Checking Running Processes..." -ForegroundColor Gray
    
    $wowProcess = Get-Process -Name "WowClassic" -ErrorAction SilentlyContinue
    if ($wowProcess) {
        Write-Host "     ✅ WoW Classic running (PID: $($wowProcess.Id))" -ForegroundColor Green
    } else {
        Add-Warning -Category "Process" -Message "WoW Classic is not running"
    }
    
    $navProcess = Get-Process -Name "AmeisenNavigationServer" -ErrorAction SilentlyContinue
    if ($navProcess) {
        Write-Host "     ✅ Navigation Server running (PID: $($navProcess.Id))" -ForegroundColor Green
    }
    
    $botProcess = Get-Process -Name "BlazorServer" -ErrorAction SilentlyContinue
    if ($botProcess) {
        Write-Host "     ✅ Bot Server running (PID: $($botProcess.Id))" -ForegroundColor Green
    }
}

function Test-NetworkPorts {
    Write-Host "  Checking Network Ports..." -ForegroundColor Gray
    
    $portsToCheck = @(
        @{ Port = 5000; Name = "Bot Web UI" },
        @{ Port = 47111; Name = "Navigation Server" }
    )
    
    foreach ($portInfo in $portsToCheck) {
        $connection = Get-NetTCPConnection -LocalPort $portInfo.Port -ErrorAction SilentlyContinue
        if ($connection) {
            $process = Get-Process -Id $connection.OwningProcess -ErrorAction SilentlyContinue
            $processName = if ($process) { $process.ProcessName } else { "Unknown" }
            Write-Host "     ✅ Port $($portInfo.Port) ($($portInfo.Name)) - In use by $processName" -ForegroundColor Green
        } else {
            Write-Host "     ℹ️  Port $($portInfo.Port) ($($portInfo.Name)) - Available" -ForegroundColor Cyan
        }
    }
}

function Test-Configuration {
    Write-Host "  Checking Configuration Files..." -ForegroundColor Gray
    
    $appSettingsPath = Join-Path $BotPath "BlazorServer\bin\Release\net10.0\appsettings.json"
    if (Test-Path $appSettingsPath) {
        try {
            $config = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
            Write-Host "     ✅ appsettings.json valid" -ForegroundColor Green
            Write-Host "        Pathing Mode: $($config.Pathing.Mode)" -ForegroundColor Gray
            Write-Host "        Reader Type: $($config.Reader.Type)" -ForegroundColor Gray
        } catch {
            Add-Issue -Category "Configuration" -Problem "appsettings.json is invalid JSON" `
                -Solution "Check the JSON syntax in appsettings.json"
        }
    } else {
        Add-Warning -Category "Configuration" -Message "appsettings.json not found in build output"
    }
    
    $navConfigPath = Join-Path $BotPath "Navigation\config.json"
    if (Test-Path $navConfigPath) {
        try {
            $navConfig = Get-Content $navConfigPath -Raw | ConvertFrom-Json
            Write-Host "     ✅ Navigation config.json valid" -ForegroundColor Green
        } catch {
            Add-Issue -Category "Configuration" -Problem "Navigation config.json is invalid" `
                -Solution "Check JSON syntax in Navigation\config.json"
        }
    }
}

function Show-Results {
    Write-Host ""
    Write-Host "  ═══════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    
    if ($script:Issues.Count -eq 0 -and $script:Warnings.Count -eq 0) {
        Write-Host "  ✅ All checks passed! Your bot should be ready to run." -ForegroundColor Green
        Write-Host ""
        return
    }
    
    if ($script:Issues.Count -gt 0) {
        Write-Host "  ❌ ISSUES FOUND ($($script:Issues.Count)):" -ForegroundColor Red
        Write-Host ""
        
        $i = 1
        foreach ($issue in $script:Issues) {
            Write-Host "     $i. [$($issue.Category)] $($issue.Problem)" -ForegroundColor Red
            Write-Host "        Solution: $($issue.Solution)" -ForegroundColor Yellow
            Write-Host ""
            $i++
        }
    }
    
    if ($script:Warnings.Count -gt 0) {
        Write-Host "  ⚠️  WARNINGS ($($script:Warnings.Count)):" -ForegroundColor Yellow
        Write-Host ""
        
        foreach ($warning in $script:Warnings) {
            Write-Host "     • [$($warning.Category)] $($warning.Message)" -ForegroundColor Yellow
        }
        Write-Host ""
    }
    
    # Offer auto-fix
    $fixableIssues = $script:Issues | Where-Object { $_.Fix -ne $null }
    if ($fixableIssues.Count -gt 0) {
        Write-Host ""
        Write-Host "  $($fixableIssues.Count) issue(s) can be auto-fixed." -ForegroundColor Cyan
        
        if ($AutoFix) {
            Write-Host "  Attempting auto-fix..." -ForegroundColor Cyan
            foreach ($issue in $fixableIssues) {
                Write-Host "     Fixing: $($issue.Problem)..." -NoNewline -ForegroundColor Gray
                try {
                    & $issue.Fix
                    Write-Host " Done" -ForegroundColor Green
                } catch {
                    Write-Host " Failed: $_" -ForegroundColor Red
                }
            }
        } else {
            Write-Host "  Run with -AutoFix to attempt automatic repairs." -ForegroundColor Gray
        }
    }
}

# Main execution
Write-Header

Write-Host "  Running diagnostics..." -ForegroundColor White
Write-Host ""

Test-DotNetRuntime
Test-BotInstallation
Test-WoWInstallation
Test-Addons
Test-NavigationServer
Test-ProcessState
Test-NetworkPorts
Test-Configuration

Show-Results

Write-Host ""
Write-Host "  Press any key to exit..." -ForegroundColor DarkGray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
