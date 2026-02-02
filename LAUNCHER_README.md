# WowClassicGrindBot - Launch System

A comprehensive launcher system for WowClassicGrindBot that handles all startup requirements with a clean, user-friendly interface.

## Quick Start

### First Time Users
```
Double-click: Setup.bat
```
This runs an interactive wizard that will:
- Detect your WoW installation
- Install required addons
- Configure navigation
- Build the bot
- Launch when ready

### Regular Usage
```
Double-click: Launch.bat
```
This will:
1. Validate all prerequisites
2. Start the Navigation Server (if available)
3. Check if WoW is running
4. Start the Bot Web UI
5. Open your browser to http://localhost:5000

### Auto-Launch (Skip Prompts)
```
Double-click: LaunchAuto.bat
```
Automatically launches WoW if not running and starts the bot.

## Available Tools

| File | Description |
|------|-------------|
| `Launch.bat` | Main launcher with guided prompts |
| `LaunchAuto.bat` | Auto-launch WoW and bot |
| `Setup.bat` | First-time setup wizard |
| `Troubleshoot.bat` | Diagnose and fix problems |
| `Status.bat` | Show status of all services |
| `StopBot.bat` | Stop all bot services |

## PowerShell Scripts

Located in `Scripts/` folder:

### WowGrindBotLauncher.ps1
Main launcher with full functionality.

```powershell
# Basic launch
.\Scripts\WowGrindBotLauncher.ps1

# Skip WoW check (already running)
.\Scripts\WowGrindBotLauncher.ps1 -SkipWoWCheck

# Auto-launch WoW
.\Scripts\WowGrindBotLauncher.ps1 -AutoLaunchWoW

# Skip Navigation Server
.\Scripts\WowGrindBotLauncher.ps1 -SkipNavServer

# Custom paths
.\Scripts\WowGrindBotLauncher.ps1 -BotPath "D:\WowBot" -WoWPath "D:\Games\WoW"
```

### ServiceMonitor.ps1
Monitor and manage bot services.

```powershell
# Show status
.\Scripts\ServiceMonitor.ps1 -Status

# Start all services
.\Scripts\ServiceMonitor.ps1 -StartAll

# Stop all services
.\Scripts\ServiceMonitor.ps1 -StopAll

# Continuous monitoring with auto-restart
.\Scripts\ServiceMonitor.ps1 -Monitor
```

### SetupWizard.ps1
Interactive first-time setup.

```powershell
.\Scripts\SetupWizard.ps1
```

### Troubleshooter.ps1
Diagnose and fix issues.

```powershell
# Run diagnostics
.\Scripts\Troubleshooter.ps1

# Auto-fix fixable issues
.\Scripts\Troubleshooter.ps1 -AutoFix
```

### BotConfiguration.psm1
PowerShell module for programmatic configuration.

```powershell
Import-Module .\Scripts\BotConfiguration.psm1

# Get current configuration
$config = Get-BotConfiguration

# Validate configuration
Test-BotConfiguration

# Set WoW path
Set-BotWoWPath -WoWPath "D:\Games\WoW"

# Set pathing mode
Set-BotPathingMode -Mode "Local"  # or "RemoteV3"
```

## Launch Sequence

The launcher performs these steps in order:

```
1. PREREQUISITES CHECK
   ├── .NET Runtime (10.0+)
   ├── BlazorServer.exe built
   ├── Navigation Server (optional)
   ├── MMAP files (optional)
   ├── MPQ file (optional)
   ├── WoW installation
   └── Required addons (DataToColor)

2. START NAVIGATION SERVER
   └── AmeisenNavigationServer.exe on port 47111

3. CHECK WOW CLIENT
   ├── Detect running WoW process
   └── Offer to launch if not running

4. FINAL SYSTEM CHECK
   ├── Verify Navigation Server
   ├── Verify WoW Client
   └── Confirm ready to start

5. START BOT SERVER
   ├── Launch BlazorServer.exe
   ├── Open browser to http://localhost:5000
   └── Display bot output
```

## Troubleshooting

### Common Issues

**"BlazorServer.exe not found"**
```
cd C:\WowClassicGrindBot
dotnet build -c Release
```

**"DataToColor addon not installed"**
```
# Run as Administrator
.\Scripts\SetupWizard.ps1
```
Or manually copy from `Addons\DataToColor` to your WoW `Interface\AddOns` folder.

**"Navigation Server failed to start"**
- Check that MMAP files exist in `Navigation\mmaps\`
- Verify `Navigation\config.json` is valid
- The bot will work without it (uses local pathfinding)

**"WoW not detected"**
- Start WoW manually
- Log in to your character
- Make sure character is in the game world

### Diagnostic Commands

```powershell
# Full diagnostics
.\Scripts\Troubleshooter.ps1

# Check service status
.\Scripts\ServiceMonitor.ps1 -Status

# Check configuration
Import-Module .\Scripts\BotConfiguration.psm1
Test-BotConfiguration
```

## System Requirements

- Windows 10/11
- PowerShell 5.1+ (comes with Windows)
- .NET 10.0 SDK/Runtime
- World of Warcraft Classic installed
- ~2GB RAM for bot + navigation server

## File Locations

```
C:\WowClassicGrindBot\
├── Launch.bat              # Main launcher
├── LaunchAuto.bat          # Auto-launch
├── Setup.bat               # Setup wizard
├── Troubleshoot.bat        # Troubleshooter
├── Status.bat              # Service status
├── StopBot.bat             # Stop services
│
├── Scripts\
│   ├── WowGrindBotLauncher.ps1   # Main launcher script
│   ├── ServiceMonitor.ps1         # Service management
│   ├── SetupWizard.ps1           # First-time setup
│   ├── Troubleshooter.ps1        # Diagnostics
│   └── BotConfiguration.psm1     # Config module
│
├── Navigation\
│   ├── AmeisenNavigationServer.exe
│   ├── config.json
│   └── mmaps\              # MMAP files here
│
├── BlazorServer\
│   └── bin\Release\net10.0\
│       └── BlazorServer.exe
│
└── Addons\
    ├── DataToColor\        # Required addon
    └── ...
```

## Support

If you encounter issues:

1. Run `Troubleshoot.bat` first
2. Check the bot's web UI logs at http://localhost:5000
3. Review the console output for errors
4. Ensure WoW is fully loaded (in-game, not loading screen)
