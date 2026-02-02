# Downloading Required Files for WowClassicGrindBot

This guide explains how to download the required MPQ and MMAP files, especially if you encountered MEGA rate limiting.

## The Problem: MEGA Rate Limiting

MEGA.nz limits bandwidth for free users. When downloading large files (~2GB+), you may hit this limit and the download stops with:

```
Transfer quota exceeded
```

## The Solution: MEGAcmd

MEGAcmd is MEGA's official command-line tool that:
- **Supports resume** - restart interrupted downloads
- **Better rate limit handling** - more efficient than browser downloads
- **No browser overhead** - direct downloads
- **Scriptable** - automate the process

---

## Quick Start (Automatic)

The easiest way is to use the provided PowerShell script:

```powershell
cd C:\WowClassicGrindBot\Scripts
.\Download-BotFiles.ps1 -FileType MPQ -Version TBC
```

This script will:
1. Check if MEGAcmd is installed
2. Install it automatically if needed
3. Download the required files
4. Support resume if interrupted

### Script Options

```powershell
# Download TBC MPQ file
.\Download-BotFiles.ps1 -FileType MPQ -Version TBC

# Download Vanilla MPQ file  
.\Download-BotFiles.ps1 -FileType MPQ -Version Vanilla

# Download WOTLK MPQ file
.\Download-BotFiles.ps1 -FileType MPQ -Version WOTLK

# Download MMAP files for V3 pathfinding
.\Download-BotFiles.ps1 -FileType MMAP -Version TBC

# Download both MPQ and MMAP
.\Download-BotFiles.ps1 -FileType Both -Version TBC
```

---

## Manual Installation Steps

If you prefer manual setup or the script doesn't work:

### 1. Install MEGAcmd

**Option A: Direct Download**
1. Go to https://mega.nz/cmd
2. Click "Download CMD for Windows"
3. Run `MEGAcmdSetup64.exe`
4. Follow the installer (default settings are fine)

**Option B: Silent Install (Command Line)**
```cmd
MEGAcmdSetup64.exe /S
```

After installation, MEGAcmd will be at: `%LOCALAPPDATA%\MEGAcmd\`

### 2. Using MEGAcmd to Download Files

Open **PowerShell** or **Command Prompt** and run:

#### For TBC expansion.MPQ (1.8GB):
```cmd
"%LOCALAPPDATA%\MEGAcmd\mega-get.bat" "https://mega.nz/#!Of4i2YQS!egDGj-SXi9RigG-_8kPITihFsLom2L1IFF-ltnB3wmU" "C:\WowClassicGrindBot\Json\MPQ\expansion.MPQ"
```

#### For Vanilla common-2.MPQ (1.7GB):
```cmd
"%LOCALAPPDATA%\MEGAcmd\mega-get.bat" "https://mega.nz/#!vXQCBCha!m7COhB9HQd86a5iNAT0-fMLsc-BtoTRO1eIBJNrdTH8" "C:\WowClassicGrindBot\Json\MPQ\common-2.MPQ"
```

#### For WOTLK lichking.MPQ (2.5GB):
```cmd
"%LOCALAPPDATA%\MEGAcmd\mega-get.bat" "https://mega.nz/#!vDYWSTrK!fvaiuHpd-FTVsQT4ghGLK6QJLZyA87c1rlBEeu1_Btk" "C:\WowClassicGrindBot\Json\MPQ\lichking.MPQ"
```

#### For MMAP files (Vanilla + TBC, ~2GB):
```cmd
"%LOCALAPPDATA%\MEGAcmd\mega-get.bat" "https://mega.nz/#!7HgkHIyA!c_gzUeTadecWY0JDY3KT39ktfPGLs2vzt_90bMvhszk" "C:\WowClassicGrindBot\Navigation\mmaps_v15.7z"
```

---

## Resume Interrupted Downloads

If your download gets interrupted (internet drops, rate limit hit, etc.):

**Just re-run the same command.**

MEGAcmd automatically resumes from where it stopped. Example:

```powershell
# Original download started
.\Download-BotFiles.ps1 -FileType MPQ -Version TBC

# Download stopped at 40%
# ...wait 6 hours for rate limit reset...

# Re-run the SAME command - it will resume!
.\Download-BotFiles.ps1 -FileType MPQ -Version TBC
```

---

## Alternative: Browser Extension + Download Manager

If MEGAcmd doesn't work for you:

### 1. Install Free Download Manager (FDM)
- Download from: https://www.freedownloadmanager.org/
- FDM supports resume and can sometimes bypass MEGA rate limits

### 2. Get Browser Extension
- Install "MEGA Downloader" extension for your browser
- Configure it to use FDM for downloads

### 3. Split Downloads
- If you keep hitting rate limits, download in smaller chunks
- Use a VPN to change IP (rate limits are per-IP)

---

## Alternative: Wait for Rate Limit Reset

MEGA's rate limit typically resets after **6 hours**. If you can wait:

1. Note when your download stopped
2. Wait 6 hours
3. Resume the download using MEGAcmd (it will resume automatically)

---

## File Locations Reference

After successful download, verify files are in the correct locations:

### MPQ Files (for V1 Pathfinding)
```
C:\WowClassicGrindBot\Json\MPQ\
├── common-2.MPQ      (Vanilla, 1.7GB)
├── expansion.MPQ     (TBC, 1.8GB)
└── lichking.MPQ      (WOTLK, 2.5GB)
```

### MMAP Files (for V3 Pathfinding)
After downloading the `.7z` file, extract it:

1. Download 7-Zip from: https://www.7-zip.org/
2. Right-click the `.7z` file → 7-Zip → Extract Here
3. Move the extracted `mmaps` folder to:
   ```
   C:\WowClassicGrindBot\Navigation\mmaps\
   ```

The final structure should look like:
```
C:\WowClassicGrindBot\Navigation\
├── AmeisenNavigationServer.exe
├── config.json
├── StartNavigationServer.bat
└── mmaps\
    ├── 000.map
    ├── 001.map
    ├── 0002727.mmtile
    └── ... (many more files)
```

---

## Troubleshooting

### "MEGAcmd not found"
- Make sure you installed MEGAcmd
- Restart PowerShell/CMD after installation
- Check that `%LOCALAPPDATA%\MEGAcmd\` exists

### "Transfer quota exceeded"
- Wait 6 hours for rate limit reset
- Use a different IP (VPN, mobile hotspot, etc.)
- Try downloading from a different location/network

### "File corrupted after download"
- Re-download using MEGAcmd (it has built-in integrity checks)
- Check available disk space
- Verify antivirus isn't interfering

### Download is very slow
- This is normal for MEGA free tier
- Consider upgrading to MEGA Pro (temporary 1-month is cheap)
- Use a download manager to maximize speed

---

## What Each File Is For

| File | Purpose | Required For |
|------|---------|--------------|
| **expansion.MPQ** | TBC map data for pathfinding | V1 Remote/Local pathfinding (TBC) |
| **common-2.MPQ** | Vanilla map data | V1 Remote/Local pathfinding (Vanilla) |
| **lichking.MPQ** | WOTLK map data | V1 Remote/Local pathfinding (WOTLK) |
| **mmaps (extracted)** | High-quality navigation meshes | V3 Remote pathfinding (AmeisenNavigation) |

---

## Next Steps After Downloading

### If you downloaded MPQ files:
1. Files should be in `C:\WowClassicGrindBot\Json\MPQ\`
2. Configure the bot to use Local pathfinding:
   ```powershell
   .\Scripts\Configure-Pathfinder.ps1 -Backend Local
   ```
3. Launch the bot:
   ```
   C:\WowClassicGrindBot\LaunchBot.bat
   ```

### If you downloaded MMAP files:
1. Extract the `.7z` file (use 7-Zip)
2. Move the `mmaps` folder to `C:\WowClassicGrindBot\Navigation\mmaps\`
3. Start the navigation server:
   ```
   C:\WowClassicGrindBot\Navigation\StartNavigationServer.bat
   ```
4. Launch the bot with full stack:
   ```
   C:\WowClassicGrindBot\StartAll.bat
   ```

---

## Support

If you continue having download issues:

1. Check the [GitHub Issues](https://github.com/Xian55/WowClassicGrindBot/issues) for similar problems
2. Join the Discord (link in repository README)
3. Try alternative download sources (check community forums)
