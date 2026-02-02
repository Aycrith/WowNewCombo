<#
.SYNOPSIS
    WowClassicGrindBot First-Time Setup Wizard
    
.DESCRIPTION
    Interactive wizard that guides new users through the complete setup process.
    Handles installation, configuration, and validation of all components.
#>

#Requires -Version 5.1

param(
    [string]$BotPath = "C:\WowClassicGrindBot",
    [switch]$SkipIntro
)

# ============================================================================
# CONFIGURATION
# ============================================================================
$script:SetupState = @{
    BotPath = $BotPath
    WoWPath = $null
    AddonsInstalled = $false
    NavigationReady = $false
    BotBuilt = $false
    ConfigurationComplete = $false
}

# ============================================================================
# UI FUNCTIONS
# ============================================================================
function Write-WizardBanner {
    try { Clear-Host } catch { }
    Write-Host ""
    Write-Host "  ╔═══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  ║                                                                       ║" -ForegroundColor Cyan
    Write-Host "  ║           WowClassicGrindBot - First Time Setup Wizard                ║" -ForegroundColor Cyan
    Write-Host "  ║                                                                       ║" -ForegroundColor Cyan
    Write-Host "  ╚═══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

function Write-WizardStep {
    param(
        [int]$StepNumber,
        [int]$TotalSteps,
        [string]$Title
    )
    
    Write-Host ""
    Write-Host "  ┌─────────────────────────────────────────────────────────────────────────┐" -ForegroundColor DarkCyan
    Write-Host "  │  Step $StepNumber of $TotalSteps : $($Title.PadRight(56))│" -ForegroundColor DarkCyan
    Write-Host "  └─────────────────────────────────────────────────────────────────────────┘" -ForegroundColor DarkCyan
    Write-Host ""
}

function Write-Info {
    param([string]$Message)
    Write-Host "     ℹ️  $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "     ✅ $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "     ⚠️  $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "     ❌ $Message" -ForegroundColor Red
}

function Read-UserInput {
    param(
        [string]$Prompt,
        [string]$Default = "",
        [switch]$Required
    )
    
    $displayPrompt = "     $Prompt"
    if ($Default) {
        $displayPrompt += " [$Default]"
    }
    $displayPrompt += ": "
    
    Write-Host $displayPrompt -NoNewline -ForegroundColor Yellow
    $input = Read-Host
    
    if ([string]::IsNullOrWhiteSpace($input)) {
        if ($Required -and -not $Default) {
            Write-Error "This field is required"
            return Read-UserInput -Prompt $Prompt -Default $Default -Required:$Required
        }
        return $Default
    }
    
    return $input
}

function Read-YesNo {
    param(
        [string]$Prompt,
        [bool]$Default = $true
    )
    
    $defaultText = if ($Default) { "Y/n" } else { "y/N" }
    Write-Host "     $Prompt [$defaultText]: " -NoNewline -ForegroundColor Yellow
    $input = Read-Host
    
    if ([string]::IsNullOrWhiteSpace($input)) {
        return $Default
    }
    
    return $input -match "^[Yy]"
}

function Show-ProgressBar {
    param(
        [string]$Activity,
        [int]$PercentComplete
    )
    
    $width = 50
    $completed = [math]::Floor($width * $PercentComplete / 100)
    $remaining = $width - $completed
    
    $bar = "█" * $completed + "░" * $remaining
    Write-Host "`r     [$bar] $PercentComplete%" -NoNewline -ForegroundColor Cyan
}

function Wait-WithProgress {
    param(
        [string]$Message,
        [int]$Seconds
    )
    
    for ($i = 0; $i -le $Seconds; $i++) {
        $percent = [math]::Floor($i / $Seconds * 100)
        Write-Host "`r     $Message... [$("█" * ($percent / 2))$("░" * (50 - $percent / 2))] $percent%" -NoNewline -ForegroundColor Cyan
        Start-Sleep -Seconds 1
    }
    Write-Host "`r     $Message... Done!                                                        " -ForegroundColor Green
}

# ============================================================================
# SETUP STEPS
# ============================================================================
function Step-Welcome {
    Write-WizardBanner
    
    if (-not $SkipIntro) {
        Write-Host "  Welcome to the WowClassicGrindBot setup wizard!" -ForegroundColor White
        Write-Host ""
        Write-Host "  This wizard will help you:" -ForegroundColor Gray
        Write-Host "    • Locate your World of Warcraft installation" -ForegroundColor Gray
        Write-Host "    • Install required addons" -ForegroundColor Gray
        Write-Host "    • Configure the navigation system" -ForegroundColor Gray
        Write-Host "    • Build and verify the bot" -ForegroundColor Gray
        Write-Host ""
        Write-Host "  Prerequisites:" -ForegroundColor Gray
        Write-Host "    • .NET 10.0 SDK installed" -ForegroundColor Gray
        Write-Host "    • World of Warcraft Classic installed" -ForegroundColor Gray
        Write-Host "    • Administrator access (for addon installation)" -ForegroundColor Gray
        Write-Host ""
        
        if (-not (Read-YesNo -Prompt "Ready to begin?")) {
            Write-Host ""
            Write-Info "Setup cancelled. Run this wizard again when ready."
            return $false
        }
    }
    
    return $true
}

function Step-DetectWoW {
    Write-WizardStep -StepNumber 1 -TotalSteps 5 -Title "Detect WoW Installation"
    
    Write-Info "Searching for World of Warcraft installation..."
    Write-Host ""
    
    $possiblePaths = @(
        "C:\Program Files (x86)\World of Warcraft\_anniversary_",
        "C:\Program Files (x86)\World of Warcraft\_classic_",
        "C:\Program Files\World of Warcraft\_anniversary_",
        "C:\Program Files\World of Warcraft\_classic_",
        "D:\World of Warcraft\_anniversary_",
        "D:\World of Warcraft\_classic_",
        "D:\Games\World of Warcraft\_anniversary_",
        "D:\Games\World of Warcraft\_classic_"
    )
    
    $detectedPath = $null
    foreach ($path in $possiblePaths) {
        $wowExe = Join-Path $path "WowClassic.exe"
        if (Test-Path $wowExe) {
            $detectedPath = $path
            break
        }
    }
    
    if ($detectedPath) {
        Write-Success "Found WoW installation: $detectedPath"
        Write-Host ""
        
        if (Read-YesNo -Prompt "Use this path?") {
            $script:SetupState.WoWPath = $detectedPath
            return $true
        }
    } else {
        Write-Warning "Could not auto-detect WoW installation"
    }
    
    Write-Host ""
    $customPath = Read-UserInput -Prompt "Enter WoW installation path" -Required
    
    $wowExe = Join-Path $customPath "WowClassic.exe"
    if (Test-Path $wowExe) {
        Write-Success "Verified WoW installation at: $customPath"
        $script:SetupState.WoWPath = $customPath
        return $true
    } else {
        Write-Error "WowClassic.exe not found at specified path"
        Write-Host ""
        
        if (Read-YesNo -Prompt "Try again?") {
            return Step-DetectWoW
        }
        return $false
    }
}

function Step-InstallAddons {
    Write-WizardStep -StepNumber 2 -TotalSteps 5 -Title "Install Required Addons"
    
    $addonsSource = Join-Path $SetupState.BotPath "Addons"
    $addonsDest = Join-Path $SetupState.WoWPath "Interface\AddOns"
    
    Write-Info "Checking addon installation..."
    Write-Host ""
    
    $requiredAddons = @("DataToColor")
    $optionalAddons = @("BindPad", "cTimerBackport", "SoundKitBackport")
    $allAddons = $requiredAddons + $optionalAddons
    
    $missingRequired = @()
    $missingOptional = @()
    
    foreach ($addon in $requiredAddons) {
        $destPath = Join-Path $addonsDest $addon
        if (-not (Test-Path $destPath)) {
            $missingRequired += $addon
        } else {
            Write-Success "$addon is installed"
        }
    }
    
    foreach ($addon in $optionalAddons) {
        $destPath = Join-Path $addonsDest $addon
        if (-not (Test-Path $destPath)) {
            $missingOptional += $addon
        } else {
            Write-Success "$addon is installed"
        }
    }
    
    Write-Host ""
    
    if ($missingRequired.Count -eq 0 -and $missingOptional.Count -eq 0) {
        Write-Success "All addons are already installed!"
        $script:SetupState.AddonsInstalled = $true
        return $true
    }
    
    if ($missingRequired.Count -gt 0) {
        Write-Warning "Missing required addons: $($missingRequired -join ', ')"
    }
    if ($missingOptional.Count -gt 0) {
        Write-Info "Missing optional addons: $($missingOptional -join ', ')"
    }
    
    Write-Host ""
    
    # Check admin rights
    $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    
    if (-not $isAdmin) {
        Write-Warning "Administrator rights required to install addons"
        Write-Host ""
        Write-Host "     Please either:" -ForegroundColor Yellow
        Write-Host "       1. Run this wizard as Administrator" -ForegroundColor Gray
        Write-Host "       2. Manually copy addons from:" -ForegroundColor Gray
        Write-Host "          $addonsSource" -ForegroundColor Cyan
        Write-Host "          to:" -ForegroundColor Gray
        Write-Host "          $addonsDest" -ForegroundColor Cyan
        Write-Host ""
        
        Prompt-Continue
        
        # Recheck after manual install
        $stillMissing = $false
        foreach ($addon in $requiredAddons) {
            $destPath = Join-Path $addonsDest $addon
            if (-not (Test-Path $destPath)) {
                $stillMissing = $true
                break
            }
        }
        
        if ($stillMissing) {
            Write-Error "Required addons still not installed"
            return $false
        }
        
        $script:SetupState.AddonsInstalled = $true
        return $true
    }
    
    # Install addons
    if (Read-YesNo -Prompt "Install addons now?") {
        Write-Host ""
        
        $toInstall = $missingRequired
        if ($missingOptional.Count -gt 0) {
            if (Read-YesNo -Prompt "Also install optional addons?") {
                $toInstall += $missingOptional
            }
        }
        
        foreach ($addon in $toInstall) {
            $sourcePath = Join-Path $addonsSource $addon
            $destPath = Join-Path $addonsDest $addon
            
            if (Test-Path $sourcePath) {
                Write-Info "Installing $addon..."
                try {
                    Copy-Item -Path $sourcePath -Destination $destPath -Recurse -Force
                    Write-Success "$addon installed"
                } catch {
                    Write-Error "Failed to install $addon: $_"
                }
            } else {
                Write-Warning "Source not found for $addon"
            }
        }
    }
    
    # Final verification
    $allRequiredInstalled = $true
    foreach ($addon in $requiredAddons) {
        $destPath = Join-Path $addonsDest $addon
        if (-not (Test-Path $destPath)) {
            $allRequiredInstalled = $false
            break
        }
    }
    
    $script:SetupState.AddonsInstalled = $allRequiredInstalled
    return $allRequiredInstalled
}

function Step-ConfigureNavigation {
    Write-WizardStep -StepNumber 3 -TotalSteps 5 -Title "Configure Navigation System"
    
    $navServerPath = Join-Path $SetupState.BotPath "Navigation\AmeisenNavigationServer.exe"
    $mmapsPath = Join-Path $SetupState.BotPath "Navigation\mmaps"
    $mpqPath = Join-Path $SetupState.BotPath "Json\MPQ\expansion.MPQ"
    
    Write-Info "Checking navigation components..."
    Write-Host ""
    
    # Check Navigation Server
    if (Test-Path $navServerPath) {
        Write-Success "AmeisenNavigation Server found"
    } else {
        Write-Warning "AmeisenNavigation Server not found"
        Write-Host "     Download from: https://github.com/Jnnshschl/AmeisenNavigation" -ForegroundColor Gray
    }
    
    # Check MMAP Files
    $mmapFiles = Get-ChildItem -Path $mmapsPath -Filter "*.map" -ErrorAction SilentlyContinue
    if ($mmapFiles.Count -gt 0) {
        Write-Success "MMAP files found: $($mmapFiles.Count) maps"
    } else {
        Write-Warning "No MMAP files found"
        Write-Host ""
        Write-Host "     MMAP files are required for server-side pathfinding." -ForegroundColor Gray
        Write-Host "     Without MMAPs, the bot will use local PPather (slower but works)." -ForegroundColor Gray
        Write-Host ""
        Write-Host "     To download MMAPs:" -ForegroundColor Yellow
        Write-Host "     1. Visit: https://mega.nz/folder/GipyXCyR#-cT2SLwsN01fBD63HJKF7w" -ForegroundColor Cyan
        Write-Host "     2. Download the mmaps folder" -ForegroundColor Gray
        Write-Host "     3. Extract to: $mmapsPath" -ForegroundColor Cyan
    }
    
    # Check MPQ File
    Write-Host ""
    if (Test-Path $mpqPath) {
        Write-Success "expansion.MPQ found (local pathfinding available)"
    } else {
        Write-Warning "expansion.MPQ not found"
        Write-Host "     This file enables local PPather pathfinding as fallback." -ForegroundColor Gray
    }
    
    Write-Host ""
    
    # Determine pathing mode
    $pathingMode = "RemoteV3"
    if ($mmapFiles.Count -eq 0) {
        if (Test-Path $mpqPath) {
            Write-Info "Recommending Local pathfinding mode (MMAPs not available)"
            $pathingMode = "Local"
        } else {
            Write-Warning "Neither MMAPs nor MPQ available - pathfinding will be limited"
        }
    }
    
    Write-Host ""
    Write-Info "Pathing mode will be set to: $pathingMode"
    
    $script:SetupState.NavigationReady = $true
    $script:SetupState.PathingMode = $pathingMode
    
    Prompt-Continue
    return $true
}

function Step-BuildBot {
    Write-WizardStep -StepNumber 4 -TotalSteps 5 -Title "Build Bot Application"
    
    $blazorPath = Join-Path $SetupState.BotPath "BlazorServer\bin\Release\net10.0\BlazorServer.exe"
    
    Write-Info "Checking bot build status..."
    Write-Host ""
    
    if (Test-Path $blazorPath) {
        Write-Success "BlazorServer.exe already built"
        $script:SetupState.BotBuilt = $true
        
        if (-not (Read-YesNo -Prompt "Rebuild anyway?")) {
            return $true
        }
    }
    
    # Check .NET SDK
    Write-Info "Checking .NET SDK..."
    $dotnetVersion = try { (dotnet --version) } catch { $null }
    
    if (-not $dotnetVersion) {
        Write-Error ".NET SDK not found!"
        Write-Host ""
        Write-Host "     Please install .NET 10.0 SDK from:" -ForegroundColor Yellow
        Write-Host "     https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Cyan
        Write-Host ""
        return $false
    }
    
    Write-Success ".NET SDK version: $dotnetVersion"
    Write-Host ""
    
    if (Read-YesNo -Prompt "Build the bot now? (This may take a few minutes)") {
        Write-Host ""
        Write-Info "Building bot in Release configuration..."
        Write-Host ""
        
        Push-Location $SetupState.BotPath
        try {
            $buildOutput = & dotnet build -c Release 2>&1
            $buildSuccess = $LASTEXITCODE -eq 0
            
            if ($buildSuccess) {
                Write-Host ""
                Write-Success "Build completed successfully!"
                $script:SetupState.BotBuilt = $true
            } else {
                Write-Host ""
                Write-Error "Build failed!"
                Write-Host ""
                Write-Host "Build output:" -ForegroundColor Yellow
                $buildOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
            }
        } finally {
            Pop-Location
        }
    }
    
    return $script:SetupState.BotBuilt
}

function Step-Summary {
    Write-WizardStep -StepNumber 5 -TotalSteps 5 -Title "Setup Summary"
    
    Write-Host "  ┌─────────────────────────────────────────────────────────────────────────┐" -ForegroundColor Green
    Write-Host "  │                        SETUP COMPLETE!                                  │" -ForegroundColor Green
    Write-Host "  └─────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Green
    Write-Host ""
    
    Write-Host "     Configuration Summary:" -ForegroundColor White
    Write-Host "     ─────────────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    
    $items = @(
        @{ Label = "Bot Installation"; Value = $SetupState.BotPath; Status = "✅" },
        @{ Label = "WoW Path"; Value = $SetupState.WoWPath; Status = if ($SetupState.WoWPath) { "✅" } else { "⚠️" } },
        @{ Label = "Addons Installed"; Value = if ($SetupState.AddonsInstalled) { "Yes" } else { "No" }; Status = if ($SetupState.AddonsInstalled) { "✅" } else { "❌" } },
        @{ Label = "Navigation Ready"; Value = if ($SetupState.NavigationReady) { "Yes" } else { "No" }; Status = if ($SetupState.NavigationReady) { "✅" } else { "⚠️" } },
        @{ Label = "Bot Built"; Value = if ($SetupState.BotBuilt) { "Yes" } else { "No" }; Status = if ($SetupState.BotBuilt) { "✅" } else { "❌" } }
    )
    
    foreach ($item in $items) {
        Write-Host "     $($item.Status) $($item.Label.PadRight(20)) : $($item.Value)" -ForegroundColor White
    }
    
    Write-Host ""
    Write-Host "     ─────────────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "     To start the bot:" -ForegroundColor Yellow
    Write-Host "       • Double-click Launch.bat in the bot folder" -ForegroundColor Gray
    Write-Host "       • Or run: .\Scripts\WowGrindBotLauncher.ps1" -ForegroundColor Gray
    Write-Host ""
    Write-Host "     Important:" -ForegroundColor Yellow
    Write-Host "       • Start WoW and log in to your character first" -ForegroundColor Gray
    Write-Host "       • Make sure DataToColor addon is enabled in-game" -ForegroundColor Gray
    Write-Host "       • The bot web UI will open at http://localhost:5000" -ForegroundColor Gray
    Write-Host ""
    
    $script:SetupState.ConfigurationComplete = $true
    return $true
}

function Prompt-Continue {
    Write-Host ""
    Write-Host "     Press [ENTER] to continue..." -ForegroundColor DarkGray
    Read-Host | Out-Null
}

# ============================================================================
# MAIN WIZARD
# ============================================================================
function Start-SetupWizard {
    try {
        # Step 0: Welcome
        if (-not (Step-Welcome)) { return }
        
        # Step 1: Detect WoW
        if (-not (Step-DetectWoW)) {
            Write-Error "Cannot continue without WoW installation path"
            return
        }
        Prompt-Continue
        
        # Step 2: Install Addons
        if (-not (Step-InstallAddons)) {
            Write-Warning "Continuing without all required addons..."
        }
        Prompt-Continue
        
        # Step 3: Configure Navigation
        Step-ConfigureNavigation
        
        # Step 4: Build Bot
        if (-not (Step-BuildBot)) {
            Write-Warning "Bot not built - you'll need to build manually"
        }
        Prompt-Continue
        
        # Step 5: Summary
        Step-Summary
        
        Write-Host ""
        if (Read-YesNo -Prompt "Launch the bot now?") {
            $launcherPath = Join-Path $SetupState.BotPath "Scripts\WowGrindBotLauncher.ps1"
            if (Test-Path $launcherPath) {
                & $launcherPath
            }
        }
        
    } catch {
        Write-Host ""
        Write-Error "An error occurred: $_"
        Write-Host ""
        Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    }
}

# Run wizard
Start-SetupWizard
