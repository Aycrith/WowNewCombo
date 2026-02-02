# WowClassicGrindBot Setup Guide

## Installation Complete! ✓

Your WowClassicGrindBot installation is located at: `C:\WowClassicGrindBot`

---

## Required Manual Steps

### 1. Download MPQ Pathfinding Data (REQUIRED)

The bot needs the MPQ file for pathfinding. Download `expansion.MPQ` (1.8 GB) from:

**MEGA Link:** https://mega.nz/folder/43U3BCjJ#NjpC4fXLLFhAluGnPKlg3w

After downloading, place it in:
```
C:\WowClassicGrindBot\Json\MPQ\expansion.MPQ
```

### 2. Install Addons (REQUIRES ADMINISTRATOR)

Run the addon installer as Administrator:

1. Open PowerShell as Administrator
2. Run:
   ```powershell
   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
   & "C:\WowClassicGrindBot\Scripts\Install-Addons.ps1"
   ```

This creates symbolic links for:
- **DataToColor** - Encodes game state as colored pixels
- **BindPad** - Provides secure action buttons (required for TBC 2.5.5+)
- **cTimerBackport** - Timer utility backport
- **SoundKitBackport** - Sound kit compatibility

---

## WoW Graphics Settings (CRITICAL)

The bot reads game state via colored pixels. You MUST configure these settings:

### In WoW System Settings → Graphics:

| Setting | Required Value |
|---------|----------------|
| **Anti-Aliasing** | None / Off |
| **Vertical Sync** | Off |
| **Render Scale** | 100% |
| **Graphics Quality** | Any (lower = better performance) |
| **Resolution** | Windowed mode recommended |

### Why These Settings Matter:
- **Anti-Aliasing** blurs pixel colors, making them unreadable
- **Render Scale** other than 100% distorts pixel positions
- **VSync** can cause input lag

---

## In-Game Addon Setup

### Enable Addons
1. At character selection, click "AddOns"
2. Enable:
   - [x] DataToColor
   - [x] BindPad
   - [x] cTimerBackport
   - [x] SoundKitBackport

### First-Time Configuration
1. Log into your character
2. Type `/dc` to open DataToColor configuration
3. The addon will create colored pixels in the top-left corner of your screen

---

## Pathfinding Setup (RECOMMENDED)

The bot supports three pathfinding backends. For full details, see: `PATHFINDER_GUIDE.md`

### Quick Setup Options:

**Option A: Best Quality (AmeisenNavigation)**
1. Add MMAP files to `C:\WowClassicGrindBot\Navigation\mmaps\`
2. Double-click: `C:\WowClassicGrindBot\StartAll.bat`

**Option B: Simple Setup (Local PPather)**
1. Download `expansion.MPQ` (1.8GB) from MEGA link above
2. Place in `C:\WowClassicGrindBot\Json\MPQ\`
3. Bot will use Local pathfinding automatically

### Configure Pathfinder:
```powershell
# Use best quality (requires MMAPs)
.\Scripts\Configure-Pathfinder.ps1 -Backend RemoteV3

# Use simple local pathfinding (requires expansion.MPQ)
.\Scripts\Configure-Pathfinder.ps1 -Backend Local
```

---

## Running the Bot

### Quick Start (Bot Only)
Double-click: `C:\WowClassicGrindBot\LaunchBot.bat`

### Full Stack (Navigation Server + Bot)
Double-click: `C:\WowClassicGrindBot\StartAll.bat`

### Manual Start
```powershell
cd C:\WowClassicGrindBot\BlazorServer\bin\Release\net10.0
.\BlazorServer.exe
```

### Web Interface
Open your browser to: **http://localhost:5000**

---

## Class Profiles

Pre-configured class profiles are in: `C:\WowClassicGrindBot\Json\class\`

Example profiles available:
- Warrior, Paladin, Hunter, Rogue, Priest
- Shaman, Mage, Warlock, Druid

### Selecting a Profile
1. Open the web UI at http://localhost:5000
2. Go to Settings → Class Profile
3. Select your class/spec JSON file

---

## Grind Paths

Path files are in: `C:\WowClassicGrindBot\Json\path\`

These define grinding routes for specific zones and level ranges.

---

## Utility Scripts

All scripts are in `C:\WowClassicGrindBot\Scripts\`:

| Script | Purpose | Requires Admin |
|--------|---------|----------------|
| `Install-Addons.ps1` | Install addon symlinks | Yes |
| `Uninstall-Bot.ps1` | Remove addon symlinks | Yes |
| `Backup-WoWSettings.ps1` | Backup WoW configs | No |
| `Restore-WoWSettings.ps1` | Restore from backup | No |

### Create a Backup Before Botting
```powershell
& "C:\WowClassicGrindBot\Scripts\Backup-WoWSettings.ps1"
```

Backups are saved to: `C:\WowClassicGrindBot\Backups\`

---

## Troubleshooting

### "DataToColor not detected"
1. Make sure the addon is enabled in-game
2. Check that symlinks were created (run Install-Addons.ps1 as Admin)
3. Type `/reload` in-game

### "Unable to find path"
1. Make sure you downloaded `expansion.MPQ`
2. Verify it's in `C:\WowClassicGrindBot\Json\MPQ\`
3. File should be ~1.8 GB

### "Screen capture not working"
1. Run WoW in **Windowed** or **Windowed Fullscreen** mode
2. Disable any overlay software (Discord, GeForce Experience, etc.)
3. Check Anti-Aliasing is set to **None**

### Bot clicks but nothing happens
1. Make sure WoW window is focused
2. Check keybindings match your character's setup
3. Verify BindPad addon is enabled and configured

---

## File Structure

```
C:\WowClassicGrindBot\
├── Addons\                    # Bot addons (symlinked to WoW)
│   ├── DataToColor\
│   ├── BindPad\
│   └── ...
├── BlazorServer\              # Web UI application
├── Navigation\                # AmeisenNavigation (V3 Pathfinder)
│   ├── AmeisenNavigationServer.exe
│   ├── config.json
│   ├── StartNavigationServer.bat
│   └── mmaps\                 # Add MMAP files here
├── Json\
│   ├── class\                 # Class profiles (83 files)
│   ├── path\                  # Grind paths (545 files)
│   └── MPQ\                   # Pathfinding data (add expansion.MPQ)
├── Scripts\                   # Utility scripts
│   ├── Install-Addons.ps1
│   ├── Configure-Pathfinder.ps1
│   └── ...
├── Backups\                   # WoW setting backups
├── LaunchBot.bat              # Quick launcher
├── StartAll.bat               # Full stack launcher
├── SETUP_GUIDE.md             # This file
└── PATHFINDER_GUIDE.md        # Pathfinding documentation
```

---

## Support & Documentation

- **GitHub Repository:** https://github.com/Xian55/WowClassicGrindBot
- **Discord:** Check the repository for Discord link
- **Wiki:** https://github.com/Xian55/WowClassicGrindBot/wiki

---

## Legal Notice

This software is for educational purposes. Using bots may violate World of Warcraft's Terms of Service and could result in account penalties. Use at your own risk.
