# Blood Elf Warlock 1-70 TBC Autonomous Leveling Guide

## Overview

This guide walks you through setting up and running the complete autonomous leveling system for a Blood Elf Warlock character from level 1 to 70 (TBC cap).

The system uses:
- **BloodElf_Warlock_1-70_TBC.json**: Complete class profile with optimal rotations for each level range
- **Orchestrate-WarlockLeveling.ps1**: State machine orchestrator handling trainer pauses, death recovery, stuck detection
- **WarlockDashboard.ps1**: Real-time monitoring dashboard (optional, in separate terminal)

---

## Pre-Flight Checklist (Manual Setup - ~20 minutes)

### 1. WoW Game Setup

In World of Warcraft:

1. **Create Character**
   - Race: Blood Elf
   - Class: Warlock
   - Start in Sunstrider Isle (Eversong Woods)

2. **Initial Trainer Visit**
   - Find the Warlock Trainer in Sunstrider Isle (near the start area)
   - Learn these spells if not automatic:
     - Shadow Bolt (Key: 2)
     - Immolate (Key: 6)
     - Imp Summon (Key: N9)

3. **Action Bar Setup**
   - Set up keybindings BEFORE starting the bot
   - Use the following layout (modify if you prefer different keys):

   ```
   KEY    SPELL/ACTION              LEVEL LEARNED
   ────────────────────────────────────────────────
   2      Shadow Bolt               1 (starter spell)
   4      Corruption                4
   5      Curse of Agony            8
   6      Immolate                  1
   7      Fear                      8
   8      Drain Life                14
   9      Demon Skin → Armor → Fel  1/20/62
   0      Shoot (Wand)              when equipped
   -      Life Tap                  10
   =      Drink (consumable)        always
   F1     Howl of Terror            40
   F2     Death Coil                42
   F3     Food (consumable)         always

   NUM PAD:
   N0     Mount                     40
   N1     Unstable Affliction       50
   N2     Siphon Life               30
   N3     Drain Soul                10
   N4     Dark Pact                 40
   N5     Seed of Corruption/       66/60
          Create Healthstone
   N6     Create Soulstone          60
   N7     Health Funnel             12
   N8     Summon Voidwalker         10
   N9     Summon Imp                1

   MACRO:
   C      NPC Interact              (sell all grey + repair macro)
          /run local b=1 while b<=16 do local n,t=UnitBuff("player",b) if not n then break end if(t=="Magic") then CancelBuff(b) break end b=b+1 end
          /script SetSellPrice(0)
          /script CloseMerchant()
   ```

   **To Set a Keybinding:**
   - Press Escape to open Game Menu
   - Select "Key Bindings"
   - Find the spell/ability
   - Click the key field and press your chosen key
   - Click "OK"

4. **Addon Verification**
   - Type `/reload` in chat to reload addons
   - Verify "DataToColor" is listed in `/addons`
   - Look for the addon data frame in the bottom-right corner of the screen (should show colored pixels)

5. **Save Settings**
   - Make sure WoW settings are saved before starting the bot
   - Keep WoW running in windowed mode for stability

### 2. PC/Bot Setup

On your computer:

1. **Verify Bot Installation**
   ```powershell
   cd C:\WowClassicGrindBot
   dotnet build MasterOfPuppets.sln -c Release
   # Should complete with 0 errors
   ```

2. **Verify Profile**
   ```powershell
   # Check that the profile file exists:
   Test-Path C:\WowClassicGrindBot\Json\class\BloodElf_Warlock_1-70_TBC.json
   # Should return TRUE
   ```

3. **Kill Existing Processes**
   ```powershell
   # Stop any running bot instances
   Get-Process | Where-Object { $_.ProcessName -like "*dotnet*" } | Stop-Process -Force
   ```

4. **Create Logs Directory**
   ```powershell
   New-Item -ItemType Directory -Path C:\WowClassicGrindBot\logs -Force | Out-Null
   ```

---

## Execution: Starting the Bot

### Step 1: Launch Bot Services (Terminal 1)

```powershell
cd C:\WowClassicGrindBot
pwsh -NoProfile -ExecutionPolicy Bypass -File Scripts\Agent-BotControl.ps1 `
  -Action StartAndValidate `
  -Profile BloodElf_Warlock_1-70_TBC.json `
  -BypassActionBar
```

**Expected Output:**
```
[ OK  ] BlazorServer process started
[ OK  ] Service health verified
[ OK  ] Session initialized
[ OK  ] Profile loaded: BloodElf_Warlock_1-70_TBC.json
[ OK  ] Bot ready for launch
```

Wait 30 seconds for the full launch sequence to complete.

**Troubleshooting:**
- If `BlazorServer` fails to start, check that no other dotnet process is running
- If health check fails, wait 30 more seconds and try again
- Check `logs/` folder for detailed error messages

### Step 2: Launch Orchestrator (Terminal 1 - Continue)

Once the bot is healthy:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File Scripts\Orchestrate-WarlockLeveling.ps1 `
  -Profile BloodElf_Warlock_1-70_TBC.json
```

**Expected Output:**
```
===== WARLOCK LEVELING ORCHESTRATOR STARTED =====
Profile: BloodElf_Warlock_1-70_TBC.json
Starting Level: 1
Poll Interval: 15s

=== Blood Elf Warlock - Autonomous Leveling ===
State:        RUNNING
Uptime:       0h 0m 0s
Level:        1 / 70
Zone:         Eversong Woods
Health:       95%
Mana:         100%
Current Goal: FollowRouteGoal
Kills:        0 (0/hr)
Deaths:       0
Next Trainer: Level 10 (+9 levels)
Next Event:   [HH:MM:SS] Snapshot retrieved
```

The orchestrator will automatically:
- Poll the bot every 15 seconds
- Update the dashboard with current status
- Detect level ups
- Pause at trainer thresholds
- Handle death/stuck recovery

### Step 3: Optional - Launch Dashboard (Terminal 2)

In a **second terminal window** (recommended):

```powershell
cd C:\WowClassicGrindBot
pwsh -NoProfile -ExecutionPolicy Bypass -File Scripts\WarlockDashboard.ps1
```

This displays a clean, refreshing status view without state machine logs. Useful for monitoring in a secondary window.

---

## Expected Progression

### Levels 1-6 (Eversong Woods)
- **Duration:** ~10-15 minutes
- **Mobs:** Spiders, snakes, Horde-friendly creatures
- **First Events:**
  - Bot spawns Imp at level 1
  - First kill within 2 minutes
  - Vendor visit around level 3-4 (bags fill with vendor trash)
  - Level 2 reached (~5 min)
  - Level 4-5 reached (can cast Corruption)

### Levels 6-12 (Eversong Woods + Ghostlands)
- **Duration:** ~20-30 minutes
- **Transitions:** Automatic zone change from Eversong → Ghostlands at ~9-10 WrongZoneGoal triggers
- **New Spells:** Curse of Agony (8), Fear (8), Drain Life (14)
- **Key Event:** Level 10 trainer pause
  - Orchestrator will STOP the bot
  - Display trainer location guide
  - Wait for you to visit trainer (or 10s auto-resume if `-AutoConfirmTrainer`)
  - Resume autonomously

### Levels 12-20 (Ghostlands)
- **Duration:** ~45-60 minutes
- **Mobs:** Undead creatures, ghouls, ghosts
- **New Spells:** Voidwalker summon (10), Health Funnel (12)
- **Key Event:** Level 20 trainer pause
  - Visit trainer in Undercity (Arcane Vaults) or Orgrimmar (Valley of Spirits)

### Levels 20-30 (The Barrens → Hillsbrad Foothills)
- **Duration:** ~90-120 minutes
- **Transitions:** Automatic zeppelin travel and zone routing
- **New Spells:** Siphon Life (30)
- **Rotation:** Full DoT suite + pet management becomes standard

### Levels 30-60 (Desolace → Eastern Plaguelands)
- **Duration:** ~10-15 hours
- **Key Events:**
  - Level 40: Trainer pause (Mount, Shadowbolt enhancement)
  - Level 42: Trainer pause (Death Coil)
  - Level 50: Trainer pause (Unstable Affliction - major rotation change)
  - Level 60: Trainer pause (Create Healthstone/Soulstone)

### Levels 60-70 (TBC - Hellfire Peninsula → Netherstorm)
- **Duration:** ~8-10 hours
- **Key Events:**
  - Level 62: Trainer pause in Thrallmar (Fel Armor, Shadowburn)
  - Level 66: Trainer pause (Seed of Corruption - AoE rotation unlock)
- **New Zones:** Hellfire Peninsula → Zangarmarsh → Nagrand → Blade's Edge → Netherstorm

---

## State Machine Overview

### States and Transitions

```
IDLE
  ↓
STARTING (BlazorServer launching, profile loading)
  ↓
RUNNING (normal operation)
  ├→ TRAINER_PAUSE (critical spell level reached)
  │  └→ (manual: visit trainer)
  │  └→ RUNNING (auto-resume)
  │
  ├→ DEATH_RECOVERY (character dies)
  │  └→ (wait for resurrection)
  │  └→ RUNNING (on resurrection)
  │
  ├→ STUCK_RECOVERY (same goal >5 minutes)
  │  └→ (attempt auto-recovery)
  │  └→ RUNNING (if successful)
  │  └→ INTERVENTION_REQUIRED (if failed 3x)
  │
  └→ INTERVENTION_REQUIRED (unrecoverable error)
     └→ (manual investigation required)
```

### State Machine Rules

| State | Trigger | Action |
|-------|---------|--------|
| **RUNNING** | Level reaches critical trainer level | Pause bot, display location guide, wait for user Enter or 10s auto-continue |
| **RUNNING** | Character dies | Poll for resurrection up to 10 minutes |
| **RUNNING** | Same goal >5 minutes | Attempt stuck recovery (stop/restart bot) |
| **RUNNING** | Stuck recovery fails 3x | Escalate to INTERVENTION_REQUIRED |
| **RUNNING** | API fails 5x | Restart BlazorServer service |
| **STUCK_RECOVERY** | No new plan after restart | Increment recovery counter, retry (max 3) |
| **INTERVENTION_REQUIRED** | (terminal state) | Display error, wait for Ctrl+C |

---

## Logging and Diagnostics

### Session Logs

All sessions are logged to: `logs/warlock-orchestrator-YYYYMMDD-HHmmss.log`

**Example:**
```
2026-02-28 14:22:15 [OK  ] ===== WARLOCK LEVELING ORCHESTRATOR STARTED =====
2026-02-28 14:22:15 [OK  ] Profile: BloodElf_Warlock_1-70_TBC.json
2026-02-28 14:22:15 [INFO] Poll Interval: 15s
2026-02-28 14:22:30 [OK  ] LEVEL UP! 1 → 2
2026-02-28 14:23:00 [OK  ] Snapshot retrieved
2026-02-28 14:45:22 [WARN] *** TRAINER PAUSE AT LEVEL 10 ***
2026-02-28 14:45:22 [WARN] Trainer Location: Silvermoon City, Sunfury Spire area
2026-02-28 14:52:15 [OK  ] Trainer pause completed, resuming at level 10
```

### Session State File

Saved to: `logs/warlock-session-state.json`

Persists:
- Current level
- Total kills
- Total deaths
- Leveling timestamps
- Recovery attempt counts

**Resume after crash:**
```powershell
pwsh -File Scripts\Orchestrate-WarlockLeveling.ps1 `
  -Profile BloodElf_Warlock_1-70_TBC.json `
  -ResumeFromLevel 24  # Resumes at last known level
```

### Trainer Locations Reference

Use this when the bot pauses for trainer visits:

| Levels | Trainer Locations |
|--------|------------------|
| 1-10 | Silvermoon City, Sunfury Spire (northeast of starting area) |
| 10-20 | Silvermoon City or Undercity (Arcane Vaults, southeast wing) |
| 20-60 | Undercity (Arcane Vaults) OR Orgrimmar (Valley of Spirits) |
| 60-70 | Thrallmar, Hellfire Peninsula (Horde outpost, main building) |

**Quick Navigation:**
- **From Eversong (1-20):** Trainer is in town; no travel needed
- **To Undercity (20+):** Zeppelin from Orgrimmar (The Drag)
- **To Thrallmar (60+):** Portal from Orgrimmar or direct travel via Hellfire Peninsula

---

## Kill Rate Expectations

Typical session metrics:

| Levels | Kill/Hour | Time to Level | XP/Kill |
|--------|-----------|---------------|---------|
| 1-10 | 80-120 | ~10 min | ~100-150 |
| 10-20 | 60-90 | ~20-30 min | ~300-500 |
| 20-30 | 40-70 | ~60-90 min | ~1000-1500 |
| 30-40 | 30-50 | ~120-150 min | ~2000-3000 |
| 40-50 | 20-40 | ~150-180 min | ~3000-4000 |
| 50-60 | 15-30 | ~180-240 min | ~4000-6000 |
| 60-70 | 10-20 | ~300-450 min | ~10000-15000 |

**Total Time to 70:** ~20-30 hours of active gameplay

---

## Troubleshooting

### Bot Won't Start

**Problem:** `BlazorServer process failed to start`

**Solutions:**
1. Verify build:
   ```powershell
   dotnet build MasterOfPuppets.sln -c Release
   ```
2. Kill all dotnet processes:
   ```powershell
   Get-Process | Where-Object { $_.ProcessName -like "*dotnet*" } | Stop-Process -Force
   ```
3. Check firewall:
   - Ensure port 5000 is accessible locally
   - Check Windows Defender Firewall settings

### Profile Not Found

**Problem:** `Profile BloodElf_Warlock_1-70_TBC.json not found`

**Solution:**
```powershell
# Verify file exists
Test-Path C:\WowClassicGrindBot\Json\class\BloodElf_Warlock_1-70_TBC.json

# If not, recreate from the guide or check directory listing
ls C:\WowClassicGrindBot\Json\class\ | grep -i warlock
```

### Bot Stuck in STUCK_RECOVERY

**Problem:** Bot keeps restarting due to same goal detection

**Causes:**
1. Path file not found → bot can't plan
2. No valid mobs in current zone → bot can't find target
3. WoW disconnected → no screen updates

**Solutions:**
1. Check zone is correct:
   - Manually verify character location in WoW
   - Confirm zone matches profile requirements
2. Check path files:
   ```powershell
   Test-Path "C:\WowClassicGrindBot\Json\path\_pack\20-30\The Barrens\20-26 Zeplin behind Camp Taurajo.json"
   ```
3. Verify WoW still running and screen is visible

### Trainer Pause Not Triggering

**Problem:** Level reached but no trainer pause dialog

**Possible Causes:**
1. Level wasn't in `$CRITICAL_TRAINER_LEVELS` list (only pauses at critical levels)
2. Level was already paused (won't pause twice)

**Solution:**
- Check which levels trigger pauses:
  ```powershell
  # Edit the orchestrator script to add more trainer levels:
  # $CRITICAL_TRAINER_LEVELS = @(10, 20, 30, ..., [add level here], ...)
  ```

### Dashboard Shows "Connection Failed"

**Problem:** Dashboard can't reach BlazorServer

**Solutions:**
1. Verify bot is still running:
   ```powershell
   Get-Process -Name dotnet
   ```
2. Check port:
   ```powershell
   netstat -an | Select-String "5000"
   ```
3. Restart BlazorServer:
   ```powershell
   Get-Process | Where-Object { $_.ProcessName -like "*dotnet*" } | Stop-Process -Force
   # Wait 5s, then orchestrator will auto-restart
   ```

---

## Advanced Options

### Resume from Specific Level

If the bot crashed at level 24 and you resumed at 22, restart with:

```powershell
pwsh -File Scripts\Orchestrate-WarlockLeveling.ps1 `
  -ResumeFromLevel 24
```

### Auto-Confirm Trainer Pauses

To skip the manual Enter-key confirmation:

```powershell
pwsh -File Scripts\Orchestrate-WarlockLeveling.ps1 `
  -AutoConfirmTrainer
```

(Useful for overnight sessions, but you won't know which trainer to visit)

### Custom Poll Interval

To check bot status more/less frequently:

```powershell
pwsh -File Scripts\Orchestrate-WarlockLeveling.ps1 `
  -PollIntervalSeconds 30  # Check every 30s instead of 15s
```

### Verbose Logging

Enable detailed debug output:

```powershell
pwsh -File Scripts\Orchestrate-WarlockLeveling.ps1 `
  -VerboseLogging
```

---

## Session Lifecycle Example

### Session 1: Levels 1-10 (~20 minutes)

```
[14:00:00] Orchestrator starts
[14:00:15] Level 1 detected, FollowRouteGoal → kill Spiders
[14:05:22] LEVEL UP! 1 → 2
[14:10:15] Level 4 reached, can cast Corruption
[14:15:22] LEVEL UP! 4 → 5
[14:20:30] LEVEL UP! 5 → 8
[14:25:00] Level 8 reached, can cast Fear/Curse
[14:30:15] LEVEL UP! 8 → 10
[14:30:16] *** TRAINER PAUSE AT LEVEL 10 ***
[14:30:16] Trainer Location: Silvermoon City, Sunfury Spire area
[14:30:16] [Waiting for user to press Enter...]
[14:35:45] [User presses Enter]
[14:35:46] Bot resumes
[14:35:55] State: RUNNING
```

### Session Restart: Level 24 (After Crash)

```powershell
pwsh -File Scripts\Orchestrate-WarlockLeveling.ps1 -ResumeFromLevel 24
```

```
[15:00:00] Orchestrator starts
[15:00:01] Session state restored: Level 24, Kills 1247, Deaths 2
[15:00:15] Level 24 detected, Current kills: 1247
[15:00:30] FollowRouteGoal active, Hillsbrad Foothills
[15:00:45] Current goal unchanged, SameGoalTickCount: 1
[...continues normally...]
[15:45:22] LEVEL UP! 24 → 25
[16:15:30] LEVEL UP! 25 → 26
[16:15:31] *** TRAINER PAUSE AT LEVEL 26 ***
```

---

## Performance Tips

1. **WoW Settings:**
   - Lower graphics settings (won't affect bot gameplay)
   - Disable shadows and effects
   - 60 FPS cap to reduce CPU usage

2. **Networking:**
   - Run pathfinding server locally (PathingAPI.exe)
   - Don't run other bandwidth-heavy apps
   - Disable Discord/browser streaming

3. **Monitoring:**
   - Run dashboard in separate window (less resource usage)
   - Reduce log verbosity for long sessions

---

## When to Stop and Investigate

Contact support or manually intervene if:

- ✋ **INTERVENTION_REQUIRED state** appears (manual recovery needed)
- ✋ **Same goal >5 minutes** even after stuck recovery attempts
- ✋ **Death without resurrection** after 10 minutes
- ✋ **API offline** for >5 minutes
- ✋ **Character died repeatedly** (>5 deaths/hour suggests broken rotation)
- ✋ **Zone mismatch** (character in wrong zone, profile can't find path)

---

## Summary

Your Warlock is now ready for autonomous leveling!

**Next Steps:**
1. Complete pre-flight checklist (WoW setup, action bars)
2. Run `Agent-BotControl.ps1` to start services
3. Run `Orchestrate-WarlockLeveling.ps1` to begin
4. Optional: Run `WarlockDashboard.ps1` in second terminal
5. Monitor orchestrator output and respond to trainer pauses
6. Check logs if anything goes wrong

**Estimated Total Time:** 20-30 hours to reach level 70

Good luck! 🎮
