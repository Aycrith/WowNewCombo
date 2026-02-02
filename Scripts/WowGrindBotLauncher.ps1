<#
.SYNOPSIS
    WowClassicGrindBot - Autonomous Launch System

.DESCRIPTION
    Comprehensive launcher that starts all necessary systems in the correct order
    with a clean, managed UX experience. Handles:
    - Prerequisites validation
    - AmeisenNavigation Server
    - WoW Client detection/launch
    - Addon verification
    - BlazorServer (Bot UI)
    - Graceful shutdown

.NOTES
    Author: WowClassicGrindBot Launcher
    Version: 2.0.0
    Requires: PowerShell 5.1+, .NET 10.0+
#>

#Requires -Version 5.1

param(
    [switch]$SkipWoWCheck,
    [switch]$SkipNavServer,
    [switch]$AutoLaunchWoW,
    [switch]$Headless,
    [string]$WoWPath = "C:\Program Files (x86)\World of Warcraft\_anniversary_",
    [string]$BotPath = "C:\WowClassicGrindBot",
    [int]$WebPort = 5000
)

# ============================================================================
# CONFIGURATION
# ============================================================================
$script:Config = @{
    BotPath           = $BotPath
    WoWPath           = $WoWPath
    WoWExecutable     = "WowClassic.exe"
    BlazorServerPath  = Join-Path $BotPath "BlazorServer\bin\Release\net10.0\BlazorServer.exe"
    NavServerPath     = Join-Path $BotPath "Navigation\AmeisenNavigationServer.exe"
    NavConfigPath     = Join-Path $BotPath "Navigation\config.json"
    MmapsPath         = Join-Path $BotPath "Navigation\mmaps"
    MPQPath           = Join-Path $BotPath "Json\MPQ"
    AddonsSourcePath  = Join-Path $BotPath "Addons"
    AddonsDestPath    = Join-Path $WoWPath "Interface\AddOns"
    WebPort           = $WebPort
    NavPort           = 47111
    RequiredAddons    = @("DataToColor")
    OptionalAddons    = @("BindPad", "cTimerBackport", "SoundKitBackport")
    StartupDelay      = 3
}

# ============================================================================
# UI HELPERS
# ============================================================================
function Write-Banner {
    try { Clear-Host } catch { }
    $banner = @"

  ╔═══════════════════════════════════════════════════════════════════════════╗
  ║                                                                           ║
  ║   ██╗    ██╗ ██████╗ ██╗    ██╗     ██████╗ ██████╗ ██╗███╗   ██╗██████╗  ║
  ║   ██║    ██║██╔═══██╗██║    ██║    ██╔════╝ ██╔══██╗██║████╗  ██║██╔══██╗ ║
  ║   ██║ █╗ ██║██║   ██║██║ █╗ ██║    ██║  ███╗██████╔╝██║██╔██╗ ██║██║  ██║ ║
  ║   ██║███╗██║██║   ██║██║███╗██║    ██║   ██║██╔══██╗██║██║╚██╗██║██║  ██║ ║
  ║   ╚███╔███╔╝╚██████╔╝╚███╔███╔╝    ╚██████╔╝██║  ██║██║██║ ╚████║██████╔╝ ║
  ║    ╚══╝╚══╝  ╚═════╝  ╚══╝╚══╝      ╚═════╝ ╚═╝  ╚═╝╚═╝╚═╝  ╚═══╝╚═════╝  ║
  ║                                                                           ║
  ║                    CLASSIC GRIND BOT - LAUNCH SYSTEM                      ║
  ║                              Version 2.0.0                                ║
  ╚═══════════════════════════════════════════════════════════════════════════╝

"@
    Write-Host $banner -ForegroundColor Cyan
}

function Write-Step {
    param(
        [string]$Message,
        [ValidateSet("Info", "Success", "Warning", "Error", "Prompt", "Progress")]
        [string]$Type = "Info"
    )

    $icons = @{
        Info     = "ℹ️ "
        Success  = "✅"
        Warning  = "⚠️ "
        Error    = "❌"
        Prompt   = "❓"
        Progress = "⏳"
    }

    $colors = @{
        Info     = "White"
        Success  = "Green"
        Warning  = "Yellow"
        Error    = "Red"
        Prompt   = "Magenta"
        Progress = "Cyan"
    }

    $icon = $icons[$Type]
    $color = $colors[$Type]
    
    Write-Host "  $icon " -NoNewline -ForegroundColor $color
    Write-Host $Message -ForegroundColor $color
}

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "  ┌─────────────────────────────────────────────────────────────────────────┐" -ForegroundColor DarkCyan
    Write-Host "  │  $($Title.PadRight(71))│" -ForegroundColor DarkCyan
    Write-Host "  └─────────────────────────────────────────────────────────────────────────┘" -ForegroundColor DarkCyan
    Write-Host ""
}

function Write-StatusLine {
    param(
        [string]$Label,
        [string]$Value,
        [ValidateSet("OK", "WARN", "FAIL", "INFO")]
        [string]$Status = "INFO"
    )

    $statusColors = @{
        OK   = "Green"
        WARN = "Yellow"
        FAIL = "Red"
        INFO = "Cyan"
    }

    $statusSymbols = @{
        OK   = "[  OK  ]"
        WARN = "[ WARN ]"
        FAIL = "[ FAIL ]"
        INFO = "[ INFO ]"
    }

    $padding = 50 - $Label.Length
    if ($padding -lt 1) { $padding = 1 }

    Write-Host "     $Label" -NoNewline -ForegroundColor White
    Write-Host "$('.' * $padding)" -NoNewline -ForegroundColor DarkGray
    Write-Host " $Value " -NoNewline -ForegroundColor White
    Write-Host $statusSymbols[$Status] -ForegroundColor $statusColors[$Status]
}

function Show-ProgressAnimation {
    param(
        [string]$Message,
        [int]$DurationSeconds
    )

    $frames = @("⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏")
    $endTime = (Get-Date).AddSeconds($DurationSeconds)
    $i = 0

    while ((Get-Date) -lt $endTime) {
        Write-Host "`r     $($frames[$i % $frames.Length]) $Message..." -NoNewline -ForegroundColor Cyan
        Start-Sleep -Milliseconds 100
        $i++
    }
    Write-Host "`r     ✓ $Message... Done!    " -ForegroundColor Green
}

function Prompt-UserAction {
    param(
        [string]$Message,
        [string]$DefaultChoice = "Y",
        [switch]$Required
    )

    Write-Host ""
    Write-Host "  ┌─ USER ACTION REQUIRED ────────────────────────────────────────────────┐" -ForegroundColor Magenta
    Write-Host "  │                                                                       │" -ForegroundColor Magenta

    # Word wrap the message
    $words = $Message -split ' '
    $line = ""
    foreach ($word in $words) {
        if (($line + " " + $word).Length -gt 67) {
            Write-Host "  │  $($line.PadRight(69))│" -ForegroundColor Magenta
            $line = $word
        } else {
            $line = if ($line) { "$line $word" } else { $word }
        }
    }
    if ($line) {
        Write-Host "  │  $($line.PadRight(69))│" -ForegroundColor Magenta
    }

    Write-Host "  │                                                                       │" -ForegroundColor Magenta
    Write-Host "  └───────────────────────────────────────────────────────────────────────┘" -ForegroundColor Magenta
    Write-Host ""

    if ($Required) {
        Write-Host "     Press [ENTER] when ready to continue..." -ForegroundColor Yellow
        Read-Host
        return $true
    } else {
        $prompt = "     Continue? [Y/n]: "
        Write-Host $prompt -NoNewline -ForegroundColor Yellow
        $response = Read-Host
        return ($response -eq "" -or $response -match "^[Yy]")
    }
}

function Show-SystemStatus {
    param([hashtable]$Status)

    Write-Section "SYSTEM STATUS"

    foreach ($key in $Status.Keys | Sort-Object) {
        $item = $Status[$key]
        Write-StatusLine -Label $item.Label -Value $item.Value -Status $item.Status
    }
}

# ============================================================================
# VALIDATION FUNCTIONS
# ============================================================================
function Test-Prerequisites {
    Write-Section "CHECKING PREREQUISITES"

    $status = @{}
    $allPassed = $true

    # Check .NET Runtime
    Write-Host "     Checking .NET Runtime..." -ForegroundColor Gray
    $dotnetVersion = try { (dotnet --version) } catch { $null }
    if ($dotnetVersion -and [version]$dotnetVersion -ge [version]"10.0") {
        $status["dotnet"] = @{ Label = ".NET Runtime"; Value = $dotnetVersion; Status = "OK" }
    } else {
        $status["dotnet"] = @{ Label = ".NET Runtime"; Value = "Not Found"; Status = "FAIL" }
        $allPassed = $false
    }

    # Check Bot Installation
    Write-Host "     Checking Bot Installation..." -ForegroundColor Gray
    if (Test-Path $Config.BlazorServerPath) {
        $status["blazor"] = @{ Label = "BlazorServer.exe"; Value = "Found"; Status = "OK" }
    } else {
        $status["blazor"] = @{ Label = "BlazorServer.exe"; Value = "Not Found"; Status = "FAIL" }
        $allPassed = $false
    }

    # Check Navigation Server
    Write-Host "     Checking Navigation Server..." -ForegroundColor Gray
    if (Test-Path $Config.NavServerPath) {
        $status["navserver"] = @{ Label = "AmeisenNavigation"; Value = "Found"; Status = "OK" }
    } else {
        $status["navserver"] = @{ Label = "AmeisenNavigation"; Value = "Not Found"; Status = "WARN" }
    }

    # Check MMAP Files
    Write-Host "     Checking MMAP Files..." -ForegroundColor Gray
    $mmapFiles = Get-ChildItem -Path $Config.MmapsPath -Filter "*.mmap" -ErrorAction SilentlyContinue
    if ($mmapFiles.Count -gt 0) {
        $status["mmaps"] = @{ Label = "MMAP Files"; Value = "$($mmapFiles.Count) files"; Status = "OK" }
    } else {
        $status["mmaps"] = @{ Label = "MMAP Files"; Value = "None found"; Status = "WARN" }
    }

    # Check MPQ Files
    Write-Host "     Checking MPQ Files..." -ForegroundColor Gray
    $mpqFile = Join-Path $Config.MPQPath "expansion.MPQ"
    if (Test-Path $mpqFile) {
        $status["mpq"] = @{ Label = "expansion.MPQ"; Value = "Found"; Status = "OK" }
    } else {
        $status["mpq"] = @{ Label = "expansion.MPQ"; Value = "Not Found"; Status = "WARN" }
    }

    # Check WoW Installation
    Write-Host "     Checking WoW Installation..." -ForegroundColor Gray
    $wowExe = Join-Path $Config.WoWPath $Config.WoWExecutable
    if (Test-Path $wowExe) {
        $status["wow"] = @{ Label = "WoW Client"; Value = "Found"; Status = "OK" }
    } else {
        $status["wow"] = @{ Label = "WoW Client"; Value = "Not Found"; Status = "WARN" }
    }

    # Check Required Addons
    Write-Host "     Checking Required Addons..." -ForegroundColor Gray
    foreach ($addon in $Config.RequiredAddons) {
        $addonPath = Join-Path $Config.AddonsDestPath $addon
        if (Test-Path $addonPath) {
            $status["addon_$addon"] = @{ Label = "Addon: $addon"; Value = "Installed"; Status = "OK" }
        } else {
            $status["addon_$addon"] = @{ Label = "Addon: $addon"; Value = "Missing"; Status = "FAIL" }
            $allPassed = $false
        }
    }

    Show-SystemStatus $status

    return @{
        Passed = $allPassed
        Status = $status
    }
}

function Install-MissingAddons {
    Write-Section "INSTALLING ADDONS"

    $installed = @()

    foreach ($addon in ($Config.RequiredAddons + $Config.OptionalAddons)) {
        $sourcePath = Join-Path $Config.AddonsSourcePath $addon
        $destPath = Join-Path $Config.AddonsDestPath $addon

        if (-not (Test-Path $destPath) -and (Test-Path $sourcePath)) {
            Write-Step "Installing $addon..." -Type Progress
            try {
                Copy-Item -Path $sourcePath -Destination $destPath -Recurse -Force
                Write-Step "$addon installed successfully" -Type Success
                $installed += $addon
            } catch {
                $errMsg = $_.Exception.Message
                Write-Step "Failed to install ${addon}: $errMsg" -Type Error
            }
        } elseif (Test-Path $destPath) {
            Write-Step "$addon already installed" -Type Info
        }
    }

    return $installed
}

# ============================================================================
# SERVICE FUNCTIONS
# ============================================================================
function Start-NavigationServer {
    Write-Section "STARTING NAVIGATION SERVER"

    # Check if already running
    $navProcess = Get-Process -Name "AmeisenNavigationServer" -ErrorAction SilentlyContinue
    if ($navProcess) {
        Write-Step "AmeisenNavigation is already running (PID: $($navProcess.Id))" -Type Success
        return $true
    }

    # Check if server exists
    if (-not (Test-Path $Config.NavServerPath)) {
        Write-Step "AmeisenNavigationServer.exe not found" -Type Warning
        Write-Step "Pathfinding will use local PPather fallback" -Type Info
        return $false
    }

    # Check for MMAP files
    $mmapFiles = Get-ChildItem -Path $Config.MmapsPath -Filter "*.mmap" -ErrorAction SilentlyContinue
    if ($mmapFiles.Count -eq 0) {
        Write-Step "No MMAP files found - Navigation Server requires MMAPs" -Type Warning
        Write-Step "Download MMAPs from the setup guide" -Type Info
        return $false
    }

    Write-Step "Starting AmeisenNavigation Server..." -Type Progress

    try {
        $navDir = Split-Path $Config.NavServerPath -Parent
        Start-Process -FilePath $Config.NavServerPath -WorkingDirectory $navDir -WindowStyle Minimized

        # Wait for server to initialize
        Show-ProgressAnimation -Message "Waiting for Navigation Server to initialize" -DurationSeconds $Config.StartupDelay

        # Verify it's running
        Start-Sleep -Seconds 1
        $navProcess = Get-Process -Name "AmeisenNavigationServer" -ErrorAction SilentlyContinue
        if ($navProcess) {
            Write-Step "Navigation Server started on port $($Config.NavPort) (PID: $($navProcess.Id))" -Type Success
            return $true
        } else {
            Write-Step "Navigation Server failed to start" -Type Error
            return $false
        }
    } catch {
        $errMsg = $_.Exception.Message
        Write-Step "Error starting Navigation Server: $errMsg" -Type Error
        return $false
    }
}

function Test-WoWClient {
    Write-Section "CHECKING WOW CLIENT"

    $wowProcess = Get-Process -Name "WowClassic" -ErrorAction SilentlyContinue

    if ($wowProcess) {
        Write-Step "WoW Classic is running (PID: $($wowProcess.Id))" -Type Success
        return @{
            Running = $true
            Process = $wowProcess
        }
    } else {
        Write-Step "WoW Classic is not running" -Type Warning
        return @{
            Running = $false
            Process = $null
        }
    }
}

function Start-WoWClient {
    param([switch]$Wait)

    $wowExe = Join-Path $Config.WoWPath $Config.WoWExecutable

    if (-not (Test-Path $wowExe)) {
        Write-Step "WoW executable not found at: $wowExe" -Type Error
        return $false
    }

    Write-Step "Launching WoW Classic..." -Type Progress

    try {
        Start-Process -FilePath $wowExe -WorkingDirectory $Config.WoWPath

        if ($Wait) {
            Write-Host ""
            Write-Host "  ┌─ MANUAL STEPS REQUIRED ────────────────────────────────────────────────┐" -ForegroundColor Yellow
            Write-Host "  │                                                                        │" -ForegroundColor Yellow
            Write-Host "  │  1. Log in to your WoW account                                        │" -ForegroundColor Yellow
            Write-Host "  │  2. Select your character                                             │" -ForegroundColor Yellow
            Write-Host "  │  3. Enter the game world                                              │" -ForegroundColor Yellow
            Write-Host "  │  4. Make sure your character is in a safe location                    │" -ForegroundColor Yellow
            Write-Host "  │                                                                        │" -ForegroundColor Yellow
            Write-Host "  │  The bot requires your character to be fully loaded in-game.          │" -ForegroundColor Yellow
            Write-Host "  │                                                                        │" -ForegroundColor Yellow
            Write-Host "  └────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Yellow
            Write-Host ""

            Prompt-UserAction -Message "Press ENTER once your character is logged in and in the game world." -Required
        }

        return $true
    } catch {
        $errMsg = $_.Exception.Message
        Write-Step "Error launching WoW: $errMsg" -Type Error
        return $false
    }
}

function Start-BotServer {
    Write-Section "STARTING BOT SERVER"

    if (-not (Test-Path $Config.BlazorServerPath)) {
        Write-Step "BlazorServer.exe not found!" -Type Error
        Write-Step "Run: dotnet build -c Release" -Type Info
        return $false
    }

    Write-Step "Starting BlazorServer..." -Type Progress

    $blazorDir = Split-Path $Config.BlazorServerPath -Parent

    # Open browser after delay
    $browserJob = Start-Job -ScriptBlock {
        param($port)
        Start-Sleep -Seconds 3
        Start-Process "http://localhost:$port"
    } -ArgumentList $Config.WebPort

    Write-Host ""
    Write-Host "  ┌─ BOT INTERFACE ────────────────────────────────────────────────────────┐" -ForegroundColor Green
    Write-Host "  │                                                                        │" -ForegroundColor Green
    Write-Host "  │  Web UI:  http://localhost:$($Config.WebPort)                                          │" -ForegroundColor Green
    Write-Host "  │                                                                        │" -ForegroundColor Green
    Write-Host "  │  The browser will open automatically.                                  │" -ForegroundColor Green
    Write-Host "  │  Press Ctrl+C in this window to stop the bot.                          │" -ForegroundColor Green
    Write-Host "  │                                                                        │" -ForegroundColor Green
    Write-Host "  └────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Green
    Write-Host ""

    Write-Section "BOT OUTPUT"

    # Start the bot (this will block)
    try {
        Push-Location $blazorDir
        & $Config.BlazorServerPath
    } finally {
        Pop-Location
        Stop-Job $browserJob -ErrorAction SilentlyContinue
        Remove-Job $browserJob -ErrorAction SilentlyContinue
    }
}

function Stop-AllServices {
    Write-Section "SHUTTING DOWN"

    Write-Step "Stopping Navigation Server..." -Type Progress
    Get-Process -Name "AmeisenNavigationServer" -ErrorAction SilentlyContinue | Stop-Process -Force

    Write-Step "All services stopped" -Type Success
}

# ============================================================================
# MAIN LAUNCH SEQUENCE
# ============================================================================
function Start-LaunchSequence {
    Write-Banner

    Write-Host "  Starting launch sequence..." -ForegroundColor White
    Write-Host "  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor DarkGray
    Write-Host ""

    # Step 1: Prerequisites Check
    $prereqResult = Test-Prerequisites

    if (-not $prereqResult.Passed) {
        Write-Host ""
        Write-Step "Some prerequisites are missing!" -Type Error

        # Check if addons need installing
        $missingAddons = $Config.RequiredAddons | Where-Object {
            -not (Test-Path (Join-Path $Config.AddonsDestPath $_))
        }

        if ($missingAddons.Count -gt 0) {
            Write-Host ""
            $installAddons = Prompt-UserAction -Message "Required addons are missing. Would you like to install them now? This requires running as Administrator for the WoW directory."

            if ($installAddons) {
                # Check for admin rights
                $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

                if (-not $isAdmin) {
                    Write-Step "Administrator rights required for addon installation" -Type Warning
                    Write-Host ""
                    Write-Host "     Please run this script as Administrator, or manually copy addons from:" -ForegroundColor Yellow
                    Write-Host "     $($Config.AddonsSourcePath)" -ForegroundColor Cyan
                    Write-Host "     to:" -ForegroundColor Yellow
                    Write-Host "     $($Config.AddonsDestPath)" -ForegroundColor Cyan
                    Write-Host ""

                    $continue = Prompt-UserAction -Message "Continue without installing addons? The bot may not function correctly."
                    if (-not $continue) {
                        return
                    }
                } else {
                    Install-MissingAddons
                }
            }
        }

        # Check for BlazorServer
        if (-not (Test-Path $Config.BlazorServerPath)) {
            Write-Host ""
            Write-Step "BlazorServer.exe not found. The bot needs to be built first." -Type Error
            Write-Host ""
            Write-Host "     Run the following commands:" -ForegroundColor Yellow
            Write-Host "       cd $($Config.BotPath)" -ForegroundColor Cyan
            Write-Host "       dotnet build -c Release" -ForegroundColor Cyan
            Write-Host ""
            return
        }
    }

    # Step 2: Start Navigation Server
    if (-not $SkipNavServer) {
        $navStarted = Start-NavigationServer
    }

    # Step 3: Check WoW Client
    if (-not $SkipWoWCheck) {
        $wowStatus = Test-WoWClient

        if (-not $wowStatus.Running) {
            Write-Host ""

            if ($AutoLaunchWoW) {
                Start-WoWClient -Wait
            } else {
                $launchWoW = Prompt-UserAction -Message "WoW Classic is not running. Would you like to launch it now?"

                if ($launchWoW) {
                    Start-WoWClient -Wait
                } else {
                    Write-Host ""
                    Write-Host "  ┌─ MANUAL START REQUIRED ────────────────────────────────────────────────┐" -ForegroundColor Yellow
                    Write-Host "  │                                                                        │" -ForegroundColor Yellow
                    Write-Host "  │  Please start WoW Classic and log in before continuing.                │" -ForegroundColor Yellow
                    Write-Host "  │  The bot needs to attach to a running WoW process.                     │" -ForegroundColor Yellow
                    Write-Host "  │                                                                        │" -ForegroundColor Yellow
                    Write-Host "  └────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Yellow
                    Write-Host ""

                    Prompt-UserAction -Message "Press ENTER once WoW is running and your character is logged in." -Required
                }
            }
        }
    }

    # Step 4: Final Check
    Write-Section "FINAL SYSTEM CHECK"

    $finalStatus = @{}

    # Navigation Server
    $navProcess = Get-Process -Name "AmeisenNavigationServer" -ErrorAction SilentlyContinue
    if ($navProcess) {
        $finalStatus["nav"] = @{ Label = "Navigation Server"; Value = "Running"; Status = "OK" }
    } else {
        $finalStatus["nav"] = @{ Label = "Navigation Server"; Value = "Not Running"; Status = "WARN" }
    }

    # WoW Client
    $wowProcess = Get-Process -Name "WowClassic" -ErrorAction SilentlyContinue
    if ($wowProcess) {
        $finalStatus["wow"] = @{ Label = "WoW Classic"; Value = "Running (PID: $($wowProcess.Id))"; Status = "OK" }
    } else {
        $finalStatus["wow"] = @{ Label = "WoW Classic"; Value = "Not Detected"; Status = "WARN" }
    }

    # Bot Server
    $finalStatus["bot"] = @{ Label = "Bot Server"; Value = "Ready to Start"; Status = "INFO" }

    Show-SystemStatus $finalStatus

    # Step 5: Start Bot (only if not in headless mode or auto-confirm)
    if ($Headless) {
        Write-Step "Headless mode - skipping bot server start" -Type Info
        return
    }

    Write-Host ""
    $startBot = Prompt-UserAction -Message "All systems ready. Start the bot now?"

    if ($startBot) {
        # Register cleanup on exit
        $null = Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action {
            Write-Host "`n  Cleaning up..." -ForegroundColor Yellow
            Get-Process -Name "AmeisenNavigationServer" -ErrorAction SilentlyContinue | Stop-Process -Force
        }

        try {
            Start-BotServer
        } finally {
            Stop-AllServices
        }
    } else {
        Write-Step "Launch cancelled by user" -Type Info
    }

    Write-Host ""
    Write-Host "  Goodbye!" -ForegroundColor Cyan
    Write-Host ""
}

# ============================================================================
# ENTRY POINT
# ============================================================================
try {
    Start-LaunchSequence
} catch {
    Write-Host ""
    $errMsg = $_.Exception.Message
    Write-Step "An error occurred: $errMsg" -Type Error
    Write-Host ""
    Write-Host "  Stack Trace:" -ForegroundColor DarkGray
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    Write-Host ""
} finally {
    if (-not $Headless) {
        Write-Host "  Press any key to exit..." -ForegroundColor DarkGray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    }
}
