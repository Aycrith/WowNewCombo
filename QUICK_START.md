# 🎮 Blood Elf Warlock 1-70 TBC - Quick Start Guide

## Fastest Way to Start (2 minutes)

```powershell
cd C:\WowClassicGrindBot
pwsh -File Scripts\Launch-WarlockLeveling.ps1 -OpenDashboard
```

The launcher will:
- ✅ Verify your build
- ✅ Start BlazorServer
- ✅ Launch the orchestrator
- ✅ Open a dashboard in a separate window
- ✅ Automatically pause at trainer levels

---

## Before You Start (Do These First!)

### In World of Warcraft (15 minutes)

1. **Create Character**
   - Race: Blood Elf
   - Class: Warlock
   - Zone: Sunstrider Isle (Eversong Woods)

2. **Set Action Bar Keys**
   Use these exact keys:
   ```
   2 = Shadow Bolt
   4 = Corruption
   5 = Curse of Agony
   6 = Immolate
   7 = Fear
   8 = Drain Life
   9 = Demon Skin/Armor/Fel Armor
   0 = Shoot (Wand)
   - = Life Tap
   = = Drink (food/drink)
   F1 = Howl of Terror
   F2 = Death Coil
   F3 = Food
   N0 = Mount (at level 40)
   N1-N9 = Pet spells (see guide for details)
   C = NPC Interact Macro
   ```

3. **Visit Trainer**
   - Find Warlock Trainer in Sunstrider Isle
   - Learn: Shadow Bolt, Immolate, Imp Summon

4. **Verify Addon**
   - Type `/reload` in chat
   - Check that "DataToColor" appears in addon list
   - Look for colored pixels in bottom-right corner

5. **Keep WoW Running**
   - Run in windowed mode
   - Don't minimize during leveling

### On Your PC (5 minutes)

```powershell
# Verify profile exists
Test-Path C:\WowClassicGrindBot\Json\class\BloodElf_Warlock_1-70_TBC.json
# Should return TRUE

# Build the bot (must succeed with 0 errors)
cd C:\WowClassicGrindBot
dotnet build MasterOfPuppets.sln -c Release

# Kill any old bot processes
Get-Process | Where-Object {$_.Name -like "*dotnet*"} | Stop-Process -Force
```

---

## Start the Bot (30 seconds)

```powershell
cd C:\WowClassicGrindBot
pwsh -File Scripts\Launch-WarlockLeveling.ps1 -OpenDashboard
```

You should see:
```
╔════════════════════════════════════════════════════════════╗
║  WARLOCK LEVELING - PREFLIGHT CHECKS                       ║
╚════════════════════════════════════════════════════════════╝
[ OK  ] Build succeeded (0 errors)
[ OK  ] Profile found: BloodElf_Warlock_1-70_TBC.json
[ OK  ] Cleaned up old processes
[ OK  ] All required directories verified
[WARN] Checklist:
[WARN]   [ ] Character is Blood Elf Warlock
[WARN]   [ ] Action bars are configured
[WARN]   [ ] DataToColor addon is loaded
[WARN]   [ ] WoW is running and visible

Press Enter when all items are complete
```

Once you press Enter, the bot will:
- Start BlazorServer (takes ~10 seconds)
- Load your profile
- Begin leveling automatically
- Show a dashboard in a second window (if you used `-OpenDashboard`)

---

## What to Expect

### First 5 Minutes
- Spawn Imp summon
- Kill first mobs (spiders, snakes)
- Vendor trash fills bags
- First vendor visit around level 3-4

### Levels 1-10 (~20 minutes)
- Farm Eversong Woods
- Pick up Corruption spell at level 4
- Pick up Fear/Curse at level 8
- **Trainer Pause at Level 10**
  - Bot pauses
  - Dialog shows: "Silvermoon City, Sunfury Spire area"
  - Visit trainer, press Enter to resume

### Levels 10-20 (~30 minutes)
- Continue Eversong Woods
- Auto-transition to Ghostlands
- Pick up Drain Life at level 14
- **Trainer Pause at Level 20**

### Levels 20-60 (~6-8 hours)
- Auto-transition: Barrens → Hillsbrad → Desolace → etc.
- Trainer pauses at: 30, 40, 42, 50, 60
- Rotation expands with new spells each level

### Levels 60-70 (~4-5 hours)
- TBC zones: Hellfire Peninsula → Nagrand → Netherstorm
- Final trainer pauses at: 62, 66
- Complete at level 70

---

## Dashboard Display

While leveling, your dashboard shows:

```
╔════════════════════════════════════════════════════════════╗
║   WARLOCK LEVELING DASHBOARD - READ ONLY                  ║
╚════════════════════════════════════════════════════════════╝

Status: ACTIVE
Uptime: 2h 14m

Level: 24 / 70
Zone: Hillsbrad Foothills

Health: 95%
Mana: 78%

Current Goal: FollowRouteGoal

Kills: 1247 (559/hr)
Deaths: 2
```

Refreshes every 10 seconds automatically.

---

## Trainer Pause Guide

When the bot reaches a trainer level, a dialog appears:

```
╔════════════════════════════════════════════════════════════╗
║                     TRAINER PAUSE                          ║
╚════════════════════════════════════════════════════════════╝
Level: 10
Trainer Location: Silvermoon City, Sunfury Spire area

Please visit your class trainer and learn new spells.
Press Enter when done...
```

**What to do:**
1. Open WoW window
2. Travel to trainer location (shown in dialog)
3. Talk to trainer, learn spells
4. Return to PowerShell window
5. Press Enter

Bot automatically resumes!

---

## Trainer Locations

| Level Range | Location |
|-------------|----------|
| 1-10 | Silvermoon City, Sunfury Spire (north part of city) |
| 10-20 | Silvermoon City OR Undercity (Arcane Vaults, southeast) |
| 20-60 | Undercity (Arcane Vaults) OR Orgrimmar (Valley of Spirits) |
| 60-70 | Thrallmar, Hellfire Peninsula (main building) |

---

## If Something Goes Wrong

### Bot won't start
```powershell
# Verify build
dotnet build MasterOfPuppets.sln -c Release

# Check for errors, fix any issues reported

# Kill old processes
Get-Process | Where-Object {$_.Name -like "*dotnet*"} | Stop-Process -Force

# Try again
pwsh -File Scripts\Launch-WarlockLeveling.ps1
```

### Bot gets stuck
- Check logs: `logs/warlock-orchestrator-*.log`
- Verify character is in correct zone
- Orchestrator auto-recovers within 5 minutes
- If still stuck, press Ctrl+C and check logs

### Character dies
- Bot waits for resurrection (up to 10 minutes)
- Manually resurrect in WoW
- Bot resumes automatically

---

## Session Information

**Session Log:**
- Location: `logs/warlock-orchestrator-YYYYMMDD-HHmmss.log`
- Shows all events, state changes, level-ups
- Check if issues occur

**Session State:**
- Location: `logs/warlock-session-state.json`
- Auto-saved every 75 seconds
- Persists level, kills, deaths across crashes

**Resume After Crash:**
```powershell
pwsh -File Scripts\Orchestrate-WarlockLeveling.ps1 -ResumeFromLevel 24
```

---

## Expected Progression

| Levels | Duration | Kill/Hour | Completion |
|--------|----------|-----------|------------|
| 1-10 | 15 mins | 80-100 | 14% |
| 10-20 | 30 mins | 60-90 | 29% |
| 20-30 | 90 mins | 40-70 | 43% |
| 30-40 | 150 mins | 30-50 | 57% |
| 40-50 | 180 mins | 20-40 | 71% |
| 50-60 | 240 mins | 15-30 | 86% |
| 60-70 | 360 mins | 10-20 | 100% |

**Total: ~20-30 hours of playtime**

---

## Advanced Options

### Auto-confirm trainer pauses (for overnight runs)
```powershell
pwsh -File Scripts\Orchestrate-WarlockLeveling.ps1 `
  -AutoConfirmTrainer
```
(Bot will auto-resume 10s after trainer pause without waiting for Enter)

### Verbose logging
```powershell
pwsh -File Scripts\Orchestrate-WarlockLeveling.ps1 `
  -VerboseLogging
```

### Longer timeout (if bot is slow)
```powershell
pwsh -File Scripts\Orchestrate-WarlockLeveling.ps1 `
  -PollIntervalSeconds 30
```

---

## Need Help?

Read these files in order:

1. **WARLOCK_LEVELING_GUIDE.md** (400 lines)
   - Complete setup instructions
   - Troubleshooting section with 10+ solutions
   - Detailed pre-flight checklist

2. **IMPLEMENTATION_SUMMARY.md** (500 lines)
   - Technical architecture
   - API integration details
   - State machine reference

3. **Check logs:**
   - `logs/warlock-orchestrator-*.log`
   - Look for `[ERROR]` or `[WARN]` entries

---

## Ready to Start?

✅ Completed WoW setup (action bars, addon, character creation)?
✅ Built bot successfully (`dotnet build` → 0 errors)?
✅ Read this quick start guide?

Then run:
```powershell
pwsh -File Scripts\Launch-WarlockLeveling.ps1 -OpenDashboard
```

Good luck! Your Warlock will be level 70 in 20-30 hours! 🎮
