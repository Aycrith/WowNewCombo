# WowClassicGrindBot — System Architecture (Technical)

## 1) Topology (What runs where)

### On the same Windows machine (default)
- **World of Warcraft Classic client** (`WowClassic.exe`)
- **BlazorServer** (`BlazorServer/bin/Release/net10.0/BlazorServer.exe`)
  - Hosts the Web UI and the bot runtime (GOAP, combat, input, readers)
- **Optional: AmeisenNavigationServer** (`Navigation/AmeisenNavigationServer.exe`)
  - RemoteV3 nav/pathing over TCP (MMAP-based)
- **Optional: PathingAPI** (`PathingAPI/bin/Release/net10.0/PathingAPI.exe`)
  - RemoteV1 pathing over HTTP (MPQ-based)

### Communication overview
```
WoW Client
  └─ Addon: DataToColor (encodes state into pixel colors)
        ↓ (screen pixels)
BlazorServer
  ├─ Screen capture (DXGI) reads pixels → AddonDataProvider
  ├─ Readers decode world/player/target state
  ├─ GOAP Planner selects goals → executes via input simulation
  ├─ Web UI + HTTP API (/api/*) for control/diagnostics/tests
  └─ Pathing: RemoteV3 → RemoteV1 → Local (auto-fallback)
```

## 2) Core runtime architecture (inside BlazorServer)

### 2.1 Data acquisition (client → bot)
- **DataToColor addon** draws an encoded “data frame” into a known screen region.
- **Screen capture** (`Reader.Type = DXGI`) grabs the game frame and reads pixel RGB values.
- **FrameConfig** (`frame_config.json`) describes the exact pixel locations to sample.
  - If the WoW window rectangle changes, frame config can be invalidated and re-generated.

### 2.2 World model & planning (bot brain)
- **Readers** transform sampled pixels into structured values (player state, target state, UI state, etc.).
- **GOAP planner** (Goal-Oriented Action Planning) selects the next goal based on current world state:
  - Typical loop: `FollowRoute → Approach → Pull → Combat → Loot → FollowRoute`

### 2.3 Actuation (bot → client)
- **Input simulation** sends keystrokes/mouse (abilities, movement, interactions).
- **ExecGameCommand** can type chat commands for automation/diagnostics.

### 2.4 UI & control surface
- **Blazor Web UI** on `http://localhost:5000`
- **HTTP APIs** (from `Frontend/Controllers/*`) provide automation hooks:
  - `/api/bot/*` start/stop, load profiles
  - `/api/diagnostics/*` validations and auto-fixes
  - `/api/test/*` end-to-end test suite (movement/combat cycles, snapshots, etc.)
  - `/api/health` liveness + startup snapshot

## 3) Pathing architecture

### 3.1 Modes
- **RemoteV3**: `AmeisenNavigationServer.exe` (TCP, MMAP-based) on port **47110**
- **RemoteV1**: `PathingAPI.exe` (HTTP) on port **5001**
- **Local**: in-process PPather using MPQ data (`Json/MPQ/expansion.MPQ`)

### 3.2 Fallback chain (implemented in `Core/DependencyInjection.cs`)
```
RemoteV3 (if TCP ping succeeds)
  ↓ else
RemoteV1 (if HTTP ping succeeds)
  ↓ else
Local (PPather in-process)
```

## 4) Startup orchestration

### 4.1 Built-in orchestrator (in-process)
- `BlazorServer/StartupHostedService.cs` triggers `Core/Startup/StartupOrchestrator`.
- Stages include:
  - Discover WoW install
  - Validate/install addons
  - (Optional) start navigation server
  - (Optional) launch WoW
  - Auto-configure frames when needed

### 4.2 Health monitoring (in-process)
- `Core/Startup/HealthMonitor.cs` can check WoW process + nav server health on an interval.
- Nav-server “self restart” was removed from `NavigationServerManager` to avoid double-restart loops.

## 5) Stability lessons (what historically broke)

### 5.1 Port mismatch caused “nav server down” false negatives
- Multiple scripts/docs used **47111** while the nav server config and appsettings used **47110**.
- Standardized to **47110** across code + scripts + docs.

### 5.2 Duplicate restarters caused crash/focus-stealing loops
- Navigation server could crash quickly; multiple monitors repeatedly respawned it.
- Result: repeated window creation/focus stealing + unstable desktop.
- Mitigation:
  - Only one component should “decide” restarts.
  - Launcher now owns restarts; internal nav-manager no longer self-restarts.

### 5.3 BindPadMinimal XML encoding failures
- If `BindPadMinimal.xml` loads with BOM/encoding issues, keybinding automation can fail.
- Launcher now rewrites `BindPadMinimal.toc/xml` using ASCII encoding (no BOM).

## 6) Production one-click launcher (what was added)

### Entry point
- `OneClickLaunch.bat` → `Scripts/OneClickLauncher.ps1`

### What it does
1. **Pre-flight**
   - Validates .NET 10 runtime
   - Detects WoW install path
   - Ensures `DataToColor` + supporting addons are installed
   - Normalizes BindPadMinimal file encoding
2. **Bring services online**
   - Starts NavigationServer if MMAPs exist and port becomes reachable
   - Starts PathingAPI if present
   - Starts BlazorServer (or reuses an existing instance on the configured port)
3. **Synchronization gate**
   - Polls `/api/health` then `/api/test/status` until:
     - addon comms marker is correct
     - frame config exists/valid
     - data freshness is updating
     - character is detected
4. **Autonomous validation (only if the character matches the expected test case)**
   - Runs `/api/diagnostics/fix/all`
   - Loads `BloodElf_Rogue_Starter_Test.json`
   - Starts the bot
   - Runs `/api/test/*` validation suite (movement + combat cycle)
5. **Continuous monitoring**
   - Periodically checks liveness (HTTP + TCP ports)
   - Restarts managed processes with exponential backoff

## 7) Known hard limits (cannot be fully automated)
- **WoW account login / character selection** cannot be done safely from this project (credentials, Battle.net flow).
- **Frame configuration** can still fail if the addon’s pixel area is obstructed (UI mods, overlays, window not visible).
- **Quest handling** is not a first-class “questing bot” subsystem in this repo; it’s primarily a grind/route/goals system.

