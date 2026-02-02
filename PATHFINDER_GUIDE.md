# WowClassicGrindBot Pathfinder Guide

This guide covers the three pathfinding backends available in WowClassicGrindBot and how to configure them for optimal performance.

## Pathfinder Overview

WowClassicGrindBot supports three pathfinding backends with automatic fallback:

| Backend | Quality | Speed | Requirements | Best For |
|---------|---------|-------|--------------|----------|
| **RemoteV3** (AmeisenNavigation) | ⭐⭐⭐⭐⭐ | Fast | MMAP files + Server | Best overall navigation |
| **RemoteV1** (PathingAPI) | ⭐⭐⭐⭐ | Good | expansion.MPQ + Server | Good alternative |
| **Local** (PPather) | ⭐⭐⭐ | Basic | expansion.MPQ only | Simplest setup |

## Fallback Chain

The bot automatically falls back through the chain:
```
RemoteV3 → RemoteV1 → Local
```

If you configure `RemoteV3` but the server isn't running, it automatically tries `RemoteV1`, then `Local`.

---

## Backend 1: RemoteV3 (AmeisenNavigation) - RECOMMENDED

AmeisenNavigation is a high-performance TCP-based navigation server that uses TrinityCore MMAPs (Movement Maps) and the recastnavigation library.

### Features
- Smooth pathfinding (Chaikin, Catmull-Rom, Bezier curves)
- Fast map loading (< 1 second per map)
- Movement raycasting
- Supports multiple WoW versions (3.3.5a, 5.4.8, etc.)
- Multi-client optimized

### Setup Instructions

1. **Navigate to the Navigation folder:**
   ```
   C:\WowClassicGrindBot\Navigation\
   ```

2. **Create the mmaps folder** (if not exists):
   ```
   C:\WowClassicGrindBot\Navigation\mmaps\
   ```

3. **Obtain MMAP files:**
   
   **Option A: Extract using TrinityCore (Recommended)**
   - Clone TrinityCore for your WoW version
   - Build the `mmaps_generator` tool
   - Extract MMAPs from WoW client data
   
   **Option B: Download pre-extracted**
   - Search for "TrinityCore 3.3.5a MMAPs" or "TBC MMAPs"
   - Common sources: GitHub releases, private server forums
   
   The mmaps folder should contain files like:
   ```
   000.map, 001.map, ...
   0002727.mmtile, 0012829.mmtile, ...
   ```

4. **Configure AmeisenNavigation** (`Navigation\config.json`):
   ```json
   {
       "iClientVersion": 0,    // 0=Auto, 1=5.4.8
       "iPort": 47111,
       "sIP": "127.0.0.1",
       "sMmapFolder": "mmaps",
       "iMmapFormat": 0,       // 0=Auto, 1=TrinityCore 3.3.5a, 2=SkyFire 5.4.8
       "iSmoothing": 2,        // 0=None, 1=Chaikin, 2=Catmull-Rom, 3=Bezier
       "bPreloadMaps": true
   }
   ```

5. **Start the server:**
   ```
   C:\WowClassicGrindBot\Navigation\StartNavigationServer.bat
   ```

6. **Configure the bot** (already set by default):
   ```json
   "Pathing": {
       "Mode": "RemoteV3",
       "hostv3": "127.0.0.1",
       "portv3": 47111
   }
   ```

---

## Backend 2: RemoteV1 (PathingAPI)

PathingAPI is the original out-of-process pathfinding server. It runs as a separate .NET application and uses MPQ data for pathfinding.

### Requirements
- **expansion.MPQ** file (~1.8GB) containing map data

### Setup Instructions

1. **Download expansion.MPQ:**
   ```
   https://mega.nz/folder/GipyXCyR#-cT2SLwsN01fBD63HJKF7w
   ```

2. **Place in MPQ folder:**
   ```
   C:\WowClassicGrindBot\Json\MPQ\expansion.MPQ
   ```

3. **Start PathingAPI:**
   ```
   C:\WowClassicGrindBot\Scripts\StartPathingAPI.bat
   ```
   
   Or manually:
   ```
   cd C:\WowClassicGrindBot\PathingAPI\bin\Release\net10.0
   PathingAPI.exe
   ```

4. **Configure the bot:**
   ```json
   "Pathing": {
       "Mode": "RemoteV1",
       "hostv1": "localhost",
       "portv1": 5001
   }
   ```

---

## Backend 3: Local (PPather)

PPather runs directly in the bot process. It's the simplest to set up but has more basic pathfinding.

### Requirements
- **expansion.MPQ** file (~1.8GB)

### Setup Instructions

1. **Download expansion.MPQ:**
   ```
   https://mega.nz/folder/GipyXCyR#-cT2SLwsN01fBD63HJKF7w
   ```

2. **Place in MPQ folder:**
   ```
   C:\WowClassicGrindBot\Json\MPQ\expansion.MPQ
   ```

3. **Configure the bot:**
   ```json
   "Pathing": {
       "Mode": "Local"
   }
   ```

No additional servers required.

---

## Quick Configuration

Use the configuration script to quickly change pathfinder settings:

```powershell
# Use RemoteV3 (AmeisenNavigation) - Best quality
.\Scripts\Configure-Pathfinder.ps1 -Backend RemoteV3

# Use RemoteV1 (PathingAPI)
.\Scripts\Configure-Pathfinder.ps1 -Backend RemoteV1

# Use Local (PPather) - Simplest
.\Scripts\Configure-Pathfinder.ps1 -Backend Local

# Custom ports
.\Scripts\Configure-Pathfinder.ps1 -Backend RemoteV3 -V3Port 47112

# Enable path visualizer (requires PathingAPI running)
.\Scripts\Configure-Pathfinder.ps1 -Backend RemoteV3 -EnableVisualizer
```

---

## Configuration File Reference

### appsettings.json Pathing Section

```json
{
  "Pathing": {
    "Mode": "RemoteV3",       // "Local", "RemoteV1", or "RemoteV3"
    "hostv1": "localhost",    // PathingAPI host
    "portv1": 5001,           // PathingAPI port
    "hostv3": "127.0.0.1",    // AmeisenNavigation host
    "portv3": 47111,          // AmeisenNavigation port
    "PathVisualizer": false   // Enable path visualization
  }
}
```

### AmeisenNavigation config.json

```json
{
    "iClientVersion": 0,              // 0=Auto, 1=MoP 5.4.8
    "iPort": 47111,                   // TCP port
    "sIP": "127.0.0.1",               // Bind IP
    "sMmapFolder": "mmaps",           // MMAPs folder path
    "iMmapFormat": 0,                 // 0=Auto, 1=Trinity 3.3.5a, 2=SkyFire
    "iSmoothing": 2,                  // 0=None, 1=Chaikin, 2=Catmull-Rom, 3=Bezier
    "bPreloadMaps": true,             // Preload maps on startup
    "sCustomMapPattern": "%03d.map",  // Custom map file pattern
    "sCustomMmtilePattern": "%03d%02d%02d.mmtile"  // Custom mmtile pattern
}
```

---

## Troubleshooting

### RemoteV3 Issues

**"Unable to connect to navigation server"**
- Make sure AmeisenNavigationServer.exe is running
- Check that port 47111 is not blocked by firewall
- Verify the mmaps folder contains valid MMAP files

**"No .map files found"**
- Download or extract MMAP files
- Ensure files are in `Navigation\mmaps\` folder

**Maps loading slowly**
- Enable `bPreloadMaps` in config.json
- MMAP loading is multi-threaded, should be < 1 second on modern CPUs

### RemoteV1 Issues

**"Unable to connect to PathingAPI"**
- Make sure PathingAPI.exe is running
- Check port 5001 is not blocked

**"MPQ file not found"**
- Download expansion.MPQ from MEGA link
- Place in `Json\MPQ\` folder

### Local (PPather) Issues

**"PPather failed to initialize"**
- Ensure expansion.MPQ is in `Json\MPQ\` folder
- MPQ file may be corrupted - re-download

**Pathing seems inaccurate**
- This is normal for Local mode
- Consider upgrading to RemoteV3 for better paths

---

## Performance Comparison

| Metric | RemoteV3 | RemoteV1 | Local |
|--------|----------|----------|-------|
| Path Quality | Excellent | Good | Basic |
| Memory Usage | Server: ~100MB | Server: ~200MB | +150MB in bot |
| CPU Impact | Low | Medium | Medium |
| Setup Complexity | Medium | Easy | Easy |
| Smooth Curves | Yes | No | No |
| Multi-Client | Excellent | Good | Per-instance |

---

## Recommended Setup for TBC Anniversary

For the best experience with TBC Anniversary:

1. **Primary: RemoteV3** with TBC-compatible MMAPs
2. **Fallback: Local** with expansion.MPQ

The default configuration is already set to use RemoteV3 with automatic fallback to Local if the server is unavailable.

---

## Files Created

```
C:\WowClassicGrindBot\
├── Navigation\
│   ├── AmeisenNavigationServer.exe  (Downloaded)
│   ├── config.json                   (Configuration)
│   ├── StartNavigationServer.bat     (Launcher)
│   └── mmaps\                         (You add MMAP files here)
├── Scripts\
│   ├── Configure-Pathfinder.ps1      (Configuration script)
│   └── StartPathingAPI.bat           (V1 server launcher)
└── Json\
    └── MPQ\                           (You add expansion.MPQ here)
```
