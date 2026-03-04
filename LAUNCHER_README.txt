# ⚠️ LAUNCHER CLEANUP NOTICE

## **USE THIS LAUNCHER ONLY:**
### `WOW_BOT_LAUNCHER.bat` ← Double-click this!

---

## **Deprecated Launch Scripts** (DO NOT USE):
The following scripts are **old/redundant** and should be ignored:

- ❌ `StartBot.bat` - Old version, replaced by unified launcher
- ❌ `LaunchBot.bat` - Old version, replaced by unified launcher  
- ❌ `Launch.bat` - Old version, calls PowerShell script
- ❌ `StartAll.bat` - Old version, hardcoded paths
- ❌ `Start.bat` - Minimal script, lacks features
- ❌ `LaunchAuto.bat` - Redundant

**Recommendation:** Delete or archive these files to avoid confusion.

---

## **How to Launch the Bot:**

1. **Start World of Warcraft**
   - Launch Battle.net
   - Start WoW Classic Anniversary
   - Log in to your character
   - Wait until in-game

2. **Double-click:** `WOW_BOT_LAUNCHER.bat`
   - Auto-detects WoW process
   - Builds bot if needed
   - Opens web UI at http://localhost:5000

3. **Start Grinding:**
   - Click "Start" in web UI
   - Monitor bot behavior
   - Press Ctrl+C in terminal to stop

---

## **Troubleshooting:**

### Bot won't start?
- Run `dotnet build -c Release` manually
- Check WoW is running
- Verify addons installed: `_anniversary_\Interface\AddOns\DataToColor`

### Keybindings not loading?
- Wait 10-15 seconds after bot starts
- Check logs: `BlazorServer/out*.log`
- Look for "Bindings initialized with 56 keys"

### Vendor navigation stuck?
- Check zone name in logs
- Verify hard-coded vendor database has your zone
- File: `Core/Database/VendorLocations.cs`

---

**Created:** 2026-02-03
**Purpose:** Consolidate launchers into single source of truth
