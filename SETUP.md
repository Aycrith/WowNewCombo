# WowCombo Setup Guide

This guide will help you get WowCombo running from a fresh clone or download.

---

## 📋 Prerequisites

| Requirement | Version | Download |
|------------|---------|----------|
| **Operating System** | Windows 10+ | - |
| **.NET SDK** | 10.0.100+ | [Download .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **World of Warcraft Classic** | Supported versions in [README](README.md) | - |
| **Git** (optional) | Any recent | [Download Git](https://git-scm.com/downloads) |

---

## 🚀 Quick Start (5 Steps)

### 1. **Clone or Download Repository**

**Option A: Git Clone**
```bash
git clone https://github.com/Aycrith/WowCombo.git
cd WowCombo
```

**Option B: Download ZIP**
- Download from: https://github.com/Aycrith/WowCombo/archive/refs/heads/main.zip
- Extract to `C:\WowCombo` (or your preferred location)

---

### 2. **Download MPQ Map Data** (Required for V1 Pathfinding)

MPQ files provide world geometry for pathfinding. Download the files for your WoW version and place them in `Json\MPQ\`:

| WoW Version | File | Size | Download Link |
|------------|------|------|---------------|
| **Vanilla** | `common-2.MPQ` | 1.7 GB | [Download](https://mega.nz/file/vXQCBCha#m7COhB9HQd86a5iNAT0-fMLsc-BtoTRO1eIBJNrdTH8) |
| **TBC** | `expansion.MPQ` | 1.8 GB | [Download](https://mega.nz/file/Of4i2YQS#egDGj-SXi9RigG-_8kPITihFsLom2L1IFF-ltnB3wmU) |
| **WOTLK** | `lichking.MPQ` | 2.5 GB | [Download](https://mega.nz/file/vDYWSTrK#fvaiuHpd-FTVsQT4ghGLK6QJLZyA87c1rlBEeu1_Btk) |

**Example directory structure:**
```
C:\WowCombo\
├── Json\
│   └── MPQ\
│       ├── common-2.MPQ      (for Vanilla)
│       ├── expansion.MPQ     (for TBC)
│       └── lichking.MPQ      (for WOTLK)
```

> **💡 Tip**: You only need to download the MPQ file for your WoW version. If playing WOTLK, download `lichking.MPQ`.

---

### 3. **Download Navigation Mesh Data** (Optional - for V3 Remote Pathfinding Only)

⚠️ **Navigation mesh files are NOT included in the git repository** due to their size (~500 MB).

If you plan to use **V3 Remote Pathfinding** (best quality, required for Cataclysm+), download the navmesh files:

| WoW Versions | Size | Download Link |
|-------------|------|---------------|
| **Vanilla + TBC** | ~400 MB | [Download](https://mega.nz/file/7HgkHIyA#c_gzUeTadecWY0JDY3KT39ktfPGLs2vzt_90bMvhszk) |
| **Vanilla + TBC + WOTLK** | ~600 MB | [Download](https://mega.nz/file/zWQ2XIKI#9EKWOPyyTMfY1LACkcP_wioZ0poVIuaGh2xcRh4V9dw) |
| **Vanilla + TBC + WOTLK + Cata** (WIP) | ~800 MB | [Download](https://mega.nz/file/7Og32TDA#5HpxZ8Sh1XvDNCmWbI8H-cOFEJzDmh97Z6FGrO2p3X4) |

**Installation:**
1. Extract the downloaded archive - you should see an `mmaps` folder
2. Copy the `mmaps` folder to `Navigation\mmaps\` in your WowCombo directory

**Example directory structure:**
```
C:\WowCombo\
├── Navigation\
│   ├── mmaps\
│   │   ├── 0000.mmtile
│   │   ├── 0001.mmtile
│   │   ├── ... (2,000+ files)
│   └── AmeisenNavigationServer.exe  (you'll build this separately)
```

> **Note**: If you don't download navigation data, you can still use **V1 Local Pathfinding** (MPQ-based), which works fine for most users.

For V3 setup instructions, see: [README.md § 2.2 Optional - Using V3 Remote Pathing](README.md#22-optional---using-v3-remote-pathing)

---

### 4. **Build the Solution**

**Option A: Using Batch File** (Easiest)
```bash
cd BlazorServer
build.bat
```

**Option B: Using Command Line**
```bash
dotnet build -c Release
```

**Option C: Using Visual Studio**
1. Open `MasterOfPuppets.sln` in Visual Studio 2022+
2. Build → Build Solution (Ctrl+Shift+B)

**Expected output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

> **⚠️ Build Errors?** Make sure you have .NET 10 SDK installed. Run `dotnet --version` to check.

---

### 5. **First Run - Configure the Addon**

1. **Start World of Warcraft** and log in with a character

2. **Run the Bot**
   ```bash
   cd BlazorServer
   run.bat
   ```
   This will:
   - Start the BlazorServer
   - Open a browser at `http://localhost:5000`

3. **Follow the Setup Wizard**
   - Navigate to **"2. Addon Configuration"**
   - Fill in **Author** (your name, e.g., "MyName")
   - Fill in **Title** (addon name, e.g., "MyBot")
   - Click **Save** → You should see "AddonConfigurator.Install successful"
   - WoW will show a loading screen
   - You should see **flashing colored pixels** at the top-left corner of WoW

4. **Frame Configuration**
   - Navigate to **"5. Frame Configuration"**
   - Click **Auto** → **Start**
   - Wait for auto-detection to complete
   - See [Guidance for good DataFrame](https://github.com/Xian55/WowClassicGrindBot/wiki/Guidance-for-good-DataFrame)

5. **Load a Class Profile**
   - Navigate to **"Dashboard"** (home page)
   - Select your class profile from `Json\class\` (e.g., `Warrior_1.json`)
   - Click **Start**

🎉 **You're ready to bot!**

---

## 🔧 Troubleshooting

### "Unable to find the Wow process"
- Make sure WoW is running **before** starting the bot
- Ensure you're logged in with a character (not at character selection)

### "Build failed" errors
- Verify .NET 10 SDK is installed: `dotnet --version`
- Delete `bin\` and `obj\` folders, then rebuild

### No colored pixels in WoW
- Make sure the addon was installed (check step 5.3 above)
- Run `/reload` in WoW
- Restart BlazorServer (`run.bat` again)

### Bot doesn't move or attack
- Check that a class profile is loaded
- Verify your WoW keybindings match the profile
- See [README § 9. Configure Wow Client - Key Bindings](README.md#9-configure-the-wow-client---key-bindings)

### Navigation/Pathfinding not working
- **V1 Local**: Make sure MPQ files are in `Json\MPQ\`
- **V3 Remote**: Make sure `AmeisenNavigationServer.exe` is running and navmesh files are in `Navigation\mmaps\`

---

## 📁 Important Directories

| Directory | Purpose | Required? |
|-----------|---------|-----------|
| `Json\MPQ\` | Map geometry files (MPQ format) | ✅ Yes (for V1 pathfinding) |
| `Navigation\mmaps\` | Navigation mesh files (V3 pathfinding) | ⚠️ Optional (for V3 only) |
| `Json\class\` | Class profiles (combat rotations) | ✅ Yes |
| `Json\path\` | Grind route paths | ✅ Yes |
| `Addons\DataToColor\` | WoW addon (reads game state) | ✅ Yes (auto-installed) |
| `BlazorServer\` | Main web UI application | ✅ Yes |

---

## 🎮 Next Steps

1. **Configure In-Game Settings** - [README § 8. Configure Wow Client - Interface Options](README.md#8-configure-the-wow-client---interface-options)
2. **Set Up Keybindings** - [README § 9. Configure Wow Client - Key Bindings](README.md#9-configure-the-wow-client---key-bindings)
3. **Customize Class Profile** - [README § 12. Class Configuration](README.md#12-class-configuration)
4. **Create Custom Grind Routes** - See paths in `Json\path\` for examples

---

## 🆘 Getting Help

- **Documentation**: See [README.md](README.md) for complete documentation
- **Coding Guidelines**: See [AGENTS.md](AGENTS.md) if contributing code
- **Changelog**: See [CHANGELOG.md](CHANGELOG.md) for what's new in WowCombo fork
- **Issues**: Report bugs at https://github.com/Aycrith/WowCombo/issues
- **Original Project**: https://github.com/Xian55/WowClassicGrindBot

---

## 🔀 WowCombo Fork Enhancements

This fork adds:
- ✅ **Autonomous Testing Infrastructure** - API endpoints for automated testing (`/api/test/*`)
- ✅ **Health Monitoring** - Real-time bot health checks and alerting
- ✅ **Enhanced Diagnostics** - Comprehensive diagnostic reports (`/api/diagnostics`)
- ✅ **Frame Detection Bug Fix** - Fixed byte overflow for frames 256+ in addon

See [CHANGELOG.md](CHANGELOG.md) for detailed changes.

---

**Happy Botting!** 🤖
