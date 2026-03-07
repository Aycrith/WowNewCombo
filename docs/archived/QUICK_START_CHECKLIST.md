# ⚡ Quick Start Checklist - Blood Elf Rogue Bot

**Goal**: Get your bot grinding in under 10 minutes!

---

## ✅ Pre-Flight Checklist

### 1. System Status (2 min)
```powershell
# Check processes
tasklist | findstr "BlazorServer WowClassic"
```
- [ ] BlazorServer.exe is running
- [ ] WowClassic.exe is running

If either is missing:
```powershell
# Start bot if needed
cd "C:\WowClassicGrindBot\BlazorServer\bin\Release\net10.0"
Start-Process "BlazorServer.exe"
```

### 2. Web UI Check (1 min)
- [ ] Open http://localhost:5000
- [ ] Verify frame detection: Shows 324/324 frames
- [ ] Verify character detected: Blood Elf Rogue

**If Web UI shows error**: Restart bot (see above), wait 30 seconds, try again

---

## 🎮 In-Game Setup (5 min)

### 3. Action Bar Keybinds
**CRITICAL - Bot won't work without these!**

| Key | Ability | Notes |
|-----|---------|-------|
| 1 | Stealth | All levels |
| 2 | Sinister Strike | All levels |
| 3 | Cheap Shot | Level 6+ (optional for 1-5) |
| 4 | Gouge | Level 6+ (optional) |
| 5 | Evasion | Level 6+ (optional) |
| = | Food | Drag food item here |

Abilities 6-7 are for higher levels, can skip for now.

- [ ] Keybinds configured
- [ ] Abilities visible on action bars

### 4. Character Position
- [ ] In Eversong Woods (starting area)
- [ ] Health/Mana full
- [ ] Not in combat
- [ ] Inventory has space
- [ ] Have food in bags

---

## 🚀 Launch Bot (2 min)

### 5. Load Profile
1. In Web UI, go to Class Configuration / Load Profile
2. Select: **BloodElf_Rogue_Starter_Test.json**
3. Click Load

- [ ] Profile loaded successfully
- [ ] Settings visible in UI

### 6. Start Grinding
1. Click **Start** button in Web UI
2. Watch character in-game

**First 2 minutes - Watch for**:
- [ ] Character enters stealth (shows stealth buff)
- [ ] Targets a nearby mob
- [ ] Approaches and attacks
- [ ] Uses Sinister Strike (key 2)
- [ ] Loots corpse after kill

**If all above work**: ✅ Bot is grinding! Monitor for 10 more minutes.

**If any fail**: See troubleshooting below.

---

## 🔍 Quick Troubleshooting

| Problem | Quick Fix |
|---------|-----------|
| **No movement** | Check path file exists, try repositioning character |
| **No abilities** | Verify keybinds, check logs for "Key not found" |
| **Stands still** | May be pathing issue, try starting bot closer to mobs |
| **Dies immediately** | Check mob levels, make sure health is full first |

---

## 📊 Expected Behavior

**Idle**:
- Character stealths
- Looks for nearby mobs

**Pull**:
- Approaches mob
- Auto-attack starts
- Sinister Strike builds combo points

**Combat**:
- Spams Sinister Strike
- Uses Eviscerate at 3+ combo points (if level 20+)
- Eats food if health drops below 50%

**Loot**:
- Walks to corpse
- Loots items

**Repeat**:
- Follows waypoint path
- Targets next mob

---

## ✨ Success = Bot Running Solo for 10+ Minutes

If you see the bot killing mobs, looting, and moving along the path for 10 minutes straight **without your input**, you're successful! 🎉

---

## 📁 Key Files

- **Profile**: `C:\WowClassicGrindBot\Json\class\BloodElf_Rogue_Starter_Test.json`
- **Setup Guide**: `C:\WowClassicGrindBot\BLOODELF_ROGUE_SETUP_GUIDE.md`
- **Full Summary**: `C:\WowClassicGrindBot\SESSION_PROGRESS_SUMMARY.md`
- **Logs**: `C:\WowClassicGrindBot\BlazorServer\bin\Release\net10.0\out20260203.log`

---

## 🆘 If Nothing Works

1. Check logs for errors:
   ```powershell
   notepad "C:\WowClassicGrindBot\BlazorServer\bin\Release\net10.0\out20260203.log"
   ```
2. Read full setup guide: `BLOODELF_ROGUE_SETUP_GUIDE.md`
3. Verify all keybinds are EXACTLY as specified
4. Try restarting both bot and WoW

---

**Estimated Time to First Kill**: 5-10 minutes  
**Good luck! 🍀**
